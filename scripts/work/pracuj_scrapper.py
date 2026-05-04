import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import re
from datetime import datetime, timezone
import psycopg2
from psycopg2 import Error

sys.stdout.reconfigure(encoding='utf-8')

BASE_URL = "https://www.pracuj.pl"
PLATFORM_NAME = "PracaPl"
COUNTER = 0


def parse_salary(text):
    if not text:
        return None, None
    digits = re.sub(r"[^\d]", "", text.split("-")[0])
    try:
        return float(digits), "PLN"
    except ValueError:
        return None, None


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
    with cnx.cursor() as cursor:
        cursor.execute(query, (
            platform_id,
            offer["title"],
            offer["price"],
            offer["currency"],
            offer["url"],
            offer["image_url"],
            offer["company"],
            offer["location"],
            offer["additional_info"],
            datetime.now(timezone.utc)
        ))
        return cursor.fetchone()[0]


def parse_listing(article):
    try:
        # title + url
        title_tag = article.find("a", class_=lambda c: c and "offer-title" in c)
        if not title_tag:
            title_tag = article.find("h2")
        if not title_tag:
            title_tag = article.find("a", href=re.compile(r"/oferta-pracy/"))
        if not title_tag:
            return None

        title = title_tag.get_text(strip=True)
        href = title_tag.get("href", "")
        if not href:
            return None
        url = BASE_URL + href if href.startswith("/") else href

        # company
        company_tag = article.find(class_=lambda c: c and any(
            k in c for k in ("employer", "company", "firma")
        ))
        company = company_tag.get_text(strip=True) if company_tag else None

        # location
        loc_tag = article.find(class_=lambda c: c and any(
            k in c for k in ("location", "city", "miejsce", "lokalizacja")
        ))
        location = loc_tag.get_text(strip=True) if loc_tag else None

        # salary
        salary_tag = article.find(class_=lambda c: c and any(
            k in c for k in ("salary", "wynagrodzenie", "wage")
        ))
        salary_text = salary_tag.get_text(strip=True) if salary_tag else None
        price, currency = parse_salary(salary_text)

        # contract type / additional info
        contract_tag = article.find(class_=lambda c: c and any(
            k in c for k in ("contract", "umowa", "offer-type")
        ))
        additional_info = contract_tag.get_text(strip=True) if contract_tag else salary_text

        return {
            "url": url,
            "title": title,
            "image_url": None,
            "company": company,
            "location": location,
            "price": price,
            "currency": currency,
            "additional_info": additional_info,
        }
    except Exception as e:
        print(f"  parse error: {e}")
        return None


def get_data_and_insert(cnx, phrase):
    global COUNTER
    platform_id = get_platform_id(cnx, PLATFORM_NAME)

    keyword = "+".join(phrase)
    url = f"{BASE_URL}/praca?phrase={keyword}&sort=publication_date"
    print(f"Fetching: {url}")

    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
        "Accept-Language": "pl-PL,pl;q=0.9",
    }
    response = requests.get(url, headers=headers, timeout=15)
    soup = BeautifulSoup(response.content, "html.parser")

    # praca.pl wraps each offer in article or li with data-offer-id
    listings = soup.find_all("article", attrs={"data-offer-id": True})
    if not listings:
        listings = soup.find_all("li", class_=lambda c: c and "offer" in c)
    if not listings:
        listings = soup.select("div[class*='offer-item'], div[class*='offerItem']")

    if not listings:
        print("No listings found")
        return

    print(f"Found {len(listings)} offer elements")

    for item in listings:
        data = parse_listing(item)
        if not data:
            continue

        if offer_exists(cnx, platform_id, data["url"]):
            print(f"  SKIP (exists): {data['url']}")
            continue

        offer_id = insert_offer(cnx, platform_id, {
            "title": data["title"],
            "price": data["price"],
            "currency": data["currency"],
            "url": data["url"],
            "image_url": data["image_url"],
            "company": data["company"],
            "location": data["location"],
            "additional_info": data["additional_info"],
        })

        COUNTER += 1
        cnx.commit()
        print(f"  +inserted: {data['title']} | {data['company']} | {data['location']}")


if __name__ == "__main__":
    print("Praca.pl scrapper starting...")
    db_config = read_db_config()
    cnx = None

    if len(sys.argv) < 2:
        print("Usage: python praca_scrapper.py <keyword>")
        print("  e.g. python praca_scrapper.py developer")
        sys.exit(1)

    phrase = []
    for arg in sys.argv[1:]:
        arg = arg.replace("'", "")
        if " " in arg:
            phrase.extend(arg.split())
        else:
            phrase.append(arg)

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connection OK")
        get_data_and_insert(cnx, phrase)
        print(f"Records inserted: {COUNTER}")

    except Error as e:
        print("PostgreSQL error:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
