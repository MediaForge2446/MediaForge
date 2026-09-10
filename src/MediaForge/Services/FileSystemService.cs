using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Services;

public sealed class FileSystemService : IFileSystemService
{
    public IReadOnlyList<FileItem> GetDirectoryItems(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var directoryInfo = new DirectoryInfo(directoryPath);
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

        return items
            .OrderBy(item => item.Kind == FileItemKind.File)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool DirectoryExists(string directoryPath) =>
        !string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath);

    public bool FileExists(string filePath) =>
        !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);

    public void CreateDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        Directory.CreateDirectory(directoryPath);
    }

    public void Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (File.Exists(path))
        {
            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            return;
        }

        throw new FileNotFoundException($"Path not found: {path}", path);
    }

    public void Move(string sourcePath, string destinationPath)
    {
        ValidatePaths(sourcePath, destinationPath);

        if (File.Exists(sourcePath))
        {
            EnsureDestinationParent(destinationPath);
            File.Move(sourcePath, destinationPath);
            return;
        }

        if (Directory.Exists(sourcePath))
        {
            EnsureDestinationParent(destinationPath);
            Directory.Move(sourcePath, destinationPath);
            return;
        }

        throw new FileNotFoundException($"Source path not found: {sourcePath}", sourcePath);
    }

    public void Rename(string sourcePath, string destinationPath)
    {
        ValidatePaths(sourcePath, destinationPath);

        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath);
            return;
        }

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
            return;
        }

        throw new FileNotFoundException($"Source path not found: {sourcePath}", sourcePath);
    }

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
        var parent = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        Directory.CreateDirectory(parent);
    }
}
