using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Downloads;

public sealed class DownloadQueue
{
    private readonly int _maxConcurrency;

    public DownloadQueue(int maxConcurrency = 2)
    {
        if (maxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        _maxConcurrency = maxConcurrency;
    }

    public async Task<IReadOnlyList<CommitItemResult>> ExecuteAsync(
        IReadOnlyList<StagingOperation> operations,
        IMediaDownloader downloader,
        IProgress<CommitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(downloader);

        using var semaphore = new SemaphoreSlim(_maxConcurrency);
        var tasks = operations.Select(operation => ExecuteOneAsync(operation, downloader, semaphore, progress, cancellationToken));
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<CommitItemResult> ExecuteOneAsync(
        StagingOperation operation,
        IMediaDownloader downloader,
        SemaphoreSlim semaphore,
        IProgress<CommitProgress>? progress,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = operation.Payload
                ?? throw new InvalidOperationException("Download operation payload is missing.");
            var sourceUrl = payload.SourceUrl
                ?? throw new InvalidOperationException("Download operation source URL is missing.");
            var outputPath = payload.DestinationPath
                ?? throw new InvalidOperationException("Download operation destination path is missing.");

            progress?.Report(new CommitProgress(operation.OperationId, 0, "Downloading"));
            var itemProgress = new Progress<DownloadProgress>(value =>
                progress?.Report(new CommitProgress(operation.OperationId, Math.Clamp(value.Percent, 0, 100), value.Status ?? "Downloading")));

            await downloader.DownloadAsync(
                sourceUrl,
                outputPath,
                payload.DesiredFormat ?? MediaForge.Core.Enums.MediaFormat.Mp3,
                itemProgress,
                cancellationToken).ConfigureAwait(false);

            progress?.Report(new CommitProgress(operation.OperationId, 100, "Completed", true));
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
        finally
        {
            semaphore.Release();
        }
    }
}
