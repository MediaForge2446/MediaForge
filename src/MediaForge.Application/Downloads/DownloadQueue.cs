using MediaForge.Application.Abstractions;
using System.Collections.Concurrent;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Downloads;

/// <summary>
/// Production download scheduler with bounded concurrency, explicit state transitions,
/// cooperative pause/resume/cancel, exponential retry and stable priority ordering.
/// </summary>
public sealed class DownloadQueue
{
    private enum ExecutionState
    {
        Queued,
        Downloading,
        Paused,
        Retrying,
        Completed,
        Failed,
        Cancelled
    }

    private sealed class Control
    {
        public readonly object Gate = new();
        public bool Paused;
        public bool Cancelled;
        public CancellationTokenSource? ActiveCancellation;
        public TaskCompletionSource<bool> ResumeSignal = CreateSignaledSignal();
        public long PauseVersion;
        public ExecutionState State = ExecutionState.Queued;

        private static TaskCompletionSource<bool> CreateSignaledSignal()
        {
            var source = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            source.TrySetResult(true);
            return source;
        }
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
            if (control.State is ExecutionState.Completed or ExecutionState.Failed or ExecutionState.Cancelled)
                return false;

            if (control.Paused)
                return true;

            control.Paused = true;
            control.PauseVersion++;
            control.ResumeSignal = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            TransitionLocked(control, ExecutionState.Paused);
            control.ActiveCancellation?.Cancel();
            return true;
        }
    }

    public bool Resume(Guid operationId)
    {
        var control = GetControl(operationId);

        lock (control.Gate)
        {
            if (control.Cancelled || control.State is ExecutionState.Completed or ExecutionState.Failed)
                return false;

            control.Paused = false;
            if (control.State == ExecutionState.Paused)
                TransitionLocked(control, ExecutionState.Queued);

            control.ResumeSignal.TrySetResult(true);
            return true;
        }
    }

    public bool Cancel(Guid operationId)
    {
        var control = GetControl(operationId);

        lock (control.Gate)
        {
            if (control.State is ExecutionState.Completed or ExecutionState.Failed or ExecutionState.Cancelled)
                return false;

            control.Cancelled = true;
            control.Paused = false;
            TransitionLocked(control, ExecutionState.Cancelled);
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

            if (control.State != ExecutionState.Queued)
                TransitionLocked(control, ExecutionState.Queued);

            control.ResumeSignal.TrySetResult(true);
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
            .GroupBy(x => x.OperationId)
            .Select(x => x.First())
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
        var attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WaitIfPausedAsync(
                operation.OperationId,
                control,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (IsCancelled(control))
            {
                ReportTerminal(progress, operation.OperationId, "Cancelled");
                return new CommitItemResult(operation.OperationId, false, "Cancelled by user.");
            }

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            var paused = false;
            try
            {
                using var localCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                lock (control.Gate)
                {
                    if (control.Cancelled)
                    {
                        ReportTerminal(progress, operation.OperationId, "Cancelled");
                        return new CommitItemResult(operation.OperationId, false, "Cancelled by user.");
                    }

                    control.ActiveCancellation = localCancellation;
                    TransitionLocked(control, ExecutionState.Downloading);
                }

                var pauseVersion = GetPauseVersion(control);

                progress?.Report(new CommitProgress(
                    operation.OperationId,
                    CurrentPercent(operation.OperationId),
                    attempt == 0 ? "Downloading" : $"Retrying ({attempt}/{_maxRetries})"));

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

                try
                {
                    await downloader.DownloadAsync(
                        sourceUrl,
                        outputPath,
                        payload.DesiredFormat ?? Core.Enums.MediaFormat.Mp3,
                        itemProgress,
                        localCancellation.Token,
                        payload.DesiredQuality ?? Core.Enums.MediaQuality.Standard128K)
                        .ConfigureAwait(false);

                    _lastPercents[operation.OperationId] = 100;

                    lock (control.Gate)
                        TransitionLocked(control, ExecutionState.Completed);

                    ReportTerminal(progress, operation.OperationId, "Completed", 100);
                    return new CommitItemResult(operation.OperationId, true);
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested &&
                    !IsCancelled(control) &&
                    WasPauseRequestedSince(control, pauseVersion))
                {
                    paused = true;

                    bool stillPaused;
                    lock (control.Gate)
                    {
                        stillPaused = control.Paused;
                        if (stillPaused && control.State == ExecutionState.Downloading)
                            TransitionLocked(control, ExecutionState.Paused);
                    }

                    if (stillPaused)
                    {
                        progress?.Report(new CommitProgress(
                            operation.OperationId,
                            CurrentPercent(operation.OperationId),
                            "Paused"));
                    }
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested &&
                    IsCancelled(control))
                {
                    lock (control.Gate)
                        TransitionLocked(control, ExecutionState.Cancelled);

                    ReportTerminal(progress, operation.OperationId, "Cancelled");

                    return new CommitItemResult(
                        operation.OperationId,
                        false,
                        "Cancelled by user.");
                }
                catch (Exception ex)
                {
                    lastError = ex;

                    if (attempt >= _maxRetries)
                    {
                        lock (control.Gate)
                            TransitionLocked(control, ExecutionState.Failed);

                        ReportTerminal(
                            progress,
                            operation.OperationId,
                            ex.Message,
                            CurrentPercent(operation.OperationId));

                        return new CommitItemResult(
                            operation.OperationId,
                            false,
                            ex.Message);
                    }

                    attempt++;

                    lock (control.Gate)
                        TransitionLocked(control, ExecutionState.Retrying);

                    progress?.Report(new CommitProgress(
                        operation.OperationId,
                        CurrentPercent(operation.OperationId),
                        $"Retrying ({attempt}/{_maxRetries})"));
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

            if (paused)
                continue;

            if (lastError is not null && attempt <= _maxRetries)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Min(8, Math.Pow(2, attempt - 1))),
                    cancellationToken).ConfigureAwait(false);

                lastError = null;
            }
        }
    }

    private static void TransitionLocked(Control control, ExecutionState next)
    {
        if (control.State == next)
            return;

        if (!IsAllowedTransition(control.State, next))
            throw new InvalidOperationException(
                $"Invalid download state transition: {control.State} -> {next}.");

        control.State = next;
    }

    private static bool IsAllowedTransition(ExecutionState current, ExecutionState next)
        => current switch
        {
            ExecutionState.Queued =>
                next is ExecutionState.Downloading or ExecutionState.Paused or ExecutionState.Cancelled,

            ExecutionState.Downloading =>
                next is ExecutionState.Paused or ExecutionState.Retrying or ExecutionState.Completed
                    or ExecutionState.Failed or ExecutionState.Cancelled,

            ExecutionState.Paused =>
                next is ExecutionState.Queued or ExecutionState.Cancelled or ExecutionState.Completed,

            ExecutionState.Retrying =>
                next is ExecutionState.Downloading or ExecutionState.Paused or ExecutionState.Cancelled,

            ExecutionState.Failed =>
                next is ExecutionState.Queued,

            ExecutionState.Cancelled =>
                next is ExecutionState.Queued or ExecutionState.Completed,

            ExecutionState.Completed => false,
            _ => false
        };

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

    private static long GetPauseVersion(Control control)
    {
        lock (control.Gate)
            return control.PauseVersion;
    }

    private static bool WasPauseRequestedSince(Control control, long pauseVersion)
    {
        lock (control.Gate)
            return control.PauseVersion > pauseVersion;
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

    private static void ReportTerminal(
        IProgress<CommitProgress>? progress,
        Guid operationId,
        string status,
        double? percent = null)
        => progress?.Report(new CommitProgress(
            operationId,
            percent ?? 0,
            status,
            true));

    private double CurrentPercent(Guid operationId)
        => _lastPercents.TryGetValue(operationId, out var value) ? value : 0;

    private Control GetControl(Guid operationId)
        => _controls.GetOrAdd(operationId, static _ => new Control());
}
