using MediaForge.Application.Library;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Tests;

public sealed class LibraryScanServiceTests
{
    [Fact]
    public async Task ScanCountsFilesFoldersMediaAndBytesWithoutMutating()
    {
        var root = new RootFolder
        {
            Path = @"C:\Music",
            Name = "Music"
        };
        var explorer = new FakeExplorer(new Dictionary<string, IReadOnlyList<ExplorerEntry>>(StringComparer.OrdinalIgnoreCase)
        {
            [root.Path] =
            [
                new("Albums", @"C:\Music\Albums", true, 0, DateTimeOffset.UtcNow),
                new("cover.jpg", @"C:\Music\cover.jpg", false, 100, DateTimeOffset.UtcNow)
            ],
            [@"C:\Music\Albums"] =
            [
                new("song.mp3", @"C:\Music\Albums\song.mp3", false, 2048, DateTimeOffset.UtcNow),
                new("notes.txt", @"C:\Music\Albums\notes.txt", false, 512, DateTimeOffset.UtcNow)
            ]
        });
        var service = new LibraryScanService(explorer);

        var result = await service.ScanAsync(root);

        Assert.True(result.Exists);
        Assert.Equal(1, result.FolderCount);
        Assert.Equal(3, result.FileCount);
        Assert.Equal(1, result.MediaCount);
        Assert.Equal(2660, result.TotalBytes);
        Assert.Equal(2, explorer.ListCalls.Count);
    }

    [Fact]
    public async Task ScanReportsMissingRoot()
    {
        var root = new RootFolder
        {
            Path = @"Z:\Missing",
            Name = "Missing"
        };
        var service = new LibraryScanService(new MissingExplorer());

        var result = await service.ScanAsync(root);

        Assert.False(result.Exists);
        Assert.Equal(0, result.FileCount);
        Assert.Equal(0, result.FolderCount);
    }

    private sealed class FakeExplorer(IReadOnlyDictionary<string, IReadOnlyList<ExplorerEntry>> entries) : IExplorerService
    {
        public List<string> ListCalls { get; } = [];

        public Task<IReadOnlyList<ExplorerEntry>> ListAsync(string directoryPath, CancellationToken cancellationToken = default)
        {
            ListCalls.Add(directoryPath);
            return Task.FromResult(entries.TryGetValue(directoryPath, out var result) ? result : Array.Empty<ExplorerEntry>());
        }
    }

    private sealed class MissingExplorer : IExplorerService
    {
        public Task<IReadOnlyList<ExplorerEntry>> ListAsync(string directoryPath, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<ExplorerEntry>>(new DirectoryNotFoundException(directoryPath));
    }
}
