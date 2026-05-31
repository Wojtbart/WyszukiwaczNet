import requests
import json
import re
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import argparse
import urllib.parse
from datetime import datetime, timezone
import psycopg2
from psycopg2 import Error

sys.stdout.reconfigure(encoding='utf-8')

BASE_URL = "https://solid.jobs"
PLATFORM_NAME = "solidjobs"
COUNTER = 0

DEFAULT_EXPERIENCES = ["Intern", "Junior", "Regular", "Senior"]
DEFAULT_CONTRACTS = ["Umowa o pracę", "B2B", "Umowa zlecenie"]

LEVEL_MAP = {"intern": "Intern", "junior": "Junior", "regular": "Regular", "senior": "Senior"}
CONTRACT_MAP = {"uop": "Umowa o pracę", "b2b": "B2B", "zlecenie": "Umowa zlecenie"}


def build_url(keyword, location=None, experience=None, contract_type=None):
    filters = []

    if location:
        filters.append("cities=" + urllib.parse.quote(location.title(), safe=''))

    filters.append("categories=Programista")

    exp_list = [experience] if experience else DEFAULT_EXPERIENCES
    filters.append("experiences=" + urllib.parse.quote(",".join(exp_list), safe=','))

    ct_list = [contract_type] if contract_type else DEFAULT_CONTRACTS
    filters.append("contractTypes=" + urllib.parse.quote(",".join(ct_list), safe=','))

    if keyword:
        filters.append("parsedSearchTerm=" + urllib.parse.quote(keyword, safe=''))

    path = "/offers/it;" + ";".join(filters)
    print(BASE_URL + path + "?sort=PublishDate")
    return BASE_URL + path + "?sort=PublishDate"


def extract_offers_from_ng_state(html):
    m = re.search(r'<script id="ng-state" type="application/json">(.*?)</script>', html, re.DOTALL)
    if not m:
        return []

    try:
        state = json.loads(m.group(1))
    except json.JSONDecodeError as e:
        print(f"JSON parse error: {e}")
        return []

    for key, val in state.items():
        if isinstance(val, dict) and "b" in val and isinstance(val["b"], list):
            offers = val["b"]
            if offers and isinstance(offers[0], dict) and "jobTitle" in offers[0]:
                return offers

    return []


def map_offer(raw):
    offer_id = raw.get("id")
    slug = raw.get("jobOfferUrl", "")
    url = f"{BASE_URL}/offer/{offer_id}/{slug}"

    salary = raw.get("salaryRange") or {}
    salary_min = salary.get("lowerBound")
    salary_max = salary.get("upperBound")
    salary_currency = salary.get("currency", "PLN")
    employment_type = salary.get("employmentType", "")
    salary_period = salary.get("salaryPeriod", "Month")

    if salary_min and salary_max:
        salary_raw = f"{salary_min} – {salary_max} {salary_currency} {employment_type}/mies."
    elif salary_min:
        salary_raw = f"{salary_min} {salary_currency} {employment_type}/mies."
    else:
        salary_raw = None

    skills = [s["name"] for s in raw.get("requiredSkills", []) if s.get("name")]
    technologies = ", ".join(skills) if skills else None

    remote = raw.get("remotePossible")
    city = raw.get("companyCity")

    return {
        "title": raw.get("jobTitle"),
        "url": url,
        "image_url": raw.get("companyLogoUrl"),
        "company": raw.get("companyName"),
        "salary_min": salary_min,
        "salary_max": salary_max,
        "salary_currency": salary_currency,
        "salary_type": salary_period.lower() if salary_period else "monthly",
        "salary_raw": salary_raw,
        "technologies": technologies,
        "location": remote,
        "work_location": remote,
        "hq_location": city,
        "additional_info": employment_type if employment_type else None,
    }


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

    url = build_url(keyword, location, experience, contract_type)
    print(f"Fetching: {url}")

    headers = {
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) "
            "Chrome/124.0.0.0 Safari/537.36"
        ),
        "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8",
        "Accept": "text/html,application/xhtml+xml,application/xhtml+xml,*/*;q=0.8",
    }

    response = requests.get(url, headers=headers, timeout=20)
    print(f"Status: {response.status_code}")

    raw_offers = extract_offers_from_ng_state(response.text)
    if not raw_offers:
        print("No offers found in ng-state — page may not use SSR transfer state")
        return

    print(f"Found {len(raw_offers)} offers in page state")

    kw_lower = keyword.lower() if keyword else None
    if kw_lower:
        def matches_keyword(r):
            title = (r.get("jobTitle") or "").lower()
            skills = " ".join(s.get("name", "") for s in r.get("requiredSkills", [])).lower()
            return kw_lower in title or kw_lower in skills
        raw_offers = [r for r in raw_offers if matches_keyword(r)]
        print(f"After keyword filter '{keyword}': {len(raw_offers)} offers")

    for raw in raw_offers:
        data = map_offer(raw)
        if not data["title"] or not data["url"]:
            continue

        if offer_exists(cnx, platform_id, data["url"]):
            print(f"  SKIP (exists): {data['url']}")
            continue

        offer_id = insert_offer(cnx, platform_id, data)
        if offer_id is None:
            continue
        insert_job_detail(cnx, offer_id, data)
        cnx.commit()

        COUNTER += 1
        print(f"  +inserted: {data['title']} | {data['company']}")


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
        print(f"Records inserted: {COUNTER}")

    except Error as e:
        print("PostgreSQL error:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
