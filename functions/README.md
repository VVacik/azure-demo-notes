# NotesFunctions

Azure Functions (.NET 10, model isolated worker) do demo „Notatki ze zdjęciami”.

| Funkcja | Wyzwalacz | Co robi |
|---|---|---|
| `GenerateThumbnail` | `BlobTrigger` na kontenerze `photos` | Tworzy miniaturę (dłuższy bok maks. 300 px) i zapisuje ją pod tą samą nazwą w kontenerze `thumbnails`. |
| `CleanupOrphanedPhotos` | `TimerTrigger` (CRON z ustawienia `CleanupSchedule`, domyślnie codziennie o 3:00 UTC) | Wywołuje `POST /api/internal/cleanup-orphans` w Notes API, które usuwa zdjęcia i miniatury bez notatki w bazie. |

## Uruchomienie lokalne

Wymagania: Azure Functions Core Tools v4 (`func`), Azurite, działające Notes API (dla sprzątania).

1. Uruchom Azurite (z głównego folderu repo):
   ```powershell
   npx azurite --location .azurite --skipApiVersionCheck
   ```
2. Uruchom API (`dotnet run --project api`), które utworzy kontenery `photos` i `thumbnails`.
3. Przygotuj ustawienia lokalne (jeśli nie istnieją):
   ```powershell
   cd functions/NotesFunctions
   Copy-Item local.settings.json.example local.settings.json
   ```
   W `NotesApiCleanupKey` wpisz ten sam klucz, który API ma w user-secrets:
   ```powershell
   dotnet user-secrets list --project ../../api
   ```
4. Start:
   ```powershell
   func start
   ```
5. Test miniatur: dodaj zdjęcie do notatki (frontend albo `api/notes.http`). Po kilku sekundach w logach funkcji pojawi się `Utworzono miniaturę …`, a lista notatek pokaże miniaturę.
6. Ręczne uruchomienie sprzątania (bez czekania na CRON):
   ```powershell
   Invoke-RestMethod -Method Post -Uri http://localhost:7071/admin/functions/CleanupOrphanedPhotos -ContentType application/json -Body '{}'
   ```
   API pomija pliki młodsze niż `Cleanup:MinBlobAgeMinutes` (domyślnie 60 min).

## Ustawienia

| Nazwa | Lokalnie | W Azure |
|---|---|---|
| `AzureWebJobsStorage` | `UseDevelopmentStorage=true` | konto Storage samej Function App (najlepiej przez Managed Identity: `AzureWebJobsStorage__accountName`) |
| `PhotoStorage` | `UseDevelopmentStorage=true` | zamiast connection stringa: `PhotoStorage__blobServiceUri` i `PhotoStorage__queueServiceUri` (Managed Identity) |
| `PhotosContainerName` / `ThumbnailsContainerName` | `photos` / `thumbnails` | bez zmian |
| `ThumbnailMaxSize` | `300` | bez zmian |
| `CleanupSchedule` | `0 0 3 * * *` | bez zmian (format CRON z sekundami) |
| `NotesApiBaseUrl` | `http://localhost:5000` | adres API w App Service |
| `NotesApiCleanupKey` | klucz z user-secrets API | Application Setting (docelowo odwołanie do Key Vault) |

Managed Identity Function App potrzebuje na koncie ze zdjęciami ról **Storage Blob Data Owner** (odczyt zdjęć, zapis miniatur, potwierdzenia triggera) i **Storage Queue Data Contributor** (kolejka „poison” dla nieudanych plików).

## Dlaczego tak?

**Brak pętli.** Trigger nasłuchuje tylko kontenera `photos`, a miniatury trafiają do `thumbnails`, więc ich zapis nie uruchamia funkcji ponownie. Dodatkowo funkcja odmawia działania, gdy oba ustawienia wskazują ten sam kontener.

**Sprzątanie przez endpoint API zamiast wspólnej biblioteki.** API ma już dostęp do bazy i Storage oraz kod `IPhotoStorage`. Wspólna biblioteka wymagałaby, żeby funkcja też łączyła się z bazą (connection string, sterowniki EF dla SQLite i SQL Server, osobne uprawnienia do Azure SQL). Endpoint jest chroniony kluczem porównywanym w stałym czasie, a bez klucza w konfiguracji zwraca 404.

**SkiaSharp zamiast ImageSharp.** SkiaSharp ma licencję MIT. ImageSharp od wersji 3 ma licencję Six Labors Split License: firmy z przychodem powyżej 1 mln USD potrzebują płatnej licencji komercyjnej. Pakiet `SkiaSharp.NativeAssets.Linux.NoDependencies` dostarcza natywną bibliotekę dla Function App na Linuksie.

## Flex Consumption: co zmienić

Plan **Flex Consumption** nie obsługuje klasycznego BlobTriggera opartego o skanowanie kontenera. Trzeba przejść na źródło Event Grid:

1. W `GenerateThumbnail.cs` dodaj do atrybutu `Source = BlobTriggerSource.EventGrid`:
   ```csharp
   [BlobTrigger("%PhotosContainerName%/{name}", Source = BlobTriggerSource.EventGrid, Connection = "PhotoStorage")]
   ```
2. Utwórz subskrypcję Event Grid na koncie Storage ze zdjęciami: zdarzenie `Microsoft.Storage.BlobCreated`, filtr tematu `/blobServices/default/containers/photos/`, endpoint typu webhook:
   `https://<function-app>.azurewebsites.net/runtime/webhooks/blobs?functionName=Host.Functions.GenerateThumbnail&code=<klucz blobs_extension>`
3. Lokalnie wariant Event Grid wymaga tunelu (np. dev tunnels), więc do nauki i demo lokalnego zostajemy przy zwykłym triggerze.

Na planach Consumption (Windows), Premium i App Service obecny kod działa bez zmian.
