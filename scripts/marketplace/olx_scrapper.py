import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import psycopg2
from psycopg2 import Error
import sys
from datetime import datetime, timezone
import re

sys.stdout.reconfigure(encoding='utf-8')

PLATFORM_ID = 5 # OLX
COUNTER = 0

def parse_year_and_mileage(text):
    if not text:
        return None, None

    year_match = re.search(r"\b(19|20)\d{2}\b", text)
    mileage_match = re.search(r"([\d\s]+)\s*km", text)

    year = int(year_match.group(0)) if year_match else None

    mileage = None
    if mileage_match:
        mileage_digits = re.sub(r"\s+", "", mileage_match.group(1))
        mileage = int(mileage_digits)

    return year, mileage

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

    digits_only = re.sub(r"[^\d]", "", text)

    if not digits_only:
        return None, None

    return float(digits_only), "PLN"

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

    final_phrase = "-".join(phrase).lower()
    URL = f"https://www.olx.pl/oferty/q-{final_phrase}/"

    page = requests.get(URL, headers={
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
        "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8",
    })

    soup = BeautifulSoup(page.content, "html.parser")

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

        # ------------------ YEAR & MILEAGE ------------------
        year = None
        mileage = None

        params_section = listing.find("div", attrs={'color': 'text-global-secondary'})
        if params_section:
            text_data = params_section.get_text(" ", strip=True)
            year, mileage = parse_year_and_mileage(text_data)

        # ------------------ INSERT OFFER ------------------
        offer_id =insert_offer(cnx, PLATFORM_ID, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": url,
            "image_url": image_url,
            "seller": None,
            "location": location,
            "additional_info": None
        })

        # ------------------ INSERT VEHICLE DETAILS ------------------
        insert_vehicle_details(cnx, offer_id, {
            "year": year,
            "mileage": mileage,
            "fuel": None,
            "gearbox": None,
            "engine_power": None,
            "body_type": None
        })

        COUNTER += 1

    cnx.commit()


if __name__ == "__main__":
    print("OLX scraper starting...")

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
