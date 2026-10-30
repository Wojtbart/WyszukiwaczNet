# -*- coding: utf-8 -*-
import requests
import re
import json
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import argparse
from datetime import datetime, timezone, timedelta
from bs4 import BeautifulSoup
import psycopg2
from psycopg2 import Error

sys.stdout.reconfigure(encoding='utf-8')

PLATFORM_NAME = "solidjobs"
COUNTER = 0
SITEMAP_URL = "https://solid.jobs/sitemap-offers.xml"

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                  "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
    "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8",
    "Accept": "text/html,application/xhtml+xml,*/*;q=0.8",
}

LEVEL_MAP = {"intern": "intern", "junior": "junior", "regular": "regular", "senior": "senior"}
CONTRACT_MAP = {"uop": "Umowa o pracę", "b2b": "B2B", "zlecenie": "Umowa zlecenie"}


def keyword_slug_terms(keyword):
    """Return list of terms to look for in URL slug for a given keyword."""
    kw = keyword.lower().strip()
    slug_map = {
        "c#": ["csharp", "c-sharp", "dotnet", "asp-net"],
        "csharp": ["csharp", "c-sharp", "dotnet"],
        ".net": ["dotnet", "net-developer", "asp-net"],
        "dotnet": ["dotnet", "net-developer", "asp-net"],
        "c++": ["cpp", "cplusplus", "c-plus", "cplusplus"],
        "cpp": ["cpp", "cplusplus", "c-plus"],
        "javascript": ["javascript", "js-developer", "nodejs", "react", "angular", "vue"],
        "typescript": ["typescript", "angular", "react"],
    }
    if kw in slug_map:
        return slug_map[kw]
    # Default: normalize keyword for slug matching
    normalized = re.sub(r'[^a-z0-9]', '', kw)
    return [normalized] if normalized else [kw]


def get_recent_offer_urls(keyword, days_back=45):
    """Fetch sitemap and return offer URLs matching keyword, modified within days_back days."""
    print(f"Fetching sitemap...")
    resp = requests.get(SITEMAP_URL, headers=HEADERS, timeout=20)
    if resp.status_code != 200:
        print(f"Sitemap fetch failed: {resp.status_code}")
        return []

    cutoff = datetime.now(timezone.utc) - timedelta(days=days_back)
    url_blocks = re.findall(r'<url>(.*?)</url>', resp.text, re.DOTALL)

    slug_terms = keyword_slug_terms(keyword)
    print(f"Slug terms for '{keyword}': {slug_terms}")

    matching = []
    for block in url_blocks:
        loc_match = re.search(r'<loc>([^<]+)</loc>', block)
        lastmod_match = re.search(r'<lastmod>(\d{4}-\d{2}-\d{2})</lastmod>', block)
        if not loc_match:
            continue
        url = loc_match.group(1)
        if lastmod_match:
            lastmod = datetime.strptime(lastmod_match.group(1), "%Y-%m-%d").replace(tzinfo=timezone.utc)
            if lastmod < cutoff:
                continue
        url_lower = url.lower()
        if any(term in url_lower for term in slug_terms):
            matching.append(url)

    print(f"Sitemap: {len(url_blocks)} total, {len(matching)} matching '{keyword}' (last {days_back} days)")
    return matching


def get_offer_details_from_page(html):
    """Extract offer details from ng-state JSON in individual offer page HTML."""
    soup = BeautifulSoup(html, "html.parser")
    el = soup.find("script", {"id": "ng-state"})
    if not el:
        return None
    try:
        state = json.loads(el.string)
    except (json.JSONDecodeError, TypeError):
        return None

    for key, val in state.items():
        if not isinstance(val, dict):
            continue
        details = val.get("b", {})
        if isinstance(details, dict) and "jobOfferDetails" in details:
            return details["jobOfferDetails"]
    return None


def matches_location(details, location):
    if not location:
        return True
    city = (details.get("companyCity") or details.get("companyAddress") or "").lower()
    return location.lower() in city


def matches_experience(details, experience):
    if not experience:
        return True
    lvl = (details.get("experienceLevel") or "").lower()
    return experience.lower() in lvl


def matches_contract(details, contract_type):
    if not contract_type:
        return True
    salary = details.get("salaryRange", {})
    emp = (salary.get("employmentType") or "").lower()
    ct = contract_type.lower()
    if ct == "b2b" and "b2b" in emp:
        return True
    if ct in ("uop", "umowa o pracę") and ("umowa" in emp or "uop" in emp):
        return True
    return False


def parse_salary(salary_range):
    if not salary_range:
        return None, None, "PLN", "monthly", None
    lo = salary_range.get("lowerBound") or 0
    hi = salary_range.get("upperBound") or 0
    currency = salary_range.get("currency", "PLN")
    period = salary_range.get("salaryPeriod", "Month")
    emp_type = salary_range.get("employmentType", "")
    salary_type = "hourly" if period.lower() == "hour" else "monthly"
    if lo == 0 and hi == 0:
        return None, None, currency, salary_type, None
    lo = int(lo) if lo else None
    hi = int(hi) if hi else None
    parts = [str(x) for x in [lo, hi] if x]
    if lo and hi and lo == hi:
        parts = [str(lo)]
    salary_raw = "–".join(parts) + f" {currency}"
    if emp_type:
        salary_raw += f" {emp_type}"
    return lo, hi, currency, salary_type, salary_raw


