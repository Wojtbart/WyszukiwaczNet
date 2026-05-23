# Wyszukiwacz — Backend API

ASP.NET Core 9 REST API obsługujące scraping ogłoszeń, autentykację JWT, powiadomienia (email/SMS/Discord/in-app) oraz subskrypcje Stripe.

## Technologie

- **.NET 9 / ASP.NET Core** — REST API
- **PostgreSQL + EF Core 9** (Npgsql) — baza danych
- **Hangfire** — zadania w tle i harmonogram powiadomień
- **JWT Bearer** — autentykacja
- **BCrypt.Net** — hashowanie haseł
- **Stripe.net 51.x** — płatności i subskrypcje
- **MailKit** — wysyłanie emaili (SMTP)
- **Twilio** — powiadomienia SMS
- **Python** — scrapery uruchamiane jako subprocesy

## Wymagania

- .NET 9 SDK
- PostgreSQL 12+
- Python 3.8+ (do scraperów)

## Konfiguracja

Wszystkie klucze konfiguracyjne ustawiane przez `appsettings.json` lub zmienne środowiskowe:

| Klucz | Opis |
|-------|------|
| `DefaultConnection` | Connection string PostgreSQL |
| `Jwt:SecretKey` | Klucz podpisywania tokenów JWT |
| `Jwt:Issuer` | Issuer tokena JWT |
| `Jwt:Audience` | Audience tokena JWT |
| `PythonPath` | Ścieżka do interpretera Python (domyślnie: `python`) |
| `ScriptsPath` | Ścieżka do katalogu ze scraperami (domyślnie: `../../scripts`) |
| `DbConfigKey` | Klucz szyfrowania konfiguracji DB |
| `Stripe:SecretKey` | Klucz API Stripe |
| `Twilio:AccountSid` | SID konta Twilio |
| `Twilio:AuthToken` | Token Twilio |
| `Twilio:FromNumber` | Numer nadawcy SMS |
| `Discord:WebhookUrl` | Webhook Discord do powiadomień |

Opcjonalnie: zmienna środowiskowa `CONFIG_KEY` do odszyfrowania `configuration.xml.enc` (AES-256, PBKDF2 100k iteracji).

## Uruchomienie

```bash
cd src/WyszukiwaczNet.Api
dotnet run
```

Domyślnie nasłuchuje na `http://localhost:5012`.

Baza danych tworzona automatycznie przy pierwszym uruchomieniu (`EnsureCreated()`).

## Endpointy API

### Użytkownicy `[api/users]`

| Metoda | Ścieżka | Opis | Auth |
|--------|---------|------|------|
| POST | `/registerUser` | Rejestracja | — |
| POST | `/login` | Logowanie → JWT | — |
| GET | `/getUserByLogin/{login}` | Pobierz usera po loginie | — |
| GET | `/{userId}` | Profil użytkownika | JWT |
| PATCH | `/{userId}/password` | Zmień hasło | JWT |
| PATCH | `/{userId}/email` | Zmień email | JWT |
| PATCH | `/{userId}/phone` | Zmień telefon | JWT |
| GET | `/{userId}/platforms` | Subskrypcje platform | JWT |
| POST | `/platforms` | Aktualizuj subskrypcję platformy | JWT |
| GET | `/{userId}/notifications` | Ustawienia powiadomień | JWT |
| GET | `/{userId}/config` | Konfiguracja alertu (opcj. kategoria) | JWT |
| GET | `/{userId}/configs` | Wszystkie konfiguracje alertów | JWT |
| POST | `/config` | Zapisz konfigurację alertu | JWT |
| PATCH | `/config/enabled` | Włącz/wyłącz konfigurację | JWT |
| GET | `/{userId}/feed` | Feed powiadomień (limit=100) | JWT |
| POST | `/{userId}/feed/read` | Oznacz wszystkie jako przeczytane | JWT |
| POST | `/{userId}/feed/{notificationId}/read` | Oznacz jedno jako przeczytane | JWT |
| POST | `/notifications` | Aktualizuj ustawienie kanału | JWT |

### Oferty `[api/offers]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| GET | `/recent` | Ostatnie oferty (limit=100) |
| GET | `/platform/{platformName}` | Oferty danej platformy |
| GET | `/platforms` | Lista platform |
| GET | `/channels` | Kanały powiadomień |
| GET | `/{id}` | Oferta po ID |

### Dane `[api/data]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| POST | `/getData` | Uruchom scrapery i zwróć wyniki |

Parametry `getData`:

```json
{
  "websites": ["pracuj", "justjoinit"],
  "phrase": "java",
  "additionalPhrase": "",
  "requestNumber": 30,
  "workLocation": "Kraków",
  "employmentLevel": 18,
  "contractType": 3,
  "priceFrom": null,
  "priceTo": null,
  "areaFrom": null,
  "areaTo": null,
  "fuel": null,
  "gearbox": null
}
```

