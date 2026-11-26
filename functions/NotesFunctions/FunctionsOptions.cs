namespace NotesFunctions;

// Ustawienia czytane z local.settings.json (lokalnie) albo z Application Settings (Azure).
public class ThumbnailOptions
{
    public string PhotosContainerName { get; set; } = "photos";
    public string ThumbnailsContainerName { get; set; } = "thumbnails";
    public int ThumbnailMaxSize { get; set; } = 300;
}

public class CleanupOptions
{
    public string? NotesApiBaseUrl { get; set; }
    public string? NotesApiCleanupKey { get; set; }
}
