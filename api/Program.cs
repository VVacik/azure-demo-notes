using NotesApi.Data;
using NotesApi.Health;
using NotesApi.Maintenance;
using NotesApi.Notes;
using NotesApi.Storage;

var builder = WebApplication.CreateBuilder(args);

// TODO (etap Key Vault): sekrety z Azure Key Vault przez Managed Identity, np.
// builder.Configuration.AddAzureKeyVault(new Uri(builder.Configuration["KeyVault:Uri"]!), new DefaultAzureCredential());

// TODO (etap Application Insights): telemetria i logi do Azure Monitor, np.
// builder.Services.AddOpenTelemetry().UseAzureMonitor();
// Logi z ILogger trafią tam bez zmian w kodzie.

builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddNotesDatabase(builder.Configuration);
builder.Services.AddPhotoStorage(builder.Configuration);
builder.Services.AddNotesHealthChecks();

builder.Services.Configure<CleanupOptions>(builder.Configuration.GetSection(CleanupOptions.SectionName));
builder.Services.AddScoped<OrphanPhotoCleaner>();

// Dozwolone originy z konfiguracji: lokalnie Vite, w Azure adres Static Web App.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Nieobsłużone wyjątki i puste odpowiedzi błędów (np. 404) zamieniamy na ProblemDetails.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Błędny JSON w żądaniu to wina klienta (400), a nie serwera (500).
    StatusCodeSelector = ex => ex is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Notes API"));
}

// Brak UseHttpsRedirection: w Azure HTTPS wymusza platforma (App Service "HTTPS Only").
app.UseCors();

app.MapNotesEndpoints();
app.MapMaintenanceEndpoints();
app.MapNotesHealthChecks();

await app.ApplyMigrationsInDevelopmentAsync();

app.Logger.LogInformation("Notes API startuje w środowisku {Environment}", app.Environment.EnvironmentName);
await app.RunAsync();

// Udostępnia klasę Program dla WebApplicationFactory w testach.
public partial class Program;
