using DownTrack.Application.Services;

namespace DownTrack.Application.Tests;

public sealed class InMemoryLibraryCatalogTests
{
    [Fact]
    public async Task AddRoot_adds_a_real_folder_once()
    {
        var path = Directory.CreateTempSubdirectory("DownTrack-").FullName;

        try
        {
            var catalog = new InMemoryLibraryCatalog();

            var root = await catalog.AddRootAsync(path);

            Assert.Single(catalog.Roots);
            Assert.Equal(root.Path, catalog.Roots[0].Path);
            await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.AddRootAsync(path));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