### Subskrypcje `[api/subscriptions]`

| Metoda | Ścieżka | Opis | Auth |
|--------|---------|------|------|
| GET | `/plans` | Plany subskrypcji | — |
| GET | `/{userId}/plan` | Plan użytkownika | JWT |
| GET | `/{userId}/limits` | Limity planu | JWT |
| POST | `/checkout` | Utwórz sesję Stripe Checkout | JWT |
| POST | `/{userId}/cancel` | Anuluj subskrypcję | JWT |
| POST | `/webhook` | Webhook Stripe | — |

### Powiadomienia `[api/notifications]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| POST | `/cronJob` | Zaplanuj zadanie (jednorazowe lub cykliczne) |
| GET | `/jobsForUser/{userId}` | Zadania użytkownika w Hangfire |
| POST | `/deleteJobsForUser` | Usuń zadania użytkownika |
| POST | `/sms` | Wyślij SMS (Twilio) |
| POST | `/discord` | Wyślij wiadomość Discord |
| POST | `/email` | Wyślij email |

## Scrapery Python

Scrapery uruchamiane jako subprocesy przez `PythonScriptService`. Timeout: 120s.

| Kategoria | Dostępne platformy |
|-----------|-------------------|
| **Praca** | pracuj, justjoinit, nofluffjobs, theprotocolit, bulldogjob, solidjobs |
| **Auta** | otomoto, autoscout, gratka, sprzedajemy, autocentrum, samochody |
| **Nieruchomości** | otodom |
| **Zakupy** | olx, amazon, allegro, aliexpress, ebay |
| **Promocje** | pepper, carrot |
| **Rolnicze** | olxciagniki, brzozowiak, sprzedajemyciagniki, otomotorolnicze |

Skrypty lądują w katalogu `scripts/` (konfigurowalny przez `ScriptsPath`). Każdy scraper zapisuje wyniki do PostgreSQL i wypisuje na stdout `Records inserted: N`.

## Autentykacja

JWT Bearer — token ważny **24 godziny**.

Claims w tokenie: `NameIdentifier` (userId), `Name` (login), `Jti` (guid).

Hasła hashowane BCrypt.

## Zadania w tle (Hangfire)

- Dashboard: `/hangfire` (read-only)
- Storage: PostgreSQL
- `NotificationJob` — pobiera oferty przez scrapery i rozsyła powiadomienia

Kanały powiadomień: **email**, **SMS**, **Discord**, **in-app** (tabela `notifications`).

## Baza danych — główne tabele

| Tabela | Opis |
|--------|------|
| `users` | Konta użytkowników |
| `platforms` | Portale ogłoszeniowe |
| `offers` | Zebrane ogłoszenia |
| `vehicle_details` | Szczegóły pojazdów (1:1 → offers) |
| `job_details` | Szczegóły ofert pracy (1:1 → offers) |
| `user_notification_configs` | Konfiguracje alertów (fraza, harmonogram, filtry JSON) |
| `notifications` | Feed powiadomień in-app |
| `background_jobs` | Metadane zadań Hangfire |
| `subscription_plans` | Plany Stripe |
| `user_subscriptions` | Aktywne subskrypcje użytkowników |

## Struktura projektu

```
src/WyszukiwaczNet.Api/
├── Controllers/
│   ├── DataController.cs         # /api/data — scraping
│   ├── UsersController.cs        # /api/users — auth, profile, alerty
│   ├── OffersController.cs       # /api/offers — oferty, platformy
│   ├── SubscriptionsController.cs # /api/subscriptions — Stripe
│   └── NotificationsController.cs # /api/notifications — zadania, kanały
├── Services/
│   ├── PythonScriptService.cs    # uruchamianie scraperów
│   ├── UserService.cs            # logika użytkowników
│   ├── SubscriptionService.cs    # logika Stripe
│   ├── JwtService.cs             # generowanie/walidacja tokenów
│   ├── EmailService.cs           # SMTP przez MailKit
│   └── EmailTemplateService.cs   # szablony HTML emaili
├── Jobs/
│   └── NotificationJob.cs        # Hangfire — wysyłanie powiadomień
├── Repositories/
│   ├── OfferRepository.cs
│   └── UserRepository.cs
├── Entities/
│   └── AllEntities.cs            # wszystkie encje EF Core
├── DTOs/
│   └── Requests.cs               # modele requestów/odpowiedzi
├── Data/
│   └── ApplicationDbContext.cs   # DbContext
├── Security/
│   └── EncryptedXmlConfigProvider.cs # AES-256 config provider
└── Program.cs                    # DI, middleware, konfiguracja
```
