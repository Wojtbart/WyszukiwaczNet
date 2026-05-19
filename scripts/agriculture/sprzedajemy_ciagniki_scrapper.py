import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import psycopg2
from psycopg2 import Error
from datetime import datetime, timezone
from urllib.parse import quote_plus
import re

sys.stdout.reconfigure(encoding='utf-8')

COUNTER = 0
BASE_URL = "https://sprzedajemy.pl"


def offer_exists(cnx, platform_id, url):
    query = "SELECT 1 FROM offers WHERE platform_id = %s AND url = %s LIMIT 1"
    with cnx.cursor() as cursor:
        cursor.execute(query, (platform_id, url))
        return cursor.fetchone() is not None


def parse_price(text):
    if not text:
        return None, None
    cleaned = text.replace("\xa0", "").replace(" ", "").replace("zł", "").strip()
    try:
        return float(cleaned), "PLN"
    except ValueError:
        return None, None


def get_platform_id(cnx, platform_name):
    with cnx.cursor() as cursor:
        cursor.execute("SELECT id FROM platforms WHERE name = %s", (platform_name,))
        row = cursor.fetchone()
        if not row:
            raise Exception(f"Platform '{platform_name}' not found in DB")
        return row[0]


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
    platform_id = get_platform_id(cnx, "sprzedajemyciagniki")

    search_phrase = quote_plus(" ".join(phrase))
    url = f"{BASE_URL}/temat/{search_phrase}?sort=inp_srt_date_d"
    print(url)

    response = requests.get(url, headers={
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
        "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8",
    }, timeout=30)

    soup = BeautifulSoup(response.content, "html.parser")
    articles = soup.select("article.element")

    if not articles:
        print("Nothing found!")
        return

    for article in articles:
        # ------------------ LINK ------------------
        link_tag = article.find("a", class_="picture")
        if not link_tag:
            link_tag = article.find("a", class_="offerLink")
        if not link_tag:
            continue

        href = link_tag.get("href", "")
        if not href:
            continue
        full_url = BASE_URL + href if href.startswith("/") else href

        if offer_exists(cnx, platform_id, full_url):
            continue

        # ------------------ IMAGE ------------------
        img_tag = article.find("img")
        image_url = img_tag["src"] if img_tag and img_tag.has_attr("src") else None

        # ------------------ TITLE ------------------
        title_tag = article.find("h2", class_="title")
        title = None
        if title_tag:
            a = title_tag.find("a")
            title = a.text.strip() if a else title_tag.text.strip()

        # ------------------ PRICE ------------------
        pricing_div = article.find("div", class_="pricing")
        price_span = pricing_div.find("span") if pricing_div else None
        price, currency = parse_price(price_span.text if price_span else None)

        # ------------------ SELLER ------------------
        seller_div = article.find("div", class_="seller-type-info")
        seller = None
        if seller_div:
            span = seller_div.find("span", class_="seller-type-info__label")
            seller = span.text.strip() if span else None

        # ------------------ DATE ------------------
        time_tag = article.find("time", class_="time")
        date_str = ""
        if time_tag:
            dt_attr = time_tag.get("datetime", "")
            if dt_attr:
                date_str = dt_attr[:10]  # "2026-05-07"

        # ------------------ LOCATION ------------------
        city_span = article.find("span", class_="city")
        city = city_span.text.strip() if city_span else ""
        location = f"{city} - {date_str}" if date_str else city

        # ------------------ ADDITIONAL INFO ------------------
        snippet_tag = article.find("p", class_="snippets")
        additional_info = snippet_tag.get_text(" ", strip=True) if snippet_tag else None

        # ------------------ INSERT ------------------
        insert_offer(cnx, platform_id, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": full_url,
            "image_url": image_url,
            "seller": seller,
            "location": location,
            "additional_info": additional_info,
        })
        COUNTER += 1

    cnx.commit()


if __name__ == "__main__":
    print("Sprzedajemy Ciagniki scraper starting...")

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
        print("Records:", COUNTER)

    except Error as e:
        print("DB error:", e)
        if cnx:
            cnx.rollback()

    finally:
        if cnx and cnx.closed == 0:
            cnx.close()
            print("Connection closed")
