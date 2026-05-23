# Wyszukiwacz — Frontend (Blazor)

Blazor Server aplikacja agregująca ogłoszenia z polskich portali w jednym miejscu. Umożliwia przeszukiwanie ofert pracy, aut, nieruchomości, produktów i maszyn rolniczych z poziomu jednego interfejsu z możliwością ustawiania alertów.

## Technologie

- **.NET 9 / Blazor Server** — renderowanie po stronie serwera
- **Radzen.Blazor 10.x** — komponenty UI (DataGrid, formularze, dialogi)
- **Blazored.LocalStorage** — przechowywanie tokena JWT i motywu
- **SignalR** — powiadomienia w czasie rzeczywistym
- **Bootstrap** — siatka i bazowe style
- **Mapster** — mapowanie DTO

## Wymagania

- .NET 9 SDK
- Backend API działający pod adresem skonfigurowanym w `appsettings.json`

## Konfiguracja

| Plik | URL backendu |
|------|-------------|
| `appsettings.json` | `http://172.22.0.15:9005/api/` (produkcja) |
| `appsettings.Development.json` | `http://localhost:5012/api/` (development) |

## Uruchomienie

```bash
dotnet run --project frontend/WyszukiwaczApp
```

## Struktura projektu

```
WyszukiwaczApp/
├── Components/
│   ├── App.razor               # root — dark mode script, motyw Radzen
│   ├── Pages/                  # strony aplikacji
│   │   ├── Home.razor          # landing page
│   │   ├── Categories.razor    # nawigacja kategorii
│   │   ├── Login.razor
│   │   ├── Register.razor
│   │   ├── Account.razor
│   │   ├── Billing.razor
│   │   ├── Pricing.razor
│   │   ├── History.razor       # historia wyszukiwań
│   │   ├── Offers.razor        # zapisane oferty
│   │   ├── UserNotifications.razor
│   │   └── Notification.razor  # ustawienia alertów
│   └── Categories/             # widoki kategorii z filtrami i gridami
│       ├── Cars.razor
│       ├── Flats.razor         # /apartments
│       ├── Shopping.razor
│       ├── Work.razor
│       └── Tractors.razor
├── Shared/
│   ├── MainLayout.razor        # layout z sidebarem (zalogowani)
│   ├── NavMenu.razor           # sidebar — nawigacja, badge powiadomień
│   └── PublicLayout.razor      # layout dla stron auth
├── Proxies/
│   ├── AuthTokenHandler.cs     # dodaje Bearer token do requestów
│   ├── DataProxy.cs            # główny klient API
│   ├── LoginProxy.cs
│   ├── NotificationProxy.cs
│   └── HistoryProxy.cs
├── Resources/
│   ├── SharedResource.pl.resx  # tłumaczenia PL
│   └── SharedResource.en.resx  # tłumaczenia EN
├── Globals.cs                  # stan sesji (IsLogged, AuthToken, Login, UserId)
├── ApiConfig.cs                # BaseUrl API
└── wwwroot/
    ├── app.css
    ├── bootstrap/
    └── images/                 # loga platform
```

## Kategorie i obsługiwane portale

| Kategoria | Ścieżka | Portale |
|-----------|---------|---------|
| **Praca** | `/work` | Pracuj.pl, JustJoin.IT, NoFluffJobs, TheProtocol.IT, BulldogJob, Solid.Jobs |
| **Auta** | `/cars` | Otomoto, OLX, Gratka, Sprzedajemy, Autocentrum, Samochody.pl |
| **Nieruchomości** | `/apartments` | Otodom |
| **Zakupy** | `/shopping` | Amazon, OLX |
| **Maszyny rolnicze** | `/tractors` | OLX Ciągniki, Brzozowiak.pl, Sprzedajemy Ciągniki, Otomoto Rolnicze |

## Autentykacja

- Logowanie → backend zwraca **JWT token**
- Token zapisywany w **LocalStorage** (`Blazored.LocalStorage`)
- `AuthTokenHandler` automatycznie dołącza `Authorization: Bearer <token>` do każdego requestu HTTP
- Stan sesji w `Globals.cs` (statyczne pola dostępne z każdego komponentu)

## Funkcje

- **Ciemny motyw** — przełącznik w sidebarze, persystowany w LocalStorage
- **Lokalizacja** — PL / EN przez `IStringLocalizer<SharedResource>`
- **Powiadomienia real-time** — SignalR, badge z licznikiem w nawigacji
- **Alerty cykliczne** — ustawianie powiadomień per kategoria i fraza
- **Historia wyszukiwań** — log zapytań na `/search-history`
- **Zaawansowane filtry** — zakres cen, typ umowy, poziom stanowiska, lokalizacja itp.
- **Responsywność** — hamburger menu na mobile
