using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Services;

public sealed class FileSystemService : IFileSystemService
{
    public IReadOnlyList<FileItem> GetDirectoryItems(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        var fullPath = Path.GetFullPath(directoryPath);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        var directoryInfo = new DirectoryInfo(fullPath);
        var items = new List<FileItem>();

        foreach (var directory in directoryInfo.EnumerateDirectories())
        {
            items.Add(new FileItem
            {
                Name = directory.Name,
                FullPath = directory.FullName,
                Kind = FileItemKind.Folder,
                SizeBytes = null,
                LastModifiedUtc = directory.LastWriteTimeUtc
            });
        }

        foreach (var file in directoryInfo.EnumerateFiles())
        {
            items.Add(new FileItem
            {
                Name = file.Name,
                FullPath = file.FullName,
                Kind = FileItemKind.File,
                SizeBytes = file.Length,
                LastModifiedUtc = file.LastWriteTimeUtc
            });
        }

        return SortItems(items);
    }

    public Task<IReadOnlyList<FileItem>> GetDirectoryItemsAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => GetDirectoryItems(directoryPath), cancellationToken);
    }

    public bool DirectoryExists(string directoryPath) =>
        !string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath);

    public bool FileExists(string filePath) =>
        !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);

    public void CreateDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        Directory.CreateDirectory(Path.GetFullPath(directoryPath));
    }

    public void Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return;
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
            return;
        }

        throw new FileNotFoundException($"Path not found: {fullPath}", fullPath);
    }

    public void Move(string sourcePath, string destinationPath)
    {
        ValidatePaths(sourcePath, destinationPath);
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        EnsureDestinationParent(destination);

        if (File.Exists(source))
        {
            File.Move(source, destination);
            return;
        }

        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
            return;
        }

        throw new FileNotFoundException($"Source path not found: {source}", source);
    }

    public void Rename(string sourcePath, string destinationPath)
    {
        ValidatePaths(sourcePath, destinationPath);
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);

        if (File.Exists(source))
        {
            File.Move(source, destination);
            return;
        }

        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
            return;
        }

        throw new FileNotFoundException($"Source path not found: {source}", source);
    }

    private static IReadOnlyList<FileItem> SortItems(IEnumerable<FileItem> items) =>
        items.OrderBy(item => item.Kind == FileItemKind.File)
             .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
             .ToArray();

    private static void ValidatePaths(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Source and destination paths must differ.");
        }

        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException($"Destination already exists: {destination}");
        }
    }

    private static void EnsureDestinationParent(string destinationPath)
    {
        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }
}
