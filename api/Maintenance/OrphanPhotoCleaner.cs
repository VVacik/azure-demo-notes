using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotesApi.Data;
using NotesApi.Storage;

namespace NotesApi.Maintenance;

public class CleanupOptions
{
    public const string SectionName = "Cleanup";

    // Klucz wyłącznie z user-secrets / Application Settings. Pusty = endpoint wyłączony.
    public string? ApiKey { get; set; }

    // Świeżych blobów nie ruszamy: upload mógł się właśnie zakończyć, a zapis do bazy jeszcze trwa.
    public int MinBlobAgeMinutes { get; set; } = 60;
}

public record CleanupResult(int Scanned, int Deleted);

public sealed class OrphanPhotoCleaner(
    NotesDbContext db,
    IPhotoStorage storage,
    IOptions<CleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<OrphanPhotoCleaner> logger)
{
    public async Task<CleanupResult> RunAsync(CancellationToken cancellationToken)
    {
        var knownBlobs = (await db.Notes
                .Where(n => n.PhotoBlobName != null)
                .Select(n => n.PhotoBlobName!)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var threshold = timeProvider.GetUtcNow().AddMinutes(-options.Value.MinBlobAgeMinutes);
        var seen = new HashSet<string>();
        var deleted = 0;

        await foreach (var blob in storage.ListAsync(cancellationToken))
        {
            // Zdjęcie i miniatura mają tę samą nazwę – sprawdzamy ją raz, DeleteAsync usuwa oba pliki.
            if (!seen.Add(blob.Name) || knownBlobs.Contains(blob.Name) || blob.CreatedOn > threshold)
            {
                continue;
            }

            logger.LogInformation("Usuwam osierocony blob {BlobName} (utworzony {CreatedOn})", blob.Name, blob.CreatedOn);
            await storage.DeleteAsync(blob.Name, cancellationToken);
            deleted++;
        }

        logger.LogInformation("Sprzątanie zakończone: sprawdzono {Scanned}, usunięto {Deleted}", seen.Count, deleted);
        return new CleanupResult(seen.Count, deleted);
    }
}
