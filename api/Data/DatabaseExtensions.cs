using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NotesApi.Data;

public static class DatabaseExtensions
{
    public static IServiceCollection AddNotesDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<NotesDbContext, SqlServerNotesDbContext>((sp, options) =>
                options.UseSqlServer(GetConnectionString(sp), sql =>
                    // Azure SQL potrafi chwilowo odrzucać połączenia (np. przy skalowaniu) – ponawiamy automatycznie.
                    sql.EnableRetryOnFailure()));
        }
        else if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<NotesDbContext, SqliteNotesDbContext>((sp, options) =>
                options.UseSqlite(GetConnectionString(sp)));
        }
        else
        {
            throw new InvalidOperationException(
                $"Nieznany Database:Provider '{provider}'. Dozwolone wartości: Sqlite, SqlServer.");
        }

        return services;
    }

    // Migracje przy starcie tylko w Development – w produkcji schemat zmieniamy świadomie (patrz README).
    public static async Task ApplyMigrationsInDevelopmentAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NotesDbContext>>();

        logger.LogInformation("Development: stosuję migracje bazy danych ({Provider})", db.Database.ProviderName);
        await db.Database.MigrateAsync();
    }

    // Connection string czytamy przy tworzeniu kontekstu, żeby zadziałały też nadpisania z testów.
    private static string GetConnectionString(IServiceProvider sp) =>
        sp.GetRequiredService<IConfiguration>().GetConnectionString("Notes")
        ?? throw new InvalidOperationException("Brak ConnectionStrings:Notes w konfiguracji.");
}

// Fabryki używane wyłącznie przez narzędzie `dotnet ef` przy generowaniu migracji (nie łączą się z bazą).
public class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteNotesDbContext>
{
    public SqliteNotesDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteNotesDbContext>().UseSqlite("Data Source=notes.db").Options);
}

public class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerNotesDbContext>
{
    public SqlServerNotesDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqlServerNotesDbContext>().UseSqlServer("Server=.;Database=Notes").Options);
}
