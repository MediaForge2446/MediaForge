using MediaForge.Core.Enums;
using MediaForge.Core.Models;
using MediaForge.Infrastructure.Persistence;

namespace MediaForge.Infrastructure.Tests;

public sealed class JsonMediaIndexTests
{
    [Fact]
    public async Task UpsertPersistsAndReloadsByVideoId()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var store = new AtomicJsonStore();
            var path = Path.Combine(tempRoot, "media-index.json");
            var first = new JsonMediaIndex(store, path);
            var entry = new MediaIndexEntry(
                "abc123",
                "https://youtube.test/watch?v=abc123",
                Path.Combine(tempRoot, "song.mp3"),
                MediaFormat.Mp3,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow);

            await first.UpsertAsync(entry);

            var second = new JsonMediaIndex(store, path);
            var loaded = await second.FindByVideoIdAsync("ABC123");

            Assert.Equal(entry, loaded);
            Assert.Single(second.Entries);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [Fact]
    public async Task RemoveDeletesEntryFromPersistentIndex()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var store = new AtomicJsonStore();
            var path = Path.Combine(tempRoot, "media-index.json");
            var index = new JsonMediaIndex(store, path);
            await index.UpsertAsync(new MediaIndexEntry(
                "abc123", "https://youtube.test/abc123", Path.Combine(tempRoot, "song.mp3"),
                MediaFormat.Mp3, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

            await index.RemoveAsync("abc123");

            var reloaded = new JsonMediaIndex(store, path);
            Assert.Null(await reloaded.FindByVideoIdAsync("abc123"));
            Assert.Empty(reloaded.Entries);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try { Directory.Delete(path, true); } catch { }
    }
}
