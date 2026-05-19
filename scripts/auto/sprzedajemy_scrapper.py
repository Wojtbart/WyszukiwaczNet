import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import psycopg2
from psycopg2 import Error
import sys
import re
import argparse
from datetime import datetime, timezone
from urllib.parse import urlencode

sys.stdout.reconfigure(encoding='utf-8')

COUNTER = 0

FUEL_MAP = {
    "petrol":        1996,
    "lpg":           2001,
    "diesel":        2011,
    "plugin-hybrid": 2016,
    "electric":      20700,
}

# ========================
# HELPERS
# ========================
def parse_price(price_text):
    if not price_text:
        return None, None

    price_text = price_text.replace(" ", "").replace("zł", "")
    try:
        return float(price_text), "PLN"
    except ValueError:
        return None, None

def parse_year(text):
    if not text:
        return None
    digits = re.sub(r"[^\d]", "", text)
    return int(digits) if len(digits) == 4 else None

def parse_mileage(text):
    if not text:
        return None
    digits = re.sub(r"[^\d]", "", text)
    return int(digits) if digits else None

# ========================
# DB FUNCTIONS
# ========================
def get_platform_id(cnx, platform_name):
    with cnx.cursor() as cursor:
        cursor.execute(
            "SELECT id FROM platforms WHERE name = %s",
            (platform_name,)
        )
        row = cursor.fetchone()
        if not row:
            raise Exception(f"Platform {platform_name} not found")
        return row[0]

def offer_exists(cnx, platform_id, url):
    with cnx.cursor() as cursor:
        cursor.execute("""
            SELECT 1
            FROM offers
            WHERE platform_id = %s AND url = %s
            LIMIT 1
        """, (platform_id, url))
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
            offer["seller"],
            offer["location"],
            offer["additional_info"],
            datetime.now(timezone.utc)
        ))
        return cursor.fetchone()[0]

def insert_vehicle_details(cnx, offer_id, vehicle):
    query = """
        INSERT INTO vehicle_details
        (offer_id, production_year, mileage, fuel_type, gearbox, engine_power)
        VALUES (%s,%s,%s,%s,%s,%s)
    """
    with cnx.cursor() as cursor:
        cursor.execute(query, (
            offer_id,
            vehicle["year"],
            vehicle["mileage"],
            vehicle["fuel"],
            vehicle["gearbox"],
            vehicle["engine_power"]
        ))

# ========================
# SCRAPER LOGIC
# ========================
def build_url(phrase, filters=None):
    parts = [p.lower() for p in phrase[:2]]
    path = "/".join(parts)
    params = {"sort": "inp_srt_date_d", "offset": 0}

    if filters:
        if filters.get("price_from") is not None:
            params["inp_price[from]"] = int(filters["price_from"])
        if filters.get("price_to") is not None:
            params["inp_price[to]"] = int(filters["price_to"])
        if filters.get("capacity_from") is not None:
            params["inp_attribute_476[from]"] = filters["capacity_from"]
        if filters.get("capacity_to") is not None:
            params["inp_attribute_476[to]"] = filters["capacity_to"]
        gearbox = filters.get("gearbox")
        if gearbox == "manual":
            params["inp_attribute_491"] = 2036
        elif gearbox == "automatic":
            params["inp_attribute_491"] = 2041
        fuel_id = FUEL_MAP.get(filters.get("fuel", ""))
        if fuel_id:
            params["inp_attribute_481[0]"] = fuel_id

    return f"https://sprzedajemy.pl/motoryzacja/samochody-osobowe/{path}?" + urlencode(params)


def get_data_and_insert(cnx, phrase, filters=None):
    global COUNTER

    platform_id = get_platform_id(cnx, "sprzedajemy")

    URL = build_url(phrase, filters)
    print(URL)

    response = requests.get(URL, headers={"User-Agent": "Mozilla/5.0"})
    soup = BeautifulSoup(response.content, "html.parser")

    listings = soup.select("ul.normal > li")

    if not listings:
        print("Nothing found")
        return

    for listing in listings:

        photo = listing.find("li", class_="photo")
        details = listing.find("li", class_="details")

        if not details:
            continue

        # ----------------------
        # TITLE + URL
        # ----------------------
        title = None
        link = None

        title_tag = details.find("h2", class_="title")
        if title_tag:
            a = title_tag.find("a", href=True)
            if a:
                title = a.text.strip()
                link = "https://sprzedajemy.pl" + a["href"]

        if not link or offer_exists(cnx, platform_id, link):
            continue

        # ----------------------
        # IMAGE
        # ----------------------
        image_url = None
        if photo:
            img = photo.find("img")
            if img and img.has_attr("src"):
                image_url = img["src"]

        # ----------------------
        # PRICE
        # ----------------------
        price = None
        currency = None

        price_tag = details.find("div", class_="pricing")
        if price_tag:
            span = price_tag.find("span")
            if span:
                price, currency = parse_price(span.text.strip())
        
        # ----------------------
        # SELLER
        # ----------------------
        seller_name = None

        seller_tag = details.find("div", class_="seller-type-info")
        if seller_tag:
            span = seller_tag.find("span")
            if span:
                seller_name = span.text.strip()
        # ----------------------
        # LOCATION
        # ----------------------
        location = None
        address_tag = details.find("div", class_="address")
        if address_tag:
            a = address_tag.find("a")
            if a:
                location = a.text.strip()

        # ----------------------
        # ADDITIONAL INFO
        # ----------------------
        additional_info = None
        attr_tag = details.find("p", class_="attributes")
        if attr_tag:
            additional_info = attr_tag.text.strip()

        # ----------------------
        # VEHICLE DETAILS
        # ----------------------
        year = None
        mileage = None
        fuel_type = None

        if attr_tag:
            spans = attr_tag.find_all("span")
            if len(spans) >= 1:
                year = parse_year(spans[0].text.strip())
            if len(spans) >= 3:
                mileage = spans[2].text.strip()
            if len(spans) >= 5:
                fuel_type = spans[4].text.strip()

        # ----------------------
        # INSERT
        # ----------------------
        offer_id = insert_offer(cnx, platform_id, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": link,
            "image_url": image_url,
            "seller": seller_name,
            "location": location,
            "additional_info": additional_info
        })

        insert_vehicle_details(cnx, offer_id, {
            "year": year,
            "mileage": parse_mileage(mileage),
            "fuel": fuel_type,
            "gearbox": None,
            "engine_power": None
        })

        COUNTER += 1

    cnx.commit()

# ========================
# MAIN
# ========================

if __name__ == "__main__":
    print("Sprzedajemy_scrapper starting...")

    parser = argparse.ArgumentParser()
    parser.add_argument("phrase", nargs="+")
    parser.add_argument("--fuel", default=None)
    parser.add_argument("--gearbox", default=None)
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
        "gearbox": args.gearbox,
        "capacity_from": args.capacity_from,
        "capacity_to": args.capacity_to,
        "price_from": args.price_from,
        "price_to": args.price_to,
    }

    db_config = read_db_config()

    try:
        cnx = psycopg2.connect(**db_config)
        print("Connection to PostgreSQL successful!")

        get_data_and_insert(cnx, phrase, filters)
        print("Records inserted:", COUNTER)

    except Error as e:
        print("Connection error:", e)

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
