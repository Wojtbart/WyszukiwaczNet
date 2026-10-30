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
        price = float(cleaned)
        if price > 99_999_999:
            return None, None
        return price, "PLN"
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
    }, timeout=30)

    soup = BeautifulSoup(page.content, "html.parser")

    container = soup.find("div", class_="offersItems")
    if not container:
        print("Nothing found!")
        return

    items = container.find_all("div", class_="listing__item")

    for item_div in items:

        # ------------------ LINK ------------------
        a_tag = item_div.find("a", class_="offerItem", href=True)
        if not a_tag:
            continue

        href = a_tag.get("href", "")
        url = ("https://www.autocentrum.pl" + href) if href.startswith("/") else href

        if offer_exists(cnx, PLATFORM_ID, url):
            continue

        # ------------------ IMAGE ------------------
        img_tag = a_tag.find("img")
        image_url = img_tag["src"] if img_tag and img_tag.has_attr("src") else None

        # ------------------ TITLE ------------------
        title_tag = a_tag.find(class_="offerItem__title")
        title = title_tag.get_text(strip=True) if title_tag else None

        # ------------------ ADDITIONAL INFO ------------------
        version_tag = a_tag.find(class_="offerItem__version")
        additional_info = version_tag.get_text(strip=True) if version_tag else None

        # ------------------ PRICE ------------------
        price_tag = a_tag.find(class_="offerItem__priceTotal")
        price_text = price_tag.get_text(strip=True) if price_tag else None
        price, currency = parse_price(price_text)

        # ------------------ VEHICLE DETAILS FROM QUICKSPECS ------------------
        year = mileage = fuel = gearbox = engine_power = body_type = None

        specs_tag = a_tag.find(class_="offerItem__quickSpecs")
        if specs_tag:
            specs_text = specs_tag.get_text(separator="\n")
            for line in specs_text.split("\n"):
                line = line.strip()
                if not line:
                    continue
                # year
                year_match = re.search(r"\b(19|20)\d{2}\b", line)
                if year_match and year is None:
                    year = int(year_match.group(0))
                # engine power
                km_match = re.search(r"(\d+)\s*KM", line, re.IGNORECASE)
                if km_match and engine_power is None:
                    engine_power = int(km_match.group(1))
                # gearbox
                line_lower = line.lower()
                if "automatyczna" in line_lower and gearbox is None:
                    gearbox = "automatyczna"
                elif ("manualna" in line_lower or "ręczna" in line_lower) and gearbox is None:
                    gearbox = "manualna"
                # fuel
                if fuel is None:
                    for keyword in ("benzyna", "diesel", "hybryda", "elektr", "lpg", "cng"):
                        if keyword in line_lower:
                            fuel = line
                            break

        # ------------------ INSERT OFFER ------------------
        offer_id = insert_offer(cnx, PLATFORM_ID, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": url,
            "image_url": image_url,
            "seller": None,
            "location": None,
            "additional_info": additional_info
        })

        # ------------------ INSERT VEHICLE DETAILS ------------------
        insert_vehicle_details(cnx, offer_id, {
            "year": year,
            "mileage": mileage,
            "fuel": fuel,
            "gearbox": gearbox,
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
        print(f"Records: {COUNTER}")

    except Error as e:
        print("DB error:", e)

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed")
