import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
from datetime import datetime, timezone
import psycopg2
from psycopg2 import Error
import sys
import urllib.parse
import unicodedata
import re

sys.stdout.reconfigure(encoding='utf-8')

PLATFORM_ID = 11
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


def normalize_city_name(city):
    return unicodedata.normalize('NFKD', city).encode('ASCII', 'ignore').decode('utf-8').lower()


def generate_otodom_url(transaction_type, city):

    city = normalize_city_name(city)

    city_province_map = {
        "krakow": "malopolskie",
        "warszawa": "mazowieckie",
        "rzeszow": "podkarpackie",
        "wroclaw": "dolnoslaskie",
        "poznan": "wielkopolskie",
        "gdansk": "pomorskie",
        "lodz": "lodzkie",
        "katowice": "slaskie",
        "szczecin": "zachodniopomorskie",
        "lublin": "lubelskie"
    }

    province = city_province_map.get(city)

    if not province:
        raise ValueError("City not found")

    return f"https://www.otodom.pl/pl/wyniki/{transaction_type}/mieszkanie/{province}/{city}"

def scrape_otodom(cnx, phrase):
    global COUNTER

    params = {
        "by": "DEFAULT",
        "direction": "DESC",
        "viewType": "listing"
    }

    transaction_type = "wynajem"

    for elem in phrase:

        if elem == "sprzedaz":
            transaction_type = "sprzedaz"

        if elem == "wynajem":
            transaction_type = "wynajem"

        if elem == "ostatnie24h":
            params["daysSinceCreated"] = "1"

        if elem == "prywatne":
            params["extras"] = "[IS_PRIVATE_OWNER]"

    BASE_URL = generate_otodom_url(transaction_type, phrase[0])

    final_url = BASE_URL + "?" + urllib.parse.urlencode(params)

    headers = {
        "User-Agent": "Mozilla/5.0",
        "Accept-Language": "pl-PL,pl;q=0.9",
        "Referer": "https://www.otodom.pl/"
    }

    response = requests.get(final_url, headers=headers)

    if response.status_code != 200:
        print("Failed request")
        return

    soup = BeautifulSoup(response.text, "html.parser")

    listings = soup.select_one('ul[data-sentry-component="CardsList"]')

    if not listings:
        print("Nothing found!")
        return

    for offer in listings:
        
        # ---------- TITLE ----------
        title_tag = offer.select_one('[data-cy="listing-item-title"]')
        title = title_tag.get_text(strip=True) if title_tag else None

        # ---------- LINK ----------
        link_tag = offer.select_one('[data-cy="listing-item-link"]')
        if not link_tag:
            continue

        url = "https://www.otodom.pl" + link_tag["href"]

        if offer_exists(cnx, PLATFORM_ID, url):
            continue

        # ---------- IMAGE ----------
        img_tag = offer.select_one('img[data-cy="listing-item-image-source"]')
        image_url = img_tag["src"] if img_tag else None

        # ---------- PRICE ----------
        price_tag = offer.select_one('span[data-sentry-element="MainPrice"]')

        price = None
        if price_tag:
            price_text = price_tag.text.replace("\xa0", "").replace("zł", "").strip()

            if price_text.isdigit():
                price = int(price_text)
            else:
                price = None

        # ---------- LOCATION ----------
        location_tag = offer.select_one('p[data-sentry-component="Address"]')
        location = location_tag.get_text(strip=True) if location_tag else None

        # ---------- SELLER ----------
        seller_info = offer.select_one('div[data-sentry-component="SellerInfo"]')
        seller_name = seller_info.select_one("span")
        seller = seller_name.get_text(strip=True) if seller_name else None

        # ---------- ADDITIONAL INFO ----------
        additional_info = None

        dl = offer.select_one("dl")

        if dl:
            dd_values = [dd.get_text(strip=True) for dd in dl.select("dd")]
            additional_info = ", ".join(dd_values)

        insert_offer(cnx, PLATFORM_ID, {
            "title": title,
            "price": price,
            "currency": "PLN",
            "url": url,
            "image_url": image_url,
            "seller": seller,
            "location": location,
            "additional_info": additional_info
        })

        COUNTER += 1
    cnx.commit()

if __name__ == "__main__":

    print("Otodom scraper starting...")

    db_config = read_db_config()

    if len(sys.argv) <= 1:
        print("Incorrect arguments")
        sys.exit()

    phrase = []

    for arg in sys.argv[1:]:
        phrase.extend(arg.split())

    try:
        cnx = psycopg2.connect(**db_config)
        print("PostgreSQL connected")

        scrape_otodom(cnx, phrase)

        print("Inserted:", COUNTER)

    except Error as e:
        print("Database error:", e)

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed")
