import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import re
import argparse
from datetime import datetime, timezone
from urllib.parse import urlencode
import psycopg2
from psycopg2 import Error

sys.stdout.reconfigure(encoding='utf-8')

BASE_URL = "https://samochody.pl"

FUEL_MAP = {
    "petrol":       "benzyna",
    "lpg":          "gaz",
    "diesel":       "diesel",
    "electric":     "elektro",
    "plugin-hybrid": "hybryda",
}
PLATFORM_ID = 13
COUNTER = 0


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
        price = float(cleaned)
        if price > 99_999_999:
            return None, None
        return price, "PLN"
    except ValueError:
        return None, None


def get_platform_id(cnx, platform_id):
    with cnx.cursor() as cursor:
        cursor.execute("SELECT id FROM platforms WHERE id = %s", (platform_id,))
        row = cursor.fetchone()
        if not row:
            raise Exception(f"Platform  not found in DB")
        return row[0]


def offer_exists(cnx, platform_id, url):
    with cnx.cursor() as cursor:
        cursor.execute(
            "SELECT 1 FROM offers WHERE platform_id = %s AND url = %s LIMIT 1",
            (platform_id, url)
        )
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
        (offer_id, production_year, mileage, fuel_type, gearbox)
        VALUES (%s,%s,%s,%s,%s)
    """
    with cnx.cursor() as cursor:
        cursor.execute(query, (
            offer_id,
            vehicle["year"],
            vehicle["mileage"],
            vehicle["fuel"],
            vehicle["gearbox"]
        ))


def extract_feature(feature_grid, icon_name):
    """Extract text after a specific material-icon in OfferFeatureGrid."""
    if not feature_grid:
        return None
    for span in feature_grid.find_all("span", recursive=False):
        icon = span.find("i", class_="material-icons")
        if icon and icon_name in icon.text:
            # get all text, strip icon text and units
            text = span.get_text(separator=" ").replace(icon.text, "").strip()
            text = re.sub(r"\s+", " ", text).strip()
            return text if text else None
    return None


def parse_listing(article):
    """Parse single offer <article> tag. Returns dict or None."""
    # link
    a_tag = article.find("a", href=True)
    href = a_tag.get("href", "") if a_tag else ""
    if not href or "/konto/" in href:
        return None
    url = BASE_URL + href if href.startswith("/") else href

    # title
    title_tag = article.find(class_=lambda c: c and "OfferCard_title" in c) or article.find("h3")
    title = title_tag.get_text(strip=True) if title_tag else None

    # image
    img = article.find("img")
    image_url = img.get("src") if img else None

    # short spec / version line
    version_div = article.find(class_=lambda c: c and "OfferCard_version" in c)
    additional_info = None
    if version_div:
        parts = [
            s.get_text(strip=True)
            for s in version_div.find_all("span")
            if s.get_text(strip=True) not in ("·", "")
        ]
        additional_info = " · ".join(parts) if parts else version_div.get_text(strip=True) or None

    # price
    price_tag = article.find(class_=lambda c: c and "OfferCard_price" in c)
    price, currency = parse_price(price_tag.get_text(strip=True) if price_tag else None)

    # seller
    seller_tag = article.find(class_=lambda c: c and "OfferCard_seller" in c)
    seller = seller_tag.get_text(strip=True) if seller_tag else None

    # location
    loc_tag = article.find(class_=lambda c: c and "OfferCard_loc" in c)
    location = None
    if loc_tag:
        icon = loc_tag.find("i")
        if icon:
            icon.decompose()
        location = loc_tag.get_text(strip=True) or None

    # dl dt/dd params: Rok produkcji, Przebieg, Paliwo, Skrzynia biegów
    year = mileage = fuel = gearbox = None
    dl = article.find("dl")
    if dl:
        dts = dl.find_all("dt")
        dds = dl.find_all("dd")
        params = {dt.get_text(strip=True): dds[i].get_text(strip=True) for i, dt in enumerate(dts) if i < len(dds)}
        year_str = params.get("Rok produkcji")
        year = int(year_str) if year_str and year_str.isdigit() else None
        mileage_str = params.get("Przebieg", "")
        mileage_digits = re.sub(r"\D", "", mileage_str)
        mileage = int(mileage_digits) if mileage_digits else None
        fuel = params.get("Paliwo")
        gearbox = params.get("Skrzynia biegów")

    return {
        "url": url,
        "title": title,
        "image_url": image_url,
        "additional_info": additional_info,
        "price": price,
        "currency": currency,
        "seller": seller,
        "location": location,
        "year": year,
        "mileage": mileage,
        "fuel": fuel,
        "gearbox": gearbox,
    }


def build_url(phrase, filters=None):
    brand = phrase[0].lower() if len(phrase) > 0 else ""
    model = phrase[1].lower() if len(phrase) > 1 else ""
    path = f"{brand}/{model}" if model else brand

    params = {"sortuj": "najnowsze"}

    if filters:
        fuel = FUEL_MAP.get(filters.get("fuel", ""))
        if fuel:
            params["paliwo"] = fuel
        gearbox = filters.get("gearbox")
        if gearbox == "manual":
            params["skrzynia-biegow-manualna"] = "true"
        elif gearbox == "automatic":
            params["skrzynia-biegow-automatyczna"] = "true"
        if filters.get("capacity_from") is not None:
            params["pojemnosc-od"] = filters["capacity_from"]
        if filters.get("capacity_to") is not None:
            params["pojemnosc-do"] = filters["capacity_to"]
        if filters.get("price_from") is not None:
            params["cena-od"] = int(filters["price_from"])
        if filters.get("price_to") is not None:
            params["cena-do"] = int(filters["price_to"])

    return f"{BASE_URL}/samochody-osobowe/{path}?" + urlencode(params)


def get_data_and_insert(cnx, phrase, filters=None):
    global COUNTER
    platform_id = get_platform_id(cnx, PLATFORM_ID)

    url = build_url(phrase, filters)
    print(f"Fetching: {url}")

    response = requests.get(url, headers={"User-Agent": "Mozilla/5.0"}, timeout=15)
    soup = BeautifulSoup(response.content, "html.parser")

    container = soup.find("div", class_=lambda c: c and "Listing_grid" in c)
    if not container:
        print("No offer list container found")
        return

    listings = container.find_all("article", class_=lambda c: c and "OfferCard_card" in c)
    if not listings:
        print("No listings found")
        return

    print(f"Found {len(listings)} offer articles")

    for article in listings:
        data = parse_listing(article)
        if not data:
            continue

        if offer_exists(cnx, platform_id, data["url"]):
            print(f"  SKIP (exists): {data['url']}")
            continue

        offer_id = insert_offer(cnx, platform_id, {
            "title": data["title"],
            "price": data["price"],
            "currency": data["currency"],
            "url": data["url"],
            "image_url": data["image_url"],
            "seller": data["seller"],
            "location": data["location"],
            "additional_info": data["additional_info"],
        })

        insert_vehicle_details(cnx, offer_id, {
            "year": data["year"],
            "mileage": data["mileage"],
            "fuel": data["fuel"],
            "gearbox": data["gearbox"],
        })

        COUNTER += 1
        cnx.commit()
        print(f"  +inserted: {data['title']} | {data['price']} {data['currency']} | {data['location']}")


if __name__ == "__main__":
    print("Samochody.pl scrapper starting...")

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
        phrase.extend(token.replace("'", "").split())

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
        print("PostgreSQL connection OK")
        get_data_and_insert(cnx, phrase, filters)
        print(f"Records: {COUNTER}")

    except Error as e:
        print("PostgreSQL error:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
