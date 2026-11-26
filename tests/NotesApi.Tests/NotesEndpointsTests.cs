using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NotesApi.Notes;

namespace NotesApi.Tests;

public class NotesEndpointsTests : IDisposable
{
    private readonly NotesApiFactory _factory = new();
    private readonly HttpClient _client;

    public NotesEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Create_ValidNote_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/notes", new { title = "  Azure  ", content = "Treść" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        Assert.NotNull(note);
        Assert.Equal("Azure", note.Title);
        Assert.Equal("Treść", note.Content);
        Assert.Null(note.PhotoUrl);
        Assert.Equal($"/api/notes/{note.Id}", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_MissingTitle_Returns400ValidationProblem(string? title)
    {
        var response = await _client.PostAsJsonAsync("/api/notes", new { title });

        await AssertValidationErrorAsync(response, "Title");
    }

    [Fact]
    public async Task Create_TitleLongerThan200_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/notes", new { title = new string('a', 201) });

        await AssertValidationErrorAsync(response, "Title");
    }

    [Fact]
    public async Task Create_TitleOfExactly200_IsAccepted()
    {
        var response = await _client.PostAsJsonAsync("/api/notes", new { title = new string('a', 200) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Get_MissingNote_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/notes/12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem?.Status);
    }

    [Fact]
    public async Task Update_ExistingNote_ChangesFields()
    {
        var created = await CreateNoteAsync("Przed");

        var response = await _client.PutAsJsonAsync($"/api/notes/{created.Id}", new { title = "Po", content = "Nowa" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await _client.GetFromJsonAsync<NoteDto>($"/api/notes/{created.Id}");
        Assert.Equal("Po", fetched?.Title);
        Assert.Equal("Nowa", fetched?.Content);
    }

    [Fact]
    public async Task Update_InvalidTitle_Returns400()
    {
        var created = await CreateNoteAsync("Notatka");

        var response = await _client.PutAsJsonAsync($"/api/notes/{created.Id}", new { title = "" });

        await AssertValidationErrorAsync(response, "Title");
    }

    [Fact]
    public async Task Update_MissingNote_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/notes/12345", new { title = "X" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingNote_Returns204AndNoteIsGone()
    {
        var created = await CreateNoteAsync("Do usunięcia");

        var response = await _client.DeleteAsync($"/api/notes/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/notes/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task List_SearchIsCaseInsensitiveAndPaginated()
    {
        await CreateNoteAsync("Azure Functions");
        await CreateNoteAsync("Blob storage w AZURE");
        await CreateNoteAsync("Azure SQL");
        await CreateNoteAsync("Zakupy");

        var firstPage = await _client.GetFromJsonAsync<PagedResult<NoteDto>>("/api/notes?search=azure&page=1&pageSize=2");
        var secondPage = await _client.GetFromJsonAsync<PagedResult<NoteDto>>("/api/notes?search=azure&page=2&pageSize=2");

        Assert.Equal(3, firstPage?.TotalCount);
        Assert.Equal(2, firstPage?.Items.Count);
        Assert.Single(secondPage!.Items);
        // Najnowsze notatki są pierwsze.
        Assert.Equal("Azure SQL", firstPage!.Items[0].Title);
    }

    [Fact]
    public async Task List_WithoutParameters_UsesDefaults()
    {
        await CreateNoteAsync("Jedna");

        var result = await _client.GetFromJsonAsync<PagedResult<NoteDto>>("/api/notes");

        Assert.Equal(1, result?.Page);
        Assert.Equal(20, result?.PageSize);
        Assert.Equal(1, result?.TotalCount);
    }

    [Theory]
    [InlineData("page=0", "Page")]
    [InlineData("pageSize=101", "PageSize")]
    public async Task List_InvalidPaging_Returns400(string query, string field)
    {
        var response = await _client.GetAsync($"/api/notes?{query}");

        await AssertValidationErrorAsync(response, field);
    }

    private async Task<NoteDto> CreateNoteAsync(string title)
    {
        var response = await _client.PostAsJsonAsync("/api/notes", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteDto>())!;
    }

    internal static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(field, problem.Errors.Keys);
    }
}
