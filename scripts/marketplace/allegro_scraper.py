import requests
import json
from mysql.connector import MySQLConnection, Error
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import sys
import urllib3
import configparser
urllib3.disable_warnings()
import os

# tutaj sciezke muszę podawać na sztywno
# path=os.path.dirname(os.path.abspath(__file__))+'\config.ini' #WINDOWS
path=os.path.dirname(os.path.abspath(__file__))+'/config.ini' #LINUX

config = configparser.ConfigParser()
config.read(path)

CLIENT_ID=config['allegro']['CLIENT_ID']
CLIENT_SECRET=config['allegro']['CLIENT_SECRET']
TOKEN_URL = "https://allegro.pl.allegrosandbox.pl/auth/oauth/token"
COUNTER=0

def get_access_token():
    try:
        data = {'grant_type': 'client_credentials'}
        access_token_response = requests.post(TOKEN_URL, data=data, verify=False, allow_redirects=False, auth=(CLIENT_ID, CLIENT_SECRET))
        tokens = json.loads(access_token_response.text)
        access_token = tokens['access_token']
        return access_token
    except requests.exceptions.HTTPError as err:
        raise SystemExit(err)

def get_main_categories(token):
    try:
        url = "https://api.allegro.pl.allegrosandbox.pl/sale/categories"
        headers = {'Authorization': 'Bearer ' + token, 'Accept': "application/vnd.allegro.public.v1+json"}
        main_categories_result = requests.get(url, headers=headers, verify=False)
        return main_categories_result
    except requests.exceptions.HTTPError as err:
        raise SystemExit(err)
   
def insert_record(cnx, product_name, image_link, has_promotion, quantity, price_in_PLN, popularity, delivery_in_PLN, seller_name):

    global COUNTER
    query = "INSERT INTO artykuly.artykuly_allegro(product_name,image_link,has_promotion, quantity, price_in_PLN, popularity, delivery_in_PLN, seller_name ) " \
            "VALUES(%s,%s,%s,%s,%s,%s,%s,%s)"
    args = (product_name,image_link,has_promotion, quantity, price_in_PLN, popularity, delivery_in_PLN, seller_name)

    try:
        cursor = cnx.cursor()
        cursor.execute(query, args)
        cnx.commit()

        COUNTER+=1
    except Error as error:
        print("Błąd przy funkcji insert!! ",error)
    finally:
        cursor.close()

def get_data_and_insert(cnx,object_list, key_name):
     for item in object_list[key_name]:
        if 'sellingMode' not in item :
            item['sellingMode']=None
        if 'popularity' not in item['sellingMode'] :
            item['sellingMode']['popularity']=None


        if  not item['images']:
            item['images']=None  
        else:
            for subitem in item['images']:
                if 'url' in subitem :
                    item['images']=subitem['url']

        insert_record(cnx,item["name"],item["images"], item['promotion']['emphasized'], item["stock"]['available'], item['sellingMode']['price']['amount'],
                        item['sellingMode']['popularity'],item['delivery']['lowestPrice']['amount'], item["seller"]["login"])
                    

def find_offers(token,phrase,limit):
    try:
        # REGULAR szukamy tylko w tytulach
        url = "https://api.allegro.pl.allegrosandbox.pl/offers/listing?phrase="f"{phrase}&fallback=false&limit="f"{limit}""" #&searchMode=REGULAR
        headers = {'Authorization': 'Bearer ' + token, 'Accept': "application/vnd.allegro.public.v1+json"}

        result = requests.get(url, headers=headers, verify=False)
        
        items = result.json()
        object_list = items['items']
        if ( not object_list.get("promoted") and not object_list.get("regular") ): return
        else:
            
            db_config = read_db_config()
            cnx=None

            try:

                cnx = MySQLConnection(**db_config)
                if (cnx.is_connected()):
                    print('Utworzono polaczenie z baza danych')
                else:
                    print('Połączenie nie powiodło się')

                get_data_and_insert(cnx, object_list,'promoted')

                get_data_and_insert(cnx, object_list,'regular')


            except Error as e:
                print("Błąd podczas łaczenia się z  MySQL", e)
            finally:
                if (cnx is not None and cnx.is_connected()):
                    cnx.close()
                    print("Polaczenie MySQL zostalo zakonczone")

    except requests.exceptions.HTTPError as err:
        raise SystemExit(err)   

if __name__ == "__main__":
    access_token = get_access_token()
    phrase=''
    print("Wykonujemy skrypt allegro_scrapper.py")

    if len(sys.argv) <= 1 or len(sys.argv) >=3 :
        print("Podano niepoprawną ilość argumentów")
        sys.exit()
    else:
        phrase=''
        n = len(sys.argv)
        for i in range(1, n):
            phrase+=sys.argv[i] 
    find_offers( access_token, phrase, 100)
    print("Liczba rekordow:",COUNTER)