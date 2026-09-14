using System.Collections.ObjectModel;
using System.IO;
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
    private readonly HashSet<Guid> _failedOperationIds = [];

    [ObservableProperty] private string _rootPath = string.Empty;
    [ObservableProperty] private string _currentPath = string.Empty;
    [ObservableProperty] private ExplorerEntryViewModel? _selectedEntry;
    [ObservableProperty] private string _newFolderName = string.Empty;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _moveDestination = string.Empty;
    [ObservableProperty] private string _statusText = "בחר תיקייה";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ExplorerEntryViewModel> Entries { get; } = [];
    public ObservableCollection<ExplorerTreeNodeViewModel> FolderTree { get; } = [];
    public bool CanGoUp => !string.IsNullOrWhiteSpace(CurrentPath) && !string.Equals(Path.GetFullPath(RootPath), Path.GetFullPath(CurrentPath), StringComparison.OrdinalIgnoreCase);
    public bool CanGoHome => CanGoUp;

    public event Action<string>? AddMediaRequested;

    public ExplorerViewModel(IExplorerService explorer, Application.Abstractions.IStagingService staging, ExplorerProjectionService? projection = null)
    {
        _explorer = explorer;
        _staging = staging;
        _projection = projection ?? new ExplorerProjectionService();
    }

    public async Task InitializeAsync(string? initialPath = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            RootPath = Path.GetFullPath(initialPath);
            CurrentPath = RootPath;
            await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        }
        if (!string.IsNullOrWhiteSpace(CurrentPath)) await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || IsBusy) return;
        IsBusy = true;
        try
        {
            var entries = await _explorer.ListAsync(CurrentPath, cancellationToken).ConfigureAwait(true);
            var projected = _projection.Project(CurrentPath, entries, _staging.Operations);
            Entries.Clear();
            foreach (var item in projected)
            {
                var isError = item.PendingOperationId is Guid operationId && _failedOperationIds.Contains(operationId);
                Entries.Add(new ExplorerEntryViewModel(item.Entry, item.IsPending, item.PendingOperationId, isError)
                {
                    MarkedForDeletion = item.MarkedForDeletion
                });
            }

            StatusText = Entries.Count == 0 ? "התיקייה ריקה" : $"{Entries.Count} פריטים";
            OnPropertyChanged(nameof(CanGoUp));
            OnPropertyChanged(nameof(CanGoHome));
        }
        catch (OperationCanceledException) { StatusText = "הפעולה בוטלה"; }
        catch (Exception ex) { StatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task BuildFolderTreeAsync(CancellationToken cancellationToken)
    {
        FolderTree.Clear();
        if (string.IsNullOrWhiteSpace(RootPath)) return;
        var root = new ExplorerTreeNodeViewModel(Path.GetFileName(RootPath.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : RootPath, RootPath);
        await PopulateTreeAsync(root, depth: 0, cancellationToken).ConfigureAwait(true);
        root.IsExpanded = true;
        FolderTree.Add(root);
    }

    private async Task PopulateTreeAsync(ExplorerTreeNodeViewModel node, int depth, CancellationToken cancellationToken)
    {
        if (depth >= 4) return;
        try
        {
            var entries = await _explorer.ListAsync(node.FullPath, cancellationToken).ConfigureAwait(true);
            foreach (var entry in entries.Where(x => x.IsDirectory).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var child = new ExplorerTreeNodeViewModel(entry.Name, entry.FullPath);
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
        if (!full.StartsWith(Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(full, Path.GetFullPath(RootPath), StringComparison.OrdinalIgnoreCase)) return;
        CurrentPath = full;
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(CanGoUp));
        OnPropertyChanged(nameof(CanGoHome));
    }

    public async Task GoHomeAsync(CancellationToken cancellationToken = default)
        => await NavigateToPathAsync(RootPath, cancellationToken).ConfigureAwait(true);

    public void RequestAddMediaToCurrentFolder()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || IsBusy) return;
        AddMediaRequested?.Invoke(CurrentPath);
    }

    public async Task ApplyCommitResultsAsync(IEnumerable<Guid> successfulOperationIds, IEnumerable<Guid> failedOperationIds, CancellationToken cancellationToken = default)
    {
        foreach (var operationId in successfulOperationIds) _failedOperationIds.Remove(operationId);
        foreach (var operationId in failedOperationIds) _failedOperationIds.Add(operationId);
        await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenAsync(ExplorerEntryViewModel? entry, CancellationToken cancellationToken)
    {
        if (entry is null || !entry.IsDirectory || entry.MarkedForDeletion || entry.IsError || IsBusy) return;
        await NavigateToPathAsync(entry.FullPath, cancellationToken).ConfigureAwait(true);
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
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || string.IsNullOrWhiteSpace(NewFolderName) || IsBusy) return;
        var name = NewFolderName.Trim();
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { StatusText = "שם התיקייה אינו חוקי"; return; }
        var target = Path.Combine(CurrentPath, name);
        if (_staging.Operations.Any(x => string.Equals(x.Payload?.DirectoryPath, target, StringComparison.OrdinalIgnoreCase)) || Directory.Exists(target))
        { StatusText = "התיקייה כבר קיימת"; return; }
        await _staging.StageAsync(CreateOperation(OperationType.CreateDirectory, target, new StagingPayload(DirectoryPath: target, IsDirectory: true)), cancellationToken).ConfigureAwait(true);
        NewFolderName = string.Empty;
        StatusText = "תיקייה נוספה לשינויים הממתינים";
        await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        if (entry is null || entry.MarkedForDeletion || entry.IsError || IsBusy) return;
        await _staging.StageAsync(CreateOperation(OperationType.Delete, entry.FullPath, new StagingPayload(SourcePath: entry.FullPath, Recursive: entry.IsDirectory, IsDirectory: entry.IsDirectory)), cancellationToken).ConfigureAwait(true);
        StatusText = "המחיקה סומנה לשמירה";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RenameSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        var name = NewName.Trim();
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
        if (string.IsNullOrWhiteSpace(RootPath) || !string.Equals(RootPath, full, StringComparison.OrdinalIgnoreCase))
        {
            RootPath = full;
            await BuildFolderTreeAsync(cancellationToken).ConfigureAwait(true);
        }
        CurrentPath = full;
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(CanGoUp));
        OnPropertyChanged(nameof(CanGoHome));
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

    public ExplorerTreeNodeViewModel(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
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

    [ObservableProperty] private bool _markedForDeletion;

    public string KindText => IsDirectory ? "תיקייה" : "קובץ";
    public string SizeText => IsDirectory ? "—" : FormatBytes(Size);
    public string StatusText => IsError ? "השינוי נכשל" : MarkedForDeletion ? "מחיקה ממתינה" : IsPending ? "שינוי ממתין" : "מסונכרן";

    public ExplorerEntryViewModel(ExplorerEntry entry, bool isPending, Guid? pendingOperationId, bool isError = false)
    {
        Name = entry.Name;
        FullPath = entry.FullPath;
        IsDirectory = entry.IsDirectory;
        Size = entry.Size;
        IsPending = isPending;
        PendingOperationId = pendingOperationId;
        IsError = isError;
    }

    partial void OnMarkedForDeletionChanged(bool value) => OnPropertyChanged(nameof(StatusText));

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.0} GB";
    }
}