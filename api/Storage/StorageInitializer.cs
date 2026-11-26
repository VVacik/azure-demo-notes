using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace NotesApi.Storage;

// Tworzy prywatne kontenery przy starcie. Błąd nie zatrzymuje API – health check pokaże, że Storage nie działa.
public sealed class StorageInitializer(
    BlobServiceClient serviceClient,
    IOptions<StorageOptions> options,
    ILogger<StorageInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        foreach (var name in new[] { options.Value.ContainerName, options.Value.ThumbnailContainerName })
        {
            try
            {
                // PublicAccessType.None = kontener prywatny; dostęp z przeglądarki tylko przez SAS.
                await serviceClient.GetBlobContainerClient(name)
                    .CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: timeout.Token);
                logger.LogInformation("Kontener {Container} jest gotowy", name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Nie udało się utworzyć kontenera {Container} w {Endpoint}. Czy Azurite jest uruchomiony?",
                    name, serviceClient.Uri);
                return;
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
