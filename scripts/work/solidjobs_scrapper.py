import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import re
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

    if keyword:
        filters.append("subcategories=" + urllib.parse.quote(keyword, safe=''))

    exp_list = [experience] if experience else DEFAULT_EXPERIENCES
    filters.append("experiences=" + urllib.parse.quote(",".join(exp_list), safe=','))

    ct_list = [contract_type] if contract_type else DEFAULT_CONTRACTS
    filters.append("contractTypes=" + urllib.parse.quote(",".join(ct_list), safe=','))

    path = "/offers/it;" + ";".join(filters)
    return BASE_URL + path + "?sort=PublishDate"


def parse_salary(salary_text):
    if not salary_text:
        return None, None, "PLN", "monthly", None

    salary_text = salary_text.strip()

    currency_match = re.search(r'\b(PLN|EUR|USD)\b', salary_text)
    currency = currency_match.group(0) if currency_match else "PLN"

    def to_int(s):
        s = s.strip()
        if s.endswith('k'):
            return int(float(s[:-1]) * 1000)
        return int(float(s.replace(',', '.')))

    sep = "–" if "–" in salary_text else ("-" if "-" in salary_text else None)
    if sep:
        parts = salary_text.split(sep, 1)
        m1 = re.search(r'[\d.,]+k?', parts[0])
        m2 = re.search(r'[\d.,]+k?', parts[1])
        if m1 and m2:
            try:
                salary_min = to_int(m1.group(0))
                salary_max = to_int(m2.group(0))
                salary_raw = f"{salary_min} – {salary_max} {currency}"
                return salary_min, salary_max, currency, "monthly", salary_raw
            except ValueError:
                pass
    else:
        m = re.search(r'[\d.,]+k?', salary_text)
        if m:
            try:
                val = to_int(m.group(0))
                return val, val, currency, "monthly", f"{val} {currency}"
            except ValueError:
                pass

    return None, None, currency, "monthly", None


def parse_offer(card):
    try:
        href = card.get("href", "")
        if not href:
            return None
        url = BASE_URL + href if href.startswith("/") else href

        h2 = card.find("h2", class_=lambda c: c and "h5" in c.split())
        if not h2:
            return None
        title = h2.get_text(strip=True)

        img_wrapper = card.find("div", class_=lambda c: c and "img-wrapper" in c.split())
        image_url = None
        if img_wrapper:
            img = img_wrapper.find("img")
            image_url = img.get("src") if img else None

        company_a = card.find("a", attrs={"mattooltip": re.compile(r"pozostałe oferty firmy")})
        company = None
        if company_a:
            span = company_a.find("span")
            if span:
                company = span.get_text(strip=True).lstrip('\xa0').strip()

        desktop_section = card.find(
            "div",
            class_=lambda c: c and "ml-auto" in c.split() and "d-none" in c.split() and "d-md-flex" in c.split()
        )

        salary_min, salary_max, salary_currency, salary_type, salary_raw = None, None, "PLN", "monthly", None
        contract_info = None

        if desktop_section:
            sj_salary = desktop_section.find("sj-salary-display")
            if sj_salary:
                salary_text = sj_salary.get_text(strip=True)
                salary_min, salary_max, salary_currency, salary_type, salary_raw = parse_salary(salary_text)

            contracts = []
            primary = desktop_section.find("span", attrs={"mattooltip": "Rodzaj umowy"})
            if primary:
                contracts.append(primary.get_text(strip=True))
            alt = desktop_section.find("span", attrs={"mattooltip": "Alternatywny rodzaj umowy"})
            if alt:
                contracts.append(alt.get_text(strip=True))
            contract_info = " / ".join(contracts) if contracts else None

        loc_a_desktop = card.find(
            "a",
            class_=lambda c: c and "d-none" in c.split() and "d-md-inline" in c.split()
        )
        work_mode = None
        hq_location = None

        if loc_a_desktop:
            loc_span = loc_a_desktop.find("span")
            if loc_span:
                loc_text = loc_span.get_text(strip=True).lstrip('\xa0').strip()
                tooltip = loc_span.get("mattooltip", "")
                if "pracy zdalnej" in tooltip:
                    work_mode = "Zdalnie"
                    m = re.search(r'\(([^)]+)\)', loc_text)
                    hq_location = m.group(1) if m else None
                else:
                    hq_location = loc_text
                    loc_a_mobile = card.find(
                        "a",
                        class_=lambda c: c and "d-inline" in c.split() and "d-md-none" in c.split()
                    )
                    if loc_a_mobile:
                        mobile_text = loc_a_mobile.get_text(strip=True)
                        work_mode = "Hybrydowo" if "Zdalnie" in mobile_text else "Stacjonarnie"
                    else:
                        work_mode = "Stacjonarnie"

        skill_displays = card.find_all("solidjobs-skill-display")
        techs = []
        for sd in skill_displays:
            inner_spans = sd.find_all("span")
            if len(inner_spans) >= 2:
                name = inner_spans[-1].get_text(strip=True)
                if name:
                    techs.append(name)
        technologies = ", ".join(techs) if techs else None

        return {
            "title": title,
            "url": url,
            "image_url": image_url,
            "company": company,
            "salary_min": salary_min,
            "salary_max": salary_max,
            "salary_currency": salary_currency,
            "salary_type": salary_type,
            "salary_raw": salary_raw,
            "technologies": technologies,
            "location": work_mode,
            "work_location": work_mode,
            "hq_location": hq_location,
            "additional_info": contract_info,
        }
    except Exception as e:
        print(f"  parse error: {e}")
        return None


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
        return cursor.fetchone()[0]


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
    print(response.status_code)
    print(response.text[:3000])
    soup = BeautifulSoup(response.content, "html.parser")

    cards = soup.find_all("a", href=re.compile(r"^/offer/\d+/"))
    if not cards:
        print("No offer cards found — site may require JS rendering")
        return

    print(f"Found {len(cards)} offers")

    for card in cards:
        data = parse_offer(card)
        if not data:
            continue

        if offer_exists(cnx, platform_id, data["url"]):
            print(f"  SKIP (exists): {data['url']}")
            continue

        offer_id = insert_offer(cnx, platform_id, data)
        insert_job_detail(cnx, offer_id, data)
        cnx.commit()

        COUNTER += 1
        print(f"  +inserted: {data['title']} | {data['company']} | {data.get('location')}")


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
