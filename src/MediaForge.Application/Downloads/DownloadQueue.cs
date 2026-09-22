using System.Collections.Concurrent;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Downloads;

/// <summary>
/// Smart bounded-concurrency download executor with per-item pause/resume/cancel controls,
/// retry/backoff, stable priority ordering, and cooperative cancellation.
/// </summary>
public sealed class DownloadQueue
{
    private sealed class Control
    {
        public readonly object Gate = new();
        public bool Paused;
        public bool Cancelled;
        public CancellationTokenSource? ActiveCancellation;
        public TaskCompletionSource<bool> ResumeSignal =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly int _maxConcurrency;
    private readonly int _maxRetries;
    private readonly ConcurrentDictionary<Guid, Control> _controls = new();
    private readonly ConcurrentDictionary<Guid, int> _priorities = new();
    private readonly ConcurrentDictionary<Guid, double> _lastPercents = new();

    public DownloadQueue(int maxConcurrency = 2, int maxRetries = 2)
    {
        if (maxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries));

        _maxConcurrency = maxConcurrency;
        _maxRetries = maxRetries;
    }

    public int MaxConcurrency => _maxConcurrency;
    public int MaxRetries => _maxRetries;

    public bool Pause(Guid operationId)
    {
        var control = GetControl(operationId);
        lock (control.Gate)
        {
            if (control.Cancelled)
                return false;

            control.Paused = true;
            control.ActiveCancellation?.Cancel();
            return true;
        }
    }

    public bool Resume(Guid operationId)
    {
        var control = GetControl(operationId);
        lock (control.Gate)
        {
            if (control.Cancelled)
                return false;

            control.Paused = false;
            control.ResumeSignal.TrySetResult(true);
            return true;
        }
    }

    public bool Cancel(Guid operationId)
    {
        var control = GetControl(operationId);
        lock (control.Gate)
        {
            control.Cancelled = true;
            control.Paused = false;
            control.ResumeSignal.TrySetResult(true);
            control.ActiveCancellation?.Cancel();
            return true;
        }
    }

