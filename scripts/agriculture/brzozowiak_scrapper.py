import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import psycopg2
from psycopg2 import Error
from datetime import datetime, timezone
import re

sys.stdout.reconfigure(encoding='utf-8')

COUNTER = 0
BASE_URL = "https://brzozowiak.pl"


def offer_exists(cnx, platform_id, url):
    query = "SELECT 1 FROM offers WHERE platform_id = %s AND url = %s LIMIT 1"
    with cnx.cursor() as cursor:
        cursor.execute(query, (platform_id, url))
        return cursor.fetchone() is not None


def parse_price(text):
    if not text:
        return None, None
    cleaned = re.sub(r"[^\d,.]", "", text)
    if not cleaned:
        return None, None
    if "," in cleaned:
        cleaned = cleaned.replace(".", "").replace(",", ".")
    else:
        cleaned = cleaned.replace(".", "")
    try:
        return float(cleaned), "PLN"
    except ValueError:
        return None, None


def get_platform_id(cnx, platform_name):
    with cnx.cursor() as cursor:
        cursor.execute("SELECT id FROM platforms WHERE name = %s", (platform_name,))
        row = cursor.fetchone()
        if not row:
            raise Exception(f"Platform '{platform_name}' not found in DB")
        return row[0]


def extract_image_url(style_attr):
    if not style_attr:
        return None
    match = re.search(r"url\('([^']+)'\)", style_attr)
    if not match:
        return None
    url = match.group(1)
    if "no_foto" in url:
        return None
    return url


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
            None,
            offer["location"],
            offer["additional_info"],
            datetime.now(timezone.utc)
        ))
        return cursor.fetchone()[0]


def get_data_and_insert(cnx, phrase):
    global COUNTER
    platform_id = get_platform_id(cnx, "brzozowiak")

    search_phrase = "+".join(phrase)
    url = f"{BASE_URL}/?tx={search_phrase}"
    print(url)

    page = requests.get(url, headers={
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
        "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8",
    }, timeout=30)

    soup = BeautifulSoup(page.content, "html.parser")
    listings = soup.find_all("div", class_="listRow")

    if not listings:
        print("Nothing found!")
        return

    for listing in listings:
        # ------------------ LINK & IMAGE ------------------
        left_cell = listing.find("div", class_="leftCell")
        link_tag = left_cell.find("a", class_="aImgT") if left_cell else None
        if not link_tag:
            continue

        relative_url = link_tag.get("href", "")
        if not relative_url:
            continue
        full_url = BASE_URL + relative_url if relative_url.startswith("/") else relative_url

        image_url = extract_image_url(link_tag.get("style", ""))

        if offer_exists(cnx, platform_id, full_url):
            continue

        # ------------------ RIGHT CELL ------------------
        right_cell = listing.find("div", class_="rightCell")
        if not right_cell:
            continue

        # ------------------ TITLE ------------------
        h2 = right_cell.find("h2")
        title_tag = h2.find("a") if h2 else None
        title = title_tag.text.strip() if title_tag else None

        # ------------------ DATE ------------------
        time_tag = right_cell.find("time")
        date_str = time_tag.text.strip() if time_tag else ""

        # ------------------ DESCRIPTION ------------------
        short_tag = right_cell.find("p", class_="short")
        description = short_tag.text.strip() if short_tag else None

        # ------------------ PRICE ------------------
        price_div = right_cell.find("div", class_="price")
        price_span = price_div.find("span") if price_div else None
        price, currency = parse_price(price_span.text if price_span else None)

        # ------------------ LOCATION ------------------
        address_tag = right_cell.find("address")
        location_raw = address_tag.text.strip() if address_tag else ""
        location = f"{location_raw} - {date_str}" if date_str else location_raw

        # ------------------ INSERT ------------------
        insert_offer(cnx, platform_id, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": full_url,
            "image_url": image_url,
            "location": location,
            "additional_info": description,
        })
        COUNTER += 1

    cnx.commit()


if __name__ == "__main__":
    print("Brzozowiak scraper starting...")

    db_config = read_db_config()
    cnx = None

    if len(sys.argv) <= 1:
        print("Incorrect number of arguments!")
        sys.exit()

    phrase = []
    for arg in sys.argv[1:]:
        phrase.extend(arg.split())

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connected")

        get_data_and_insert(cnx, phrase)
        print("Records:", COUNTER)

    except Error as e:
        print("DB error:", e)
        if cnx:
            cnx.rollback()

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed")
