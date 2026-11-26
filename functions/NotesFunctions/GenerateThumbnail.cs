using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace NotesFunctions;

public class GenerateThumbnail(BlobServiceClient serviceClient, IOptions<ThumbnailOptions> options, ILogger<GenerateThumbnail> logger)
{
    // Trigger nasłuchuje wyłącznie kontenera ze zdjęciami. Miniatury trafiają do innego kontenera,
    // więc ich zapis nie uruchomi funkcji ponownie (brak pętli).
    [Function(nameof(GenerateThumbnail))]
    public async Task Run(
        [BlobTrigger("%PhotosContainerName%/{name}", Connection = "PhotoStorage")] byte[] photo,
        string name,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // Dodatkowy bezpiecznik: gdyby ktoś ustawił ten sam kontener dla obu, funkcja wpadłaby w pętlę.
        if (string.Equals(settings.PhotosContainerName, settings.ThumbnailsContainerName, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("PhotosContainerName i ThumbnailsContainerName muszą być różne – pomijam {BlobName}", name);
            return;
        }

        if (FormatFor(name) is not { } target)
        {
            logger.LogWarning("Pomijam {BlobName}: nieobsługiwane rozszerzenie pliku", name);
            return;
        }

        var thumbnail = ThumbnailGenerator.Create(photo, target.Format, settings.ThumbnailMaxSize);
        if (thumbnail is null)
        {
            logger.LogWarning("Pomijam {BlobName}: plik nie jest poprawnym obrazem", name);
            return;
        }

        var container = serviceClient.GetBlobContainerClient(settings.ThumbnailsContainerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        // Ta sama nazwa co oryginał – API wylicza adres miniatury bez zaglądania do bazy.
        await container.GetBlobClient(name).UploadAsync(
            BinaryData.FromBytes(thumbnail),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = target.ContentType } },
            cancellationToken);

        logger.LogInformation(
            "Utworzono miniaturę {BlobName}: {OriginalSize} B → {ThumbnailSize} B", name, photo.Length, thumbnail.Length);
    }

    private static (SKEncodedImageFormat Format, string ContentType)? FormatFor(string blobName) =>
        Path.GetExtension(blobName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => (SKEncodedImageFormat.Jpeg, "image/jpeg"),
            ".png" => (SKEncodedImageFormat.Png, "image/png"),
            ".webp" => (SKEncodedImageFormat.Webp, "image/webp"),
            _ => null,
        };
}
