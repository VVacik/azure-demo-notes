using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace NotesApi.Storage;

public sealed class BlobPhotoStorage(
    BlobServiceClient serviceClient,
    IOptions<StorageOptions> options,
    TimeProvider timeProvider,
    ILogger<BlobPhotoStorage> logger) : IPhotoStorage
{
    private readonly StorageOptions _options = options.Value;
    private readonly SemaphoreSlim _keyLock = new(1, 1);
    private UserDelegationKey? _delegationKey;

    private BlobContainerClient Photos => serviceClient.GetBlobContainerClient(_options.ContainerName);
    private BlobContainerClient Thumbnails => serviceClient.GetBlobContainerClient(_options.ThumbnailContainerName);

    public async Task<string> UploadAsync(int noteId, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        // Losowa nazwa: nie da się zgadnąć adresu, a nowe zdjęcie nie nadpisuje starego w cache przeglądarki.
        var blobName = $"{noteId}/{Guid.NewGuid():N}{ExtensionFor(contentType)}";

        await Photos.GetBlobClient(blobName).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        }, cancellationToken);

        logger.LogInformation("Zapisano zdjęcie {BlobName} dla notatki {NoteId}", blobName, noteId);
        return blobName;
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        await Photos.DeleteBlobIfExistsAsync(blobName, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        await Thumbnails.DeleteBlobIfExistsAsync(blobName, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);

        logger.LogInformation("Usunięto zdjęcie {BlobName} wraz z miniaturą", blobName);
    }

    public async Task<Uri> GetReadUrlAsync(string blobName, PhotoVariant variant, CancellationToken cancellationToken = default)
    {
        var container = variant == PhotoVariant.Thumbnail ? Thumbnails : Photos;
        var blob = container.GetBlobClient(blobName);
        var expiresOn = timeProvider.GetUtcNow().AddMinutes(_options.ReadUrlLifetimeMinutes);

        // Klient z connection stringa zna klucz konta, więc sam podpisze SAS.
        if (blob.CanGenerateSasUri)
        {
            return blob.GenerateSasUri(BlobSasPermissions.Read, expiresOn);
        }

        // Managed Identity nie ma klucza konta: prosimy Entra ID o klucz delegowany i nim podpisujemy SAS.
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = container.Name,
            BlobName = blobName,
            Resource = "b",
            // Kilka minut zapasu na różnice zegarów między serwerami.
            StartsOn = timeProvider.GetUtcNow().AddMinutes(-5),
            ExpiresOn = expiresOn,
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var key = await GetDelegationKeyAsync(expiresOn, cancellationToken);
        return new BlobUriBuilder(blob.Uri)
        {
            Sas = sasBuilder.ToSasQueryParameters(key, serviceClient.AccountName),
        }.ToUri();
    }

    public async IAsyncEnumerable<StoredBlob> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var container in new[] { Photos, Thumbnails })
        {
            await foreach (var item in container.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                yield return new StoredBlob(item.Name, item.Properties.CreatedOn ?? DateTimeOffset.MinValue);
            }
        }
    }

    // Klucz delegowany jest ważny do 7 dni – pobieramy go raz na jakiś czas zamiast przy każdym zdjęciu.
    private async Task<UserDelegationKey> GetDelegationKeyAsync(DateTimeOffset sasExpiresOn, CancellationToken cancellationToken)
    {
        if (_delegationKey is { } cached && cached.SignedExpiresOn > sasExpiresOn)
        {
            return cached;
        }

        await _keyLock.WaitAsync(cancellationToken);
        try
        {
            if (_delegationKey is { } fresh && fresh.SignedExpiresOn > sasExpiresOn)
            {
                return fresh;
            }

            var now = timeProvider.GetUtcNow();
            var response = await serviceClient.GetUserDelegationKeyAsync(now.AddMinutes(-5), now.AddHours(2), cancellationToken);
            logger.LogInformation("Pobrano nowy User Delegation Key (ważny do {ExpiresOn})", response.Value.SignedExpiresOn);
            return _delegationKey = response.Value;
        }
        finally
        {
            _keyLock.Release();
        }
    }

    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => string.Empty,
    };
}
