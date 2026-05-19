import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import psycopg2
from psycopg2 import Error
import sys
import argparse
from datetime import datetime, timezone
from urllib.parse import urlencode
import re

sys.stdout.reconfigure(encoding='utf-8')

PLATFORM_ID = 7  # autocentrum
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

def parse_mileage(text):
    if not text:
        return None

    cleaned = re.sub(r"\s+", "", text)

    match = re.search(r"\d+", cleaned)
    if match:
        return int(match.group(0))

    return None

def parse_engine_power(text):
    if not text:
        return None

    match = re.search(r"(\d+)\s*KM", text)
    return int(match.group(1)) if match else None

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

def insert_vehicle_details(cnx, offer_id, vehicle):
    query = """
        INSERT INTO vehicle_details
        (offer_id, production_year, mileage, fuel_type, gearbox, engine_power, body_type)
        VALUES (%s,%s,%s,%s,%s,%s,%s)
    """
    with cnx.cursor() as cursor:
        cursor.execute(query, (
            offer_id,
            vehicle["year"],
            vehicle["mileage"],
            vehicle["fuel"],
            vehicle["gearbox"],
            vehicle["engine_power"],
            vehicle["body_type"],
        ))

FUEL_TYPE_MAP = {
    "petrol":       9,
    "lpg":          10,
    "diesel":       1,
    "electric":     8,
    "plugin-hybrid": 5,
}


def build_url(phrase, filters=None):
    parts = [p.lower() for p in phrase[:2]]
    path = "/".join(parts)
    base = f"https://www.autocentrum.pl/ogloszenia/{path}/"

    params = {"order": "date-desc"}
    has_filter = False

    if filters:
        fuel_id = FUEL_TYPE_MAP.get(filters.get("fuel", ""))
        if fuel_id:
            params["engineType[]"] = fuel_id
            has_filter = True
        if filters.get("capacity_from") is not None:
            params["engineCapacityFrom"] = filters["capacity_from"]
            has_filter = True
        if filters.get("capacity_to") is not None:
            params["engineCapacityTo"] = filters["capacity_to"]
            has_filter = True
        if filters.get("price_from") is not None:
            params["priceFrom"] = int(filters["price_from"])
            has_filter = True
        if filters.get("price_to") is not None:
            params["priceTo"] = int(filters["price_to"])
            has_filter = True

    if has_filter:
        params["s"] = 1

    return base + "?" + urlencode(params)


def get_data_and_insert(cnx, phrase, filters=None):
    global COUNTER

    URL = build_url(phrase, filters)
    print(URL)

    page = requests.get(URL, headers={
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
        "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8",
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Connection": "keep-alive",
    })

    soup = BeautifulSoup(page.content, "html.parser")

    listings = soup.find("div", class_="offer-list-wrapper")
    if not listings:
        print("Nothing found!")
        return

    articles = listings.find_all("div", class_="offer-item")

    for listing in articles:

        # ------------------ LINK ------------------
        link_tag = listing.find("a", href=True)
        if not link_tag:
            continue

        url = "https://www.autocentrum.pl" + link_tag["href"]

        if offer_exists(cnx, PLATFORM_ID, url):
            continue

        # ------------------ IMAGE ------------------
        img_tag = listing.find("img")
        image_url = img_tag["src"] if img_tag and img_tag.has_attr("src") else None

        # ------------------ TITLE ------------------
        title_tag = listing.find("h2")
        title = title_tag.text.strip() if title_tag else None

        # ------------------ ADDITIONAL INFO ------------------
        sub_tag = listing.find("div", class_="sub-hd")
        additional_info = sub_tag.text.strip() if sub_tag else None

        # ------------------ LOCATION ------------------
        address_tag = listing.find("div", class_="address")
        location = address_tag.text.strip() if address_tag else None

        # ------------------ PRICE ------------------
        price_span = listing.select_one(".price span")
        price, currency = parse_price(price_span.text if price_span else None)

        # ------------------ OTHER DETAILS ------------------
        mileage = year = fuel = gearbox = body_type = engine = None
        engine_power = None

        labels = listing.select_one(".labels")
        if labels:
            items = labels.find_all("div")

            for item in items:

                text = item.get_text(strip=True).lower()

                # ---- FUEL TYPE ----
                if item.find("i", class_="icon-fuel"):
                    fuel = text
                    continue

                # ---- BODY TYPE ----
                if item.find("i", class_="icon-cab"):
                    body_type = text
                    continue

                # ---- MILEAGE ----
                if "km" in text:
                    mileage = parse_mileage(text)
                    continue

                # ---- ENGINE POWER ----
                if item.find("i", class_="icon-rocket"):
                    engine_power = parse_engine_power(text)
                    continue

                # ---- YEAR ----
                if text.isdigit() and len(text) == 4:
                    year = int(text)
                    continue

                # ---- GEARBOX ----
                if "manualna" in text or "automatyczna" in text:
                    gearbox = text
                    continue
               
        # ------------------ INSERT OFFER ------------------
        offer_id = insert_offer(cnx, PLATFORM_ID, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": url,
            "image_url": image_url,
            "seller": None,
            "location": location,
            "additional_info": additional_info
        })

        # ------------------ INSERT VEHICLE DETAILS ------------------
        insert_vehicle_details(cnx, offer_id, {
            "year": year,
            "mileage": mileage,
            "fuel": fuel,
            "gearbox": gearbox,
            "engine": engine,
            "engine_power": engine_power,
            "body_type": body_type
        })

        COUNTER += 1
    cnx.commit()

if __name__ == "__main__":
    print("Autocentrum scraper starting...")

    parser = argparse.ArgumentParser()
    parser.add_argument("phrase", nargs="+")
    parser.add_argument("--fuel", default=None)
    parser.add_argument("--capacity-from", type=int, default=None)
    parser.add_argument("--capacity-to", type=int, default=None)
    parser.add_argument("--price-from", type=float, default=None)
    parser.add_argument("--price-to", type=float, default=None)
    args = parser.parse_args()

    phrase = []
    for token in args.phrase:
        phrase.extend(token.split())

    filters = {
        "fuel": args.fuel,
        "capacity_from": args.capacity_from,
        "capacity_to": args.capacity_to,
        "price_from": args.price_from,
        "price_to": args.price_to,
    }

    db_config = read_db_config()
    cnx = None

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connected")

        get_data_and_insert(cnx, phrase, filters)
        print("Inserted:", COUNTER)

    except Error as e:
        print("DB error:", e)

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed")
