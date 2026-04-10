import requests
from bs4 import BeautifulSoup
from python_dbconfig import read_db_config
import psycopg2
from psycopg2 import Error
import sys
from datetime import datetime, timezone
import re

sys.stdout.reconfigure(encoding='utf-8')

PLATFORM_ID = 1  # AMAZON
COUNTER = 0

def offer_exists(cnx, platform_id, url):
    query = """
        SELECT 1 FROM offers
        WHERE platform_id = %s AND url = %s
        LIMIT 1
    """
    with cnx.cursor() as cursor:
        cursor.execute(query, (platform_id, url))
        return cursor.fetchone() is not None


def parse_price(text):
    if not text:
        return None, None

    cleaned = text.replace("\xa0", "").replace("zł", "").strip()
    cleaned = cleaned.replace(",", ".")
    cleaned = re.sub(r"[^\d\.]", "", cleaned)

    parts = cleaned.split(".")
    if len(parts) > 2:
        cleaned = parts[0] + "." + "".join(parts[1:])
    try:
        return float(cleaned), "PLN"
    except:
        return None, None

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
            offer["seller"],
            offer["location"],
            offer["additional_info"],
            datetime.now(timezone.utc)
        ))
        return cursor.fetchone()[0]

def get_data_and_insert(cnx, phrase):
    global COUNTER

    final_phrase = "+".join(phrase)
    URL = f"https://www.amazon.pl/s?k={final_phrase}"

    page = requests.get(URL, headers={
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8"
    })

    soup = BeautifulSoup(page.content, "html.parser")

    listings = soup.select(".s-main-slot div[data-component-type='s-search-result']")

    if not listings:
        print("Nothing found!")
        return

    for listing in listings:
        # ---------- TITLE ----------
        title_tag = listing.select_one("h2 span")
        if not title_tag:
            continue

        title = title_tag.get_text(strip=True)

        # ---------- LINK ----------
        link_tag = listing.select_one(".a-link-normal")

        if not link_tag:
            continue

        url  = "https://www.amazon.pl" + link_tag["href"]

        if offer_exists(cnx, PLATFORM_ID, url):
            continue
        
        # ---------- IMAGE ----------
        img_tag = listing.select_one("img.s-image")
        image_url = img_tag["src"] if img_tag else None
        # ---------- PRICE ----------
        price = None
        currency = None

        whole = listing.select_one(".a-price-whole")
        fraction = listing.select_one(".a-price-fraction")

        if whole:
            price_text = whole.text.replace("\xa0", "")

            if fraction:
                price_text += "," + fraction.text

            price, currency = parse_price(price_text)

        # ---------- DELIVERY ----------
        additional_info = None
        delivery = listing.select_one(".udm-primary-delivery-message")

        if delivery:
            additional_info = delivery.get_text(strip=True)

        insert_offer(cnx, PLATFORM_ID, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": url,
            "image_url": image_url,
            "seller": "Amazon",
            "location": None,
            "additional_info": additional_info
        })

        COUNTER += 1
    cnx.commit()

if __name__ == "__main__":

    print("Amazon scraper starting...")

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
        print("Inserted:", COUNTER)

    except Error as e:
        print("DB error:", e)

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed")
