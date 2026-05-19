import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import sys
import re
import argparse
from datetime import datetime, timezone
from urllib.parse import urlencode
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
        return float(cleaned), "PLN"
    except ValueError:
        return None, None

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
            None,  # seller unknown on listing level- BRAK
            offer["location"],
            offer["additional_info"],
            datetime.now(timezone.utc)
        ))
        return cursor.fetchone()[0]

def insert_vehicle_details(cnx, offer_id, vehicle):
    query = """
        INSERT INTO vehicle_details
        (offer_id, production_year, mileage, fuel_type, body_type)
        VALUES (%s,%s,%s,%s,%s)
    """
    with cnx.cursor() as cursor:
        cursor.execute(query, (
            offer_id,
            vehicle["year"],
            vehicle["mileage"],
            vehicle["fuel"],
            vehicle["body_type"]
        ))

# =========================
# SCRAPER
# =========================
def build_url(phrase, filters=None):
    parts = [p.lower() for p in phrase[:2]]
    path = "/".join(parts)

    fuel = filters.get("fuel") if filters else None
    if fuel:
        base = f"https://motogratka.pl/motoryzacja/osobowe/{path}/{fuel}"
    else:
        base = f"https://motogratka.pl/motoryzacja/osobowe/{path}"

    params = {"sort": "newest"}
    if filters:
        if filters.get("gearbox"):
            params["skrzynia-biegow[0]"] = filters["gearbox"]
        if filters.get("capacity_from") is not None:
            params["pojemnosc-silnika:min"] = filters["capacity_from"]
        if filters.get("capacity_to") is not None:
            params["pojemnosc-silnika:max"] = filters["capacity_to"]
        if filters.get("price_from") is not None:
            params["cena-calkowita:min"] = filters["price_from"]
        if filters.get("price_to") is not None:
            params["cena-calkowita:max"] = filters["price_to"]

    return base + "?" + urlencode(params)


def get_data_and_insert(cnx, phrase, filters=None):
    global COUNTER

    platform_id = get_platform_id(cnx, "gratka")

    URL = build_url(phrase, filters)
    print(URL)

    page = requests.get(URL, headers={"User-Agent": "Mozilla/5.0"})
    soup = BeautifulSoup(page.content, "html.parser")

    listings = soup.find("div", class_="listing__content")
    if not listings:
        print("Nothing found")
        return

    articles = listings.find_all("div", class_="listing__teaserWrapper")

    for listing in articles:
        link_tag = listing.find("a", class_="teaserLink")
        if not link_tag or not link_tag.has_attr("href"):
            continue

        url = link_tag["href"]

        if offer_exists(cnx, platform_id, url):
            continue

        article = listing.find("article")
        if not article:
            continue

        # IMAGE
        img_tag = article.select_one("div.teaserUnified__photo img")
        image_url = img_tag["src"] if img_tag and img_tag.has_attr("src") else None

        # TITLE + LOCATION
        main_info = article.find("div", class_="teaserUnified__mainInfo")
        if not main_info:
            continue

        title_tag = main_info.find("h2")
        title = title_tag.text.strip() if title_tag else None

        location=None
        location_tag = main_info.find("span", class_="teaserUnified__location")
        location_text = location_tag.text.strip() if location_tag else None
        location = location_text.replace(" ", "")

        # ADDITIONAL INFO (params list)
        additional_info = None
        params = main_info.find("ul", class_="teaserUnified__paramsWithKey")
        param_texts = []

        if params:
            for li in params.find_all("li"):
                text = li.text.strip()
                if text:
                    param_texts.append(text)

        additional_info = " | ".join(param_texts) if param_texts else None

        # Extract structured data
        year = mileage = body_type = fuel = None

        for text in param_texts:
            if "Przebieg" in text:
                mileage = parse_mileage(text)
            elif "Stan techniczny" in text:
                fuel = text
            elif "Typ" in text:
                body_type = text
            elif re.search(r"\b(19|20)\d{2}\b", text):
                year = parse_year(text)

        # PRICE
        price_tag = listing.select_one("p.teaserUnified__price")
        price, currency = parse_price(price_tag.text if price_tag else None)

        # INSERT
        offer_id = insert_offer(cnx, platform_id, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": url,
            "image_url": image_url,
            "location": location,
            "additional_info": additional_info
        })

        insert_vehicle_details(cnx, offer_id, {
            "year": year,
            "mileage": mileage,
            "fuel": fuel,
            "body_type": body_type
        })

        COUNTER += 1

    cnx.commit()

# =========================
# MAIN
# =========================

if __name__ == "__main__":
    print("Gratka_scrapper starting...")

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
    cnx = None

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
