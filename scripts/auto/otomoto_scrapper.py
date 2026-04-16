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

COUNTER=0

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

def parse_price(price_text):
    if not price_text:
        return None, None

    price_text = price_text.replace(" ", "").replace("zł", "")
    try:
        return float(price_text), "PLN"
    except ValueError:
        return None, None

def get_second_div(section):
    divs = section.find_all("div", recursive=False)
    return divs[1] if len(divs) >= 2 else None

def get_third_div(section):
    divs = section.find_all("div", recursive=False)
    return divs[2] if len(divs) >= 3 else None

def get_data_and_insert(cnx, phrase):
    global COUNTER
    platform_id = get_platform_id(cnx, "otomoto")

    # (URL building zostaje prawie bez zmian)
    final_phrase = "/".join(phrase[:2]).lower()
    URL = f"https://www.otomoto.pl/osobowe/{final_phrase}"

    page = requests.get(URL, headers={"User-Agent": "Mozilla/5.0"})
    soup = BeautifulSoup(page.content, "html.parser")

    listings = soup.find_all("article", {"data-orientation": "horizontal"})
    if not listings:
        print("Nothing found")
        return

    for listing in listings:
        section = listing.find("section")

        title= None
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

        if offer_exists(cnx, platform_id, link):
            continue

        # image
        img = section.find("img")
        image_url = img["src"] if img and img.has_attr("src") else None

        # price
        price_h3 = section.find("h3")
        price, currency = parse_price(price_h3.text if price_h3 else None)

        # location, seller_name
        location = None
        seller_name = None

        third_div = get_third_div(section)
        if third_div:
            # bierzemy TYLKO ul, które mają <p>
            uls = third_div.find_all("ul")

            target_ul = None
            for ul in uls:
                if ul.find("p"):
                    target_ul = ul
                    break

            if target_ul:
                li_tags = target_ul.find_all("li", recursive=False)

                if len(li_tags) >= 1:
                    p = li_tags[0].find("p")
                    if p:
                        location = p.text.strip()

                if len(li_tags) >= 2:
                    p = li_tags[1].find("p")
                    if p:
                        seller_name = p.text.strip()

        # vehicle data
        mileage = fuel = gearbox = year = None
        dds = section.find_all("dd")
        if len(dds) >= 4:
            mileage = dds[0].text.strip()
            fuel = dds[1].text.strip()
            gearbox = dds[2].text.strip()
            year = dds[3].text.strip()

        offer_id = insert_offer(cnx, platform_id, {
            "title": title,
            "price": price,
            "currency": currency,
            "url": link,
            "image_url": image_url,
            "seller": seller_name,
            "location": location,
            "additional_info" : additional_info
        })

        insert_vehicle_details(cnx, offer_id, {
            "year": parse_year(year),
            "mileage": parse_mileage(mileage),
            "fuel": fuel,
            "gearbox": gearbox
        })
        COUNTER+=1

        cnx.commit()

if __name__ == "__main__":
    print("Oto_moto_scrapper starting...")
    db_config = read_db_config()
    cnx=None

    if len(sys.argv) <= 1:
        print("Incorrect number of arguments specified!")
        sys.exit()
    else:
        phrase=[]
        n = len(sys.argv)
        for item in sys.argv:
            item = item.replace('\'', '')
        for i in range(1, n):
            if " " in  sys.argv[i]:
                phrase.append(sys.argv[i].split())
                output_array = [item for sublist in phrase for item in sublist]
                phrase= output_array
            else:
                phrase.append(sys.argv[i])
    try:
        cnx = psycopg2.connect(**db_config)
        print("Connection to PostgreSQL from script successful!")

        get_data_and_insert(cnx, phrase)
        print("Records inserted:", COUNTER)

    except Error as e:
        print("Connection error with PostgreSQL:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("Connection closed.")
