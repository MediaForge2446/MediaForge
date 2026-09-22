using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.Application.Explorer;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.App.ViewModels;

public partial class ExplorerViewModel : ObservableObject
{
    private readonly IExplorerService _explorer;
    private readonly Application.Abstractions.IStagingService _staging;
    private readonly ExplorerProjectionService _projection;
    private readonly IMediaIndex? _mediaIndex;
    private readonly HashSet<Guid> _failedOperationIds = [];
    private readonly Stack<string> _backHistory = [];
    private readonly Stack<string> _forwardHistory = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private bool _historyNavigation;

    [ObservableProperty] private string _rootPath = string.Empty;
    [ObservableProperty] private string _currentPath = string.Empty;
    [ObservableProperty] private ExplorerEntryViewModel? _selectedEntry;
    [ObservableProperty] private string _newFolderName = string.Empty;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _moveDestination = string.Empty;
    [ObservableProperty] private string _statusText = "בחר תיקייה";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _sortMode = "שם";

    public ObservableCollection<ExplorerEntryViewModel> Entries { get; } = [];
    public ObservableCollection<ExplorerTreeNodeViewModel> FolderTree { get; } = [];
    public bool CanGoUp => !string.IsNullOrWhiteSpace(RootPath) && !string.IsNullOrWhiteSpace(CurrentPath) && !string.Equals(Path.GetFullPath(RootPath), Path.GetFullPath(CurrentPath), StringComparison.OrdinalIgnoreCase);
    public bool CanGoHome => CanGoUp;
    public bool CanGoBack => _backHistory.Count > 0;
    public bool CanGoForward => _forwardHistory.Count > 0;
    public bool HasEntries => Entries.Count > 0;
    public bool HasSelectedEntry => SelectedEntry is not null && !SelectedEntry.MarkedForDeletion && !SelectedEntry.IsError;
    public IReadOnlyList<string> SortOptions { get; } = ["שם", "סוג", "גודל", "עודכן לאחרונה"];

