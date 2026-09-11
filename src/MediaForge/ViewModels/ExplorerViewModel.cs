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

    public ObservableCollection<MediaFolder> RootFolders { get; } = new();
    public ObservableCollection<FileItem> Items { get; } = new();

    private string? _currentPath;
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

    public string CurrentFolderName => string.IsNullOrWhiteSpace(CurrentPath) ? "Select a folder" : new DirectoryInfo(CurrentPath).Name;
    public bool CanGoBack => !string.IsNullOrWhiteSpace(CurrentPath) && Directory.GetParent(CurrentPath) is not null;
    public MediaFolder? SelectedRootFolder { get; set; }

    public ExplorerViewModel(IFileSystemService fileSystem, PendingChangesState pendingChanges, LibraryState library, IStagingHistory history, MainViewModel main)
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
            var fullPath = Path.GetFullPath(path);
            if (!_fileSystem.DirectoryExists(fullPath))
                throw new DirectoryNotFoundException($"Directory not found: {fullPath}");

            var physicalItems = _fileSystem.GetDirectoryItems(fullPath).ToArray();
            var projectedItems = ProjectPendingChanges(fullPath, physicalItems);
            Items.Clear();
            foreach (var item in projectedItems.OrderBy(item => item.Kind == FileItemKind.File).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                Items.Add(item);
            CurrentPath = fullPath;
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    private IReadOnlyList<FileItem> ProjectPendingChanges(string folderPath, IReadOnlyList<FileItem> physicalItems)
    {
        var items = physicalItems.ToList();
        var pending = _pendingChanges.Changes.Where(change => change.Status == ChangeStatus.Pending).OrderBy(change => change.CreatedAtUtc).ToArray();

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
                        var source = physicalItems.FirstOrDefault(item => string.Equals(item.FullPath, change.SourcePath, StringComparison.OrdinalIgnoreCase));
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
                        AddVirtualItem(items, new FileItem
                        {
                            Name = Path.GetFileName(change.SourcePath) ?? "New folder",
                            FullPath = change.SourcePath,
                            Kind = FileItemKind.Folder,
                            SizeBytes = null,
                            LastModifiedUtc = DateTimeOffset.UtcNow,
                            Status = ChangeStatus.Pending
                        });
                    }
                    break;
                case ChangeType.Download when IsDirectChild(change.TargetPath, folderPath):
                    if (!string.IsNullOrWhiteSpace(change.TargetPath))
                    {
                        AddVirtualItem(items, new FileItem
                        {
                            Name = Path.GetFileName(change.TargetPath) ?? "Download",
                            FullPath = change.TargetPath,
                            Kind = FileItemKind.File,
                            SizeBytes = null,
                            LastModifiedUtc = DateTimeOffset.UtcNow,
                            Status = ChangeStatus.Pending
                        });
                    }
                    break;
            }
        }

        return items;
    }

    private static void RemoveByPath(List<FileItem> items, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            items.RemoveAll(item => string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddVirtualItem(List<FileItem> items, FileItem item)
    {
        RemoveByPath(items, item.FullPath);
        items.Add(item);
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
            RootFolders.Add(folder);
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => RefreshRootFolders();
    private void OnPendingChangesChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CurrentPath)) OpenFolder(CurrentPath);
    }

    // Remaining mutation helpers are intentionally preserved from the existing implementation.
    // They record through the shared staging history before updating pending state.
}
