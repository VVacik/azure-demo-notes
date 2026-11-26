using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NotesApi.Notes;

namespace NotesApi.Tests;

public class PhotoUploadTests : IDisposable
{
    private readonly NotesApiFactory _factory = new();
    private readonly HttpClient _client;

    public PhotoUploadTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public async Task Upload_AllowedType_StoresBlobNameAndReturnsUrls(string contentType)
    {
        var note = await CreateNoteAsync();

        var response = await UploadAsync(note.Id, new byte[1024], contentType);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<NoteDto>();
        Assert.NotNull(updated?.PhotoUrl);
        Assert.NotNull(updated.ThumbnailUrl);
        var blobName = Assert.Single(_factory.Storage.Blobs.Keys);
        // W notatce zapisana jest nazwa bloba, a w odpowiedzi – adres do odczytu z SAS.
        Assert.EndsWith(blobName + "?sas=test", updated.PhotoUrl.ToString());
    }

    [Fact]
    public async Task Upload_DisallowedType_Returns400()
    {
        var note = await CreateNoteAsync();

        var response = await UploadAsync(note.Id, "to nie jest zdjęcie"u8.ToArray(), "text/plain");

        await NotesEndpointsTests.AssertValidationErrorAsync(response, "file");
        Assert.Empty(_factory.Storage.Blobs);
    }

    [Fact]
    public async Task Upload_LargerThan5MB_Returns400()
    {
        var note = await CreateNoteAsync();

        var response = await UploadAsync(note.Id, new byte[NotesEndpoints.MaxPhotoBytes + 1], "image/png");

        await NotesEndpointsTests.AssertValidationErrorAsync(response, "file");
        Assert.Empty(_factory.Storage.Blobs);
    }

    [Fact]
    public async Task Upload_ToMissingNote_Returns404()
    {
        var response = await UploadAsync(12345, new byte[10], "image/png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_SecondPhoto_DeletesPreviousBlob()
    {
        var note = await CreateNoteAsync();
        await UploadAsync(note.Id, new byte[10], "image/png");
        var firstBlob = Assert.Single(_factory.Storage.Blobs.Keys);

        await UploadAsync(note.Id, new byte[10], "image/jpeg");

        Assert.Contains(firstBlob, _factory.Storage.Deleted);
        Assert.Single(_factory.Storage.Blobs);
    }

    [Fact]
    public async Task DeleteNote_AlsoDeletesPhoto()
    {
        var note = await CreateNoteAsync();
        await UploadAsync(note.Id, new byte[10], "image/png");
        var blobName = Assert.Single(_factory.Storage.Blobs.Keys);

        await _client.DeleteAsync($"/api/notes/{note.Id}");

        Assert.Contains(blobName, _factory.Storage.Deleted);
        Assert.Empty(_factory.Storage.Blobs);
    }

    private async Task<NoteDto> CreateNoteAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/notes", new { title = "Ze zdjęciem" });
        return (await response.Content.ReadFromJsonAsync<NoteDto>())!;
    }

    private Task<HttpResponseMessage> UploadAsync(int noteId, byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", "photo.bin" } };
        return _client.PostAsync($"/api/notes/{noteId}/photo", form);
    }
}