    public void PrepareForRetry(Guid operationId)
    {
        var control = GetControl(operationId);
        lock (control.Gate)
        {
            control.Cancelled = false;
            control.Paused = false;
            control.ActiveCancellation?.Cancel();
            control.ResumeSignal.TrySetResult(true);
            control.ResumeSignal = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _lastPercents[operationId] = 0;
    }

    public void SetPriority(Guid operationId, int priority)
        => _priorities[operationId] = priority;

    public int GetPriority(Guid operationId)
        => _priorities.TryGetValue(operationId, out var priority) ? priority : 0;

    public async Task<IReadOnlyList<CommitItemResult>> ExecuteAsync(
        IReadOnlyList<StagingOperation> operations,
        IMediaDownloader downloader,
        IProgress<CommitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(downloader);

        using var semaphore = new SemaphoreSlim(_maxConcurrency);

        var ordered = operations
            .Select((operation, index) =>
            {
                GetControl(operation.OperationId);
                return (operation, priority: GetPriority(operation.OperationId), index);
            })
            .OrderByDescending(x => x.priority)
            .ThenBy(x => x.index)
            .Select(x => x.operation)
            .ToArray();

        var tasks = ordered.Select(operation =>
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
        var control = GetControl(operation.OperationId);

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
            await WaitIfPausedAsync(operation.OperationId, control, progress, cancellationToken).ConfigureAwait(false);

            if (IsCancelled(control))
            {
                progress?.Report(new CommitProgress(
                    operation.OperationId, 0, "Cancelled", true));
                return new CommitItemResult(operation.OperationId, false, "Cancelled by user.");
            }

            if (attempt > 0)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(8, Math.Pow(2, attempt - 1)));
                progress?.Report(new CommitProgress(
                    operation.OperationId,
                    CurrentPercent(operation.OperationId),
                    $"Retrying ({attempt}/{_maxRetries})"));

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                await WaitIfPausedAsync(operation.OperationId, control, progress, cancellationToken).ConfigureAwait(false);
            }

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var localCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                lock (control.Gate)
                {
                    if (control.Cancelled)
                    {
                        progress?.Report(new CommitProgress(
                            operation.OperationId, 0, "Cancelled", true));
                        return new CommitItemResult(operation.OperationId, false, "Cancelled by user.");
                    }

                    control.ActiveCancellation = localCancellation;
                }

                try
                {
                    progress?.Report(new CommitProgress(
                        operation.OperationId,
                        CurrentPercent(operation.OperationId),
                        "Downloading"));

                    var itemProgress = new Progress<DownloadProgress>(value =>
                    {
                        var percent = Math.Clamp(value.Percent, 0, 100);
                        _lastPercents[operation.OperationId] = percent;

                        progress?.Report(new CommitProgress(
                            operation.OperationId,
                            percent,
                            value.Status ?? "Downloading",
                            false,
                            value.SpeedBytesPerSecond,
                            value.Eta,
                            value.DownloadedBytes,
                            value.TotalBytes));
                    });

                    await downloader.DownloadAsync(
                        sourceUrl,
                        outputPath,
                        payload.DesiredFormat ?? MediaFormat.Mp3,
                        itemProgress,
                        localCancellation.Token,
                        payload.DesiredQuality ?? MediaQuality.Standard128K).ConfigureAwait(false);

                    _lastPercents[operation.OperationId] = 100;

                    progress?.Report(new CommitProgress(
                        operation.OperationId, 100, "Completed", true));

                    return new CommitItemResult(operation.OperationId, true);
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested &&
                    IsPaused(control))
                {
                    progress?.Report(new CommitProgress(
                        operation.OperationId,
                        CurrentPercent(operation.OperationId),
                        "Paused"));
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested &&
                    IsCancelled(control))
                {
                    progress?.Report(new CommitProgress(
                        operation.OperationId, 0, "Cancelled", true));

                    return new CommitItemResult(
                        operation.OperationId,
                        false,
                        "Cancelled by user.");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt == _maxRetries)
                    {
                        progress?.Report(new CommitProgress(
                            operation.OperationId,
                            CurrentPercent(operation.OperationId),
                            ex.Message,
                            true));
                        return new CommitItemResult(
                            operation.OperationId,
                            false,
                            ex.Message);
                    }
                }
                finally
                {
                    lock (control.Gate)
                    {
                        if (ReferenceEquals(control.ActiveCancellation, localCancellation))
                            control.ActiveCancellation = null;
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }

            if (IsPaused(control))
            {
                await WaitIfPausedAsync(
                    operation.OperationId,
                    control,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        var message = lastError?.Message ?? "Download failed.";
        progress?.Report(new CommitProgress(
            operation.OperationId,
            CurrentPercent(operation.OperationId),
            message,
            true));

        return new CommitItemResult(operation.OperationId, false, message);
    }

    private static bool IsPaused(Control control)
    {
        lock (control.Gate)
            return control.Paused && !control.Cancelled;
    }

    private static bool IsCancelled(Control control)
    {
        lock (control.Gate)
            return control.Cancelled;
    }

    private async Task WaitIfPausedAsync(
        Guid operationId,
        Control control,
        IProgress<CommitProgress>? progress,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            Task resumeTask;

            lock (control.Gate)
            {
                if (control.Cancelled || !control.Paused)
                    return;

                resumeTask = control.ResumeSignal.Task;
            }

            progress?.Report(new CommitProgress(
                operationId,
                CurrentPercent(operationId),
                "Paused"));

            await resumeTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private double CurrentPercent(Guid operationId)
        => _lastPercents.TryGetValue(operationId, out var value) ? value : 0;

    private Control GetControl(Guid operationId)
        => _controls.GetOrAdd(operationId, static _ => new Control());
}
