import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import re
import argparse
import urllib.parse
import unicodedata
from datetime import datetime, timezone
import psycopg2
from psycopg2 import Error

sys.stdout.reconfigure(encoding='utf-8')

BASE_URL = "https://theprotocol.it"
PLATFORM_NAME = "theprotocolit"
COUNTER = 0

DEFAULT_LEVELS = ["trainee", "assistant", "junior", "mid", "senior", "expert"]
DEFAULT_CONTRACTS = ["kontrakt-b2b", "umowa-o-prace", "umowa-zlecenie"]


def to_city_slug(text):
    normalized = unicodedata.normalize('NFD', text)
    ascii_str = ''.join(c for c in normalized if unicodedata.category(c) != 'Mn')
    return ascii_str.lower().replace(' ', '-')


def build_url(keyword, location=None, experience_level=None, contract_type=None):
    # keyword split by whitespace → individual technologies
    techs = keyword.split()
    tech_parts = [urllib.parse.quote(t.lower(), safe='.,') for t in techs]

    segments = []
    if tech_parts:
        segments.append(",".join(tech_parts) + ";t")

    levels = [experience_level] if experience_level else DEFAULT_LEVELS
    segments.append(",".join(levels) + ";p")

    contracts = [contract_type] if contract_type else DEFAULT_CONTRACTS
    segments.append(",".join(contracts) + ";c")

    if location:
        segments.append(to_city_slug(location) + ";wp")

    return BASE_URL + "/filtry/" + "/".join(segments) + "?sort=date"


def parse_salary(salary_div):
    if not salary_div:
        return None, None, "PLN", "monthly", None

    amount_span = salary_div.find("span", class_=lambda c: c and "banjm3n" in c)
    unit_span = salary_div.find("span", class_=lambda c: c and "m1dmmm5h" in c)

    if not amount_span:
        return None, None, "PLN", "monthly", None

    amount_text = amount_span.get_text(strip=True)
    unit_text = unit_span.get_text(strip=True) if unit_span else ""

    salary_type = "hourly" if any(x in unit_text for x in ["/hr", "/godz", "/ hr", "/ godz"]) else "monthly"

    sep = "–" if "–" in amount_text else ("-" if "-" in amount_text else None)
    if sep:
        parts = amount_text.split(sep, 1)
        try:
            salary_min = int(re.sub(r"\D", "", parts[0]))
            salary_max = int(re.sub(r"\D", "", parts[1]))
        except (ValueError, IndexError):
            return None, None, "PLN", salary_type, None
    else:
        num = re.sub(r"\D", "", amount_text)
        salary_min = salary_max = int(num) if num else None

    if salary_min and salary_max and salary_min != salary_max:
        salary_raw = f"{salary_min}–{salary_max} {unit_text}".strip()
    elif salary_min:
        salary_raw = f"{salary_min} {unit_text}".strip()
    else:
        salary_raw = None

    return salary_min, salary_max, "PLN", salary_type, salary_raw


def parse_offer(card):
    try:
        href = card.get("href", "")
        if not href:
            return None
        clean_path = href.split("?")[0]
        url = BASE_URL + clean_path if clean_path.startswith("/") else clean_path

        title_tag = card.find("h2", attrs={"data-test": "text-jobTitle"})
        if not title_tag:
            return None
        title = title_tag.get_text(strip=True)

        img_tag = card.find("img", attrs={"data-test": "icon-companyLogo"})
        image_url = img_tag.get("src") if img_tag else None

        salary_div = card.find("div", attrs={"data-test": "text-salary"})
        salary_min, salary_max, salary_currency, salary_type, salary_raw = parse_salary(salary_div)

        company_div = card.find("div", attrs={"data-test": "text-employerName"})
        company = company_div.get_text(strip=True) if company_div else None

        work_mode_div = card.find("div", attrs={"data-test": "text-workModes"})
        work_mode = work_mode_div.get_text(strip=True) if work_mode_div else None

        workplaces_div = card.find("div", attrs={"data-test": "text-workplaces"})
        hq_location = workplaces_div.get_text(strip=True) if workplaces_div else None

        tech_chips = card.find_all("div", attrs={"data-test": "chip-expectedTechnology"})
        technologies = ", ".join(
            chip.get("data-test-name", "") for chip in tech_chips if chip.get("data-test-name")
        ) or None

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
            "additional_info": None,
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


def get_data_and_insert(cnx, keyword, location=None, experience_level=None, contract_type=None):
    global COUNTER
    platform_id = get_platform_id(cnx)

    url = build_url(keyword, location, experience_level, contract_type)
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
    soup = BeautifulSoup(response.content, "html.parser")

    offers_section = soup.find("div", attrs={"data-test": "offersList"})
    if not offers_section:
        print("offersList not found — site may require JS rendering")
        return

    cards = offers_section.find_all("a", attrs={"data-test": "list-item-offer"})
    if not cards:
        print("No list-item-offer elements found")
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
    print("TheProtocol.IT scrapper starting...")
    db_config = read_db_config()
    cnx = None

    parser = argparse.ArgumentParser()
    parser.add_argument("keyword")
    parser.add_argument("--loc", default=None, dest="location")
    parser.add_argument("--level", default=None, dest="experience_level",
                        choices=["trainee", "assistant", "junior", "mid", "senior", "expert"])
    parser.add_argument("--contract", default=None, dest="contract_type",
                        choices=["kontrakt-b2b", "umowa-o-prace"])
    args = parser.parse_args()

    keyword = args.keyword.replace("'", "")

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connection OK")
        get_data_and_insert(cnx, keyword, args.location, args.experience_level, args.contract_type)
        print(f"Records inserted: {COUNTER}")

    except Error as e:
        print("PostgreSQL error:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
