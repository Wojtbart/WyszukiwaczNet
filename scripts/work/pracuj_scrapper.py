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

BASE_URL = "https://it.pracuj.pl"
PLATFORM_NAME = "pracuj"
COUNTER = 0


def parse_salary(raw):
    if not raw:
        return None, None, "PLN", "monthly"
    text = raw.replace('\xa0', ' ').replace('–', '-').replace('—', '-')
    nums = []
    for m in re.findall(r'\d[\d ]*', text):
        try:
            n = int(m.replace(' ', ''))
            if n >= 10:
                nums.append(n)
        except ValueError:
            pass
    salary_min = nums[0] if nums else None
    salary_max = nums[1] if len(nums) > 1 else salary_min
    currency = "EUR" if "EUR" in raw else "USD" if "USD" in raw else "PLN"
    salary_type = "hourly" if "godz" in raw else "daily" if "dzień" in raw else "monthly"
    return salary_min, salary_max, currency, salary_type


def get_platform_id(cnx, platform_name):
    with cnx.cursor() as cursor:
        cursor.execute("SELECT id FROM platforms WHERE name = %s", (platform_name,))
        row = cursor.fetchone()
        if not row:
            raise Exception(f"Platform '{platform_name}' not found in DB")
        return row[0]


def offer_exists(cnx, platform_id, url):
    with cnx.cursor() as cursor:
        cursor.execute(
            "SELECT 1 FROM offers WHERE platform_id = %s AND url = %s LIMIT 1",
            (platform_id, url)
        )
        return cursor.fetchone() is not None


def insert_offer(cnx, platform_id, offer):
    query = """
        INSERT INTO offers
        (platform_id, title, price, currency, url, image_url,
         seller_name, location, additional_info, created_at)
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING id
    """
    additional = (offer["additional_info"] or "")[:255]
    with cnx.cursor() as cursor:
        cursor.execute(query, (
            platform_id,
            offer["title"],
            offer["salary_min"],
            offer["salary_currency"],
            offer["url"],
            offer["image_url"],
            offer["company"],
            offer["work_location"],
            additional,
            datetime.now(timezone.utc)
        ))
        return cursor.fetchone()[0]


def insert_job_detail(cnx, offer_id, job):
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
            job["salary_min"],
            job["salary_max"],
            job["salary_currency"],
            job["salary_type"],
            job["salary_raw"],
            job["technologies"],
            job["work_location"],
            job["hq_location"],
        ))


def parse_locations(offer_div):
    work_location = None
    hq_location = None
    region = offer_div.find("h4", attrs={"data-test": "text-region"})
    if region:
        for span in region.find_all("span"):
            span_text = span.get_text(strip=True)
            strong = span.find("strong")
            if not strong:
                continue
            strong_text = strong.get_text(strip=True)
            if "Miejsce pracy" in span_text:
                work_location = strong_text
            elif "Siedziba firmy" in span_text:
                hq_location = strong_text
    return work_location, hq_location


def parse_offer(offer_div):
    try:
        title_tag = offer_div.find("a", attrs={"data-test": "link-offer-title"})
        if not title_tag:
            return None
        title = title_tag.get_text(strip=True)

        link_tag = offer_div.find("a", attrs={"data-test": "link-offer"})
        href = (link_tag or title_tag).get("href", "")
        if not href:
            return None
        # strip tracking params — keep clean URL up to first ?
        clean_href = href.split("?")[0]
        url = clean_href if clean_href.startswith("http") else "https://www.pracuj.pl" + clean_href

        img_tag = offer_div.find("img", attrs={"data-test": "image-responsive"})
        image_url = img_tag.get("src") if img_tag else None

        company_tag = offer_div.find("h3", attrs={"data-test": "text-company-name"})
        company = company_tag.get_text(strip=True) if company_tag else None

        salary_tag = offer_div.find("div", attrs={"data-test": "offer-salary"})
        salary_raw = salary_tag.get_text(strip=True) if salary_tag else None
        salary_min, salary_max, salary_currency, salary_type = parse_salary(salary_raw)

        tech_tags = offer_div.find_all("span", attrs={"data-test": "technologies-item"})
        technologies = ", ".join(t.get_text(strip=True) for t in tech_tags) or None

        work_location, hq_location = parse_locations(offer_div)

        info_tags = offer_div.find_all("li", attrs={"data-test": re.compile(r"^offer-additional-info-")})
        additional_info = " | ".join(t.get_text(strip=True) for t in info_tags) or None

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
            "work_location": work_location,
            "hq_location": hq_location,
            "additional_info": additional_info,
        }
    except Exception as e:
        print(f"  parse error: {e}")
        return None


def build_url(phrase, location=None, employment_level=None, contract_type=None):
    encoded = urllib.parse.quote(phrase)
    if location:
        encoded_loc = urllib.parse.quote(location)
        path = f"{BASE_URL}/praca/{encoded};kw/{encoded_loc};wp"
    else:
        path = f"{BASE_URL}/praca/{encoded};kw"
    params = ["rd=0"]
    if employment_level is not None:
        params.append(f"et={employment_level}")
    params.append("sc=0")
    if contract_type is not None:
        params.append(f"tc={contract_type}")
    return f"{path}?{'&'.join(params)}"


def get_data_and_insert(cnx, phrase, location=None, employment_level=None, contract_type=None):
    global COUNTER
    platform_id = get_platform_id(cnx, PLATFORM_NAME)

    url = build_url(phrase, location, employment_level, contract_type)
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

    section = soup.find("div", attrs={"data-test": "section-offers"})
    if not section:
        print("section-offers not found — site may use JS rendering")
        return

    offers = section.find_all("div", attrs={"data-test": "default-offer"})
    if not offers:
        print("No default-offer divs found")
        return

    print(f"Found {len(offers)} offers")

    for offer_div in offers:
        data = parse_offer(offer_div)
        if not data:
            continue

        if offer_exists(cnx, platform_id, data["url"]):
            print(f"  SKIP (exists): {data['url']}")
            continue

        offer_id = insert_offer(cnx, platform_id, data)
        insert_job_detail(cnx, offer_id, data)
        cnx.commit()

        COUNTER += 1
        print(f"  +inserted: {data['title']} | {data['company']} | {data.get('work_location')}")


if __name__ == "__main__":
    print("Pracuj.pl scrapper starting...")
    db_config = read_db_config()
    cnx = None

    parser = argparse.ArgumentParser()
    parser.add_argument("phrase")
    parser.add_argument("--loc", default=None, dest="location")
    parser.add_argument("--et", default=None, type=int, dest="employment_level")
    parser.add_argument("--tc", default=None, type=int, dest="contract_type")
    args = parser.parse_args()

    phrase = args.phrase.replace("'", "")

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connection OK")
        get_data_and_insert(cnx, phrase, args.location, args.employment_level, args.contract_type)
        print(f"Records inserted: {COUNTER}")

    except Error as e:
        print("PostgreSQL error:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
