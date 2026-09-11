using System.Collections.ObjectModel;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.State;

namespace MediaForge.ViewModels;

public sealed class ExplorerViewModel : ViewModelBase, IDisposable
{
    private readonly IFileSystemService _fileSystem;
    private readonly PendingChangesState _pendingChanges;
    private readonly LibraryState _library;
    private readonly IStagingHistory _history;
    private readonly MainViewModel _main;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private string? _currentPath;
    private bool _isLoading;
    private bool _disposed;

    public ObservableCollection<FileItem> Items { get; } = new();
    public ObservableCollection<MediaFolder> RootFolders { get; } = new();

    public string? CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (SetProperty(ref _currentPath, value))
            {
                OnPropertyChanged(nameof(CurrentFolderName));
                OnPropertyChanged(nameof(CanGoBack));
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string CurrentFolderName =>
        string.IsNullOrWhiteSpace(CurrentPath)
            ? "Select a library folder"
            : new DirectoryInfo(CurrentPath).Name;

    public bool CanGoBack =>
        !string.IsNullOrWhiteSpace(CurrentPath) && Directory.GetParent(CurrentPath!) is not null;

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

    public async Task OpenFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);

        if (!_fileSystem.DirectoryExists(fullPath))
        {
            _main.ReportError(new DirectoryNotFoundException($"Directory not found: {fullPath}"));
            return;
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IsLoading = true;
            var physicalItems = await _fileSystem
                .GetDirectoryItemsAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var projectedItems = ProjectPendingChanges(fullPath, physicalItems);

            await App.MainWindowDispatcherAsync(() =>
            {
                Items.Clear();
                foreach (var item in projectedItems)
                {
                    Items.Add(item);
                }

                CurrentPath = fullPath;
            }).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
        finally
        {
            IsLoading = false;
            _refreshGate.Release();
        }
    }

    public void OpenFolder(string path)
    {
        _ = OpenFolderAsync(path, _lifetimeCts.Token);
    }

    public void CreateFolder(string name)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(CurrentPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            var safeName = name.Trim();
            ValidateFileName(safeName, "folder");
            var path = Path.Combine(CurrentPath!, safeName);
            EnsurePendingTargetAvailable(path);
            AddPendingChange(new PendingChange
            {
                Id = Guid.NewGuid(), Type = ChangeType.CreateFolder,
                Status = ChangeStatus.Pending, SourcePath = path,
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
            AddPendingChange(new PendingChange
            {
                Id = Guid.NewGuid(), Type = ChangeType.Delete,
                Status = ChangeStatus.Pending, SourcePath = item.FullPath,
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
            ValidateFileName(trimmedName, "name");
            var parent = Path.GetDirectoryName(item.FullPath)
                         ?? throw new InvalidOperationException("Unable to determine the parent directory.");
            var targetPath = Path.Combine(parent, trimmedName);
            if (string.Equals(item.FullPath, targetPath, StringComparison.OrdinalIgnoreCase)) return;
            EnsurePendingTargetAvailable(targetPath);
            AddPendingChange(new PendingChange
            {
                Id = Guid.NewGuid(), Type = ChangeType.Rename,
                Status = ChangeStatus.Pending, SourcePath = item.FullPath,
                TargetPath = targetPath, CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    public void Move(FileItem item, string targetFolderPath)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetFolderPath);
            var destinationFolder = Path.GetFullPath(targetFolderPath);
            var pendingFolder = _pendingChanges.Changes.Any(change =>
                change.Type == ChangeType.CreateFolder &&
                string.Equals(change.SourcePath, destinationFolder, StringComparison.OrdinalIgnoreCase));

            if (!_fileSystem.DirectoryExists(destinationFolder) && !pendingFolder)
            {
                throw new DirectoryNotFoundException($"Destination directory not found: {destinationFolder}");
            }

            var targetPath = Path.Combine(destinationFolder, item.Name);
            EnsurePendingTargetAvailable(targetPath);
            AddPendingChange(new PendingChange
            {
                Id = Guid.NewGuid(), Type = ChangeType.Move,
                Status = ChangeStatus.Pending, SourcePath = item.FullPath,
                TargetPath = targetPath, CreatedAtUtc = DateTimeOffset.UtcNow
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
            _ = OpenFolderAsync(CurrentPath, _lifetimeCts.Token);
        }
    }

    private void AddPendingChange(PendingChange change)
    {
        _history.Record(change);
        _pendingChanges.Add(change);
    }

    private IReadOnlyList<FileItem> ProjectPendingChanges(string folderPath, IReadOnlyList<FileItem> physicalItems)
    {
        var items = physicalItems.ToList();
        var pending = _pendingChanges.Changes
            .Where(change => change.Status == ChangeStatus.Pending)
            .OrderBy(change => change.CreatedAtUtc)
            .ToArray();

        foreach (var change in pending)
        {
            switch (change.Type)
            {
                case ChangeType.Delete:
                    RemoveByPath(items, change.SourcePath);
                    break;
                case ChangeType.Rename:
                case ChangeType.Move:
                    RemoveByPath(items, change.SourcePath);
                    if (IsDirectChild(change.TargetPath, folderPath))
                    {
                        RemoveByPath(items, change.TargetPath);
                        var source = physicalItems.FirstOrDefault(item =>
                            string.Equals(item.FullPath, change.SourcePath, StringComparison.OrdinalIgnoreCase));
                        if (source is not null && !string.IsNullOrWhiteSpace(change.TargetPath))
                        {
                            items.Add(source with
                            {
                                Name = Path.GetFileName(change.TargetPath) ?? source.Name,
                                FullPath = change.TargetPath,
                                Status = ChangeStatus.Pending
                            });
                        }
                    }
                    break;
                case ChangeType.CreateFolder when IsDirectChild(change.SourcePath, folderPath):
                    if (!string.IsNullOrWhiteSpace(change.SourcePath))
                    {
                        items.Add(new FileItem
                        {
                            Name = Path.GetFileName(change.SourcePath) ?? "New folder",
                            FullPath = change.SourcePath,
                            Kind = FileItemKind.Folder,
                            Status = ChangeStatus.Pending,
                            LastModifiedUtc = DateTimeOffset.UtcNow
                        });
                    }
                    break;
                case ChangeType.Download when IsDirectChild(change.TargetPath, folderPath):
                    if (!string.IsNullOrWhiteSpace(change.TargetPath))
                    {
                        items.Add(new FileItem
                        {
                            Name = Path.GetFileName(change.TargetPath) ?? "Download",
                            FullPath = change.TargetPath,
                            Kind = FileItemKind.File,
                            Status = ChangeStatus.Pending,
                            LastModifiedUtc = DateTimeOffset.UtcNow
                        });
                    }
                    break;
            }
        }

        return items
            .OrderBy(item => item.Kind == FileItemKind.File)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void RemoveByPath(List<FileItem> items, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            items.RemoveAll(item => string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static bool IsDirectChild(string? path, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalizedFolder = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(normalizedPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(parent, normalizedFolder, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshRootFolders()
    {
        RootFolders.Clear();
        foreach (var folder in _library.RootFolders)
        {
            RootFolders.Add(folder);
        }
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

    private static void ValidateFileName(string name, string kind)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
        {
            throw new ArgumentException($"The {kind} name contains invalid characters.", nameof(name));
        }
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => RefreshRootFolders();

    private void OnPendingChangesChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            _ = OpenFolderAsync(CurrentPath, _lifetimeCts.Token);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _library.Changed -= OnLibraryChanged;
        _pendingChanges.Changed -= OnPendingChangesChanged;
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        _refreshGate.Dispose();
    }
}
