# azure-demo-notes

Demo „Notatki ze zdjęciami” do nauki Azure: React + ASP.NET Core API + Azure Functions + Blob Storage.

Kod działa w całości lokalnie, a przejście na Azure to **zmiana konfiguracji, bez przepisywania kodu**. W repo nie ma żadnych sekretów.

## Architektura

```
                     ┌──────────────────────────┐
   przeglądarka ───▶ │ web (React, Vite)         │   lokalnie: Vite :5173
        │            │ → Azure Static Web Apps   │
        │            └────────────┬─────────────┘
        │                         │ REST (VITE_API_URL)
        │                         ▼
        │            ┌──────────────────────────┐      ┌──────────────────────┐
        │            │ api (ASP.NET Core 10)     │ ───▶ │ baza: SQLite          │
        │            │ → Azure App Service       │      │ → Azure SQL Database  │
        │            └────────────┬─────────────┘      └──────────────────────┘
        │   zdjęcia przez SAS     │ upload / delete / SAS
        │   (tylko odczyt, 15 min)▼
        │            ┌──────────────────────────┐
        └──────────▶ │ Blob Storage              │   lokalnie: Azurite :10000
                     │  photos/  thumbnails/     │
                     └────────────┬─────────────┘
                                  │ BlobTrigger (nowe zdjęcie)
                                  ▼
                     ┌──────────────────────────┐
                     │ functions (isolated .NET) │   lokalnie: func :7071
                     │ GenerateThumbnail         │ → zapis do thumbnails/
                     │ CleanupOrphanedPhotos ────┼─▶ POST /api/internal/cleanup-orphans
                     └──────────────────────────┘     (TimerTrigger, raz na dobę)
```

Najważniejsze decyzje:

- **Kontenery są prywatne.** API zapisuje w bazie tylko nazwę pliku, a przy odczycie generuje adres SAS ważny 15 minut i tylko do odczytu. Przeglądarka pobiera zdjęcia bezpośrednio ze Storage, z pominięciem API.
- **Logowanie do Storage zależy od konfiguracji.** Z `Storage:ConnectionString` (Azurite) API podpisuje SAS kluczem konta. Z samym `Storage:BlobServiceUri` używa `DefaultAzureCredential` (w Azure: Managed Identity) i User Delegation SAS, więc nigdzie nie ma klucza konta.
- **Miniatury tworzy Azure Function asynchronicznie.** Do czasu jej powstania frontend pokazuje oryginał.

## Struktura repo

| Folder | Zawartość |
|---|---|
| [api/](api/) | Notes API: Minimal API, EF Core (SQLite / SQL Server), Blob Storage, health checks, [notes.http](api/notes.http) |
| [tests/NotesApi.Tests/](tests/NotesApi.Tests/) | Testy xUnit z `WebApplicationFactory` (SQLite w pamięci, fake storage) |
| [web/](web/) | Frontend: Vite + React + TypeScript + TanStack Query |
| [functions/NotesFunctions/](functions/NotesFunctions/) | Azure Functions: miniatury i sprzątanie, szczegóły w [functions/README.md](functions/README.md) |
| [dev.ps1](dev.ps1) | Uruchamia całe środowisko lokalne w osobnych oknach |
| [.vscode/](.vscode/) | Debugowanie API i Functions, zadania build/test |

## Wymagania

- .NET SDK 10
- Node.js LTS (z npm)
- Azure Functions Core Tools v4 (`func`), tylko dla Functions
- Azurite: uruchamiany przez `npx`, więc nie wymaga instalacji globalnej
- opcjonalnie VS Code z rozszerzeniami z [.vscode/extensions.json](.vscode/extensions.json)

## Uruchomienie lokalne

### Szybko: `dev.ps1`

```powershell
./dev.ps1                 # Azurite + API + frontend
./dev.ps1 -WithFunctions  # dodatkowo Azure Functions
```

Każda usługa startuje w osobnym oknie. Aby wszystko zatrzymać, zamknij te okna.

### Krok po kroku

**0. Jednorazowo: sekret sprzątania.** Ten sam losowy klucz musi znać API (user-secrets) i Functions (`local.settings.json`):

```powershell
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "Cleanup:ApiKey" $key --project api
Copy-Item functions/NotesFunctions/local.settings.json.example functions/NotesFunctions/local.settings.json
# w local.settings.json wpisz $key jako NotesApiCleanupKey
```

(`RandomNumberGenerator.GetBytes` wymaga PowerShell 7. W Windows PowerShell 5.1 użyj np. `[guid]::NewGuid().ToString('N')`.)

**1. Azurite** (emulator Blob Storage), w głównym folderze:

```powershell
npx azurite --location .azurite --skipApiVersionCheck
```

**2. API**:

