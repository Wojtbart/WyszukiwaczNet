from configparser import ConfigParser
import os

# tutaj sciezke musze podawac na sztywno
path=os.path.dirname(os.path.abspath(__file__))+'/config.ini' #LINUX, +'\config.ini' #WINDOWS

def read_db_config(filename=path, section='postgres'):

    parser = ConfigParser()
    parser.read(filename)

    db = {}
    if parser.has_section(section):
        items = parser.items(section)
        for item in items:
            db[item[0]] = item[1]
    else:
        raise Exception(f'Section {section} not found in {filename} file.')

    return db