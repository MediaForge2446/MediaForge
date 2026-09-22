using MediaForge.Application.Commit;
using MediaForge.Application.Downloads;
using MediaForge.Application.Staging;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Core.State;

namespace MediaForge.Application.Tests;

public sealed class WorkflowCoverageTests
{
    [Fact]
    public void MediaState_ContainsEveryProductState()
    {
        var expected = new[]
        {
            MediaState.Synced, MediaState.Pending, MediaState.Downloading, MediaState.Converting,
            MediaState.Verifying, MediaState.Completed, MediaState.Failed, MediaState.Cancelled,
            MediaState.Missing, MediaState.Conflict, MediaState.Interrupted, MediaState.Recovering
        };

        Assert.Equal(expected, Enum.GetValues<MediaState>());
    }

    [Theory]
    [InlineData(MediaFormat.Mp3, ".mp3")]
    [InlineData(MediaFormat.Mp4, ".mp4")]
    [InlineData(MediaFormat.Wav, ".wav")]
    [InlineData(MediaFormat.M4a, ".m4a")]
    public void MediaImport_MapsEverySupportedFormat(MediaFormat format, string extension)
        => Assert.Equal(extension, MediaImportService.GetExtension(format));

    [Fact]
    public async Task Staging_UndoAndClearNeverTouchDisk()
    {
        var repository = new InMemoryStagingRepository();
        var staging = new StagingService(repository, new StagingHistory());
        var disk = new InMemoryFileSystem();
        await staging.InitializeAsync();

        var operation = Download("C:\\Media\\song.mp3", MediaFormat.Mp3);
        await staging.StageAsync(operation);

        Assert.Single(staging.Operations);
        Assert.False(await disk.FileExistsAsync("C:\\Media\\song.mp3"));

        Assert.True(await staging.UndoAsync(operation.OperationId));
        Assert.Empty(staging.Operations);

        await staging.StageAsync(operation);
        await staging.ClearAsync();
        Assert.Empty(staging.Operations);
        Assert.False(await disk.FileExistsAsync("C:\\Media\\song.mp3"));
    }

