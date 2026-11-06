# Deploy na nowe Raspberry Pi

## Zmienne — ustaw przed komendami
```
PI_IP=192.168.0.X
PI_USER=raspberry
PI_PASS=twoje_haslo
```

---

## 1. Przygotowanie Pi — zainstaluj zależności

### .NET 9
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
dotnet --version
```

### Python + pip + biblioteki
```bash
sudo apt update
sudo apt install -y python3 python3-pip python3-venv
pip3 install requests beautifulsoup4 psycopg2-binary lxml
```

### PostgreSQL
```bash
sudo apt install -y postgresql postgresql-contrib
sudo systemctl enable postgresql
sudo systemctl start postgresql
```

### Utwórz bazę i użytkownika (dostosuj hasło)
```bash
sudo -u postgres psql -c "CREATE USER wyszukiwacz WITH PASSWORD 'twoje_haslo_db';"
sudo -u postgres psql -c "CREATE DATABASE wyszukiwaczdb OWNER wyszukiwacz;"
```

---

## 2. Struktura folderów na Pi

```bash
mkdir -p /home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/backend/backend
mkdir -p /home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/frontend/frontend
mkdir -p /home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/scripts
```

---

## 3. Build na Windows (z folderu projektu)

```bash
dotnet publish src/WyszukiwaczNet.Api -c Release -o ./publish/backend
dotnet publish frontend/WyszukiwaczApp -c Release -o ./publish/frontend
```

---

## 4. Wgranie plików przez SCP (z Windows)

### Backend
```bash
scp -r ./publish/backend/* raspberry@PI_IP:/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/backend/backend/
```

### Frontend
```bash
scp -r ./publish/frontend/* raspberry@PI_IP:/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/frontend/frontend/
```

### Skrypty Python
```bash
scp -r ./scripts/* raspberry@PI_IP:/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/scripts/
```

### Konfiguracja (config.ini / config.ini.enc)
```bash
scp scripts/config/config.ini raspberry@PI_IP:/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/scripts/config/
```

---

## 5. Konfiguracja backendu na Pi

Plik `configuration.xml` (lub `.enc`) musi być w katalogu backendu:
```bash
scp src/WyszukiwaczNet.Api/configuration.xml raspberry@PI_IP:/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/backend/backend/
```

---

## 6. Systemd services — utwórz na Pi

### Backend service
```bash
sudo nano /etc/systemd/system/wyszukiwacz-backend.service
```
Wklej:
```ini
[Unit]
Description=Wyszukiwacz API
After=network.target postgresql.service

[Service]
WorkingDirectory=/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/backend/backend
ExecStart=/home/raspberry/.dotnet/dotnet WyszukiwaczNet.Api.dll
Restart=always
User=raspberry
Environment=ASPNETCORE_URLS=http://+:5012
Environment=DOTNET_ROOT=/home/raspberry/.dotnet
Environment=PATH=/home/raspberry/.dotnet:/usr/bin:/bin
Environment=CONFIG_KEY=qazedctgb123

[Install]
WantedBy=multi-user.target
```

### Frontend service
```bash
sudo nano /etc/systemd/system/wyszukiwacz-frontend.service
```
Wklej:
```ini
[Unit]
Description=Wyszukiwacz Frontend
After=network.target wyszukiwacz-backend.service

[Service]
WorkingDirectory=/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/frontend/frontend
ExecStart=/home/raspberry/.dotnet/dotnet WyszukiwaczApp.dll
Restart=always
User=raspberry
Environment=ASPNETCORE_URLS=http://+:9005
Environment=DOTNET_ROOT=/home/raspberry/.dotnet
Environment=PATH=/home/raspberry/.dotnet:/usr/bin:/bin

[Install]
WantedBy=multi-user.target
```

---

## 7. Uruchom services

```bash
sudo systemctl daemon-reload
sudo systemctl enable wyszukiwacz-backend.service
sudo systemctl enable wyszukiwacz-frontend.service
sudo systemctl start wyszukiwacz-backend.service
sudo systemctl start wyszukiwacz-frontend.service
```

---

## 8. Sprawdź status

```bash
sudo systemctl status wyszukiwacz-backend.service
sudo systemctl status wyszukiwacz-frontend.service
```

Logi w razie błędu:
```bash
journalctl -u wyszukiwacz-backend.service -n 50
journalctl -u wyszukiwacz-frontend.service -n 50
```

---

## Aktualizacja (kolejne deploye)

```bash
dotnet publish src/WyszukiwaczNet.Api -c Release -o ./publish/backend
dotnet publish frontend/WyszukiwaczApp -c Release -o ./publish/frontend
scp -r ./publish/backend/* raspberry@PI_IP:/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/backend/backend/
scp -r ./publish/frontend/* raspberry@PI_IP:/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/frontend/frontend/
scp -r ./scripts/* raspberry@PI_IP:/home/raspberry/Desktop/Wyszukiwacz/Wyszukiwacz/scripts/
```

Następnie na Pi:
```bash
sudo systemctl restart wyszukiwacz-backend.service && sudo systemctl restart wyszukiwacz-frontend.service
```
