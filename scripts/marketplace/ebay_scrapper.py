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

PLATFORM_ID = 6  # EBAY
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

    digits_only = re.sub(r"[^\d,\.]", "", text)

    if not digits_only:
        return None, None

    digits_only = digits_only.replace(",", ".")
    return float(digits_only), "PLN"

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

# --------------------------------------------------
# MAIN SCRAPER
# --------------------------------------------------
def get_data_and_insert(cnx, phrase):
    global COUNTER

    final_phrase = "+".join(phrase)
    URL = f"https://www.ebay.pl/sch/i.html?_nkw={final_phrase}"

    page = requests.get(URL, headers={
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8"
    })

    soup = BeautifulSoup(page.content, "html.parser")

    listings = soup.select("li.s-card")

    if not listings:
        print("Nothing found!")
        return

    for listing in listings:

        # ------------------ TITLE ------------------
        title_tag = listing.select_one(".s-card__title")
        if not title_tag:
            continue

        title = title_tag.get_text(strip=True)

        if "Shop on eBay" in title:
            continue

        # ------------------ LINK ------------------
        link_tag = listing.select_one("a.s-card__link")
        if not link_tag:
            continue

        url = link_tag["href"]

        if offer_exists(cnx, PLATFORM_ID, url):
            continue

        # ------------------ IMAGE ------------------
        img_tag = listing.select_one(".s-card__image")
        image_url = img_tag["src"] if img_tag and img_tag.has_attr("src") else None

        # ------------------ PRICE ------------------
        price_tag = listing.select_one(".s-card__price")
        price, currency = parse_price(price_tag.text if price_tag else None)

        # ------------------ SELLER ------------------
        seller_tag = listing.select_one(".s-card__subtitle")
        seller = seller_tag.get_text(strip=True) if seller_tag else None

        # ------------------ LOCATION ------------------
        location = None
        attributes_container = listing.find("div", class_="su-card-container__attributes__primary")

        if attributes_container:
            all_divs = attributes_container.find_all("div", recursive=False)

            if all_divs:
                last_div = all_divs[-1]  # ostatni div

                spans = last_div.find_all("span")
                if spans:
                    location = spans[-1].get_text(strip=True)
                    if "z:" in location:
                        location = location.replace("z:", "").strip()
                    else:
                        location = None

        insert_offer(cnx, PLATFORM_ID, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": url,
            "image_url": image_url,
            "seller": seller,
            "location": location,
            "additional_info": None
        })

        COUNTER += 1

    cnx.commit()

if __name__ == "__main__":
    print("Ebay scraper starting...")

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
