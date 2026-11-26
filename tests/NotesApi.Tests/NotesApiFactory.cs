using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NotesApi.Data;
using NotesApi.Storage;

namespace NotesApi.Tests;

public class NotesApiFactory : WebApplicationFactory<Program>
{
    public const string CleanupKey = "test-cleanup-key";

    // Baza SQLite w pamięci żyje tylko, dopóki jest otwarte choć jedno połączenie – trzymamy je przez cały test.
    private readonly string _connectionString = $"Data Source=file:notes-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive;

    public NotesApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    public FakePhotoStorage Storage { get; } = new();

    protected virtual string? CleanupApiKey => CleanupKey;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Notes"] = _connectionString,
            // Wymagane przez walidację opcji; prawdziwy klient Storage i tak nie jest używany.
            ["Storage:ConnectionString"] = "UseDevelopmentStorage=true",
            ["Cleanup:ApiKey"] = CleanupApiKey,
        }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPhotoStorage>();
            services.AddSingleton<IPhotoStorage>(Storage);

            // Bez tworzenia kontenerów przy starcie – testy nie potrzebują Azurite.
            var initializer = services.Single(d => d.ImplementationType == typeof(StorageInitializer));
            services.Remove(initializer);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<NotesDbContext>().Database.Migrate();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}

public class NoCleanupKeyFactory : NotesApiFactory
{
    protected override string? CleanupApiKey => null;
}
