using MediaForge.Application.Abstractions;
using MediaForge.Application.Downloads;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Commit;

public sealed class CommitEngine : ICommitEngine
{
    private readonly IFileSystem _fileSystem;
    private readonly IMediaDownloader _mediaDownloader;
    private readonly IStagingService _staging;
    private readonly DownloadQueue _downloadQueue;

    public CommitEngine(
        IFileSystem fileSystem,
        IMediaDownloader mediaDownloader,
        IStagingService staging,
        DownloadQueue? downloadQueue = null)
    {
        _fileSystem = fileSystem;
        _mediaDownloader = mediaDownloader;
        _staging = staging;
        _downloadQueue = downloadQueue ?? new DownloadQueue();
    }

    public async Task<CommitResult> CommitAsync(
        IReadOnlyCollection<StagingOperation> operations,
        CancellationToken cancellationToken = default)
        => await CommitAsync(operations, progress: null, cancellationToken).ConfigureAwait(false);

    public async Task<CommitResult> CommitAsync(
        IReadOnlyCollection<StagingOperation> operations,
        IProgress<CommitProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0)
            return new CommitResult([]);

        var results = new List<CommitItemResult>(operations.Count);
        var downloadBatch = new List<StagingOperation>();

        foreach (var operation in operations.OrderBy(x => x.CreatedAt))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (operation.OperationType == OperationType.Download)
            {
                downloadBatch.Add(operation);
                continue;
            }

            if (downloadBatch.Count > 0)
            {
                results.AddRange(await CommitDownloadsAsync(downloadBatch, progress, cancellationToken).ConfigureAwait(false));
                downloadBatch.Clear();
            }

            results.Add(await CommitFileOperationAsync(operation, progress, cancellationToken).ConfigureAwait(false));
        }

        if (downloadBatch.Count > 0)
            results.AddRange(await CommitDownloadsAsync(downloadBatch, progress, cancellationToken).ConfigureAwait(false));

        return new CommitResult(results);
    }

    private async Task<IReadOnlyList<CommitItemResult>> CommitDownloadsAsync(
        IReadOnlyList<StagingOperation> operations,
        IProgress<CommitProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = await _downloadQueue.ExecuteAsync(
            operations,
            _mediaDownloader,
            progress,
            cancellationToken).ConfigureAwait(false);

        foreach (var result in results.Where(x => x.Success))
        {
            var operation = operations.First(x => x.OperationId == result.OperationId);
            var outputPath = operation.Payload?.DestinationPath;
            if (string.IsNullOrWhiteSpace(outputPath) ||
                !await _fileSystem.FileExistsAsync(outputPath, cancellationToken).ConfigureAwait(false))
            {
                results = results.Select(x => x.OperationId == result.OperationId
                    ? new CommitItemResult(x.OperationId, false, "Download completed but the output file was not found on disk.")
                    : x).ToArray();
                continue;
            }

            await _staging.CompleteAsync(result.OperationId, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    private async Task<CommitItemResult> CommitFileOperationAsync(
        StagingOperation operation,
        IProgress<CommitProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report(new CommitProgress(operation.OperationId, 0, operation.OperationType.ToString()));
            var payload = operation.Payload ?? throw new InvalidOperationException("Operation payload is missing.");

            switch (operation.OperationType)
            {
                case OperationType.CreateDirectory:
                    var directory = payload.DirectoryPath ?? throw new InvalidOperationException("Directory path is missing.");
                    await _fileSystem.CreateDirectoryAsync(directory, cancellationToken).ConfigureAwait(false);
                    if (!await _fileSystem.DirectoryExistsAsync(directory, cancellationToken).ConfigureAwait(false))
                        throw new IOException("The directory was not created successfully.");
                    break;

                case OperationType.Delete:
                    var target = payload.SourcePath ?? throw new InvalidOperationException("Delete target path is missing.");
                    await _fileSystem.DeleteAsync(target, payload.Recursive, cancellationToken).ConfigureAwait(false);
                    if (await _fileSystem.FileExistsAsync(target, cancellationToken).ConfigureAwait(false) ||
                        await _fileSystem.DirectoryExistsAsync(target, cancellationToken).ConfigureAwait(false))
                        throw new IOException("The target still exists after deletion.");
                    break;

                case OperationType.Rename:
                    var renamePath = payload.SourcePath ?? throw new InvalidOperationException("Rename source path is missing.");
                    var newName = payload.NewName ?? throw new InvalidOperationException("New name is missing.");
                    await _fileSystem.RenameAsync(renamePath, newName, cancellationToken).ConfigureAwait(false);
                    break;

                case OperationType.Move:
                    var source = payload.SourcePath ?? throw new InvalidOperationException("Move source path is missing.");
                    var destination = payload.DestinationPath ?? throw new InvalidOperationException("Move destination path is missing.");
                    await _fileSystem.MoveAsync(source, destination, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new NotSupportedException($"Operation type '{operation.OperationType}' is not supported by the commit engine.");
            }

            progress?.Report(new CommitProgress(operation.OperationId, 100, "Completed", true));
            await _staging.CompleteAsync(operation.OperationId, cancellationToken).ConfigureAwait(false);
            return new CommitItemResult(operation.OperationId, true);
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new CommitProgress(operation.OperationId, 0, "Cancelled", true));
            throw;
        }
        catch (Exception ex)
        {
            progress?.Report(new CommitProgress(operation.OperationId, 0, ex.Message, true));
            return new CommitItemResult(operation.OperationId, false, ex.Message);
        }
    }
}
