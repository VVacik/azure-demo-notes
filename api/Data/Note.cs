namespace NotesApi.Data;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }

    // Tylko nazwa bloba, nigdy pełny URL: adres z SAS jest krótkotrwały i generowany przy odczycie.
    public string? PhotoBlobName { get; set; }

    public DateTime CreatedAt { get; set; }
}
