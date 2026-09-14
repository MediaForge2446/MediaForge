using MediaForge.Core.Enums;
using MediaForge.Core.Models;
using MediaForge.Infrastructure.Persistence;

namespace MediaForge.Infrastructure.Tests;

public sealed class JsonMediaIndexTests
{
    [Fact]
    public async Task UpsertPersistsAndReloadsByVideoId()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "MediaForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var store = new AtomicJsonStore();
            var paths = new LocalAppPathsForTest(tempRoot);
            var first = new JsonMediaIndex(store, paths);
            var entry = new MediaIndexEntry(
                "abc123",
                "https://youtube.test/watch?v=abc123",
                Path.Combine(tempRoot, "song.mp3"),
                MediaFormat.Mp3,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow);

            await first.UpsertAsync(entry);

            var second = new JsonMediaIndex(store, paths);
            var loaded = await second.FindByVideoIdAsync("ABC123");

            Assert.Equal(entry, loaded);
            Assert.Single(second.Entries);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    [Fact]
    public async Task RemoveDeletesEntryFromPersistentIndex()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "MediaForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var store = new AtomicJsonStore();
            var paths = new LocalAppPathsForTest(tempRoot);
            var index = new JsonMediaIndex(store, paths);
            await index.UpsertAsync(new MediaIndexEntry(
                "abc123", "https://youtube.test/abc123", Path.Combine(tempRoot, "song.mp3"),
                MediaFormat.Mp3, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

            await index.RemoveAsync("abc123");

            var reloaded = new JsonMediaIndex(store, paths);
            Assert.Null(await reloaded.FindByVideoIdAsync("abc123"));
            Assert.Empty(reloaded.Entries);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private sealed class LocalAppPathsForTest : LocalAppPaths
    {
        public LocalAppPathsForTest(string root) => Root = root;
        public string Root { get; }
        public override string AppDirectory => Root;
    }
}
