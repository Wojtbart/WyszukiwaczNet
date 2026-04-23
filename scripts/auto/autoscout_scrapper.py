import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import sys
import re
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

    clean = re.sub(r"[^\d,\.]", "", text)
    clean = clean.replace(",", ".")
    try:
        return float(clean), "€"
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
def get_data_and_insert(cnx, phrase):
    global COUNTER

    platform_id = get_platform_id(cnx, "autoscout")

    final_phrase = "/".join(phrase).lower()
    URL = f"https://www.autoscout24.pl/lst/{final_phrase}"

    page = requests.get(URL, headers={"User-Agent": "Mozilla/5.0"})
    soup = BeautifulSoup(page.content, "html.parser")

    listings = soup.find("main")
    if not listings:
        print("Nothing found")
        return
    articles = listings.find_all("article")

    for listing in articles:
        # =====================
        # LINK
        # =====================
        #AKTUALNIE JEST TUTAJ PROBLEM Z WYCIAGNIECIEM LINKU< GDY SCRAPUJĘ STRONE
        link_tag = listing.find("a", recursive=False)
        if not link_tag:
            continue

        url = link_tag["href"]

        if offer_exists(cnx, platform_id, url):
            continue

        # =====================
        # TITLE
        # =====================
        title_spans = listing.select("h2 span")
        title = " ".join([s.text.strip() for s in title_spans if s.text.strip()])

        # =====================
        # IMAGE
        # =====================
        img_tag = listing.select_one("img[data-testid='list-item-image']")
        image_url = img_tag["src"] if img_tag and img_tag.has_attr("src") else None

        # =====================
        # PRICE
        # =====================
        price_tag = listing.select_one("[data-testid='regular-price']")
        price, currency = parse_price(price_tag.text if price_tag else None)

        # =====================
        # SELLER + LOCATION
        # =====================
        seller_tag = listing.select_one("[data-testid='dealer-company-name']")
        location_tag = listing.select_one("[data-testid='dealer-address']")

        seller = seller_tag.text.strip() if seller_tag else None
        location = location_tag.text.strip() if location_tag else None

        # =====================
        # VEHICLE DETAILS
        # =====================
        year = mileage = fuel = gearbox = None

        calendar = listing.select_one("[data-testid='VehicleDetails-calendar']")
        mileage_tag = listing.select_one("[data-testid='VehicleDetails-mileage_odometer']")
        fuel_tag = listing.select_one("[data-testid='VehicleDetails-gas_pump']")
        power_tag = listing.select_one("[data-testid='VehicleDetails-speedometer']")

        if calendar:
            year = parse_year(calendar.text)

        if mileage_tag:
            mileage = parse_mileage(mileage_tag.text)

        if fuel_tag:
            fuel = fuel_tag.text.strip()

        # Autoscout często nie pokazuje gearbox bezpośrednio
        gearbox = None

        additional_info = engine_power = power_tag.text.strip() if power_tag else None

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
            "engine_power": parse_engine_power(engine_power)
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
        print("Records inserted:", COUNTER)

    except Error as e:
        print("Connection error:", e)

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
