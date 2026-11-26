using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using NotesApi.Storage;

namespace NotesApi.Tests;

// Zastępuje Blob Storage w testach: pliki trzymane w pamięci, bez Azurite.
public sealed class FakePhotoStorage : IPhotoStorage
{
    public ConcurrentDictionary<string, StoredBlob> Blobs { get; } = new();
    public ConcurrentBag<string> Deleted { get; } = [];

    public async Task<string> UploadAsync(int noteId, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await content.CopyToAsync(Stream.Null, cancellationToken);
        var name = $"{noteId}/{Guid.NewGuid():N}";
        Blobs[name] = new StoredBlob(name, DateTimeOffset.UtcNow);
        return name;
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        Blobs.TryRemove(blobName, out _);
        Deleted.Add(blobName);
        return Task.CompletedTask;
    }

    public Task<Uri> GetReadUrlAsync(string blobName, PhotoVariant variant, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uri($"https://fake.blob.local/{variant.ToString().ToLowerInvariant()}/{blobName}?sas=test"));

    public async IAsyncEnumerable<StoredBlob> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var blob in Blobs.Values)
        {
            yield return blob;
        }

        await Task.CompletedTask;
    }

    public void AddBlob(string name, DateTimeOffset createdOn) => Blobs[name] = new StoredBlob(name, createdOn);
}
