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
    private readonly MainViewModel _main;

    public ObservableCollection<FileItem> Items { get; } = new();
    public ObservableCollection<MediaFolder> RootFolders { get; } = new();

    private string? _currentPath;
    public string? CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    public ExplorerViewModel(
        IFileSystemService fileSystem,
        PendingChangesState pendingChanges,
        LibraryState library,
        MainViewModel main)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _pendingChanges = pendingChanges ?? throw new ArgumentNullException(nameof(pendingChanges));
        _library = library ?? throw new ArgumentNullException(nameof(library));
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
            if (safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("The folder name contains invalid characters.", nameof(name));
            }

            var path = Path.Combine(CurrentPath, safeName);
            EnsurePendingTargetAvailable(path);

            _pendingChanges.Add(new PendingChange
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

            _pendingChanges.Add(new PendingChange
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
            if (trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("The name contains invalid characters.", nameof(newName));
            }

            var parent = Path.GetDirectoryName(item.FullPath)
                ?? throw new InvalidOperationException("Unable to determine the parent directory.");
            var targetPath = Path.Combine(parent, trimmedName);
            if (string.Equals(item.FullPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            EnsurePendingTargetAvailable(targetPath);

            _pendingChanges.Add(new PendingChange
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

    public void Refresh()
    {
        RefreshRootFolders();
        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            OpenFolder(CurrentPath);
        }
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

    private void OnLibraryChanged(object? sender, EventArgs e) => RefreshRootFolders();

    private void OnPendingChangesChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            OpenFolder(CurrentPath);
        }
    }
}
