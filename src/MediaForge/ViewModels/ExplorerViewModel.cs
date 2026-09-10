using System.Collections.ObjectModel;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.State;

namespace MediaForge.ViewModels;

public sealed class ExplorerViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystem;
    private readonly PendingChangesState _pendingChanges;
    private readonly LibraryState _library;
    private readonly IStagingHistory _history;
    private readonly MainViewModel _main;

    public ObservableCollection<FileItem> Items { get; } = new();
    public ObservableCollection<MediaFolder> RootFolders { get; } = new();

    private string? _currentPath;
    public string? CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    public bool CanUndo => _history.Snapshot.Count > 0;

    public ExplorerViewModel(
        IFileSystemService fileSystem,
        PendingChangesState pendingChanges,
        LibraryState library,
        IStagingHistory history,
        MainViewModel main)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _pendingChanges = pendingChanges ?? throw new ArgumentNullException(nameof(pendingChanges));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _main = main ?? throw new ArgumentNullException(nameof(main));

        RefreshRootFolders();
        _library.Changed += OnLibraryChanged;
        _pendingChanges.Changed += OnPendingChangesChanged;
    }

    public void OpenFolder(string path)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var fullPath = Path.GetFullPath(path);
            if (!_fileSystem.DirectoryExists(fullPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
            }

            var items = _fileSystem.GetDirectoryItems(fullPath);
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item with { Status = GetStatus(item.FullPath) });
            }

            CurrentPath = fullPath;
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    public void CreateFolder(string name)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(CurrentPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var safeName = name.Trim();
            ValidateFileName(safeName, nameof(name));

            var path = Path.Combine(CurrentPath, safeName);
            EnsurePendingTargetAvailable(path);

            AddPending(new PendingChange
            {
                Id = Guid.NewGuid(),
                Type = ChangeType.CreateFolder,
                Status = ChangeStatus.Pending,
                SourcePath = path,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    public void Delete(FileItem item)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(item);

            if (_pendingChanges.Changes.Any(change =>
                    change.Status == ChangeStatus.Pending &&
                    string.Equals(change.SourcePath, item.FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("This item already has a pending change.");
            }

            AddPending(new PendingChange
            {
                Id = Guid.NewGuid(),
                Type = ChangeType.Delete,
                Status = ChangeStatus.Pending,
                SourcePath = item.FullPath,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    public void Rename(FileItem item, string newName)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentException.ThrowIfNullOrWhiteSpace(newName);

            var trimmedName = newName.Trim();
            ValidateFileName(trimmedName, nameof(newName));

            var parent = Path.GetDirectoryName(item.FullPath)
                ?? throw new InvalidOperationException("Unable to determine the parent directory.");
            var targetPath = Path.Combine(parent, trimmedName);
            if (string.Equals(item.FullPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            EnsurePendingTargetAvailable(targetPath);

            AddPending(new PendingChange
            {
                Id = Guid.NewGuid(),
                Type = ChangeType.Rename,
                Status = ChangeStatus.Pending,
                SourcePath = item.FullPath,
                TargetPath = targetPath,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    public void Move(FileItem item, string destinationDirectory)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

            var destination = Path.GetFullPath(destinationDirectory.Trim());
            if (!_fileSystem.DirectoryExists(destination))
            {
                throw new DirectoryNotFoundException($"Destination directory not found: {destination}");
            }

            var targetPath = Path.Combine(destination, item.Name);
            var sourcePath = Path.GetFullPath(item.FullPath);

            if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(sourcePath, destination, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("An item cannot be moved into itself.");
            }

            if (item.Kind == FileItemKind.Folder && IsSubPath(destination, sourcePath))
            {
                throw new InvalidOperationException("A folder cannot be moved into one of its own descendants.");
            }

            EnsurePendingTargetAvailable(targetPath);

            AddPending(new PendingChange
            {
                Id = Guid.NewGuid(),
                Type = ChangeType.Move,
                Status = ChangeStatus.Pending,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    public bool Undo(Guid changeId)
    {
        try
        {
            if (!_history.Remove(changeId, out var removed) || removed is null)
            {
                return false;
            }

            if (!_pendingChanges.Remove(removed.Id))
            {
                _history.Restore(removed);
                return false;
            }

            Refresh();
            OnPropertyChanged(nameof(CanUndo));
            return true;
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
            return false;
        }
    }

    public bool UndoLast()
    {
        try
        {
            var last = _history.Snapshot.LastOrDefault();
            return last is not null && Undo(last.Id);
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
            return false;
        }
    }

    public void CancelAllPendingChanges()
    {
        try
        {
            var pending = _pendingChanges.Changes
                .Where(change => change.Status == ChangeStatus.Pending)
                .Select(change => change.Id)
                .ToArray();

            foreach (var changeId in pending)
            {
                _pendingChanges.Remove(changeId);
                _history.Remove(changeId, out _);
            }

            Refresh();
            OnPropertyChanged(nameof(CanUndo));
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    public void Refresh()
    {
        RefreshRootFolders();
        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            OpenFolder(CurrentPath);
        }

        OnPropertyChanged(nameof(CanUndo));
    }

    private void AddPending(PendingChange change)
    {
        _pendingChanges.Add(change);
        _history.Record(change);
        OnPropertyChanged(nameof(CanUndo));
    }

    private void RefreshRootFolders()
    {
        RootFolders.Clear();
        foreach (var folder in _library.RootFolders)
        {
            RootFolders.Add(folder);
        }
    }

    private ChangeStatus GetStatus(string path)
    {
        var pending = _pendingChanges.Changes.FirstOrDefault(change =>
            change.Status != ChangeStatus.Synced &&
            (string.Equals(change.SourcePath, path, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(change.TargetPath, path, StringComparison.OrdinalIgnoreCase)));

        return pending?.Status ?? ChangeStatus.Synced;
    }

    private void EnsurePendingTargetAvailable(string path)
    {
        var normalized = Path.GetFullPath(path);
        if (_fileSystem.FileExists(normalized) || _fileSystem.DirectoryExists(normalized))
        {
            throw new IOException($"The path already exists: {normalized}");
        }

        if (_pendingChanges.Changes.Any(change =>
                string.Equals(change.SourcePath, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(change.TargetPath, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new IOException("A pending change already targets this path.");
        }
    }

    private static bool IsSubPath(string childPath, string parentPath)
    {
        var child = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateFileName(string value, string parameterName)
    {
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The name contains invalid characters.", parameterName);
        }
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => RefreshRootFolders();

    private void OnPendingChangesChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            OpenFolder(CurrentPath);
        }
    }
}
