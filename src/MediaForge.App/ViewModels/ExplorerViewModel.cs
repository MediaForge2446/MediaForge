using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.App.ViewModels;

public partial class ExplorerViewModel : ObservableObject
{
    private readonly IExplorerService _explorer;
    private readonly Application.Abstractions.IStagingService _staging;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private ExplorerEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private string _newFolderName = string.Empty;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _moveDestination = string.Empty;

    [ObservableProperty]
    private string _statusText = "בחר תיקייה כדי להתחיל";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ExplorerEntryViewModel> Entries { get; } = [];

    public bool CanGoUp => !string.IsNullOrWhiteSpace(CurrentPath) &&
                           !string.Equals(Path.GetPathRoot(CurrentPath), CurrentPath, StringComparison.OrdinalIgnoreCase);

    public ExplorerViewModel(IExplorerService explorer, Application.Abstractions.IStagingService staging)
    {
        _explorer = explorer;
        _staging = staging;
    }

    public async Task InitializeAsync(string? initialPath = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(initialPath))
            CurrentPath = Path.GetFullPath(initialPath);
        if (!string.IsNullOrWhiteSpace(CurrentPath))
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var entries = await _explorer.ListAsync(CurrentPath, cancellationToken).ConfigureAwait(true);
            var projected = entries.ToDictionary(x => x.FullPath, StringComparer.OrdinalIgnoreCase);

            foreach (var operation in _staging.Operations)
            {
                var payload = operation.Payload;
                if (operation.OperationType == OperationType.CreateDirectory &&
                    !string.IsNullOrWhiteSpace(payload?.DirectoryPath) &&
                    string.Equals(Path.GetDirectoryName(payload.DirectoryPath), CurrentPath, StringComparison.OrdinalIgnoreCase) &&
                    !projected.ContainsKey(payload.DirectoryPath))
                {
                    projected[payload.DirectoryPath] = new ExplorerEntry(
                        Path.GetFileName(payload.DirectoryPath),
                        payload.DirectoryPath,
                        true,
                        0,
                        DateTimeOffset.UtcNow);
                }
            }

            Entries.Clear();
            foreach (var entry in projected.Values
                         .OrderByDescending(x => x.IsDirectory)
                         .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var pending = FindPendingOperation(entry.FullPath);
                Entries.Add(new ExplorerEntryViewModel(entry, pending is not null, pending?.OperationId));
            }

            foreach (var operation in _staging.Operations.Where(x => x.OperationType == OperationType.Delete))
            {
                var target = operation.Payload?.SourcePath;
                if (!string.IsNullOrWhiteSpace(target) &&
                    string.Equals(Path.GetDirectoryName(target), CurrentPath, StringComparison.OrdinalIgnoreCase))
                {
                    var item = Entries.FirstOrDefault(x => string.Equals(x.FullPath, target, StringComparison.OrdinalIgnoreCase));
                    if (item is not null)
                        item.MarkedForDeletion = true;
                }
            }

