using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.State;
using MediaForge.ViewModels;

namespace MediaForge.Core.Tests;

public sealed class MediaDownloaderViewModelTests
{
    [Fact]
    public void AddSelectedToPendingChanges_UsesCurrentTargetFolderWithoutCreatingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForge-DownloaderTests", Guid.NewGuid().ToString("N"));
        var pending = new PendingChangesState();
        var history = new StagingHistory();
        var reported = new List<Exception>();
        var viewModel = new MediaDownloaderViewModel(
            new FakeResolver(),
            pending,
            history,
            reported.Add,
            () => root);

        viewModel.Items.Add(new MediaItem
        {
            Id = Guid.NewGuid().ToString("N"),
            SourceUrl = "https://example.com/song",
            Title = "Test Song",
            Format = MediaFormat.Mp3,
            IsSelected = true
        });

        var count = viewModel.AddSelectedToPendingChanges();

        Assert.Equal(1, count);
        var change = Assert.Single(pending.Changes);
        Assert.Equal(ChangeType.Download, change.Type);
        Assert.Equal(Path.Combine(root, "Test Song.mp3"), change.TargetPath);
        Assert.False(Directory.Exists(root));
        Assert.Empty(reported);
        Assert.Single(history.GetPendingChanges());
    }

    private sealed class FakeResolver : IMediaResolver
    {
        public Task<IReadOnlyList<MediaItem>> ResolveAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MediaItem>>(Array.Empty<MediaItem>());
    }
}
