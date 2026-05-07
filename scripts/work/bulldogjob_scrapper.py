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

BASE_URL = "https://bulldogjob.pl"
PLATFORM_NAME = "bulldogjob"
COUNTER = 0

DEFAULT_LEVELS = ["intern", "junior", "medium", "senior", "lead"]


def build_url(keyword, location=None, experience_level=None):
    segments = ["s"]

    if location:
        segments.append("city," + urllib.parse.quote(location, safe=''))

    skills = [urllib.parse.quote(s, safe='') for s in keyword.split()]
    if skills:
        segments.append("skills," + ",".join(skills))

    levels = [experience_level] if experience_level else DEFAULT_LEVELS
    segments.append("experienceLevel," + ",".join(levels))

    segments.append("order,published,desc")

    return BASE_URL + "/companies/jobs/" + "/".join(segments)


def parse_salary(salary_text):
    if not salary_text:
        return None, None, "PLN", "monthly", None

    currency_match = re.search(r'\b(PLN|EUR|USD)\b', salary_text)
    currency = currency_match.group(0) if currency_match else "PLN"

    clean = salary_text.replace('\xa0', '').replace(' ', '')
    numbers = re.findall(r'\d+', clean)

    if len(numbers) >= 2:
        salary_min, salary_max = int(numbers[0]), int(numbers[1])
        salary_raw = f"{salary_min} - {salary_max} {currency}"
    elif len(numbers) == 1:
        salary_min = salary_max = int(numbers[0])
        salary_raw = f"{salary_min} {currency}"
    else:
        return None, None, currency, "monthly", None

    return salary_min, salary_max, currency, "monthly", salary_raw


def parse_offer(card):
    try:
        href = card.get("href", "")
        if not href:
            return None
        url = href if href.startswith("http") else BASE_URL + href

        title_tag = card.find("h3")
        if not title_tag:
            return None
        title = title_tag.get_text(strip=True)

        img_tag = card.find("img")
        image_url = img_tag.get("src") if img_tag else None

        company_div = card.find("div", class_=lambda c: c and "text-xxs" in c.split() and "uppercase" in c.split())
        company = company_div.get_text(strip=True) if company_div else None

        # Salary
        salary_div = card.find("div", class_=lambda c: c and "JobListItem_item__salary__" in c)
        salary_text = salary_div.get_text(strip=True) if salary_div else ""
        salary_min, salary_max, salary_currency, salary_type, salary_raw = parse_salary(salary_text)

        # Technologies
        tags_div = card.find("div", class_=lambda c: c and "JobListItem_item__tags__" in c)
        tech_spans = tags_div.find_all("span", class_=lambda c: c and "py-2" in c.split() and "px-3" in c.split()) if tags_div else []
        technologies = ", ".join(s.get_text(strip=True) for s in tech_spans if s.get_text(strip=True)) or None

        # Details section: location, contract, experience level
        details_div = card.find("div", class_=lambda c: c and "JobListItem_item__details__" in c)
        location = None
        contract_type = None
        experience_level = None

        if details_div:
            loc_container = details_div.find("div", class_=lambda c: c and "relative" in c.split())
            if loc_container:
                btn = loc_container.find("button")
                if btn:
                    location = btn.get_text(strip=True)
                else:
                    loc_span = loc_container.find("span", class_="text-xs")
                    location = loc_span.get_text(strip=True) if loc_span else None

            info_divs = details_div.find_all(
                "div",
                class_=lambda c: c and "items-start" in c.split() and "hidden" in c.split()
            )
            if len(info_divs) > 0:
                span = info_divs[0].find("span")
                contract_type = span.get_text(strip=True) if span else None
            if len(info_divs) > 1:
                span = info_divs[1].find("span")
                experience_level = span.get_text(strip=True) if span else None

        additional_info = contract_type

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
            "location": location,
            "work_location": location,
            "hq_location": experience_level,
            "additional_info": additional_info,
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


def get_data_and_insert(cnx, keyword, location=None, experience_level=None):
    global COUNTER
    platform_id = get_platform_id(cnx)

    url = build_url(keyword, location, experience_level)
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

    cards = soup.find_all("a", href=re.compile(r"/companies/jobs/\d+"))
    if not cards:
        print("No job offer elements found")
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
    print("BulldogJob scrapper starting...")
    db_config = read_db_config()
    cnx = None

    parser = argparse.ArgumentParser()
    parser.add_argument("keyword")
    parser.add_argument("--loc", default=None, dest="location")
    parser.add_argument("--level", default=None, dest="experience_level",
                        choices=["intern", "junior", "medium", "senior", "lead"])
    args = parser.parse_args()

    keyword = args.keyword.replace("'", "")

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connection OK")
        get_data_and_insert(cnx, keyword, args.location, args.experience_level)
        print(f"Records inserted: {COUNTER}")

    except Error as e:
        print("PostgreSQL error:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
