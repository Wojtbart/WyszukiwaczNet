# Wyszukiwacz — Agregator Ogłoszeń

Platforma agregująca ogłoszenia z polskich portali internetowych w jednym miejscu. Użytkownik wyszukuje oferty pracy, aut, nieruchomości, produktów i maszyn rolniczych z wielu serwisów jednocześnie, bez potrzeby odwiedzania każdego z osobna. Możliwość ustawiania cyklicznych alertów z powiadomieniami email / SMS / Discord.

## Architektura

```
WyszukiwaczNet/
├── src/                    # Backend — ASP.NET Core 9 REST API
├── frontend/               # Frontend — Blazor Server
├── scripts/                # Scrapery Python
│   ├── work/               # Portale pracy (Pracuj, JustJoin.IT, NoFluffJobs…)
│   ├── auto/               # Auta (Otomoto, OLX, Gratka…)
│   ├── apartment/          # Nieruchomości (Otodom)
│   ├── marketplace/        # Zakupy (Amazon, OLX, Allegro…)
│   ├── promotions/         # Promocje (Pepper, Carrot)
│   ├── agriculture/        # Maszyny rolnicze (OLX, Otomoto Rolnicze…)
│   └── config/             # Konfiguracja połączenia z DB
└── tools/                  # Narzędzia pomocnicze
    └── ConfigTool/         # Szyfrowanie konfiguracji (AES-256)
```

## Komponenty

### Backend (`src/WyszukiwaczNet.Api`)
REST API w ASP.NET Core 9. Odpowiada za autentykację JWT, uruchamianie scraperów Python jako subprocesy, zapis wyników do PostgreSQL, obsługę płatności Stripe i wysyłanie powiadomień przez Hangfire.

→ Szczegóły: [`src/WyszukiwaczNet.Api/README.md`](src/WyszukiwaczNet.Api/README.md)

### Frontend (`frontend/WyszukiwaczApp`)
Aplikacja Blazor Server z interfejsem do wyszukiwania po wielu portalach jednocześnie, zarządzania alertami i historią wyszukiwań. Ciemny motyw, lokalizacja PL/EN, powiadomienia real-time przez SignalR.

→ Szczegóły: [`frontend/WyszukiwaczApp/README.md`](frontend/WyszukiwaczApp/README.md)

### Scrapery Python (`scripts/`)
Niezależne skrypty uruchamiane przez backend. Każdy scraper pobiera ogłoszenia z konkretnego portalu i zapisuje wyniki do PostgreSQL. Obsługują filtry (lokalizacja, cena, typ umowy, paliwo itp.).

| Kategoria | Portale |
|-----------|---------|
| **Praca** | Pracuj.pl, JustJoin.IT, NoFluffJobs, TheProtocol.IT, BulldogJob, Solid.Jobs |
| **Auta** | Otomoto, OLX, Gratka, Sprzedajemy.pl, Autocentrum, Samochody.pl |
| **Nieruchomości** | Otodom |
| **Zakupy** | Amazon, OLX, Allegro, AliExpress, eBay |
| **Promocje** | Pepper.pl, Carrot |
| **Maszyny rolnicze** | OLX Ciągniki, Brzozowiak.pl, Sprzedajemy Ciągniki, Otomoto Rolnicze |

## Stack technologiczny

| Warstwa | Technologia |
|---------|------------|
| Backend | ASP.NET Core 9, EF Core 9, PostgreSQL, Hangfire |
| Frontend | Blazor Server (.NET 9), Radzen.Blazor, SignalR |
| Scrapery | Python 3.8+, requests, BeautifulSoup4, psycopg2, cloudscraper |
| Auth | JWT Bearer, BCrypt |
| Płatności | Stripe |
| Powiadomienia | MailKit (SMTP), Twilio (SMS), Discord Webhooks |

## Wymagania

- .NET 9 SDK
- Python 3.8+ z zależnościami (`pip install requests beautifulsoup4 psycopg2 cloudscraper`)
- PostgreSQL 12+

## Uruchomienie

**Backend:**
```bash
cd src/WyszukiwaczNet.Api
dotnet run
# → http://localhost:5012
```

**Frontend:**
```bash
cd frontend/WyszukiwaczApp
dotnet run
# → http://localhost:7215
```

**Konfiguracja:**

Ustaw zmienne środowiskowe lub uzupełnij `appsettings.Development.json` w projekcie API:
```json
{
  "DefaultConnection": "Host=localhost;Database=wyszukiwacz;Username=...;Password=...",
  "Jwt": { "SecretKey": "...", "Issuer": "...", "Audience": "..." },
  "PythonPath": "python",
  "ScriptsPath": "../../scripts"
}
```

Baza danych tworzona automatycznie przy pierwszym uruchomieniu.

## Przepływ danych

```
Użytkownik (Blazor) → POST /api/data/getData
    → Backend uruchamia skrypt Python (subprocess)
    → Skrypt pobiera ogłoszenia z portalu i zapisuje do PostgreSQL
    → Backend odczytuje nowe oferty z DB
    → Wyniki zwracane do frontendu
```

Alerty cykliczne:
```
Hangfire (cron/interval) → NotificationJob
    → Uruchamia scrapery
    → Porównuje z poprzednimi wynikami
    → Wysyła email / SMS / Discord / in-app
```
