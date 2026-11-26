using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace NotesApi.Storage;

public sealed class BlobStorageHealthCheck(BlobServiceClient serviceClient, IOptions<StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Sprawdzamy kontener, a nie całe konto: Managed Identity zwykle ma uprawnienia tylko do danych.
            await serviceClient.GetBlobContainerClient(options.Value.ContainerName)
                .GetPropertiesAsync(cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Blob Storage jest niedostępny.", ex);
        }
    }
}
