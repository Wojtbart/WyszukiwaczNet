# -*- coding: utf-8 -*-
import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import re
import json
import argparse
import urllib.parse
import unicodedata
from datetime import datetime, timezone
import psycopg2
from psycopg2 import Error

sys.stdout.reconfigure(encoding='utf-8')

BASE_URL = "https://justjoin.it"
PLATFORM_NAME = "justjoinit"
COUNTER = 0


def to_city_slug(text):
    """'Rzeszów' → 'rzeszow' — strip diacritics, lowercase."""
    normalized = unicodedata.normalize('NFD', text)
    ascii_str = ''.join(c for c in normalized if unicodedata.category(c) != 'Mn')
    return ascii_str.lower().replace(' ', '-')


def build_url(keyword, location=None, experience_level=None, employment_type=None):
    path = f"{BASE_URL}/job-offers"
    if location:
        path += f"/{to_city_slug(location)}"
    params = []
    if employment_type:
        params.append(("employment-type", employment_type))
    if experience_level:
        params.append(("experience-level", experience_level))
    params += [
        ("keyword", keyword),
        ("orderBy", "DESC"),
        ("sortBy", "newest"),
    ]
    return f"{path}?{urllib.parse.urlencode(params)}"


def extract_offers_from_html(html):
    """Pull offers from Next.js streaming chunks embedded in self.__next_f.push(...)."""
    pushes = re.findall(
        r'self\.__next_f\.push\(\[\d+,"((?:[^"\\]|\\.)*)"\]\)',
        html, re.DOTALL,
    )
    chunks = []
    for p in pushes:
        try:
            chunks.append(json.loads('"' + p + '"'))
        except Exception:
            continue
    big = "".join(chunks)

    pos = 0
    while True:
        i = big.find('"data":[', pos)
        if i == -1:
            return []
        arr_start = big.find('[', i)
        arr_text = _extract_balanced_array(big, arr_start)
        if arr_text and 'applyUrl' in arr_text and 'companyName' in arr_text:
            try:
                arr = json.loads(arr_text)
                if isinstance(arr, list) and arr and isinstance(arr[0], dict) and "slug" in arr[0]:
                    return arr
            except Exception:
                pass
        pos = i + 1


def _extract_balanced_array(text, start):
    depth = 0
    in_str = False
    esc = False
    for j in range(start, len(text)):
        c = text[j]
        if in_str:
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == '"':
                in_str = False
        else:
            if c == '"':
                in_str = True
            elif c == '[':
                depth += 1
            elif c == ']':
                depth -= 1
                if depth == 0:
                    return text[start:j + 1]
    return None


def pick_salary(employment_types):
    """Prefer PLN original; fall back to first non-null entry."""
    if not employment_types:
        return None, None, "PLN", "monthly", None, None

    pln_original = next(
        (e for e in employment_types
         if e.get("currency") == "PLN" and e.get("currencySource") == "original"),
        None,
    )
    pln_any = next((e for e in employment_types if e.get("currency") == "PLN"), None)
    chosen = pln_original or pln_any or employment_types[0]

    salary_min = chosen.get("from")
    salary_max = chosen.get("to")
    currency = chosen.get("currency") or "PLN"
    unit = chosen.get("unit") or "month"
    salary_type = "hourly" if unit == "hour" else "monthly"
    contract_type = chosen.get("type")

    if salary_min is None and salary_max is None:
        salary_raw = None
    elif salary_min is not None and salary_max is not None and salary_min != salary_max:
        salary_raw = f"{int(round(salary_min))} - {int(round(salary_max))} {currency}/{unit}"
    else:
        v = salary_min if salary_min is not None else salary_max
        salary_raw = f"{int(round(v))} {currency}/{unit}"

    return salary_min, salary_max, currency, salary_type, salary_raw, contract_type


def parse_offer(offer):
    try:
        slug = offer.get("slug")
        if not slug:
            return None
        url = f"{BASE_URL}/job-offer/{slug}"

        title = offer.get("title") or offer.get("body")
        if not title:
            return None

        company = offer.get("companyName")
        image_url = offer.get("companyLogoThumbUrl")

        city = offer.get("city")
        workplace = offer.get("workplaceType")
        if workplace and workplace != "office":
            location = f"{city} / {workplace}" if city else workplace.capitalize()
        else:
            location = city

        skills = offer.get("requiredSkills") or []
        technologies = ", ".join(skills) if skills else None

        salary_min, salary_max, salary_currency, salary_type, salary_raw, contract = pick_salary(
            offer.get("employmentTypes") or []
        )

        exp_level = offer.get("experienceLevel")
        bits = [b for b in (contract, exp_level) if b]
        additional_info = " | ".join(bits) if bits else None

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
            "hq_location": offer.get("street"),
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
    additional = (data["additional_info"] or "")[:255]
    price = int(round(data["salary_min"])) if data["salary_min"] is not None else None
    with cnx.cursor() as cursor:
        cursor.execute(query, (
            platform_id,
            data["title"],
            price,
            data["salary_currency"],
            data["url"],
            data["image_url"],
            data["company"],
            data["location"],
            additional,
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


def get_data_and_insert(cnx, keyword, location=None, experience_level=None, employment_type=None):
    global COUNTER
    platform_id = get_platform_id(cnx)

    url = build_url(keyword, location, experience_level, employment_type)
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
    html = response.text

    offers = extract_offers_from_html(html)
    if not offers:
        print("No offers extracted from streaming chunks")
        return

    print(f"Found {len(offers)} offers")

    for offer in offers:
        data = parse_offer(offer)
        if not data:
            continue

        if offer_exists(cnx, platform_id, data["url"]):
            print(f"  SKIP (exists): {data['url']}")
            continue

        try:
            offer_id = insert_offer(cnx, platform_id, data)
            insert_job_detail(cnx, offer_id, data)
            cnx.commit()
        except Exception as e:
            cnx.rollback()
            print(f"  insert error: {e}")
            continue

        COUNTER += 1
        print(f"  +inserted: {data['title']} | {data['company']} | {data.get('location')}")


if __name__ == "__main__":
    print("JustJoinIT scrapper starting...")
    db_config = read_db_config()
    cnx = None

    parser = argparse.ArgumentParser()
    parser.add_argument("keyword")
    parser.add_argument("--loc", default=None, dest="location")
    parser.add_argument("--el", default=None, dest="experience_level")
    parser.add_argument("--emp", default=None, dest="employment_type")
    args = parser.parse_args()

    keyword = args.keyword.replace("'", "")

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connection OK")
        get_data_and_insert(cnx, keyword, args.location, args.experience_level, args.employment_type)
        print(f"Records inserted: {COUNTER}")

    except Error as e:
        print("PostgreSQL error:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
