import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import sys
import re
import json
from datetime import datetime, timezone
import psycopg2
from psycopg2 import Error

sys.stdout.reconfigure(encoding='utf-8')

COUNTER = 0

# =========================
# HELPERS
# =========================
def parse_mileage(text):
    if not text:
        return None
    digits = re.sub(r"[^\d]", "", text)
    return int(digits) if digits else None

def parse_year(text):
    if not text:
        return None
    digits = re.sub(r"[^\d]", "", text)
    return int(digits) if len(digits) == 4 else None

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
        return float(cleaned), "€"
    except ValueError:
        return None, None

def parse_engine_power(text):
    if not text:
        return None, None

    text = text.strip()
    # szukamy KM (horsepower)
    hp_match = re.search(r"(\d+)\s*KM", text, re.IGNORECASE)
    engine_power_hp = int(hp_match.group(1)) if hp_match else None

    return  engine_power_hp

# =========================
# DATABASE
# =========================
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
    query = """
        SELECT 1
        FROM offers
        WHERE platform_id = %s AND url = %s
        LIMIT 1
    """
    with cnx.cursor() as cursor:
        cursor.execute(query, (platform_id, url))
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

# =========================
# SCRAPER
# =========================
def find_listings_in_json(obj, depth=0):
    """Recursively find listings array in __NEXT_DATA__ JSON."""
    if depth > 6:
        return None
    if isinstance(obj, list) and len(obj) > 0:
        first = obj[0]
        if isinstance(first, dict) and "url" in first and "id" in first and str(first.get("url", "")).startswith("/oferta/"):
            return obj
    if isinstance(obj, dict):
        for v in obj.values():
            result = find_listings_in_json(v, depth + 1)
            if result:
                return result
    elif isinstance(obj, list):
        for item in obj:
            if isinstance(item, dict):
                result = find_listings_in_json(item, depth + 1)
                if result:
                    return result
    return None


def get_data_and_insert(cnx, phrase):
    global COUNTER

    platform_id = get_platform_id(cnx, "autoscout")

    final_phrase = "/".join(phrase).lower()
    URL = f"https://www.autoscout24.pl/lst/{final_phrase}"

    page = requests.get(URL, headers={"User-Agent": "Mozilla/5.0"}, timeout=30)
    soup = BeautifulSoup(page.content, "html.parser")

    script_tag = soup.find("script", {"id": "__NEXT_DATA__"})
    if not script_tag:
        print("Nothing found")
        return

    data = json.loads(script_tag.string)
    listings = find_listings_in_json(data)
    if not listings:
        print("Nothing found")
        return

    for item in listings:
        url_path = item.get("url", "")
        if not url_path:
            continue
        url = "https://www.autoscout24.pl" + url_path

        if offer_exists(cnx, platform_id, url):
            continue

        # =====================
        # TITLE
        # =====================
        title = item.get("title", "") or None

        # =====================
        # IMAGE
        # =====================
        images = item.get("images") or []
        image_url = images[0] if images else None

        # =====================
        # PRICE
        # =====================
        price_data = item.get("price") or {}
        price_raw = price_data.get("priceRaw")
        price = float(price_raw) if price_raw is not None else None
        currency = "EUR"

        # =====================
        # LOCATION + SELLER
        # =====================
        location_data = item.get("location") or {}
        city = location_data.get("city", "")
        country = location_data.get("countryCode", "")
        location = f"{city}, {country}".strip(", ") or None

        seller_data = item.get("seller") or {}
        seller = seller_data.get("companyName") or None

        # =====================
        # VEHICLE DETAILS
        # =====================
        vehicle = item.get("vehicle") or {}
        fuel = vehicle.get("fuel") or None
        gearbox = vehicle.get("transmission") or None

        vehicle_details = item.get("vehicleDetails") or []
        year = mileage = engine_power = None
        for vd in vehicle_details:
            icon = vd.get("iconName", "")
            text = vd.get("data", "")
            if icon == "calendar":
                year = parse_year(text)
            elif icon == "mileage_odometer":
                mileage = parse_mileage(text)
            elif icon == "speedometer":
                engine_power = parse_engine_power(text)

        additional_info = ", ".join(
            vd.get("data", "") for vd in vehicle_details if vd.get("data")
        ) or None

        # =====================
        # INSERT
        # =====================
        offer_id = insert_offer(cnx, platform_id, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": url,
            "image_url": image_url,
            "seller": seller,
            "location": location,
            "additional_info": additional_info
        })

        insert_vehicle_details(cnx, offer_id, {
            "year": year,
            "mileage": mileage,
            "fuel": fuel,
            "gearbox": gearbox,
            "body_type": None,
            "engine_power": engine_power
        })

        COUNTER += 1

    cnx.commit()

# =========================
# MAIN
# =========================
if __name__ == "__main__":
    print("Autoscout_scrapper starting...")

    db_config = read_db_config()
    cnx = None

    if len(sys.argv) <= 1:
        print("Incorrect number of arguments specified!")
        sys.exit()

    phrase = []
    for arg in sys.argv[1:]:
        phrase.extend(arg.split())

    try:
        cnx = psycopg2.connect(**db_config)
        print("Connection to PostgreSQL successful!")

        get_data_and_insert(cnx, phrase)
        print(f"Records: {COUNTER}")

    except Error as e:
        print("Connection error:", e)

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