    [Fact]
    public async Task CommitEngine_ExecutesEveryFileOperation()
    {
        var fileSystem = new InMemoryFileSystem();
        var staging = new StagingService(new InMemoryStagingRepository(), new StagingHistory());
        await staging.InitializeAsync();
        await fileSystem.CreateDirectoryAsync("C:\\Music");
        fileSystem.AddFile("C:\\Music\\old.mp3");

        var create = new StagingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow, OperationType.CreateDirectory,
            "C:\\Music\\New", nameof(MediaState.Missing), nameof(MediaState.Pending),
            new StagingPayload(DirectoryPath: "C:\\Music\\New"));
        var rename = new StagingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(1), OperationType.Rename,
            "C:\\Music\\old.mp3", nameof(MediaState.Synced), nameof(MediaState.Pending),
            new StagingPayload(SourcePath: "C:\\Music\\old.mp3", NewName: "renamed.mp3"));
        var move = new StagingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(2), OperationType.Move,
            "C:\\Music\\renamed.mp3", nameof(MediaState.Synced), nameof(MediaState.Pending),
            new StagingPayload(SourcePath: "C:\\Music\\renamed.mp3", DestinationPath: "C:\\Music\\New\\renamed.mp3"));
        var delete = new StagingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(3), OperationType.Delete,
            "C:\\Music\\New\\renamed.mp3", nameof(MediaState.Synced), nameof(MediaState.Pending),
            new StagingPayload(SourcePath: "C:\\Music\\New\\renamed.mp3"));

        var engine = new CommitEngine(fileSystem, new FakeMediaDownloader(fileSystem), staging, new DownloadQueue(1));
        var result = await engine.CommitAsync([create, rename, move, delete]);

        Assert.Equal(4, result.Items.Count);
        Assert.All(result.Items, item => Assert.True(item.Success, item.Error));
        Assert.True(await fileSystem.DirectoryExistsAsync("C:\\Music\\New"));
        Assert.False(await fileSystem.FileExistsAsync("C:\\Music\\New\\renamed.mp3"));
        Assert.Empty(staging.Operations);
    }

    [Fact]
    public async Task CommitEngine_FailedDownloadRemainsAvailableForRetry()
    {
        var fileSystem = new InMemoryFileSystem();
        var staging = new StagingService(new InMemoryStagingRepository(), new StagingHistory());
        await staging.InitializeAsync();
        var operation = Download("C:\\Music\\failed.mp3", MediaFormat.Mp3);
        await staging.StageAsync(operation);

        var engine = new CommitEngine(fileSystem, new FailingDownloader(), staging, new DownloadQueue(1));
        var result = await engine.CommitAsync([operation]);

        Assert.False(result.Success);
        Assert.Single(staging.Operations);
        Assert.Equal(operation.OperationId, staging.Operations[0].OperationId);
    }

    [Fact]
    public async Task DownloadQueue_RetriesTransientFailures()
    {
        var fileSystem = new InMemoryFileSystem();
        var staging = new StagingService(new InMemoryStagingRepository(), new StagingHistory());
        await staging.InitializeAsync();

        var operation = Download("C:\\Music\\retry.mp3", MediaFormat.Mp3);
        var downloader = new FailOnceDownloader(fileSystem);
        var queue = new DownloadQueue(maxConcurrency: 1, maxRetries: 1);

        var result = await queue.ExecuteAsync(
            [operation],
            downloader,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Single().Success, result.Single().Error);
        Assert.Equal(2, downloader.Attempts);
        Assert.True(await fileSystem.FileExistsAsync(operation.Payload!.DestinationPath!));
    }

    [Fact]
    [Fact]
    public async Task CommitEngine_CancellationStopsBeforeNextOperation()
    {
        var fileSystem = new InMemoryFileSystem();
        var staging = new StagingService(new InMemoryStagingRepository(), new StagingHistory());
        await staging.InitializeAsync();
        var first = Download("C:\\Music\\one.mp3", MediaFormat.Mp3);
        var second = Download("C:\\Music\\two.mp3", MediaFormat.Mp3);
        await staging.StageAsync(first);
        await staging.StageAsync(second);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var engine = new CommitEngine(fileSystem, new FakeMediaDownloader(fileSystem), staging, new DownloadQueue(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.CommitAsync([first, second], cts.Token));
        Assert.Equal(2, staging.Operations.Count);
    }

    private static StagingOperation Download(string destination, MediaFormat format) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, OperationType.Download, destination,
            nameof(MediaState.Missing), nameof(MediaState.Pending),
            new StagingPayload(SourceUrl: "https://youtube.example/test", DestinationPath: destination, DesiredFormat: format, VideoId: Guid.NewGuid().ToString("N")));

    private sealed class FailOnceDownloader : IMediaDownloader
    {
        private readonly InMemoryFileSystem _fileSystem;

        public FailOnceDownloader(InMemoryFileSystem fileSystem) => _fileSystem = fileSystem;

        public int Attempts { get; private set; }

        public Task DownloadAsync(
            string sourceUrl,
            string outputPath,
            MediaFormat format,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts == 1)
                return Task.FromException(new InvalidOperationException("Transient failure"));

            return CompleteAsync(outputPath, progress, cancellationToken);
        }

        private async Task CompleteAsync(
            string outputPath,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            await _fileSystem.CreateDirectoryAsync(Path.GetDirectoryName(outputPath) ?? "C:\\Music", cancellationToken);
            _fileSystem.AddFile(outputPath);
            progress?.Report(new DownloadProgress(100, "Completed"));
        }
    }

    private sealed class FailingDownloader : IMediaDownloader
    {
        public Task DownloadAsync(string sourceUrl, string outputPath, MediaFormat format, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("Synthetic download failure"));
    }
}