def get_platform_id(cnx):
    with cnx.cursor() as cursor:
        cursor.execute("SELECT id FROM platforms WHERE name = %s", (PLATFORM_NAME,))
        row = cursor.fetchone()
        if not row:
            raise Exception(f"Platform '{PLATFORM_NAME}' not found in DB")
        return row[0]


def offer_exists(cnx, platform_id, url):
    with cnx.cursor() as cursor:
        cursor.execute(
            "SELECT 1 FROM offers WHERE platform_id = %s AND url = %s LIMIT 1",
            (platform_id, url)
        )
        return cursor.fetchone() is not None


def insert_offer(cnx, platform_id, data):
    query = """
        INSERT INTO offers
        (platform_id, title, price, currency, url, image_url,
         seller_name, location, additional_info, created_at)
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        ON CONFLICT (platform_id, url) DO NOTHING
        RETURNING id
    """
    with cnx.cursor() as cursor:
        cursor.execute(query, (
            platform_id,
            data["title"],
            data["salary_min"],
            data["salary_currency"],
            data["url"],
            data["image_url"],
            data["company"],
            data["location"],
            data["additional_info"],
            datetime.now(timezone.utc)
        ))
        row = cursor.fetchone()
        return row[0] if row else None


def insert_job_detail(cnx, offer_id, data):
    query = """
        INSERT INTO job_details
        (offer_id, salary_min, salary_max, salary_currency, salary_type,
         salary_raw, technologies, work_location, hq_location)
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s)
        ON CONFLICT (offer_id) DO NOTHING
    """
    with cnx.cursor() as cursor:
        cursor.execute(query, (
            offer_id,
            data["salary_min"],
            data["salary_max"],
            data["salary_currency"],
            data["salary_type"],
            data["salary_raw"],
            data["technologies"],
            data["work_location"],
            data["hq_location"],
        ))


def get_data_and_insert(cnx, keyword, location=None, experience=None, contract_type=None):
    global COUNTER
    platform_id = get_platform_id(cnx)

    offer_urls = get_recent_offer_urls(keyword)
    if not offer_urls:
        print("No matching URLs in sitemap")
        return

    for offer_url in offer_urls:
        if offer_exists(cnx, platform_id, offer_url):
            print(f"  SKIP (exists): {offer_url}")
            continue

        offer_resp = requests.get(offer_url, headers=HEADERS, timeout=20)
        details = get_offer_details_from_page(offer_resp.text)
        if not details:
            print(f"  SKIP (no details): {offer_url}")
            continue

        if not matches_location(details, location):
            continue
        if not matches_experience(details, experience):
            continue
        if not matches_contract(details, contract_type):
            continue

        title = details.get("jobTitle")
        if not title:
            continue

        company = details.get("companyName")
        city = details.get("companyCity") or details.get("companyAddress")
        logo = details.get("companyLogoUrl")
        remote = details.get("remotePossible", "")
        skills = [s["name"] for s in details.get("requiredSkills", []) if s.get("name")]
        technologies = ", ".join(skills) if skills else None

        salary_min, salary_max, salary_currency, salary_type, salary_raw = parse_salary(
            details.get("salaryRange")
        )

        emp_type = (details.get("salaryRange") or {}).get("employmentType", "")
        additional_info = f"{emp_type} | {remote}" if emp_type and remote else (emp_type or remote or None)

        data = {
            "title": title,
            "url": offer_url,
            "image_url": logo,
            "company": company,
            "location": city,
            "salary_min": salary_min,
            "salary_max": salary_max,
            "salary_currency": salary_currency,
            "salary_type": salary_type,
            "salary_raw": salary_raw,
            "technologies": technologies,
            "work_location": remote,
            "hq_location": city,
            "additional_info": additional_info,
        }

        offer_id = insert_offer(cnx, platform_id, data)
        if offer_id is None:
            continue
        insert_job_detail(cnx, offer_id, data)
        cnx.commit()

        COUNTER += 1
        print(f"  +inserted: {title} | {company} | {city}")


if __name__ == "__main__":
    print("SolidJobs scrapper starting...")
    db_config = read_db_config()
    cnx = None

    parser = argparse.ArgumentParser()
    parser.add_argument("keyword")
    parser.add_argument("--loc", default=None, dest="location")
    parser.add_argument("--level", default=None, dest="experience",
                        choices=list(LEVEL_MAP.keys()))
    parser.add_argument("--contract", default=None, dest="contract_type",
                        choices=list(CONTRACT_MAP.keys()))
    args = parser.parse_args()

    keyword = args.keyword.replace("'", "")
    experience = LEVEL_MAP.get(args.experience) if args.experience else None
    contract_type = CONTRACT_MAP.get(args.contract_type) if args.contract_type else None

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connection OK")
        get_data_and_insert(cnx, keyword, args.location, experience, contract_type)
        print(f"Records: {COUNTER}")

    except Error as e:
        print("PostgreSQL error:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
