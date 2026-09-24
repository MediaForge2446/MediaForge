using MediaForge.Application.Downloads;
using MediaForge.Application.Staging;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Core.State;

namespace MediaForge.Application.Tests;

public sealed class MediaImportPerTrackOptionsTests
{
    [Fact]
    public async Task StageDownloadsAsync_PreservesPerTrackFormatAndQuality()
    {
        var repository = new InMemoryStagingRepository();
        var staging = new StagingService(repository, new StagingHistory());
        await staging.InitializeAsync();

        var items = new[]
        {
            new ResolvedMediaItem(
                "mp3-track",
                "https://youtube.test/mp3-track",
                new MediaMetadata("Track A"),
                MediaFormat.Mp3,
                MediaQuality.High192K),
            new ResolvedMediaItem(
                "mp4-track",
                "https://youtube.test/mp4-track",
                new MediaMetadata("Track B"),
                MediaFormat.Mp4,
                MediaQuality.VeryHigh256K)
        };

        var service = new MediaImportService(new FakeResolver(items), staging);
        var staged = await service.StageDownloadsAsync(
            items,
            @"C:\Media",
            MediaFormat.Mp3,
            MediaQuality.Maximum320K);

        Assert.Equal(2, staged.Count);
        Assert.Equal(MediaFormat.Mp3, staged[0].Payload?.DesiredFormat);
        Assert.Equal(MediaQuality.High192K, staged[0].Payload?.DesiredQuality);
        Assert.Equal(MediaFormat.Mp4, staged[1].Payload?.DesiredFormat);
        Assert.Equal(MediaQuality.VeryHigh256K, staged[1].Payload?.DesiredQuality);
    }

    [Fact]
    public async Task StageDownloadsAsync_UsesSmart192KFallback_WhenItemQualityIsDefaultValue()
    {
        var repository = new InMemoryStagingRepository();
        var staging = new StagingService(repository, new StagingHistory());
        await staging.InitializeAsync();

        var item = new ResolvedMediaItem(
            "default-quality",
            "https://youtube.test/default-quality",
            new MediaMetadata("Track"));

        var service = new MediaImportService(new FakeResolver([item]), staging);
        var staged = await service.StageDownloadsAsync(
            [item],
            @"C:\Media",
            MediaFormat.Mp3);

        Assert.Single(staged);
        Assert.Equal(MediaQuality.High192K, staged[0].Payload?.DesiredQuality);
    }

    private sealed class FakeResolver : IMediaMetadataResolver
    {
        private readonly IReadOnlyList<ResolvedMediaItem> _items;

        public FakeResolver(IReadOnlyList<ResolvedMediaItem> items)
        {
            _items = items;
        }

        public Task<MediaResolveResult> ResolveAsync(
            string sourceUrl,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MediaResolveResult(
                _items.Count > 1,
                _items.Count > 1 ? "Test playlist" : null,
                _items));
    }
}
