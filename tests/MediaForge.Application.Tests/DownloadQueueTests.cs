using MediaForge.Application.Downloads;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Tests;

public sealed class DownloadQueueTests
{
    [Fact]
    public async Task ExecuteAsync_UsesPriorityOrder_WhenConcurrencyIsOne()
    {
        var fileSystem = new InMemoryFileSystem();
        var downloader = new OrderedDownloader(fileSystem);
        var queue = new DownloadQueue(maxConcurrency: 1, maxRetries: 0);

        var low = CreateOperation("low");
        var high = CreateOperation("high");

        queue.SetPriority(low.OperationId, 1);
        queue.SetPriority(high.OperationId, 10);

        var result = await queue.ExecuteAsync([low, high], downloader);

        Assert.All(result, item => Assert.True(item.Success));
        Assert.Equal(new[] { "high", "low" }, downloader.Order);
    }

    [Fact]
    public async Task PauseThenResume_ReusesTheSameOperation()
    {
        var fileSystem = new InMemoryFileSystem();
        var downloader = new PausableDownloader(fileSystem);
        var queue = new DownloadQueue(maxConcurrency: 1, maxRetries: 0);
        var operation = CreateOperation("resume");

        var execution = queue.ExecuteAsync([operation], downloader);

        await downloader.FirstCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(queue.Pause(operation.OperationId));
        await downloader.FirstCallCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(queue.Resume(operation.OperationId));

        var result = await execution.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(result);
        Assert.True(result[0].Success);
        Assert.Equal(2, downloader.CallCount);
    }

    private static StagingOperation CreateOperation(string name)
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(@"C:\MediaForge.Tests", name + ".mp3");

        return new StagingOperation(
            id,
            DateTimeOffset.UtcNow,
            OperationType.Download,
            path,
            nameof(MediaState.Missing),
            nameof(MediaState.Pending),
            new StagingPayload(
                SourceUrl: "https://example.invalid/watch?v=" + name,
                DestinationPath: path,
                DesiredFormat: MediaFormat.Mp3,
                DesiredQuality: MediaQuality.Standard128K,
                VideoId: name,
                Title: name));
    }

    private sealed class OrderedDownloader : IMediaDownloader
    {
        private readonly InMemoryFileSystem _fileSystem;
        public List<string> Order { get; } = [];

        public OrderedDownloader(InMemoryFileSystem fileSystem)
            => _fileSystem = fileSystem;

        public Task DownloadAsync(
            string sourceUrl,
            string outputPath,
            MediaFormat format,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default,
            MediaQuality quality = MediaQuality.Standard128K)
        {
            Order.Add(Path.GetFileNameWithoutExtension(outputPath));
            _fileSystem.AddFile(outputPath);
            progress?.Report(new DownloadProgress(100, "Completed"));
            return Task.CompletedTask;
        }
    }

    private sealed class PausableDownloader : IMediaDownloader
    {
        private readonly InMemoryFileSystem _fileSystem;

        public TaskCompletionSource<bool> FirstCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> FirstCallCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public PausableDownloader(InMemoryFileSystem fileSystem)
            => _fileSystem = fileSystem;

        public async Task DownloadAsync(
            string sourceUrl,
            string outputPath,
            MediaFormat format,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default,
            MediaQuality quality = MediaQuality.Standard128K)
        {
            CallCount++;

            if (CallCount == 1)
            {
                FirstCallStarted.TrySetResult(true);
                progress?.Report(new DownloadProgress(15, "Downloading"));

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    FirstCallCancelled.TrySetResult(true);
                    throw;
                }
            }

            _fileSystem.AddFile(outputPath);
            progress?.Report(new DownloadProgress(100, "Completed"));
        }
    }
}