            StatusText = $"{Entries.Count} פריטים";
            OnPropertyChanged(nameof(CanGoUp));
        }
        catch (OperationCanceledException)
        {
            StatusText = "הפעולה בוטלה";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAsync(ExplorerEntryViewModel? entry, CancellationToken cancellationToken)
    {
        if (entry is null || !entry.IsDirectory || entry.MarkedForDeletion || IsBusy)
            return;
        CurrentPath = entry.FullPath;
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task GoUpAsync(CancellationToken cancellationToken)
    {
        if (!CanGoUp || IsBusy)
            return;
        var parent = Directory.GetParent(CurrentPath)?.FullName;
        if (parent is null)
            return;
        CurrentPath = parent;
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreateFolderAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || string.IsNullOrWhiteSpace(NewFolderName) || IsBusy)
            return;

        var name = NewFolderName.Trim();
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            StatusText = "שם התיקייה אינו חוקי";
            return;
        }

        var target = Path.Combine(CurrentPath, name);
        if (_staging.Operations.Any(x => string.Equals(x.Payload?.DirectoryPath, target, StringComparison.OrdinalIgnoreCase)) || Directory.Exists(target))
        {
            StatusText = "התיקייה כבר קיימת";
            return;
        }

        await _staging.StageAsync(CreateOperation(OperationType.CreateDirectory, target, new StagingPayload(DirectoryPath: target)), cancellationToken).ConfigureAwait(true);
        NewFolderName = string.Empty;
        StatusText = "תיקייה נוספה לשינויים ממתינים";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        if (entry is null || entry.MarkedForDeletion || IsBusy)
            return;

        await _staging.StageAsync(CreateOperation(
            OperationType.Delete,
            entry.FullPath,
            new StagingPayload(SourcePath: entry.FullPath, Recursive: entry.IsDirectory)), cancellationToken).ConfigureAwait(true);
        StatusText = "מחיקה סומנה לשמירה";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RenameSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        var name = NewName.Trim();
        if (entry is null || string.IsNullOrWhiteSpace(name) || IsBusy)
            return;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            StatusText = "השם החדש אינו חוקי";
            return;
        }

        var destination = Path.Combine(Path.GetDirectoryName(entry.FullPath) ?? CurrentPath, name);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            StatusText = "כבר קיים פריט בשם הזה";
            return;
        }

        await _staging.StageAsync(CreateOperation(
            OperationType.Rename,
            entry.FullPath,
            new StagingPayload(SourcePath: entry.FullPath, DestinationPath: destination, NewName: name)), cancellationToken).ConfigureAwait(true);
        NewName = string.Empty;
        StatusText = "שינוי השם סומן לשמירה";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MoveSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        var destination = MoveDestination.Trim();
        if (entry is null || string.IsNullOrWhiteSpace(destination) || IsBusy)
            return;

        var destinationPath = Path.GetFullPath(destination);
        if (entry.IsDirectory && IsSameOrChildPath(destinationPath, entry.FullPath))
        {
            StatusText = "אי אפשר להעביר תיקייה לתוך עצמה";
            return;
        }
        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            StatusText = "יעד ההעברה כבר קיים";
            return;
        }

        await _staging.StageAsync(CreateOperation(
            OperationType.Move,
            entry.FullPath,
            new StagingPayload(SourcePath: entry.FullPath, DestinationPath: destinationPath)), cancellationToken).ConfigureAwait(true);
        MoveDestination = string.Empty;
        StatusText = "העברה סומנה לשמירה";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task UndoSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        if (entry?.PendingOperationId is not Guid operationId || IsBusy)
            return;

        if (await _staging.UndoAsync(operationId, cancellationToken).ConfigureAwait(true))
            StatusText = "השינוי בוטל";
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task SetInitialPathAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        CurrentPath = Path.GetFullPath(path);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    private static StagingOperation CreateOperation(OperationType type, string target, StagingPayload payload)
        => new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            type,
            target,
            nameof(MediaState.Missing),
            nameof(MediaState.Pending),
            payload);

    private StagingOperation? FindPendingOperation(string path)
        => _staging.Operations.LastOrDefault(operation =>
            string.Equals(operation.Payload?.DestinationPath, path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operation.Payload?.DirectoryPath, path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operation.Payload?.SourcePath, path, StringComparison.OrdinalIgnoreCase));

    private static bool IsSameOrChildPath(string candidate, string parent)
    {
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
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

    [ObservableProperty]
    private bool _markedForDeletion;

    public string KindText => IsDirectory ? "תיקייה" : "קובץ";
    public string SizeText => IsDirectory ? "—" : FormatBytes(Size);
    public string StatusText => MarkedForDeletion ? "מחיקה ממתינה" : IsPending ? "שינוי ממתין" : "מסונכרן";

    public ExplorerEntryViewModel(ExplorerEntry entry, bool isPending, Guid? pendingOperationId)
    {
        Name = entry.Name;
        FullPath = entry.FullPath;
        IsDirectory = entry.IsDirectory;
        Size = entry.Size;
        IsPending = isPending;
        PendingOperationId = pendingOperationId;
    }

    partial void OnMarkedForDeletionChanged(bool value)
        => OnPropertyChanged(nameof(StatusText));

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.0} GB";
    }
}
