using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.Storage;

namespace NotesApi.Notes;

public static class NotesEndpoints
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024;
    public static readonly string[] AllowedPhotoTypes = ["image/jpeg", "image/png", "image/webp"];

    private const int DefaultPageSize = 20;

    public static IEndpointRouteBuilder MapNotesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notes").WithTags("Notes");

        group.MapGet("/", GetNotes);
        group.MapGet("/{id:int}", GetNote);
        group.MapPost("/", CreateNote);
        group.MapPut("/{id:int}", UpdateNote);
        group.MapDelete("/{id:int}", DeleteNote);
        group.MapPost("/{id:int}/photo", UploadPhoto)
            // API nie używa ciasteczek, więc ochrona antiforgery dla formularzy nie jest tu potrzebna.
            .DisableAntiforgery();

        return app;
    }

    private static async Task<Ok<PagedResult<NoteDto>>> GetNotes(
        [AsParameters] NotesQuery query, NotesDbContext db, IPhotoStorage storage, CancellationToken ct)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? DefaultPageSize;

        var notes = db.Notes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // LIKE zamiast Contains: w SQLite Contains rozróżnia wielkość liter, LIKE działa tak samo jak w SQL Server.
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            notes = notes.Where(n => EF.Functions.Like(n.Title, pattern, "\\"));
        }

        var totalCount = await notes.CountAsync(ct);
        var items = await notes
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = new List<NoteDto>(items.Count);
        foreach (var note in items)
        {
            dtos.Add(await note.ToDtoAsync(storage, ct));
        }

        return TypedResults.Ok(new PagedResult<NoteDto>(dtos, page, pageSize, totalCount));
    }

    private static async Task<Results<Ok<NoteDto>, ProblemHttpResult>> GetNote(
        int id, NotesDbContext db, IPhotoStorage storage, CancellationToken ct)
    {
        var note = await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
        return note is null
            ? NoteNotFound(id)
            : TypedResults.Ok(await note.ToDtoAsync(storage, ct));
    }

    private static async Task<Created<NoteDto>> CreateNote(
        NoteRequest request, NotesDbContext db, IPhotoStorage storage, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var note = new Note
        {
            Title = request.Title.Trim(),
            Content = request.Content,
            CreatedAt = DateTime.UtcNow,
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);

        Logger(loggerFactory).LogInformation("Utworzono notatkę {NoteId}", note.Id);
        return TypedResults.Created($"/api/notes/{note.Id}", await note.ToDtoAsync(storage, ct));
    }

    private static async Task<Results<Ok<NoteDto>, ProblemHttpResult>> UpdateNote(
        int id, NoteRequest request, NotesDbContext db, IPhotoStorage storage, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var note = await db.Notes.FindAsync([id], ct);
        if (note is null)
        {
            return NoteNotFound(id);
        }

        note.Title = request.Title.Trim();
        note.Content = request.Content;
        await db.SaveChangesAsync(ct);

        Logger(loggerFactory).LogInformation("Zaktualizowano notatkę {NoteId}", id);
        return TypedResults.Ok(await note.ToDtoAsync(storage, ct));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteNote(
        int id, NotesDbContext db, IPhotoStorage storage, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = Logger(loggerFactory);
        var note = await db.Notes.FindAsync([id], ct);
        if (note is null)
        {
            return NoteNotFound(id);
        }

        db.Notes.Remove(note);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Usunięto notatkę {NoteId}", id);

        if (note.PhotoBlobName is { } blobName)
        {
            await TryDeletePhotoAsync(storage, blobName, logger, ct);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<NoteDto>, ProblemHttpResult, ValidationProblem>> UploadPhoto(
        int id, IFormFile? file, NotesDbContext db, IPhotoStorage storage, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = Logger(loggerFactory);
        var note = await db.Notes.FindAsync([id], ct);
        if (note is null)
        {
            return NoteNotFound(id);
        }

        if (ValidatePhoto(file) is { } error)
        {
            logger.LogWarning("Odrzucono zdjęcie dla notatki {NoteId}: {Reason}", id, error);
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [error] });
        }

        var previousBlob = note.PhotoBlobName;
        await using (var stream = file!.OpenReadStream())
        {
            note.PhotoBlobName = await storage.UploadAsync(id, stream, file.ContentType.ToLowerInvariant(), ct);
        }

        // Jeśli zapis do bazy się nie uda, blob zostanie osierocony – sprząta go funkcja CleanupOrphanedPhotos.
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Notatka {NoteId} ma nowe zdjęcie {BlobName} ({Size} B)", id, note.PhotoBlobName, file.Length);

        if (previousBlob is not null)
        {
            await TryDeletePhotoAsync(storage, previousBlob, logger, ct);
        }

        return TypedResults.Ok(await note.ToDtoAsync(storage, ct));
    }

    private static string? ValidatePhoto(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return "Wybierz plik ze zdjęciem.";
        }

        if (file.Length > MaxPhotoBytes)
        {
            return "Zdjęcie może mieć maksymalnie 5 MB.";
        }

        if (!AllowedPhotoTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return "Dozwolone formaty: JPEG, PNG, WebP.";
        }

        return null;
    }

    // Notatka jest już usunięta/zmieniona – błąd Storage tylko logujemy, osierocony plik usunie sprzątanie.
    private static async Task TryDeletePhotoAsync(IPhotoStorage storage, string blobName, ILogger logger, CancellationToken ct)
    {
        try
        {
            await storage.DeleteAsync(blobName, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nie udało się usunąć zdjęcia {BlobName}; zostanie usunięte przez sprzątanie", blobName);
        }
    }

    private static ProblemHttpResult NoteNotFound(int id) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Nie znaleziono notatki",
            detail: $"Notatka o id {id} nie istnieje.");

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static ILogger Logger(ILoggerFactory factory) => factory.CreateLogger("NotesApi.Notes");
}
