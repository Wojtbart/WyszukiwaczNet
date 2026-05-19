import requests
from bs4 import BeautifulSoup
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import psycopg2
from psycopg2 import Error
import re
from datetime import datetime, timezone

sys.stdout.reconfigure(encoding='utf-8')

COUNTER = 0


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


def parse_year(text):
    if not text:
        return None
    digits = re.sub(r"[^\d]", "", text)
    return int(digits) if len(digits) == 4 else None


def get_platform_id(cnx, platform_name):
    with cnx.cursor() as cursor:
        cursor.execute("SELECT id FROM platforms WHERE name = %s", (platform_name,))
        row = cursor.fetchone()
        if not row:
            raise Exception(f"Platform '{platform_name}' not found in DB")
        return row[0]


def offer_exists(cnx, platform_id, url):
    query = "SELECT 1 FROM offers WHERE platform_id = %s AND url = %s LIMIT 1"
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


def insert_vehicle_details(cnx, offer_id, year):
    query = """
        INSERT INTO vehicle_details
        (offer_id, production_year, mileage, fuel_type, gearbox, engine_power, body_type)
        VALUES (%s,%s,%s,%s,%s,%s,%s)
    """
    with cnx.cursor() as cursor:
        cursor.execute(query, (offer_id, year, None, None, None, None, None))


def get_second_div(section):
    divs = section.find_all("div", recursive=False)
    return divs[1] if len(divs) >= 2 else None


def get_third_div(section):
    divs = section.find_all("div", recursive=False)
    return divs[2] if len(divs) >= 3 else None


def get_data_and_insert(cnx, phrase):
    global COUNTER
    platform_id = get_platform_id(cnx, "otomotorolnicze")

    # phrase[0] = brand (e.g. "ursus"), phrase[1:] = model query (e.g. ["c330"])
    brand = phrase[0].lower()
    if len(phrase) > 1:
        model = "-".join(w.lower() for w in phrase[1:])
        URL = f"https://www.otomoto.pl/maszyny-rolnicze/{brand}/q-{model}?search%5Border%5D=created_at_first%3Adesc"
    else:
        URL = f"https://www.otomoto.pl/maszyny-rolnicze/{brand}?search%5Border%5D=created_at_first%3Adesc"
    print(URL)

    page = requests.get(URL, headers={
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
        "Accept-Language": "pl-PL,pl;q=0.9,en;q=0.8",
    }, timeout=30)

    soup = BeautifulSoup(page.content, "html.parser")
    listings = soup.find_all("article", {"data-orientation": "horizontal"})

    if not listings:
        print("Nothing found!")
        return

    for listing in listings:
        section = listing.find("section")
        if not section:
            continue

        # ------------------ TITLE & LINK ------------------
        title = None
        link = None
        additional_info = None

        second_div = get_second_div(section)
        if second_div:
            a_tag = second_div.find("a", href=True)
            if a_tag:
                title = a_tag.text.strip()
                link = a_tag["href"]
            p_tag = second_div.find("p")
            if p_tag:
                additional_info = p_tag.text.strip()

        if not link:
            continue
        if offer_exists(cnx, platform_id, link):
            continue

        # ------------------ IMAGE ------------------
        img = section.find("img")
        image_url = img["src"] if img and img.has_attr("src") else None

        # ------------------ PRICE ------------------
        price_h3 = section.find("h3")
        price, currency = parse_price(price_h3.text if price_h3 else None)

        # ------------------ LOCATION & SELLER ------------------
        location = None
        seller_name = None

        third_div = get_third_div(section)
        if third_div:
            for ul in third_div.find_all("ul"):
                if ul.find("p"):
                    li_tags = ul.find_all("li", recursive=False)
                    if len(li_tags) >= 1:
                        p = li_tags[0].find("p")
                        if p:
                            location = p.text.strip()
                    if len(li_tags) >= 2:
                        p = li_tags[1].find("p")
                        if p:
                            seller_name = p.text.strip()
                    break

        # ------------------ YEAR (only vehicle data available in listing) ------------------
        year_dd = section.find("dd", {"data-parameter": "year"})
        year = parse_year(year_dd.text.strip() if year_dd else None)

        # ------------------ INSERT ------------------
        offer_id = insert_offer(cnx, platform_id, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": link,
            "image_url": image_url,
            "seller": seller_name,
            "location": location,
            "additional_info": additional_info,
        })
        insert_vehicle_details(cnx, offer_id, year)
        COUNTER += 1

    cnx.commit()


if __name__ == "__main__":
    print("OtoMoto Rolnicze scraper starting...")

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
