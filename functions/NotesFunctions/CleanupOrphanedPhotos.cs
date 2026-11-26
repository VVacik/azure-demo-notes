using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NotesFunctions;

// Funkcja tylko wywołuje API: to API zna bazę i Storage, więc logika sprzątania jest w jednym miejscu.
public class CleanupOrphanedPhotos(IHttpClientFactory httpClientFactory, IOptions<CleanupOptions> options, ILogger<CleanupOrphanedPhotos> logger)
{
    public const string HttpClientName = "NotesApi";

    [Function(nameof(CleanupOrphanedPhotos))]
    public async Task Run([TimerTrigger("%CleanupSchedule%")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.NotesApiBaseUrl) || string.IsNullOrWhiteSpace(settings.NotesApiCleanupKey))
        {
            logger.LogWarning("Sprzątanie pominięte: brak NotesApiBaseUrl lub NotesApiCleanupKey w konfiguracji");
            return;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri(new Uri(settings.NotesApiBaseUrl), "/api/internal/cleanup-orphans"));
        request.Headers.Add("X-Cleanup-Key", settings.NotesApiCleanupKey);

        logger.LogInformation("Uruchamiam sprzątanie osieroconych zdjęć w {ApiUrl}", settings.NotesApiBaseUrl);
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Wyjątek oznacza wywołanie funkcji jako nieudane – będzie to widać w monitoringu.
            throw new InvalidOperationException($"API zwróciło {(int)response.StatusCode}: {body}");
        }

        logger.LogInformation("Sprzątanie zakończone: {Result}. Następne uruchomienie: {Next}", body, timer.ScheduleStatus?.Next);
    }
}
