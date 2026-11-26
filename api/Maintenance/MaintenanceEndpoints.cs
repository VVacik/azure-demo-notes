using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace NotesApi.Maintenance;

public static class MaintenanceEndpoints
{
    public const string KeyHeader = "X-Cleanup-Key";

    public static IEndpointRouteBuilder MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        // Endpoint wewnętrzny, wywoływany przez Azure Function – nie pokazujemy go w Swaggerze.
        app.MapPost("/api/internal/cleanup-orphans", CleanupOrphans).ExcludeFromDescription();
        return app;
    }

    private static async Task<Results<Ok<CleanupResult>, NotFound, ProblemHttpResult>> CleanupOrphans(
        [FromHeader(Name = KeyHeader)] string? providedKey,
        IOptions<CleanupOptions> options,
        OrphanPhotoCleaner cleaner,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var expectedKey = options.Value.ApiKey;

        // Brak skonfigurowanego klucza = endpoint udaje, że nie istnieje.
        if (string.IsNullOrEmpty(expectedKey))
        {
            return TypedResults.NotFound();
        }

        if (string.IsNullOrEmpty(providedKey) || !KeysMatch(providedKey, expectedKey))
        {
            loggerFactory.CreateLogger("NotesApi.Maintenance").LogWarning("Odrzucono wywołanie sprzątania: nieprawidłowy klucz");
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Nieprawidłowy klucz");
        }

        return TypedResults.Ok(await cleaner.RunAsync(ct));
    }

    // Porównanie w stałym czasie: hashujemy oba klucze, żeby nawet długość nie wpływała na czas odpowiedzi.
    private static bool KeysMatch(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
