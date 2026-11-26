using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotesApi.Data;
using NotesApi.Storage;

namespace NotesApi.Health;

public static class HealthEndpoints
{
    public static IServiceCollection AddNotesHealthChecks(this IServiceCollection services)
    {
        // Timeout, żeby niedostępna usługa nie blokowała odpowiedzi (Azure SDK domyślnie ponawia żądania).
        services.AddHealthChecks()
            .AddDbContextCheck<NotesDbContext>("database")
            .AddCheck<BlobStorageHealthCheck>("storage", timeout: TimeSpan.FromSeconds(5));
        return services;
    }

    public static IEndpointRouteBuilder MapNotesHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/api/health", new HealthCheckOptions { ResponseWriter = WriteResponse });
        return app;
    }

    // Prosty JSON bez szczegółów wyjątków – endpoint jest publiczny.
    private static Task WriteResponse(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
        });
}
