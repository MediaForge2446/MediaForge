using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Services;

namespace MediaForge.IntegrationTests;

public sealed class CommitServiceTests
{
    [Fact]
    public async Task CommitAsync_ReportsProgressAndMarksSuccessfulDownloadSynced()
    {
        var downloadService = new FakeDownloadService(success: true);
        var service = new CommitService(
            new FakeFileSystemService(),
            downloadService,
            new FakeLoggingService());

        var change = CreateDownloadChange();
        var progressEvents = new List<CommitProgress>();

        var result = await service.CommitAsync(
            new[] { change },
            new Progress<CommitProgress>(progressEvents.Add));

        var committed = Assert.Single(result);
        Assert.Equal(ChangeStatus.Synced, committed.Status);
        Assert.Null(committed.ErrorMessage);
        Assert.Contains(progressEvents, progress => progress.Progress >= 1d);
        Assert.True(downloadService.Called);
    }

    [Fact]
    public async Task CommitAsync_ConvertsDownloadFailureToFailedChange()
    {
        var expected = new InvalidOperationException("yt-dlp failed");
        var downloadService = new FakeDownloadService(success: false, exception: expected);
        var service = new CommitService(
            new FakeFileSystemService(),
            downloadService,
            new FakeLoggingService());

        var change = CreateDownloadChange();
        var progressEvents = new List<CommitProgress>();

        var result = await service.CommitAsync(
            new[] { change },
            new Progress<CommitProgress>(progressEvents.Add));

        var failed = Assert.Single(result);
        Assert.Equal(ChangeStatus.Failed, failed.Status);
        Assert.Equal(expected.Message, failed.ErrorMessage);
        Assert.Contains(progressEvents, progress => progress.Error?.Message == expected.Message);
    }

    private static PendingChange CreateDownloadChange() =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = ChangeType.Download,
            Status = ChangeStatus.Pending,
            SourcePath = "https://example.com/media",
            TargetPath = Path.Combine(Path.GetTempPath(), "MediaForgeTests", "sample.mp3"),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

    private sealed class FakeDownloadService : IDownloadService
    {
        private readonly bool _success;
        private readonly Exception? _exception;

        public bool Called { get; private set; }

        public FakeDownloadService(bool success, Exception? exception = null)
        {
            _success = success;
            _exception = exception;
        }

        public Task<string> DownloadAsync(
            DownloadTask task,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new DownloadProgress
            {
                DownloadId = task.Id,
                Progress = 0.5d,
                Attempt = 1,
                MaxAttempts = 3,
                Message = "Downloading"
            });

            if (!_success)
            {
                throw _exception ?? new InvalidOperationException("Download failed");
            }

            progress?.Report(new DownloadProgress
            {
                DownloadId = task.Id,
                Progress = 1d,
                Attempt = 1,
                MaxAttempts = 3,
                Message = "Download completed"
            });

            return Task.FromResult(task.TargetPath);
        }
    }

    private sealed class FakeFileSystemService : IFileSystemService
    {
        public IReadOnlyList<FileItem> GetDirectoryItems(string directoryPath) => Array.Empty<FileItem>();
        public bool DirectoryExists(string directoryPath) => false;
        public bool FileExists(string filePath) => false;
        public void CreateDirectory(string directoryPath) { }
        public void Delete(string path) { }
        public void Move(string sourcePath, string destinationPath) { }
        public void Rename(string sourcePath, string destinationPath) { }
    }

    private sealed class FakeLoggingService : ILoggingService
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
