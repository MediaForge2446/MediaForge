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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var results = new List<PendingChange>(changes.Count);

        foreach (var change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ApplyAsync(change, cancellationToken).ConfigureAwait(false);
                results.Add(change with
                {
                    Status = ChangeStatus.Synced,
                    ErrorMessage = null
                });
                _logging.Info($"Committed change {change.Id} ({change.Type}).");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                results.Add(change with
                {
                    Status = ChangeStatus.Failed,
                    ErrorMessage = exception.Message
                });
                _logging.Error($"Failed to commit change {change.Id} ({change.Type}).", exception);
            }
        }

        return results;
    }

    private async Task ApplyAsync(PendingChange change, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(change.SourcePath))
        {
            throw new InvalidOperationException("A source path is required.");
        }

        switch (change.Type)
        {
            case ChangeType.CreateFolder:
                _fileSystem.CreateDirectory(change.SourcePath);
                break;

            case ChangeType.Delete:
                _fileSystem.Delete(change.SourcePath);
                break;

            case ChangeType.Rename:
            case ChangeType.Move:
                if (string.IsNullOrWhiteSpace(change.TargetPath))
                {
                    throw new InvalidOperationException("A target path is required.");
                }

                _fileSystem.Move(change.SourcePath, change.TargetPath);
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

                await _downloadService.DownloadAsync(downloadTask, cancellationToken: cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(change), change.Type, "Unsupported change type.");
        }
    }

    private static MediaFormat GetFormat(string targetPath)
    {
        return string.Equals(Path.GetExtension(targetPath), ".mp4", StringComparison.OrdinalIgnoreCase)
            ? MediaFormat.Mp4
            : MediaFormat.Mp3;
    }
}
