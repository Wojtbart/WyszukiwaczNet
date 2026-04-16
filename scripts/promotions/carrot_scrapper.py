import requests
from bs4 import BeautifulSoup
from mysql.connector import MySQLConnection, Error
# from python_mysql_dbconfig import read_db_config
import sys

COUNTER=0

def insert_record(cnx, arr):
    global COUNTER
    query = "INSERT INTO artykuly.carrot(Tytul, Link, Zdjecie, cena, stan, lokalizacja, obserwuj) " \
            "VALUES(%s,%s,%s,%s,%s,%s,%s)"

    try:
        cursor = cnx.cursor()
        cursor.execute(query, arr)
        cnx.commit()

        COUNTER+=1
    except Error as error:
        print("Błąd w funkcji insert!! ",error)
    finally:
        cursor.close()

def get_data_and_insert(cnx,phrase):

    final_phrase=''
    # if len(phrase)>1:
    #     final_phrase="/".join(str(x) for x in phrase)
    # else:
    #     final_phrase=''.join(phrase)

    URL = F"https://www.carrot.pl/"

    page = requests.get(URL, headers = {
        'User-Agent': 'Popular browser\'s user-agent',
    })
    page_content = BeautifulSoup(page.content, "html.parser")

    pagination = page_content.find('div',class_="pagination")
    link_list = pagination.find_all('a')
    arr= []
    for item in link_list:
        arr.append((item.text.strip()))
    numeric_values = [int(x) for x in arr if x.isdigit()]
    print(max(numeric_values))

    for x in range(max(numeric_values)+1): 
        print(x) 
        url_of_website = F"https://carrot.pl/widok-lista/strona-{x}"

        page = requests.get(url_of_website, headers = {
            'User-Agent': 'Popular browser\'s user-agent',
        })
        page_content = BeautifulSoup(page.content, "html.parser")
        listings = page_content.find('div',{"id": "listing-desktop"})
        articles = listings.find_all("div", class_="single-auction")
        if not listings:
            print("Nic nie znaleziono")
        else:
            for listing in articles:
                item = listing.find('div', class_="picture")
                photo_section = item.find('a')
                if(photo_section is not None):
                    photo = photo_section.find('img')
                    if(photo is not None):
                        if photo.has_attr('src'):
                            img = photo['src']
                            img_url='https://carrot.pl'+img
                            print(img_url)
                
                title_link = listing.find('div', class_="auction-content")
                title_link_a = title_link.find('a')
                if title_link_a.has_attr('href'):
                    link = title_link_a['href']
                    url='https://carrot.pl'+link
                    print(url)
                title = title_link_a.find("h3")
                if title is not None:
                    print(title.text.strip())
                
                additional_info = title_link_a.find('div', class_="vehicle-info")
                additional_info_values = additional_info.find_all('div')
                for item in additional_info_values:
                    print(item.text.strip())

                price_div = listing.find("div", class_="options")
                price_div_a = price_div.find("a")
                price_info = price_div_a.find("div", class_="price")
                if price_info is not None:
                    print(price_info.text.strip())
                price_div_commision = price_div_a.find("p", class_="commission")
                if price_div_commision is not None:
                    print(price_div_commision.text.strip())
            print("\n\n")
    #         car_info = additional_info.find('div', class_="VehicleDetailTable_container__XhfV1")
    #         car_info_spans = car_info.find_all('span')
    #         for item in car_info_spans:
    #             if item is not None:
    #                 print(item.text.strip())

            # print("\n\n")
        #  insert_record(cnx,arr)

if __name__ == "__main__":
    print("Wykonujemy skrypt autoscout_scrapper.py")
    # db_config = read_db_config()
    cnx=None

    if len(sys.argv) <= 1 or len(sys.argv) >5 :
        print("Podano niepoprawną ilość argumentów")
        sys.exit()
    else:
        phrase=[]
        n = len(sys.argv)
        for i in range(1, n):
            phrase.append(sys.argv[i] )
    # try:
        # cnx = MySQLConnection(**db_config)
        # if (cnx.is_connected()):
        #         print('Utworzono połączenie')
        # else:
        #     print('Połączenie nie powiodło się')

    get_data_and_insert(cnx, phrase)
    print("Liczba rekordow:",COUNTER)

    # except Error as e:
    #     print("Błąd podczas łaczenia się z  MySQL", e)
    # finally:
    #     if (cnx is not None and cnx.is_connected()):
    #         cnx.close()
    #         print("Połączenie MySQL zostało zakończone")