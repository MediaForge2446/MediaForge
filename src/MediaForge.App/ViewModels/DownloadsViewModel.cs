using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.App.Localization;
using MediaForge.Application.Abstractions;
using MediaForge.Application.Downloads;
using MediaForge.Core.Enums;
using MediaForge.Core.Models;
using MediaForge.App.Services;

namespace MediaForge.App.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly MediaImportService _importService;
    private readonly IFolderPicker _folderPicker;
    private readonly LocalizationService _localization;
    private readonly UserPreferencesService _preferences;
    private readonly DownloadQueue _downloadQueue;

    [ObservableProperty] private string _sourceUrl = string.Empty;
    [ObservableProperty] private string _destinationDirectory = string.Empty;
    [ObservableProperty] private MediaFormat _selectedFormat = MediaFormat.Mp3;
    [ObservableProperty] private MediaQuality _selectedQuality = MediaQuality.Standard128K;
    [ObservableProperty] private string _collectionTitle = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private ResolvedMediaItemViewModel? _previewItem;

    public ObservableCollection<ResolvedMediaItemViewModel> Items { get; } = [];
    public ObservableCollection<DownloadQueueItemViewModel> QueueItems { get; } = [];

    public IReadOnlyList<MediaFormat> Formats { get; } =
        [MediaFormat.Mp3, MediaFormat.Mp4, MediaFormat.Wav, MediaFormat.M4a];

    public IReadOnlyList<MediaQuality> Qualities { get; } =
        [MediaQuality.Standard128K, MediaQuality.High192K, MediaQuality.VeryHigh256K, MediaQuality.Maximum320K];

    public int DownloadQueueConcurrency => _downloadQueue.MaxConcurrency;

    public bool IsMp3Selected => SelectedFormat == MediaFormat.Mp3;
    public bool IsMp4Selected => SelectedFormat == MediaFormat.Mp4;
    public bool IsWavSelected => SelectedFormat == MediaFormat.Wav;
    public bool IsM4aSelected => SelectedFormat == MediaFormat.M4a;

    public bool HasItems => Items.Count > 0;
    public int SelectedCount => Items.Count(x => x.IsSelected);
    public bool HasSelection => SelectedCount > 0;
    public int QueueCount => QueueItems.Count;
    public bool HasQueue => QueueItems.Count > 0;
    public int ActiveQueueCount => QueueItems.Count(x => x.IsActive);
    public int QueuedCount => QueueItems.Count(x => x.State is DownloadQueueState.Queued or DownloadQueueState.Retrying);
    public event Func<Task>? MediaStaged;
    public event Func<Guid, Task>? RetryRequested;
    public event Action<string>? LogRequested;

    public DownloadsViewModel(
        MediaImportService importService,
        IFolderPicker folderPicker,
        LocalizationService localization,
        UserPreferencesService preferences,
        DownloadQueue? downloadQueue = null)
    {
        _importService = importService;
        _folderPicker = folderPicker;
        _localization = localization;
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _downloadQueue = downloadQueue ?? new DownloadQueue();
        _selectedFormat = _preferences.DefaultFormat;
        _localization.CultureChanged += OnCultureChanged;
        StatusText = _localization.Get("Downloads_PastePrompt");
    }

    public void SetDefaultDestination(string path)
    {
        if (string.IsNullOrWhiteSpace(DestinationDirectory))
            DestinationDirectory = path;
    }

    public void SetDestination(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            DestinationDirectory = path;
    }

    public void PrepareForFolder(string path)
    {
        DestinationDirectory = path;
        SourceUrl = string.Empty;
        CollectionTitle = string.Empty;
        PreviewItem = null;
        SelectedFormat = _preferences.DefaultFormat;
        SelectedQuality = MediaQuality.Standard128K;
        StatusText = _localization.Get("Downloads_PastePrompt");
        UnsubscribeItems();
        Items.Clear();
        NotifySelectionState();
    }

    public void SyncFromStaging(IEnumerable<StagingOperation> operations)
    {
        var downloadOperations = operations
            .Where(x => x.OperationType == OperationType.Download)
            .OrderBy(x => x.CreatedAt)
            .ToArray();

        var activeIds = downloadOperations.Select(x => x.OperationId).ToHashSet();

        foreach (var item in QueueItems.Where(x =>
                     !activeIds.Contains(x.OperationId) &&
                     !x.IsTerminal).ToArray())
        {
            QueueItems.Remove(item);
        }

        foreach (var operation in downloadOperations)
        {
            if (QueueItems.Any(x => x.OperationId == operation.OperationId))
                continue;

            QueueItems.Add(DownloadQueueItemViewModel.FromStaging(
                operation,
                _localization));
        }

        NotifyQueueState();
    }

    public void ApplyCommitProgress(CommitProgress progress)
    {
        var item = QueueItems.FirstOrDefault(x => x.OperationId == progress.OperationId);
        if (item is null)
            return;

        item.ApplyProgress(progress);
        NotifyQueueState();
    }

    public IReadOnlyList<Guid> GetOrderedOperationIds()
        => QueueItems
            .Select(x => x.OperationId)
            .ToArray();

    public void MoveQueueItem(Guid operationId, int targetIndex)
    {
        var currentIndex = QueueItems
            .Select((item, index) => (item, index))
            .FirstOrDefault(x => x.item.OperationId == operationId)
            .index;

        var item = QueueItems.FirstOrDefault(x => x.OperationId == operationId);
        if (item is null)
            return;

        targetIndex = Math.Clamp(targetIndex, 0, Math.Max(0, QueueItems.Count - 1));
        if (currentIndex == targetIndex)
            return;

        QueueItems.Move(currentIndex, targetIndex);

        for (var i = 0; i < QueueItems.Count; i++)
            _downloadQueue.SetPriority(QueueItems[i].OperationId, QueueItems.Count - i);
    }

    [RelayCommand]
    private void PauseQueueItem(DownloadQueueItemViewModel? item)
    {
        if (item is null || item.IsTerminal || item.State == DownloadQueueState.Paused)
            return;

        if (_downloadQueue.Pause(item.OperationId))
            item.SetControlState(DownloadQueueState.Paused);
    }

    [RelayCommand]
    private void ResumeQueueItem(DownloadQueueItemViewModel? item)
    {
        if (item is null || item.IsTerminal)
            return;

        if (_downloadQueue.Resume(item.OperationId))
            item.SetControlState(DownloadQueueState.Queued);
    }

    [RelayCommand]
    private void CancelQueueItem(DownloadQueueItemViewModel? item)
    {
        if (item is null || item.IsTerminal)
            return;

        if (_downloadQueue.Cancel(item.OperationId))
            item.SetControlState(DownloadQueueState.Cancelled);
    }

    [RelayCommand]
    private async Task RetryQueueItemAsync(DownloadQueueItemViewModel? item)
    {
        if (item is null || item.State != DownloadQueueState.Failed)
            return;

        _downloadQueue.PrepareForRetry(item.OperationId);
        item.SetControlState(DownloadQueueState.Queued);

        if (RetryRequested is { } handler)
            await handler(item.OperationId).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ShowQueueLog(DownloadQueueItemViewModel? item)
    {
        if (item is not null)
            LogRequested?.Invoke(item.LogText);
    }

    public void ApplyCommitResults(IEnumerable<CommitItemResult> results)
    {
        foreach (var result in results)
        {
            var item = QueueItems.FirstOrDefault(x => x.OperationId == result.OperationId);
            if (item is null)
                continue;

            item.ApplyResult(result);
        }

        NotifyQueueState();
    }

    private void NotifyQueueState()
    {
        OnPropertyChanged(nameof(QueueCount));
        OnPropertyChanged(nameof(HasQueue));
        OnPropertyChanged(nameof(ActiveQueueCount));
        OnPropertyChanged(nameof(QueuedCount));
    }

    private void NotifySelectionState()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in QueueItems)
            item.RefreshLocalizedState();
        StatusText = _localization.Get("Downloads_PastePrompt");
        OnPropertyChanged(nameof(QueueCount));
        OnPropertyChanged(nameof(ActiveQueueCount));
        OnPropertyChanged(nameof(QueuedCount));
    }

    private void SubscribeItem(ResolvedMediaItemViewModel item)
        => item.PropertyChanged += OnMediaItemPropertyChanged;

    private void UnsubscribeItems()
    {
        foreach (var item in Items)
            item.PropertyChanged -= OnMediaItemPropertyChanged;
    }

    private void OnMediaItemPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResolvedMediaItemViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    [RelayCommand]
    private async Task ResolveAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(SourceUrl))
            return;

        IsBusy = true;
        UnsubscribeItems();
        Items.Clear();
        CollectionTitle = string.Empty;
        StatusText = _localization.Get("Downloads_Resolving");
        NotifySelectionState();

        try
        {
            var result = await _importService.ResolveAsync(
                SourceUrl.Trim(),
                cancellationToken).ConfigureAwait(true);

            CollectionTitle = result.CollectionTitle ?? string.Empty;

            foreach (var item in result.Items)
            {
                var itemViewModel = new ResolvedMediaItemViewModel(item, Formats);
                SubscribeItem(itemViewModel);
                Items.Add(itemViewModel);
            }

            PreviewItem = Items.FirstOrDefault();

            StatusText = result.IsPlaylist
                ? string.Format(
                    _localization.CurrentCulture,
                    _localization.Get("Downloads_PlaylistFound"),
                    Items.Count,
                    SelectedCount)
                : _localization.Get("Downloads_SingleFound");

            NotifySelectionState();
        }
        catch (OperationCanceledException)
        {
            StatusText = _localization.Get("Status_Canceled");
        }
        catch (Exception ex)
        {
            StatusText = $"{_localization.Get("Downloads_ResolveFailed")}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedFormatChanged(MediaFormat value)
    {
        if (Enum.IsDefined(value))
            _preferences.SetDefaultFormat(value);

        OnPropertyChanged(nameof(IsMp3Selected));
        OnPropertyChanged(nameof(IsMp4Selected));
        OnPropertyChanged(nameof(IsWavSelected));
        OnPropertyChanged(nameof(IsM4aSelected));
    }

    [RelayCommand]
    private void SelectMp3()
    {
        SelectedFormat = MediaFormat.Mp3;
        foreach (var item in Items.Where(x => x.IsSelected))
            item.DesiredFormat = MediaFormat.Mp3;
    }

    [RelayCommand]
    private void SelectMp4()
    {
        SelectedFormat = MediaFormat.Mp4;
        foreach (var item in Items.Where(x => x.IsSelected))
            item.DesiredFormat = MediaFormat.Mp4;
    }

    [RelayCommand]
    private void SelectWav()
    {
        SelectedFormat = MediaFormat.Wav;
        foreach (var item in Items.Where(x => x.IsSelected))
            item.DesiredFormat = MediaFormat.Wav;
    }

    [RelayCommand]
    private void SelectM4a()
    {
        SelectedFormat = MediaFormat.M4a;
        foreach (var item in Items.Where(x => x.IsSelected))
            item.DesiredFormat = MediaFormat.M4a;
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Items)
            item.IsSelected = true;

        StatusText = string.Format(
            _localization.CurrentCulture,
            _localization.Get("Downloads_Selected"),
            SelectedCount,
            Items.Count);

        NotifySelectionState();
    }

    [RelayCommand]
    private void Mp3ToAll()
    {
        foreach (var item in Items)
        {
            item.DesiredFormat = MediaFormat.Mp3;
            item.IsSelected = true;
        }

        SelectedFormat = MediaFormat.Mp3;
        StatusText = string.Format(
            _localization.CurrentCulture,
            _localization.Get("Downloads_Mp3Selected"),
            SelectedCount);

        NotifySelectionState();
    }

    [RelayCommand]
    private async Task StageSelectedAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(DestinationDirectory))
        {
            StatusText = _localization.Get("Downloads_NoDestination");
            return;
        }

        var selected = Items
            .Where(x => x.IsSelected)
            .Select(x => x.ToResolvedItem())
            .ToArray();

        if (selected.Length == 0)
        {
            StatusText = _localization.Get("Downloads_NoSelection");
            return;
        }

        IsBusy = true;
        StatusText = _localization.Get("Downloads_Staging");

        try
        {
            var staged = await _importService.StageDownloadsAsync(
                selected,
                DestinationDirectory,
                SelectedFormat,
                SelectedQuality,
                cancellationToken).ConfigureAwait(true);

                foreach (var operation in staged)
            {                QueueItems.Add(DownloadQueueItemViewModel.FromStaging(
                    operation,
                    _localization));
            }

            NotifyQueueState();

            StatusText = staged.Count == selected.Length
                ? string.Format(
                    _localization.CurrentCulture,
                    _localization.Get("Downloads_StagedAll"),
                    staged.Count)
                : string.Format(
                    _localization.CurrentCulture,
                    _localization.Get("Downloads_StagedPartial"),
                    staged.Count);

            if (MediaStaged is { } handler)
                await handler().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusText = _localization.Get("Status_Canceled");
        }
        catch (Exception ex)
        {
            StatusText = $"{_localization.Get("Downloads_StageFailed")}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PickDestinationAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        var path = await _folderPicker
            .PickFolderAsync(cancellationToken)
            .ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(path))
            DestinationDirectory = path;
    }
}