```powershell
dotnet run --project api
```

W Development API samo stosuje migracje (tworzy `api/notes.db`) i zakłada kontenery `photos` i `thumbnails`.
Sprawdź: http://localhost:5000/api/health oraz Swagger: http://localhost:5000/swagger.

**3. Functions** (opcjonalnie):

```powershell
cd functions/NotesFunctions
func start
```

**4. Frontend**:

```powershell
cd web
npm install
npm run dev
```

Otwórz http://localhost:5173.

### Testy

```powershell
dotnet test
```

Testy nie potrzebują Azurite ani pliku bazy. Ten sam krok posłuży jako bramka w pipeline „build → test → deploy”.

### Frontend: lint i build

```powershell
cd web
npm run lint
npm run build
```

`VITE_API_URL` jest wklejany do kodu JS **podczas buildu**. Produkcyjny `npm run build` musi mieć tę zmienną ustawioną: w pipeline jako zmienną środowiskową, a lokalnie w `web/.env.local`. Ustawienie jej w Azure Static Web App nic nie da, bo pliki są już zbudowane.

## Konfiguracja

W App Service i Function App dwukropek w nazwie zastępujemy podwójnym podkreśleniem (`Storage:BlobServiceUri` → `Storage__BlobServiceUri`, element tablicy: `Cors__AllowedOrigins__0`).

### API (`api/appsettings*.json`, user-secrets, App Service → Environment variables)

| Nazwa | Przeznaczenie | Wartość lokalna | Wartość w Azure |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Środowisko (Swagger i auto-migracje tylko w `Development`) | `Development` | `Production` (domyślne) |
| `Database:Provider` | Provider EF Core | `Sqlite` | `SqlServer` |
| `ConnectionStrings:Notes` | Połączenie z bazą | `Data Source=notes.db` | `Server=tcp:<serwer>.database.windows.net,1433;Database=<baza>;Authentication=Active Directory Default;Encrypt=True;` (Managed Identity, bez hasła) |
| `Storage:ConnectionString` | Połączenie ze Storage przez klucz | `UseDevelopmentStorage=true` (Azurite) | *puste* |
| `Storage:BlobServiceUri` | Połączenie przez Entra ID / Managed Identity | *puste* | `https://<konto>.blob.core.windows.net` |
| `Storage:ContainerName` | Kontener zdjęć | `photos` | `photos` |
| `Storage:ThumbnailContainerName` | Kontener miniatur | `thumbnails` | `thumbnails` |
| `Storage:ReadUrlLifetimeMinutes` | Ważność linków SAS | `15` | `15` |
| `Cleanup:ApiKey` | Klucz endpointu sprzątania (pusty = endpoint zwraca 404) | user-secrets | App Setting, docelowo odwołanie do Key Vault |
| `Cleanup:MinBlobAgeMinutes` | Młodszych plików nie usuwamy (upload w trakcie) | `60` | `60` |
| `Cors:AllowedOrigins` | Skąd frontend może wołać API | `http://localhost:5173` | `https://<nazwa>.azurestaticapps.net` |

Managed Identity API potrzebuje roli **Storage Blob Data Contributor** na koncie Storage. Ta rola pozwala też pobrać klucz do User Delegation SAS.

### Frontend (`web/.env.*`)

| Nazwa | Przeznaczenie | Wartość lokalna | Wartość w Azure |
|---|---|---|---|
| `VITE_API_URL` | Adres API (bez końcowego `/`) | `http://localhost:5000` (`.env.development`) | `https://<api>.azurewebsites.net`, ustawiany w kroku build pipeline'u |

### Functions (`functions/NotesFunctions/local.settings.json`, Function App → Environment variables)

| Nazwa | Przeznaczenie | Wartość lokalna | Wartość w Azure |
|---|---|---|---|
| `AzureWebJobsStorage` | Storage samego hosta Functions | `UseDevelopmentStorage=true` | `AzureWebJobsStorage__accountName=<konto>` (Managed Identity) |
| `FUNCTIONS_WORKER_RUNTIME` | Model uruchomieniowy | `dotnet-isolated` | `dotnet-isolated` |
| `PhotoStorage` | Konto ze zdjęciami (trigger i zapis miniatur) | `UseDevelopmentStorage=true` | `PhotoStorage__blobServiceUri` i `PhotoStorage__queueServiceUri` (Managed Identity) |
| `PhotosContainerName` | Obserwowany kontener | `photos` | `photos` |
| `ThumbnailsContainerName` | Kontener na miniatury (musi być inny!) | `thumbnails` | `thumbnails` |
| `ThumbnailMaxSize` | Dłuższy bok miniatury w px | `300` | `300` |
| `CleanupSchedule` | CRON sprzątania (z sekundami, UTC) | `0 0 3 * * *` | `0 0 3 * * *` |
| `NotesApiBaseUrl` | Adres API | `http://localhost:5000` | `https://<api>.azurewebsites.net` |
| `NotesApiCleanupKey` | Ten sam klucz co `Cleanup:ApiKey` w API | klucz z user-secrets | odwołanie do Key Vault |