    public IEnumerable<ExplorerEntryViewModel> FilteredEntries
    {
        get
        {
            IEnumerable<ExplorerEntryViewModel> query = Entries;

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var term = SearchQuery.Trim();
                query = query.Where(x =>
                    x.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                    x.FullPath.Contains(term, StringComparison.CurrentCultureIgnoreCase));
            }

            return SortMode switch
            {
                "סוג" => query.OrderByDescending(x => x.IsDirectory)
                              .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
                "גודל" => query.OrderByDescending(x => x.Size)
                                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
                "עודכן לאחרונה" => query.OrderByDescending(x => x.LastModifiedUtc)
                                        .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
                _ => query.OrderByDescending(x => x.IsDirectory)
                          .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            };
        }
    }

    public event Action<string>? AddMediaRequested;

    public ExplorerViewModel(
        IExplorerService explorer,
        Application.Abstractions.IStagingService staging,
        IMediaIndex? mediaIndex = null,
        ExplorerProjectionService? projection = null)
    {
        _explorer = explorer;
        _staging = staging;
        _mediaIndex = mediaIndex;
        _projection = projection ?? new ExplorerProjectionService();
    }

    public async Task InitializeAsync(string? initialPath = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            RootPath = Path.GetFullPath(initialPath);
            CurrentPath = RootPath;
            _backHistory.Clear();
            _forwardHistory.Clear();
            await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        }
        if (!string.IsNullOrWhiteSpace(CurrentPath)) await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
            return;

        await _reloadGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            IsBusy = true;
            var entries = await _explorer.ListAsync(CurrentPath, cancellationToken).ConfigureAwait(true);
            var projected = _projection.Project(CurrentPath, entries, _staging.Operations);
            Entries.Clear();
            foreach (var item in projected)
            {
                var isError = item.PendingOperationId is Guid operationId && _failedOperationIds.Contains(operationId);
                var pendingOperation = item.PendingOperationId is Guid pendingId
                    ? _staging.Operations.FirstOrDefault(x => x.OperationId == pendingId)
                    : null;
                var indexed = _mediaIndex?.Entries.FirstOrDefault(x =>
                    string.Equals(x.PhysicalPath, item.Entry.FullPath, StringComparison.OrdinalIgnoreCase));

                Entries.Add(new ExplorerEntryViewModel(
                    item.Entry,
                    item.IsPending,
                    item.PendingOperationId,
                    isError,
                    indexed,
                    pendingOperation)
                {
                    MarkedForDeletion = item.MarkedForDeletion
                });
            }

            OnPropertyChanged(nameof(HasEntries));
            OnPropertyChanged(nameof(HasSelectedEntry));
            OnPropertyChanged(nameof(FilteredEntries));
            var pendingCount = projected.Count(x => x.IsPending);
            StatusText = Entries.Count == 0
                ? "התיקייה ריקה"
                : pendingCount > 0
                    ? $"{Entries.Count} פריטים · {pendingCount} שינויים ממתינים"
                    : $"{Entries.Count} פריטים";
            OnPropertyChanged(nameof(CanGoUp));
            OnPropertyChanged(nameof(CanGoHome));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
        catch (OperationCanceledException) { StatusText = "הפעולה בוטלה"; }
        catch (Exception ex) { StatusText = ex.Message; }
        finally
        {
            IsBusy = false;
            _reloadGate.Release();
        }
    }

    public async Task RefreshFromStagingAsync(CancellationToken cancellationToken = default)
    {
        await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task BuildFolderTreeAsync(CancellationToken cancellationToken)
    {
        FolderTree.Clear();
        if (string.IsNullOrWhiteSpace(RootPath)) return;
        var rootName = Path.GetFileName(RootPath.TrimEnd(Path.DirectorySeparatorChar));
        var root = new ExplorerTreeNodeViewModel(string.IsNullOrWhiteSpace(rootName) ? RootPath : rootName, RootPath);
        await PopulateTreeAsync(root, 0, cancellationToken).ConfigureAwait(true);
        root.IsExpanded = true;
        FolderTree.Add(root);
    }

    private async Task PopulateTreeAsync(ExplorerTreeNodeViewModel node, int depth, CancellationToken cancellationToken)
    {
        if (depth >= 4) return;
        try
        {
            var actualEntries = await _explorer.ListAsync(node.FullPath, cancellationToken).ConfigureAwait(true);
            var projectedEntries = _projection.Project(node.FullPath, actualEntries, _staging.Operations);

            foreach (var item in projectedEntries.Where(x => x.Entry.IsDirectory)
                         .OrderBy(x => x.Entry.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var child = new ExplorerTreeNodeViewModel(
                    item.Entry.Name,
                    item.Entry.FullPath,
                    item.IsPending,
                    item.MarkedForDeletion);

                node.Children.Add(child);
                await PopulateTreeAsync(child, depth + 1, cancellationToken).ConfigureAwait(true);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
    }

    public async Task NavigateToPathAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RootPath) || string.IsNullOrWhiteSpace(path)) return;
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(full, root, StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(full, CurrentPath, StringComparison.OrdinalIgnoreCase)) return;

        if (!_historyNavigation && !string.IsNullOrWhiteSpace(CurrentPath))
        {
            _backHistory.Push(CurrentPath);
            _forwardHistory.Clear();
        }

        CurrentPath = full;
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(CanGoUp));
        OnPropertyChanged(nameof(CanGoHome));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    [RelayCommand]
    private async Task GoBackAsync(CancellationToken cancellationToken)
    {
        if (_backHistory.Count == 0 || IsBusy) return;
        var target = _backHistory.Pop();
        if (!string.IsNullOrWhiteSpace(CurrentPath)) _forwardHistory.Push(CurrentPath);
        _historyNavigation = true;
        try { await NavigateToPathAsync(target, cancellationToken).ConfigureAwait(true); }
        finally { _historyNavigation = false; }
    }

    [RelayCommand]
    private async Task GoForwardAsync(CancellationToken cancellationToken)
    {
        if (_forwardHistory.Count == 0 || IsBusy) return;
        var target = _forwardHistory.Pop();
        if (!string.IsNullOrWhiteSpace(CurrentPath)) _backHistory.Push(CurrentPath);
        _historyNavigation = true;
        try { await NavigateToPathAsync(target, cancellationToken).ConfigureAwait(true); }
        finally { _historyNavigation = false; }
    }

    public async Task GoHomeAsync(CancellationToken cancellationToken = default)
        => await NavigateToPathAsync(RootPath, cancellationToken).ConfigureAwait(true);

    public async Task OpenFromDoubleClickAsync(ExplorerEntryViewModel entry, CancellationToken cancellationToken = default)
    {
        if (!entry.IsDirectory || entry.MarkedForDeletion || entry.IsError || IsBusy) return;
        await NavigateToPathAsync(entry.FullPath, cancellationToken).ConfigureAwait(true);
    }

    public void RequestAddMediaToCurrentFolder()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || IsBusy) return;
        AddMediaRequested?.Invoke(CurrentPath);
    }

    public void ApplyCommitProgress(CommitProgress progress)
    {
        var operationId = progress.OperationId;
        var entry = Entries.FirstOrDefault(x => x.PendingOperationId == operationId);
        entry?.ApplyProgress(progress);
    }

    public async Task ApplyCommitResultsAsync(IEnumerable<Guid> successfulOperationIds, IEnumerable<Guid> failedOperationIds, CancellationToken cancellationToken = default)
    {
        foreach (var operationId in successfulOperationIds) _failedOperationIds.Remove(operationId);
        foreach (var operationId in failedOperationIds) _failedOperationIds.Add(operationId);
        await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    partial void OnSelectedEntryChanged(ExplorerEntryViewModel? value)
        => OnPropertyChanged(nameof(HasSelectedEntry));

    partial void OnSearchQueryChanged(string value)
        => OnPropertyChanged(nameof(FilteredEntries));

    partial void OnSortModeChanged(string value)
        => OnPropertyChanged(nameof(FilteredEntries));

    [RelayCommand]
    private async Task RefreshCurrentFolderAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(CurrentPath))
            return;

        StatusText = "מרענן את התיקייה…";
        await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenInWindowsExplorer()
    {
        var entry = SelectedEntry;
        if (entry is null || IsBusy)
            return;

        var target = entry.IsDirectory ? entry.FullPath : Path.GetDirectoryName(entry.FullPath);
        if (string.IsNullOrWhiteSpace(target))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{target}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"לא ניתן לפתוח את סייר Windows: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenAsync(ExplorerEntryViewModel? entry, CancellationToken cancellationToken)
    {
        if (entry is not null) await OpenFromDoubleClickAsync(entry, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task GoUpAsync(CancellationToken cancellationToken)
    {
        if (!CanGoUp || IsBusy) return;
        var parent = Directory.GetParent(CurrentPath)?.FullName;
        if (parent is null) return;
        if (!parent.StartsWith(Path.GetFullPath(RootPath), StringComparison.OrdinalIgnoreCase)) parent = RootPath;
        await NavigateToPathAsync(parent, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreateFolderAsync(CancellationToken cancellationToken)
        => await CreateFolderWithNameAsync(NewFolderName, cancellationToken).ConfigureAwait(true);

    public async Task CreateFolderWithNameAsync(string? folderName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || string.IsNullOrWhiteSpace(folderName) || IsBusy) return;
        var name = folderName.Trim();
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { StatusText = "שם התיקייה אינו חוקי"; return; }
        var target = Path.Combine(CurrentPath, name);
        if (_staging.Operations.Any(x => string.Equals(x.Payload?.DirectoryPath, target, StringComparison.OrdinalIgnoreCase)) || Directory.Exists(target))
        { StatusText = "התיקייה כבר קיימת"; return; }
        await _staging.StageAsync(CreateOperation(OperationType.CreateDirectory, target, new StagingPayload(DirectoryPath: target, IsDirectory: true)), cancellationToken).ConfigureAwait(true);
        NewFolderName = string.Empty;
        StatusText = "התיקייה נוספה לשינויים הממתינים";
        await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync(CancellationToken cancellationToken)
        => await DeleteSelectedFromViewAsync(cancellationToken).ConfigureAwait(true);

    public async Task DeleteSelectedFromViewAsync(CancellationToken cancellationToken = default)
    {
        var entry = SelectedEntry;
        if (entry is null || entry.MarkedForDeletion || entry.IsError || IsBusy) return;
        await _staging.StageAsync(CreateOperation(OperationType.Delete, entry.FullPath, new StagingPayload(SourcePath: entry.FullPath, Recursive: entry.IsDirectory, IsDirectory: entry.IsDirectory)), cancellationToken).ConfigureAwait(true);
        StatusText = "המחיקה סומנה לשמירה";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(HasSelectedEntry));
    }

    [RelayCommand]
    private async Task RenameSelectedAsync(CancellationToken cancellationToken)
        => await RenameSelectedWithNameAsync(NewName, cancellationToken).ConfigureAwait(true);

    public async Task RenameSelectedWithNameAsync(string? newName, CancellationToken cancellationToken = default)
    {
        var entry = SelectedEntry;
        var name = newName?.Trim() ?? string.Empty;
        if (entry is null || entry.MarkedForDeletion || entry.IsError || string.IsNullOrWhiteSpace(name) || IsBusy) return;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { StatusText = "השם החדש אינו חוקי"; return; }
        var destination = Path.Combine(Path.GetDirectoryName(entry.FullPath) ?? CurrentPath, name);
        if (File.Exists(destination) || Directory.Exists(destination)) { StatusText = "כבר קיים פריט בשם הזה"; return; }
        if (_staging.Operations.Any(x => string.Equals(x.Payload?.DestinationPath, destination, StringComparison.OrdinalIgnoreCase))) { StatusText = "כבר קיים שינוי ממתין ליעד הזה"; return; }
        await _staging.StageAsync(CreateOperation(OperationType.Rename, entry.FullPath, new StagingPayload(SourcePath: entry.FullPath, DestinationPath: destination, NewName: name, IsDirectory: entry.IsDirectory)), cancellationToken).ConfigureAwait(true);
        NewName = string.Empty;
        StatusText = "שינוי השם סומן לשמירה";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MoveSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        var destination = MoveDestination.Trim();
        if (entry is null || entry.MarkedForDeletion || entry.IsError || string.IsNullOrWhiteSpace(destination) || IsBusy) return;
        var destinationPath = Path.GetFullPath(destination);
        if (entry.IsDirectory && IsSameOrChildPath(destinationPath, entry.FullPath)) { StatusText = "אי אפשר להעביר תיקייה לתוך עצמה"; return; }
        if (File.Exists(destinationPath) || Directory.Exists(destinationPath)) { StatusText = "יעד ההעברה כבר קיים"; return; }
        if (_staging.Operations.Any(x => string.Equals(x.Payload?.DestinationPath, destinationPath, StringComparison.OrdinalIgnoreCase))) { StatusText = "כבר קיים שינוי ממתין ליעד הזה"; return; }
        await _staging.StageAsync(CreateOperation(OperationType.Move, entry.FullPath, new StagingPayload(SourcePath: entry.FullPath, DestinationPath: destinationPath, IsDirectory: entry.IsDirectory)), cancellationToken).ConfigureAwait(true);
        MoveDestination = string.Empty;
        StatusText = "ההעברה סומנה לשמירה";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task UndoSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        if (entry?.PendingOperationId is not Guid operationId || IsBusy) return;
        _failedOperationIds.Remove(operationId);
        if (await _staging.UndoAsync(operationId, cancellationToken).ConfigureAwait(true)) StatusText = "השינוי בוטל";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private void AddMediaAsync() => RequestAddMediaToCurrentFolder();

    public async Task SetInitialPathAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var full = Path.GetFullPath(path);
        RootPath = full;
        CurrentPath = full;
        _backHistory.Clear();
        _forwardHistory.Clear();
        await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(CanGoUp));
        OnPropertyChanged(nameof(CanGoHome));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    private static StagingOperation CreateOperation(OperationType type, string target, StagingPayload payload)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, type, target, nameof(MediaState.Missing), nameof(MediaState.Pending), payload);

    private static bool IsSameOrChildPath(string candidate, string parent)
    {
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed partial class ExplorerTreeNodeViewModel : ObservableObject
{
    public string Name { get; }
    public string FullPath { get; }
    public ObservableCollection<ExplorerTreeNodeViewModel> Children { get; } = [];

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isPending;
    [ObservableProperty] private bool _markedForDeletion;

    public ExplorerTreeNodeViewModel(
        string name,
        string fullPath,
        bool isPending = false,
        bool markedForDeletion = false)
    {
        Name = name;
        FullPath = fullPath;
        _isPending = isPending;
        _markedForDeletion = markedForDeletion;
    }
}

public sealed partial class ExplorerEntryViewModel : ObservableObject
{
    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public long Size { get; }
    public bool IsPending { get; }
    public Guid? PendingOperationId { get; }
    public bool IsError { get; }
    public string? ThumbnailUrl { get; }
    public string? MediaTitle { get; }
    public string? MediaArtist { get; }

    [ObservableProperty] private bool _markedForDeletion;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private double? _speedBytesPerSecond;
    [ObservableProperty] private TimeSpan? _eta;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public DateTimeOffset LastModifiedUtc { get; }
    public string KindText => IsDirectory ? "תיקייה" : "קובץ";
    public string SizeText => IsDirectory ? "—" : FormatBytes(Size);
    public string StatusText => IsError
        ? "השינוי נכשל"
        : MarkedForDeletion
            ? "מחיקה ממתינה"
            : IsPending
                ? "שינוי ממתין"
                : "מסונכרן";
    public string SpeedText => SpeedBytesPerSecond is { } speed && speed > 0
        ? FormatSpeed(speed)
        : "—";
    public string EtaText => Eta is { } eta && eta.TotalSeconds >= 0
        ? eta.ToString(eta.TotalHours >= 1 ? @"h:mm:ss" : @"mm:ss")
        : "—";
    public bool HasProgress => IsPending && ProgressPercent > 0 && !IsDirectory;
    public bool IsMedia => !IsDirectory && (
        FullPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
        FullPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
        FullPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
        FullPath.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase));

    public ExplorerEntryViewModel(
        ExplorerEntry entry,
        bool isPending,
        Guid? pendingOperationId,
        bool isError = false,
        MediaIndexEntry? indexed = null,
        StagingOperation? pendingOperation = null)
    {
        Name = entry.Name;
        FullPath = entry.FullPath;
        IsDirectory = entry.IsDirectory;
        Size = entry.Size;
        IsPending = isPending;
        PendingOperationId = pendingOperationId;
        IsError = isError;
        LastModifiedUtc = entry.LastModifiedUtc;

        ThumbnailUrl = pendingOperation?.Payload?.ThumbnailUrl ?? indexed?.ThumbnailUrl;
        MediaTitle = pendingOperation?.Payload?.Title ?? indexed?.Title;
        MediaArtist = pendingOperation?.Payload?.Artist ?? indexed?.Artist;
        _errorMessage = isError ? "השינוי נכשל" : string.Empty;
    }

    public void ApplyProgress(CommitProgress progress)
    {
        if (PendingOperationId != progress.OperationId)
            return;

        ProgressPercent = Math.Clamp(progress.Percent, 0, 100);
        SpeedBytesPerSecond = progress.SpeedBytesPerSecond;
        Eta = progress.Eta;

        if (progress.IsTerminal && progress.Percent <= 0)
            ErrorMessage = progress.Status;

        OnPropertyChanged(nameof(HasProgress));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(EtaText));
    }

    partial void OnMarkedForDeletionChanged(bool value) => OnPropertyChanged(nameof(StatusText));

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond:0} B/s";
        var value = bytesPerSecond / 1024d;
        if (value < 1024)
            return $"{value:0.0} KB/s";
        return $"{value / 1024d:0.0} MB/s";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.0} GB";
    }
}