public enum DownloadQueueState
{
    Queued,
    Downloading,
    Paused,
    Retrying,
    Completed,
    Failed,
    Cancelled
}

public partial class DownloadQueueItemViewModel : ObservableObject
{
    private readonly LocalizationService _localization;

    public Guid OperationId { get; }
    public string Title { get; }
    public string Artist { get; }
    public string? ThumbnailUrl { get; }
    public string DestinationPath { get; }
    public MediaFormat Format { get; }
    public MediaQuality Quality { get; }
    public string FormatText => Format switch
    {
        MediaFormat.Mp3 => "MP3",
        MediaFormat.Mp4 => "MP4",
        MediaFormat.Wav => "WAV",
        MediaFormat.M4a => "M4A",
        _ => Format.ToString()
    };

    [ObservableProperty] private DownloadQueueState _state = DownloadQueueState.Queued;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private double? _speedBytesPerSecond;
    [ObservableProperty] private TimeSpan? _eta;
    [ObservableProperty] private long? _downloadedBytes;
    [ObservableProperty] private long? _totalBytes;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _logText = string.Empty;

    public bool IsActive => State is DownloadQueueState.Downloading or DownloadQueueState.Retrying;
    public bool IsTerminal => State is DownloadQueueState.Completed or DownloadQueueState.Failed or DownloadQueueState.Cancelled;
    public bool CanPause => State is DownloadQueueState.Queued or DownloadQueueState.Downloading or DownloadQueueState.Retrying;
    public bool CanResume => State == DownloadQueueState.Paused;
    public bool CanCancel => !IsTerminal;
    public bool CanRetry => State == DownloadQueueState.Failed;
    public string StatusText => GetLocalizedState();
    public string SpeedText => SpeedBytesPerSecond is { } speed && speed > 0
        ? FormatSpeed(speed)
        : "—";
    public string EtaText => Eta is { } eta && eta.TotalSeconds >= 0
        ? FormatEta(eta)
        : "—";
    public string SizeText => TotalBytes is { } total ? FormatBytes(total) : "—";
    public string DownloadedSizeText => DownloadedBytes is { } value ? FormatBytes(value) : "—";
    public string QualityText => Quality switch
    {
        MediaQuality.High192K => "192 kbps",
        MediaQuality.VeryHigh256K => "256 kbps",
        MediaQuality.Maximum320K => "320 kbps",
        _ => "128 kbps"
    };

