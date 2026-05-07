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

BASE_URL = "https://nofluffjobs.com"
PLATFORM_NAME = "nofluffjobs"
COUNTER = 0


def to_city_slug(text):
    normalized = unicodedata.normalize('NFD', text)
    ascii_str = ''.join(c for c in normalized if unicodedata.category(c) != 'Mn')
    return ascii_str.lower().replace(' ', '-')


def build_url(keyword, location=None, seniority=None, employment=None):
    # first token → URL path (main category), remaining → requirement in criteria
    kw_parts = keyword.split(None, 1)
    path_keyword = kw_parts[0]
    requirement = kw_parts[1] if len(kw_parts) > 1 else None

    path_parts = ["/pl"]
    if location:
        path_parts.append(to_city_slug(location))
    path_parts.append(urllib.parse.quote(path_keyword, safe=''))
    url = BASE_URL + "/".join(path_parts)

    criteria_parts = []
    if employment:
        criteria_parts.append(f"employment={employment}")
    if requirement:
        criteria_parts.append(f"requirement={requirement}")
    if seniority:
        criteria_parts.append(f"seniority={seniority}")

    if criteria_parts:
        url += "?" + urllib.parse.urlencode({"criteria": " ".join(criteria_parts)}, quote_via=urllib.parse.quote)

    return url


def parse_salary(span):
    if not span:
        return None, None, "PLN", None

    text = span.get_text(" ", strip=True).replace('\xa0', '')
    currency_match = re.search(r'\b[A-Z]{3}\b', text)
    currency = currency_match.group(0) if currency_match else "PLN"
    clean = text.replace(currency, '').strip()

    if '–' in clean:
        parts = clean.split('–')
        try:
            salary_min = int(re.sub(r'\D', '', parts[0]))
            salary_max = int(re.sub(r'\D', '', parts[1]))
        except (ValueError, IndexError):
            return None, None, currency, None
    else:
        num = re.sub(r'\D', '', clean)
        salary_min = salary_max = int(num) if num else None

    if salary_min and salary_max and salary_min != salary_max:
        salary_raw = f"{salary_min} - {salary_max} {currency}"
    elif salary_min:
        salary_raw = f"{salary_min} {currency}"
    else:
        salary_raw = None

    return salary_min, salary_max, currency, salary_raw


def parse_offer(card):
    try:
        href = card.get("href", "")
        if not href:
            return None
        url = BASE_URL + href if href.startswith("/") else href

        title_tag = card.find("h3", attrs={"data-cy": "title position on the job offer listing"})
        if not title_tag:
            return None
        title = title_tag.get_text(strip=True)

        img_tag = card.find("img", alt="Company logo")
        image_url = img_tag.get("src") if img_tag else None

        salary_span = card.find("span", attrs={"data-cy": "salary ranges on the job offer listing"})
        salary_min, salary_max, salary_currency, salary_raw = parse_salary(salary_span)

        tech_spans = card.find_all("span", attrs={"data-cy": "category name on the job offer listing"})
        technologies = ", ".join(s.get_text(strip=True) for s in tech_spans if s.get_text(strip=True)) or None

        company_tag = card.find("h4", class_="company-name")
        company = company_tag.get_text(strip=True) if company_tag else None

        location = None
        city_tag = card.find("nfj-posting-item-city")
        if city_tag:
            city_span = city_tag.find("span", class_=lambda c: c and "tw-text-ellipsis" in c)
            if city_span:
                location = city_span.get_text(strip=True)

        return {
            "title": title,
            "url": url,
            "image_url": image_url,
            "company": company,
            "salary_min": salary_min,
            "salary_max": salary_max,
            "salary_currency": salary_currency,
            "salary_type": "monthly",
            "salary_raw": salary_raw,
            "technologies": technologies,
            "location": location,
            "work_location": location,
            "hq_location": None,
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


def get_data_and_insert(cnx, keyword, location=None, seniority=None, employment=None):
    global COUNTER
    platform_id = get_platform_id(cnx)

    url = build_url(keyword, location, seniority, employment)
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

    containers = soup.find_all("div", class_="list-container")
    if not containers:
        print("list-container not found — site may require JS rendering")
        return

    cards = []
    for container in containers:
        cards.extend(container.find_all("a", class_="posting-list-item"))
    if not cards:
        print("No posting-list-item elements found")
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
    print("NoFluffJobs scrapper starting...")
    db_config = read_db_config()
    cnx = None

    parser = argparse.ArgumentParser()
    parser.add_argument("keyword")
    parser.add_argument("--loc", default=None, dest="location")
    parser.add_argument("--seniority", default=None, dest="seniority")
    parser.add_argument("--employment", default=None, dest="employment")
    args = parser.parse_args()

    keyword = args.keyword.replace("'", "")

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connection OK")
        get_data_and_insert(cnx, keyword, args.location, args.seniority, args.employment)
        print(f"Records inserted: {COUNTER}")

    except Error as e:
        print("PostgreSQL error:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
