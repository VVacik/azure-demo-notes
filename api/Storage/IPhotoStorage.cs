namespace NotesApi.Storage;

public enum PhotoVariant
{
    Original,
    Thumbnail,
}

public record StoredBlob(string Name, DateTimeOffset CreatedOn);

public interface IPhotoStorage
{
    /// <summary>Zapisuje zdjęcie i zwraca nazwę bloba.</summary>
    Task<string> UploadAsync(int noteId, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Usuwa zdjęcie wraz z miniaturą (jeśli istnieją).</summary>
    Task DeleteAsync(string blobName, CancellationToken cancellationToken = default);

    /// <summary>Zwraca krótkotrwały adres tylko do odczytu.</summary>
    Task<Uri> GetReadUrlAsync(string blobName, PhotoVariant variant, CancellationToken cancellationToken = default);

    /// <summary>Wylicza bloby zdjęć i miniatur (do sprzątania osieroconych plików).</summary>
    IAsyncEnumerable<StoredBlob> ListAsync(CancellationToken cancellationToken = default);
}
