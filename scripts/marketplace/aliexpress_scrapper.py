import requests
from bs4 import BeautifulSoup
#from mysql.connector import MySQLConnection, Error
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "config"))
from dbconfig import read_db_config
import sys
import re
import time


import psycopg2
from psycopg2 import sql, Error
sys.stdout.reconfigure(encoding='utf-8')

COUNTER=0

def check_records_is_duplicate(cnx, arr):
    query = "Select * from aliexpress order by id desc limit 20"
    
    try:
        cursor = cnx.cursor()
        cursor.execute(query)
        myresult = cursor.fetchall()
        cursor.close()

        for x in myresult:
            if(x[4].strip()==arr[3].strip()): #porownuje po nazwach
                return True
        return False
    except Error as error:
        print("Błąd w funkcji select!", error)

    
def insert_record(cnx, arr):
    global COUNTER
    # query = "INSERT INTO aliexpress(create_date, link, image, title, price, shipping, seller) " \
    #         "VALUES(%s,%s,%s,%s,%s,%s,%s)"
    
    query = """
        INSERT INTO aliexpress(create_date, link, image, title, price, shipping, seller)
        VALUES(%s,%s,%s,%s,%s,%s,%s)
    """

    try:
        cursor = cnx.cursor()
        cursor.execute(query, arr)
        cnx.commit()

        COUNTER+=1
    except (Exception, psycopg2.DatabaseError) as error:
        print("Insert function error!!", error)
        cnx.rollback()  # 🔴 Rollback transaction on failure    
    except Error as error:
        print("Błąd w funkcji insert!! ",error)
    finally:
        cursor.close()

def get_data_and_insert(cnx,phrase):

    final_phrase=''
    if len(phrase)>1:
        final_phrase="-".join(str(x) for x in phrase)
    else:
        final_phrase=''.join(phrase)

    URL = F"https://www.aliexpress.com/w/wholesale-{final_phrase}.html"

    page = requests.get(URL, headers = {
        'User-Agent': 'Popular browser\'s user-agent',
    })
    page_content = BeautifulSoup(page.content, "html.parser")

    not_found_result=page_content.findAll(string='''Przepraszamy, Twoje wyszukiwanie "{final_phrase}" nie pasuje do żadnego produktu. Spróbuj ponownie.''')

    if not_found_result:
        print("Nothing found!")
    else:
        results = page_content.find("div", {"id": "card-list"})

        for div in results:
            arr_of_elements=[]
            arr_of_elements.append(time.strftime('%Y-%m-%d %H:%M:%S'))

            allInformationATag = div.find("a", {"class":"search-card-item"})
            if(allInformationATag is not None):

                # LINK
                if allInformationATag.has_attr('href'):
                    link = allInformationATag['href']
                   # print(link)
                    arr_of_elements.append(link)
                else:
                    arr_of_elements.append(None)

                #image
                first_child_div = allInformationATag.contents[0]
                img_tag=first_child_div.find("img")
                if(img_tag is not None):
                    if img_tag.has_attr('src'):
                        img = img_tag['src']
                       # print(img)
                        arr_of_elements.append(img)
                    else:
                        arr_of_elements.append(None)
                else:
                    arr_of_elements.append(None)
                
                #title
                second_child_div = allInformationATag.contents[1]
                title = second_child_div.contents[0]
                if title.has_attr('title'):
                    arr_of_elements.append(title['title'])
               # print(title['title'])
                

                #price
                price = second_child_div.find("div", {"class":"multi--price-sale--U-S0jtj"})
              #  print(price.text)
                arr_of_elements.append(price.text)

                #shipping
                shipping = second_child_div.find("div", {"class":"multi--serviceContainer--3vRdzWN"})
                if(shipping is not None):
                   # print(shipping.text)
                    arr_of_elements.append(shipping.text)
                else:
                    arr_of_elements.append(None)

                #seller
                seller = second_child_div.find("span", {"class":"cards--store--3GyJcot"})
                #print(seller.text)
                arr_of_elements.append(seller.text)

                # print(arr_of_elements)
                duplicate = check_records_is_duplicate(cnx,arr_of_elements)
                print(duplicate)
                if(duplicate is False):
                    insert_record(cnx,arr_of_elements)

if __name__ == "__main__":
    print("Aliexpress_scrapper starting...")
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
    # try:
    #     cnx = MySQLConnection(**db_config)
    #     if (cnx.is_connected()==False):
    #             print('Connection to database failed!')

    #     get_data_and_insert(cnx, phrase)
    #     print("Records number:",COUNTER)

    # except Error as e:
    #     print("Connection error with MySQL:", e)
    # finally:
    #     if (cnx is not None and cnx.is_connected()):
    #         cnx.close()
    try:
        cnx = psycopg2.connect(**db_config)  # Connect to PostgreSQL
        print("✅ Connection to PostgreSQL successful!")

        get_data_and_insert(cnx, phrase)
        print("Records inserted:", COUNTER)

    except Error as e:
        print("❌ Connection error with PostgreSQL:", e)

    finally:
        if cnx is not None and cnx.closed == 0:
            cnx.close()
            print("🔌 Connection closed.")