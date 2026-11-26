using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace NotesApi.Storage;

public static class StorageExtensions
{
    public static IServiceCollection AddPhotoStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(o => o.UsesConnectionString || o.BlobServiceUri is not null,
                "Ustaw Storage:ConnectionString (lokalnie) albo Storage:BlobServiceUri (Azure).")
            // Błędna konfiguracja ma wywrócić aplikację przy starcie, a nie przy pierwszym uploadzie.
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;

            if (options.UsesConnectionString)
            {
                return new BlobServiceClient(options.ConnectionString);
            }

            // Lokalnie DefaultAzureCredential użyje np. `az login`, w Azure – Managed Identity aplikacji.
            return new BlobServiceClient(options.BlobServiceUri, new DefaultAzureCredential());
        });

        services.AddSingleton<IPhotoStorage, BlobPhotoStorage>();
        services.AddHostedService<StorageInitializer>();

        return services;
    }
}
