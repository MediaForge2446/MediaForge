using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Services;

public sealed class CommitService : ICommitService
{
    private readonly IFileSystemService _fileSystem;
    private readonly IDownloadService _downloadService;
    private readonly ILoggingService _logging;

    public CommitService(
        IFileSystemService fileSystem,
        IDownloadService downloadService,
        ILoggingService logging)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _logging = logging ?? throw new ArgumentNullException(nameof(logging));
    }

    public async Task<IReadOnlyList<PendingChange>> CommitAsync(
        IReadOnlyList<PendingChange> changes,
        IProgress<CommitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var activeChanges = changes
            .Where(change => change.Status == ChangeStatus.Pending)
            .ToArray();

        if (activeChanges.Length == 0)
        {
            return changes.ToArray();
        }

        var resultMap = changes.ToDictionary(change => change.Id);
        var completedCount = 0;

        foreach (var change in activeChanges)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ApplyAsync(
                    change,
                    activeChanges.Length,
                    completedCount,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                resultMap[change.Id] = change with
                {
                    Status = ChangeStatus.Synced,
                    ErrorMessage = null
                };

                completedCount++;
                progress?.Report(new CommitProgress
                {
                    ChangeId = change.Id,
                    Progress = 1d,
                    CompletedCount = completedCount,
                    TotalCount = activeChanges.Length,
                    Message = "Saved successfully"
                });

                _logging.Info($"Committed change {change.Id} ({change.Type}).");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                resultMap[change.Id] = change with
                {
                    Status = ChangeStatus.Failed,
                    ErrorMessage = exception.Message
                };

                progress?.Report(new CommitProgress
                {
                    ChangeId = change.Id,
                    Progress = 0d,
                    CompletedCount = completedCount,
                    TotalCount = activeChanges.Length,
                    Message = "Save failed",
                    Error = exception
                });

                _logging.Error($"Failed to commit change {change.Id} ({change.Type}).", exception);
            }
        }

        return changes
            .Select(change => resultMap.TryGetValue(change.Id, out var result) ? result : change)
            .ToArray();
    }

    private async Task ApplyAsync(
        PendingChange change,
        int totalCount,
        int completedCount,
        IProgress<CommitProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(change.SourcePath))
        {
            throw new InvalidOperationException("A source path is required.");
        }

        switch (change.Type)
        {
            case ChangeType.CreateFolder:
                await Task.Run(
                    () => _fileSystem.CreateDirectory(change.SourcePath),
                    cancellationToken).ConfigureAwait(false);
                break;

            case ChangeType.Delete:
                await Task.Run(
                    () => _fileSystem.Delete(change.SourcePath),
                    cancellationToken).ConfigureAwait(false);
                break;

            case ChangeType.Rename:
            case ChangeType.Move:
                if (string.IsNullOrWhiteSpace(change.TargetPath))
                {
                    throw new InvalidOperationException("A target path is required.");
                }

                await Task.Run(
                    () => _fileSystem.Move(change.SourcePath, change.TargetPath),
                    cancellationToken).ConfigureAwait(false);
                break;

            case ChangeType.Download:
                if (string.IsNullOrWhiteSpace(change.TargetPath))
                {
                    throw new InvalidOperationException("A target path is required for downloads.");
                }

                var downloadTask = new DownloadTask
                {
                    Id = change.Id,
                    SourceUrl = change.SourcePath,
                    TargetPath = change.TargetPath,
                    Format = GetFormat(change.TargetPath)
                };

                var downloadProgress = new Progress<DownloadProgress>(state =>
                {
                    var normalized = Math.Clamp(state.Progress, 0d, 1d);
                    progress?.Report(new CommitProgress
                    {
                        ChangeId = change.Id,
                        Progress = normalized,
                        CompletedCount = completedCount,
                        TotalCount = totalCount,
                        Message = state.Message,
                        IsRetrying = state.IsRetrying,
                        RetryAttempt = state.Attempt,
                        Error = state.Error
                    });
                });

                await _downloadService.DownloadAsync(
                    downloadTask,
                    downloadProgress,
                    cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(change), change.Type, "Unsupported change type.");
        }
    }

    private static MediaFormat GetFormat(string targetPath) =>
        string.Equals(Path.GetExtension(targetPath), ".mp4", StringComparison.OrdinalIgnoreCase)
            ? MediaFormat.Mp4
            : MediaFormat.Mp3;
}