    public DownloadQueueItemViewModel(
        Guid operationId,
        string title,
        string artist,
        string? thumbnailUrl,
        string destinationPath,
        MediaFormat format,
        MediaQuality quality,
        LocalizationService localization)
    {
        OperationId = operationId;
        Title = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(destinationPath)
            : title;
        Artist = string.IsNullOrWhiteSpace(artist) ? "YouTube" : artist;
        ThumbnailUrl = thumbnailUrl;
        DestinationPath = destinationPath;
        Format = format;
        Quality = quality;
        _localization = localization;
    }

    public static DownloadQueueItemViewModel FromStaging(
        StagingOperation operation,
        LocalizationService localization)
    {
        var payload = operation.Payload
            ?? throw new InvalidOperationException("Download operation payload is missing.");

        return new DownloadQueueItemViewModel(
            operation.OperationId,
            payload.Title ?? string.Empty,
            payload.Artist ?? string.Empty,
            payload.ThumbnailUrl,
            payload.DestinationPath ?? operation.Target,
            payload.DesiredFormat ?? MediaFormat.Mp3,
            payload.DesiredQuality ?? MediaQuality.Standard128K,
            localization);
    }

    public void ApplyProgress(CommitProgress progress)
    {
        ProgressPercent = Math.Clamp(progress.Percent, 0, 100);
        SpeedBytesPerSecond = progress.SpeedBytesPerSecond;
        Eta = progress.Eta;
        DownloadedBytes = progress.DownloadedBytes;
        TotalBytes = progress.TotalBytes;
        AppendLog($"{DateTime.Now:T}  {progress.Status}  {ProgressPercent:0.0}%");

        State = progress.Status switch
        {
            var status when status.StartsWith("Retrying", StringComparison.OrdinalIgnoreCase)
                => DownloadQueueState.Retrying,
            var status when status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                => DownloadQueueState.Completed,
            var status when status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                => DownloadQueueState.Cancelled,
            var status when status.Equals("Downloading", StringComparison.OrdinalIgnoreCase)
                => DownloadQueueState.Downloading,
            _ when progress.IsTerminal && progress.Percent <= 0
                => DownloadQueueState.Failed,
            _ => State
        };

        ErrorMessage = State == DownloadQueueState.Failed
            ? progress.Status
            : string.Empty;

        NotifyDerivedProperties();
    }

