using MediaForge.Application.Downloads;
using MediaForge.Application.Staging;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Core.State;

namespace MediaForge.Application.Tests;

public sealed class MediaImportServiceTests
{
    [Fact]
    public async Task StageDownloadsAsync_DoesNotTouchDisk()
    {
        var repository = new InMemoryStagingRepository();
        var staging = new StagingService(repository, new StagingHistory());
        var fileSystem = new InMemoryFileSystem();
        var resolver = new FakeResolver();
        var service = new MediaImportService(resolver, staging);

        await staging.InitializeAsync();
        var result = await service.ResolveAsync("https://www.youtube.com/watch?v=abc");
        var staged = await service.StageDownloadsAsync(result.Items, @"C:\Media", MediaFormat.Mp3);

        Assert.Single(staged);
        Assert.False(await fileSystem.DirectoryExistsAsync(@"C:\Media"));
        Assert.Single(staging.Operations);
        Assert.Equal(MediaFormat.Mp3, staging.Operations[0].Payload?.DesiredFormat);
    }

    [Fact]
    public async Task StageDownloadsAsync_SanitizesNamesAndMakesDuplicatesUnique()
    {
        var repository = new InMemoryStagingRepository();
        var staging = new StagingService(repository, new StagingHistory());
        var service = new MediaImportService(new FakeResolver(
        [
            new ResolvedMediaItem("one", "https://youtube.test/one", new MediaMetadata("A:B")),
            new ResolvedMediaItem("two", "https://youtube.test/two", new MediaMetadata("A:B"))
        ]), staging);

        var result = await service.ResolveAsync("https://example.test/playlist");
        var staged = await service.StageDownloadsAsync(result.Items, @"C:\Media", MediaFormat.Mp3);

        Assert.Equal(2, staged.Count);
        Assert.EndsWith("A_B.mp3", staged[0].Payload!.DestinationPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("A_B (2).mp3", staged[1].Payload!.DestinationPath, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeResolver : IMediaMetadataResolver
    {
        private readonly IReadOnlyList<ResolvedMediaItem> _items;

        public FakeResolver(IReadOnlyList<ResolvedMediaItem>? items = null)
        {
            _items = items ??
            [new ResolvedMediaItem("abc", "https://youtube.test/abc", new MediaMetadata("Test song"))];
        }

        public Task<MediaResolveResult> ResolveAsync(string sourceUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(new MediaResolveResult(false, null, _items));
    }
}
