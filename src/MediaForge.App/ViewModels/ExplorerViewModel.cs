using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.Application.Abstractions;
using MediaForge.Application.Staging;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.App.ViewModels;

public partial class ExplorerViewModel : ObservableObject
{
    private readonly IExplorerService _explorer;
    private readonly StagingService _staging;

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

    public ExplorerViewModel(IExplorerService explorer, StagingService staging)
    {
        _explorer = explorer;
        _staging = staging;
    }

    public async Task InitializeAsync(string? initialPath = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(initialPath))
            CurrentPath = Path.GetFullPath(initialPath);
        if (!string.IsNullOrWhiteSpace(CurrentPath))
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenAsync(ExplorerEntryViewModel? entry, CancellationToken cancellationToken)
    {
        if (entry is null || !entry.IsDirectory || IsBusy)
            return;

        CurrentPath = entry.FullPath;
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
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
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var entries = await _explorer.ListAsync(CurrentPath, cancellationToken).ConfigureAwait(true);
            Entries.Clear();
            foreach (var entry in entries)
            {
                var pending = FindPendingOperation(entry.FullPath);
                Entries.Add(new ExplorerEntryViewModel(entry, pending is not null, pending?.OperationId));
            }
            StatusText = $"{Entries.Count} פריטים";
            OnPropertyChanged(nameof(CanGoUp));
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
        if (_staging.Operations.Any(x => string.Equals(x.Payload?.DirectoryPath, target, StringComparison.OrdinalIgnoreCase)) ||
            Directory.Exists(target))
        {
            StatusText = "התיקייה כבר קיימת";
            return;
        }

        await _staging.StageAsync(new StagingOperation
        {
            OperationType = OperationType.CreateDirectory,
            Payload = new StagingPayload(DirectoryPath: target),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken).ConfigureAwait(true);

        NewFolderName = string.Empty;
        StatusText = "תיקייה נוספה לשינויים ממתינים";
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        if (entry is null || IsBusy)
            return;

        await _staging.StageAsync(new StagingOperation
        {
            OperationType = OperationType.Delete,
            Payload = new StagingPayload(SourcePath: entry.FullPath, Recursive: entry.IsDirectory),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken).ConfigureAwait(true);

        StatusText = "מחיקה סומנה לשמירה";
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
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

        await _staging.StageAsync(new StagingOperation
        {
            OperationType = OperationType.Rename,
            Payload = new StagingPayload(SourcePath: entry.FullPath, NewName: name),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken).ConfigureAwait(true);

        NewName = string.Empty;
        StatusText = "שינוי השם סומן לשמירה";
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MoveSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        var destination = MoveDestination.Trim();
        if (entry is null || string.IsNullOrWhiteSpace(destination) || IsBusy)
            return;

        var destinationPath = Path.GetFullPath(destination);
        if (entry.IsDirectory && destinationPath.StartsWith(entry.FullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "אי אפשר להעביר תיקייה לתוך עצמה";
            return;
        }

        await _staging.StageAsync(new StagingOperation
        {
            OperationType = OperationType.Move,
            Payload = new StagingPayload(SourcePath: entry.FullPath, DestinationPath: destinationPath),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken).ConfigureAwait(true);

        MoveDestination = string.Empty;
        StatusText = "העברה סומנה לשמירה";
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task UndoSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        if (entry?.PendingOperationId is not Guid operationId || IsBusy)
            return;

        if (await _staging.UndoAsync(operationId, cancellationToken).ConfigureAwait(true))
            StatusText = "השינוי בוטל";
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task SetInitialPathAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        CurrentPath = Path.GetFullPath(path);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    private StagingOperation? FindPendingOperation(string path)
        => _staging.Operations.LastOrDefault(operation =>
            string.Equals(operation.Payload?.DestinationPath, path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operation.Payload?.DirectoryPath, path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operation.Payload?.SourcePath, path, StringComparison.OrdinalIgnoreCase));
}

public sealed class ExplorerEntryViewModel : ObservableObject
{
    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public long Size { get; }
    public bool IsPending { get; }
    public Guid? PendingOperationId { get; }

    public string KindText => IsDirectory ? "תיקייה" : "קובץ";
    public string SizeText => IsDirectory ? "—" : FormatBytes(Size);
    public string StatusText => IsPending ? "ממתין לשמירה" : "מסונכרן";

    public ExplorerEntryViewModel(ExplorerEntry entry, bool isPending, Guid? pendingOperationId)
    {
        Name = entry.Name;
        FullPath = entry.FullPath;
        IsDirectory = entry.IsDirectory;
        Size = entry.Size;
        IsPending = isPending;
        PendingOperationId = pendingOperationId;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.0} GB";
    }
}
