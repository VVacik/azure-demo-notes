using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotesFunctions;

var builder = FunctionsApplication.CreateBuilder(args);

// TODO (etap Application Insights): telemetria z workera, np.
// builder.Services.AddApplicationInsightsTelemetryWorkerService().ConfigureFunctionsApplicationInsights();

builder.Services.Configure<ThumbnailOptions>(builder.Configuration);
builder.Services.Configure<CleanupOptions>(builder.Configuration);

// To samo połączenie "PhotoStorage", którego używa BlobTrigger:
// lokalnie connection string do Azurite, w Azure PhotoStorage__blobServiceUri + Managed Identity.
builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration["PhotoStorage"];
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        return new BlobServiceClient(connectionString);
    }

    var serviceUri = builder.Configuration["PhotoStorage:blobServiceUri"]
        ?? throw new InvalidOperationException("Ustaw PhotoStorage (connection string) albo PhotoStorage__blobServiceUri.");
    return new BlobServiceClient(new Uri(serviceUri), new DefaultAzureCredential());
});

builder.Services.AddHttpClient(CleanupOrphanedPhotos.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(5));

builder.Build().Run();
