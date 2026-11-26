using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NotesApi.Maintenance;
using NotesApi.Notes;

namespace NotesApi.Tests;

public class CleanupEndpointTests : IDisposable
{
    private const string Url = "/api/internal/cleanup-orphans";

    private readonly NotesApiFactory _factory = new();
    private readonly HttpClient _client;

    public CleanupEndpointTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task WithoutConfiguredKey_Returns404()
    {
        using var factory = new NoCleanupKeyFactory();
        var client = factory.CreateClient();

        var response = await PostAsync(client, "dowolny-klucz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("zly-klucz")]
    public async Task WithWrongKey_Returns401(string? key)
    {
        var response = await PostAsync(_client, key);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithValidKey_DeletesOnlyOldOrphans()
    {
        // Blob przypisany do notatki – zostaje.
        var created = await _client.PostAsJsonAsync("/api/notes", new { title = "Ze zdjęciem" });
        var note = (await created.Content.ReadFromJsonAsync<NoteDto>())!;
        var file = new ByteArrayContent(new byte[10]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        await _client.PostAsync($"/api/notes/{note.Id}/photo", new MultipartFormDataContent { { file, "file", "a.png" } });
        var ownedBlob = Assert.Single(_factory.Storage.Blobs.Keys);

        // Stary sierota – do usunięcia; świeży sierota – może to upload w trakcie, zostaje.
        _factory.Storage.AddBlob("old-orphan", DateTimeOffset.UtcNow.AddHours(-2));
        _factory.Storage.AddBlob("fresh-orphan", DateTimeOffset.UtcNow);

        var response = await PostAsync(_client, NotesApiFactory.CleanupKey);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CleanupResult>();
        Assert.Equal(1, result?.Deleted);
        Assert.Equal(["old-orphan"], _factory.Storage.Deleted);
        Assert.Contains(ownedBlob, _factory.Storage.Blobs.Keys);
        Assert.Contains("fresh-orphan", _factory.Storage.Blobs.Keys);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url);
        if (key is not null)
        {
            request.Headers.Add(MaintenanceEndpoints.KeyHeader, key);
        }

        return client.SendAsync(request);
    }
}
