namespace NotesApi.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    // Lokalnie: "UseDevelopmentStorage=true" (Azurite). W Azure zostaje puste.
    public string? ConnectionString { get; set; }

    // W Azure: https://<konto>.blob.core.windows.net – logowanie przez Managed Identity, bez kluczy.
    public Uri? BlobServiceUri { get; set; }

    public string ContainerName { get; set; } = "photos";

    // Kontener zapisywany przez Azure Function generującą miniatury.
    public string ThumbnailContainerName { get; set; } = "thumbnails";

    public int ReadUrlLifetimeMinutes { get; set; } = 15;

    public bool UsesConnectionString => !string.IsNullOrWhiteSpace(ConnectionString);
}