## Baza danych

### Wybór providera

`Database:Provider` = `Sqlite` (domyślnie) albo `SqlServer`. Oba pakiety EF Core są zainstalowane. Przy SQL Server włączone jest `EnableRetryOnFailure`, bo Azure SQL potrafi chwilowo odrzucać połączenia.

Każdy provider ma osobny zestaw migracji, bo SQLite i SQL Server generują inny SQL. Dlatego są dwie klasy kontekstu: `SqliteNotesDbContext` i `SqlServerNotesDbContext`. Reszta kodu używa wspólnego `NotesDbContext`.

### Migracje dla SQL Server (etap Azure SQL)

Nie są jeszcze wygenerowane. Gdy będą potrzebne:

```powershell
dotnet ef migrations add InitialCreate --project api --context SqlServerNotesDbContext --output-dir Migrations/SqlServer
```

Nowa zmiana modelu oznacza od tej pory dwie migracje, po jednej dla każdego kontekstu (`--context SqliteNotesDbContext` i `--context SqlServerNotesDbContext`).

### Dlaczego nie migrujemy bazy przy starcie w produkcji?

W Development API stosuje migracje przy starcie, bo to wygodne. W produkcji tego nie robimy, bo:

- **Kilka instancji naraz.** App Service może uruchomić kilka kopii aplikacji (skalowanie, deployment slots), a każda próbowałaby zmieniać schemat w tym samym momencie.
- **Uprawnienia.** Tożsamość aplikacji powinna mieć tylko prawa do danych (odczyt i zapis), a nie do zmiany schematu (DDL).
- **Kontrola i widoczność.** Nieudana migracja ma zatrzymać wdrożenie w pipeline, z czytelnym logiem, a nie wywracać aplikację przy starcie.

Zamiast tego w pipeline CI/CD, przed wdrożeniem nowej wersji API:

```powershell
# jednorazowy plik wykonywalny z migracjami (uruchamiany w pipeline z connection stringiem do Azure SQL)
dotnet ef migrations bundle --project api --context SqlServerNotesDbContext --output efbundle.exe
./efbundle.exe --connection "<connection string>"

# albo idempotentny skrypt SQL do przejrzenia i uruchomienia
dotnet ef migrations script --idempotent --project api --context SqlServerNotesDbContext --output migrate.sql
```

## Sekrety

- W repo nie ma żadnego sekretu. `UseDevelopmentStorage=true` to publicznie znany skrót do Azurite.
- Lokalne sekrety API trzymamy w **dotnet user-secrets** (`dotnet user-secrets list --project api`).
- Ustawienia lokalne Functions są w `local.settings.json`, który jest w `.gitignore`. W repo jest tylko `local.settings.json.example`.
- W Azure: Managed Identity zamiast haseł do Storage i SQL, a jedyny sekret aplikacyjny (`Cleanup:ApiKey`) docelowo trafi do Key Vault. Miejsca na Key Vault i Application Insights są oznaczone `TODO` w `api/Program.cs` i `functions/NotesFunctions/Program.cs`.

## Debugowanie w VS Code

- **API (Notes):** uruchamia API z debuggerem na porcie 5000.
- **Functions (Notes):** buduje funkcje, startuje `func host start` i podłącza debugger (wymaga rozszerzenia Azure Functions).
- **API + Functions:** oba naraz.
- Zadania (`Ctrl+Shift+P` → *Tasks: Run Task*): `azurite`, `web: dev`, `test`, `build: api`, `build: functions`.

## Typowe problemy

| Objaw | Przyczyna i rozwiązanie |
|---|---|
| `/api/health` pokazuje `storage: Unhealthy`, a upload zwraca 500 | Azurite nie działa. Uruchom go i zrestartuj API (kontenery tworzą się przy starcie). |
| Azurite odrzuca żądania: *The API version … is not supported* | Uruchom go z `--skipApiVersionCheck` (dev.ps1 robi to automatycznie). |
| Frontend: *Brak VITE_API_URL* | Brak pliku `.env.development` lub `.env.local`. Skopiuj `web/.env.example`. |
| Błąd CORS w konsoli przeglądarki | Frontend działa pod innym adresem niż w `Cors:AllowedOrigins`. Vite ma ustawiony stały port 5173. |
| Miniatura się nie pojawia | Functions nie działają (`func start`) albo minęło za mało czasu. Trigger blobów lokalnie reaguje z kilkusekundowym opóźnieniem. |
