using MediaForge.Application.Abstractions;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Downloads;

/// <summary>
/// Executes staged download operations with bounded concurrency and automatic retry/backoff.
/// </summary>
public sealed class DownloadQueue
{
    private readonly int _maxConcurrency;
    private readonly int _maxRetries;

    public DownloadQueue(int maxConcurrency = 2, int maxRetries = 2)
    {
        if (maxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries));

        _maxConcurrency = maxConcurrency;
        _maxRetries = maxRetries;
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
        var tasks = operations
            .OrderBy(operation => operation.CreatedAt)
            .Select(operation =>
                ExecuteOneAsync(
                    operation,
                    downloader,
                    semaphore,
                    progress,
                    cancellationToken));

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<CommitItemResult> ExecuteOneAsync(
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

            Exception? lastError = null;

            for (var attempt = 0; attempt <= _maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt > 0)
                {
                    var delay = TimeSpan.FromSeconds(Math.Min(8, Math.Pow(2, attempt - 1)));
                    progress?.Report(new CommitProgress(
                        operation.OperationId,
                        0,
                        $"Retrying ({attempt}/{_maxRetries})"));

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    progress?.Report(new CommitProgress(
                        operation.OperationId,
                        0,
                        "Downloading"));

                    var itemProgress = new Progress<DownloadProgress>(value =>
                        progress?.Report(new CommitProgress(
                            operation.OperationId,
                            Math.Clamp(value.Percent, 0, 100),
                            value.Status ?? "Downloading",
                            false,
                            value.SpeedBytesPerSecond,
                            value.Eta)));

                    await downloader.DownloadAsync(
                        sourceUrl,
                        outputPath,
                        payload.DesiredFormat ?? MediaForge.Core.Enums.MediaFormat.Mp3,
                        itemProgress,
                        cancellationToken).ConfigureAwait(false);

                    progress?.Report(new CommitProgress(
                        operation.OperationId,
                        100,
                        "Completed",
                        true));

                    return new CommitItemResult(operation.OperationId, true);
                }
                catch (OperationCanceledException)
                {
                    progress?.Report(new CommitProgress(
                        operation.OperationId,
                        0,
                        "Cancelled",
                        true));
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt == _maxRetries)
                        break;
                }
            }

            var message = lastError?.Message ?? "Download failed.";
            progress?.Report(new CommitProgress(
                operation.OperationId,
                0,
                message,
                true));

            return new CommitItemResult(operation.OperationId, false, message);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
