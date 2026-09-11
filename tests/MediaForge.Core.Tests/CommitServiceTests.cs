using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Services;

namespace MediaForge.Core.Tests;

public sealed class CommitServiceTests
{
    [Fact]
    public async Task CommitAsync_ProcessesCreateFolderBeforeDownloadAndDeleteLast()
    {
        var operations = new List<string>();
        var fileSystem = new RecordingFileSystemService(operations);
        var downloads = new RecordingDownloadService(operations);
        var logging = new RecordingLoggingService();
        var service = new CommitService(fileSystem, downloads, logging);

        var create = new PendingChange
        {
            Id = Guid.NewGuid(),
            Type = ChangeType.CreateFolder,
            Status = ChangeStatus.Pending,
            SourcePath = "C:\\Media\\New",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        var download = new PendingChange
        {
            Id = Guid.NewGuid(),
            Type = ChangeType.Download,
            Status = ChangeStatus.Pending,
            SourcePath = "https://example.com/song",
            TargetPath = "C:\\Media\\New\\song.mp3",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(3)
        };
        var delete = new PendingChange
        {
            Id = Guid.NewGuid(),
            Type = ChangeType.Delete,
            Status = ChangeStatus.Pending,
            SourcePath = "C:\\Media\\old.mp3",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(2)
        };

        var results = await service.CommitAsync(new[] { download, delete, create });

        Assert.Equal(ChangeStatus.Synced, results.Single(change => change.Id == create.Id).Status);
        Assert.Equal(ChangeStatus.Synced, results.Single(change => change.Id == download.Id).Status);
        Assert.Equal(ChangeStatus.Synced, results.Single(change => change.Id == delete.Id).Status);
        Assert.Equal(
            new[] { "create:C:\\Media\\New", "download:C:\\Media\\New\\song.mp3", "delete:C:\\Media\\old.mp3" },
            operations);
    }

    private sealed class RecordingFileSystemService(List<string> operations) : IFileSystemService
    {
        public IReadOnlyList<FileItem> GetDirectoryItems(string directoryPath) => Array.Empty<FileItem>();
        public bool DirectoryExists(string directoryPath) => true;
        public bool FileExists(string filePath) => false;
        public void CreateDirectory(string directoryPath) => operations.Add($"create:{directoryPath}");
        public void Delete(string path) => operations.Add($"delete:{path}");
        public void Move(string sourcePath, string destinationPath) => operations.Add($"move:{sourcePath}->{destinationPath}");
        public void Rename(string sourcePath, string destinationPath) => operations.Add($"rename:{sourcePath}->{destinationPath}");
    }

    private sealed class RecordingDownloadService(List<string> operations) : IDownloadService
    {
        public Task<string> DownloadAsync(
            DownloadTask task,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            operations.Add($"download:{task.TargetPath}");
            progress?.Report(new DownloadProgress
            {
                DownloadId = task.Id,
                Progress = 1d,
                Attempt = 1,
                MaxAttempts = 1,
                Message = "Download completed"
            });
            return Task.FromResult(task.TargetPath);
        }
    }

    private sealed class RecordingLoggingService : ILoggingService
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
