using MediaForge.Infrastructure.Persistence;
using MediaForge.Core.Models;

namespace MediaForge.Infrastructure.Tests;

public sealed class AtomicJsonStoreTests
{
    [Fact]
    public async Task SaveThenLoad_RoundTripsLibrary()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForgeTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "library.json");

        try
        {
            var store = new AtomicJsonStore();
            var library = new Library
            {
                RootFolders =
                [
                    new RootFolder { Path = "C:\\Music", Name = "Music" }
                ]
            };

            await store.SaveAsync(path, library);
            var loaded = await store.LoadAsync<Library>(path);

            Assert.NotNull(loaded);
            var folder = Assert.Single(loaded!.RootFolders);
            Assert.Equal("C:\\Music", folder.Path);
            Assert.Equal("Music", folder.Name);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
