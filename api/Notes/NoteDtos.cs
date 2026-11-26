using System.ComponentModel.DataAnnotations;
using NotesApi.Data;
using NotesApi.Storage;

namespace NotesApi.Notes;

public record NoteDto(
    int Id,
    string Title,
    string? Content,
    Uri? PhotoUrl,
    Uri? ThumbnailUrl,
    DateTime CreatedAt);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public class NoteRequest
{
    [Required(ErrorMessage = "Tytuł jest wymagany.")]
    [MaxLength(NotesDbContext.TitleMaxLength, ErrorMessage = "Tytuł może mieć maksymalnie 200 znaków.")]
    public string Title { get; set; } = string.Empty;

    public string? Content { get; set; }
}

public class NotesQuery
{
    public string? Search { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Numer strony musi być większy od 0.")]
    public int? Page { get; set; }

    [Range(1, 100, ErrorMessage = "Rozmiar strony musi być w zakresie 1-100.")]
    public int? PageSize { get; set; }
}

public static class NoteMapping
{
    public static async Task<NoteDto> ToDtoAsync(this Note note, IPhotoStorage storage, CancellationToken cancellationToken)
    {
        Uri? photoUrl = null;
        Uri? thumbnailUrl = null;

        if (note.PhotoBlobName is { } blobName)
        {
            photoUrl = await storage.GetReadUrlAsync(blobName, PhotoVariant.Original, cancellationToken);
            // Miniaturę tworzy Azure Function asynchronicznie – zaraz po uploadzie może jej jeszcze nie być,
            // dlatego frontend przy błędzie ładowania wraca do pełnego zdjęcia.
            thumbnailUrl = await storage.GetReadUrlAsync(blobName, PhotoVariant.Thumbnail, cancellationToken);
        }

        return new NoteDto(note.Id, note.Title, note.Content, photoUrl, thumbnailUrl, note.CreatedAt);
    }
}
