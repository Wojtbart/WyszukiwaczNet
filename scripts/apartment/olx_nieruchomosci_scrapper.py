import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import psycopg2
from psycopg2 import Error
import sys
import argparse
from datetime import datetime, timezone
import re
import unicodedata
from urllib.parse import urlencode

sys.stdout.reconfigure(encoding='utf-8')

PLATFORM_ID = 26  # OlxNieruchomosci
COUNTER = 0

_CHAR_MAP = str.maketrans({
    'ł': 'l', 'Ł': 'l',
    'ą': 'a', 'Ą': 'a',
    'ę': 'e', 'Ę': 'e',
    'ó': 'o', 'Ó': 'o',
    'ś': 's', 'Ś': 's',
    'ź': 'z', 'Ź': 'z',
    'ż': 'z', 'Ż': 'z',
    'ć': 'c', 'Ć': 'c',
    'ń': 'n', 'Ń': 'n',
})

def normalize_city(city):
    city = city.translate(_CHAR_MAP)
    city = unicodedata.normalize('NFKD', city).encode('ASCII', 'ignore').decode('utf-8')
    return city.lower().replace(' ', '-')


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


def build_url(city_normalized, is_private, filters=None):
    base = f"https://www.olx.pl/nieruchomosci/{city_normalized}/"
    params = {"search[order]": "created_at:desc"}
    if is_private:
        params["search[private_business]"] = "private"
    if filters:
        if filters.get("price_from") is not None:
            params["search[filter_float_price:from]"] = filters["price_from"]
        if filters.get("price_to") is not None:
            params["search[filter_float_price:to]"] = filters["price_to"]
    return base + "?" + urlencode(params)


def get_data_and_insert(cnx, phrase, filters=None):
    global COUNTER

    city_raw = phrase[0]
    city_norm = normalize_city(city_raw)
    is_private = "prywatne" in [p.lower() for p in phrase]

    URL = build_url(city_norm, is_private, filters)
    print(URL)

    try:
        response = requests.get(URL, headers={
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8",
        }, timeout=30)
    except requests.exceptions.Timeout:
        print("Request timed out")
        return

    soup = BeautifulSoup(response.content, "html.parser")
    listings = soup.find_all("div", {"data-cy": "l-card"})

    if not listings:
        print("Nothing found!")
        return

    for listing in listings:

        # ------------------ LINK ------------------
        link_tag = listing.find("a", href=True)
        if not link_tag:
            continue

        url = link_tag["href"]
        if url.startswith("/"):
            url = "https://www.olx.pl" + url

        if offer_exists(cnx, PLATFORM_ID, url):
            continue

        # ------------------ TITLE ------------------
        title_tag = listing.find("h4")
        title = title_tag.text.strip() if title_tag else None

        # ------------------ IMAGE ------------------
        img_tag = listing.find("img")
        image_url = img_tag["src"] if img_tag and img_tag.has_attr("src") else None

        # ------------------ PRICE ------------------
        price_tag = listing.find("p", {"data-testid": "ad-price"})
        price, currency = parse_price(price_tag.text if price_tag else None)

        # ------------------ LOCATION ------------------
        location_tag = listing.find("p", {"data-testid": "location-date"})
        location = location_tag.text.strip() if location_tag else None

        # ------------------ ADDITIONAL INFO (area, type badges) ------------------
        badges = listing.find_all("div", class_="PKliU")
        additional_info = ", ".join(b.get_text(strip=True) for b in badges) or None

        # ------------------ INSERT ------------------
        insert_offer(cnx, PLATFORM_ID, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": url,
            "image_url": image_url,
            "seller": None,
            "location": location,
            "additional_info": additional_info
        })

        COUNTER += 1

    cnx.commit()


if __name__ == "__main__":
    print("OLX Nieruchomosci scraper starting...")

    parser = argparse.ArgumentParser()
    parser.add_argument("phrase", nargs="+")
    parser.add_argument("--price-from", type=float, default=None)
    parser.add_argument("--price-to", type=float, default=None)
    args = parser.parse_args()

    phrase = []
    for token in args.phrase:
        phrase.extend(token.split())

    filters = {
        "price_from": args.price_from,
        "price_to": args.price_to,
    }

    db_config = read_db_config()
    cnx = None

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connected")

        get_data_and_insert(cnx, phrase, filters)
        print(f"Records: {COUNTER}")

    except Error as e:
        if cnx:
            cnx.rollback()
        print("DB error:", e)

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed")
