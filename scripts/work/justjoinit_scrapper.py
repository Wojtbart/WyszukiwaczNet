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


def parse_salary(salary_span):
    if not salary_span:
        return None, None, "PLN", "monthly", None

    undisclosed = salary_span.find("div")
    if undisclosed:
        return None, None, "PLN", "monthly", undisclosed.get_text(strip=True)

    spans = salary_span.find_all("span")
    nums = []
    salary_unit = None
    for s in spans:
        t = s.get_text(strip=True).replace("\xa0", "").replace(" ", "")
        if t.isdigit():
            nums.append(int(t))
        elif "/" in t:
            salary_unit = t

    salary_min = nums[0] if nums else None
    salary_max = nums[1] if len(nums) > 1 else salary_min
    currency = salary_unit.split("/")[0] if salary_unit else "PLN"
    salary_type = "hourly" if salary_unit and "/h" in salary_unit else "monthly"

    if salary_min is not None and salary_max is not None and salary_min != salary_max:
        salary_raw = f"{salary_min} - {salary_max} {salary_unit}" if salary_unit else f"{salary_min} - {salary_max}"
    elif salary_min is not None:
        salary_raw = f"{salary_min} {salary_unit}" if salary_unit else str(salary_min)
    else:
        salary_raw = None

    return salary_min, salary_max, currency, salary_type, salary_raw


def parse_offer(card):
    try:
        href = card.get("href", "")
        if not href:
            return None
        url = BASE_URL + href if href.startswith("/") else href

        title_tag = card.find("h3")
        if not title_tag:
            return None
        title = title_tag.get_text(strip=True)

        img_tag = card.find("img", id="offerCardCompanyLogo")
        image_url = img_tag.get("src") if img_tag else None

        # company name: p sibling after ApartmentRoundedIcon svg
        company = None
        apt_svg = card.find("svg", attrs={"data-testid": "ApartmentRoundedIcon"})
        if apt_svg:
            p = apt_svg.find_next_sibling("p")
            company = p.get_text(strip=True) if p else None

        # salary
        salary_span = card.find("span", class_=lambda c: c and "MuiTypography-lead4" in c)
        salary_min, salary_max, salary_currency, salary_type, salary_raw = parse_salary(salary_span)

        # city from location button
        city_span = card.find("span", class_=lambda c: c and "mui-1o4wo1x" in c)
        city = city_span.get_text(strip=True) if city_span else None

        # remote indicator: p sibling after ShareLocationRoundedIcon svg
        remote_p = None
        share_svg = card.find("svg", attrs={"data-testid": "ShareLocationRoundedIcon"})
        if share_svg:
            p = share_svg.find_next_sibling("p")
            if p and p.get_text(strip=True).lower() == "remote":
                remote_p = "Remote"

        if city and remote_p:
            location = f"{city} / {remote_p}"
        elif remote_p:
            location = remote_p
        else:
            location = city

        # days left + technologies from chip divs
        days_left = None
        technologies = []
        seen = set()
        chips = card.find_all("div", class_=lambda c: c and "mui-jikuwi" in c)
        for chip in chips:
            text = chip.get_text(strip=True)
            if not text or text in seen:
                continue
            seen.add(text)
            if re.match(r'\d+d left', text):
                days_left = text
            elif "click Apply" not in text and "Super offer" not in text:
                technologies.append(text)

        tech_str = ", ".join(technologies) if technologies else None

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
            "technologies": tech_str,
            "location": location,
            "work_location": location,
            "hq_location": None,
            "additional_info": days_left,
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
    soup = BeautifulSoup(response.content, "html.parser")

    container = soup.find("div", id="up-offers-list")
    if not container:
        print("up-offers-list not found — site may require JS rendering")
        return

    cards = container.find_all("a", class_="offer-card")
    if not cards:
        print("No offer-card elements found")
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