    public void ApplyResult(CommitItemResult result)
    {
        State = result.Success
            ? DownloadQueueState.Completed
            : DownloadQueueState.Failed;

        ProgressPercent = result.Success ? 100 : ProgressPercent;
        ErrorMessage = result.Error ?? string.Empty;
        if (result.Error is not null)
            AppendLog($"{DateTime.Now:T}  {result.Error}");
        NotifyDerivedProperties();
    }

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(QualityText));
        OnPropertyChanged(nameof(FormatText));
    }

    public void SetControlState(DownloadQueueState state)
    {
        State = state;
        if (state == DownloadQueueState.Paused)
            AppendLog($"{DateTime.Now:T}  {_localization.Get("Queue_Paused")}");
        NotifyDerivedProperties();
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        LogText = string.IsNullOrWhiteSpace(LogText)
            ? line
            : LogText + Environment.NewLine + line;
    }

    private void NotifyDerivedProperties()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsTerminal));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(EtaText));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(DownloadedSizeText));
        OnPropertyChanged(nameof(QualityText));
    }

    private string GetLocalizedState() => State switch
    {
        DownloadQueueState.Downloading => _localization.Get("Queue_Downloading"),
        DownloadQueueState.Retrying => _localization.Get("Queue_Retrying"),
        DownloadQueueState.Completed => _localization.Get("Queue_Completed"),
        DownloadQueueState.Failed => _localization.Get("Queue_Failed"),
        DownloadQueueState.Cancelled => _localization.Get("Queue_Cancelled"),
        _ => _localization.Get("Queue_Queued")
    };

    private static string FormatSpeed(double bytesPerSecond)
    {
        var kb = bytesPerSecond / 1024d;
        if (kb < 1024)
            return $"{kb:0.0} KB/s";

        var mb = kb / 1024d;
        if (mb < 1024)
            return $"{mb:0.0} MB/s";

        return $"{mb / 1024d:0.00} GB/s";
    }

    private static string FormatEta(TimeSpan value)
    {
        if (value.TotalHours >= 1)
            return value.ToString(@"h:mm:ss");

        return value.ToString(@"mm:ss");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        var value = bytes / 1024d;
        if (value < 1024)
            return $"{value:0.0} KiB";

        value /= 1024d;
        if (value < 1024)
            return $"{value:0.0} MiB";

        value /= 1024d;
        return $"{value:0.00} GiB";
    }
}

public partial class ResolvedMediaItemViewModel : ObservableObject
{
    private readonly ResolvedMediaItem _item;

    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private MediaFormat _desiredFormat = MediaFormat.Mp3;

    public IReadOnlyList<MediaFormat> Formats { get; }
    public string VideoId => _item.VideoId;
    public string SourceUrl => _item.SourceUrl;
    public string Artist => _item.Metadata.Artist ?? "YouTube";
    public string DurationText => _item.Metadata.Duration is { } duration
        ? duration.ToString(@"hh\:mm\:ss")
        : "—";
    public string? ThumbnailUrl => _item.Metadata.ThumbnailUrl;

    public ResolvedMediaItemViewModel(
        ResolvedMediaItem item,
        IReadOnlyList<MediaFormat> formats)
    {
        _item = item;
        _title = item.Metadata.Title;
        _desiredFormat = item.DesiredFormat;
        Formats = formats;
    }

    public ResolvedMediaItem ToResolvedItem()
        => _item with
        {
            Metadata = _item.Metadata with { Title = Title.Trim() },
            DesiredFormat = DesiredFormat
        };
}
