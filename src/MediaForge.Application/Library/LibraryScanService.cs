using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Library;

/// <summary>Reads the physical filesystem through the explorer abstraction and never mutates user files.</summary>
public sealed class LibraryScanService
{
    private readonly IExplorerService _explorer;
    public LibraryScanService(IExplorerService explorer) => _explorer = explorer;

    public async Task<LibraryScanResult> ScanAsync(RootFolder root, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        var folderCount = 0;
        var fileCount = 0;
        var mediaCount = 0;
        long totalBytes = 0;
        var stack = new Stack<string>();
        stack.Push(root.Path);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = stack.Pop();
            IReadOnlyList<ExplorerEntry> entries;
            try { entries = await _explorer.ListAsync(directory, cancellationToken).ConfigureAwait(false); }
            catch (DirectoryNotFoundException)
            {
                if (directory == root.Path) return new LibraryScanResult(root.Id, root.Path, false, 0, 0, 0, 0, DateTimeOffset.Now);
                continue;
            }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.IsDirectory) { folderCount++; stack.Push(entry.FullPath); continue; }
                fileCount++;
                totalBytes = checked(totalBytes + Math.Max(0, entry.Size));
                if (IsMediaFile(entry.Name)) mediaCount++;
            }
        }
        return new LibraryScanResult(root.Id, root.Path, true, folderCount, fileCount, mediaCount, totalBytes, DateTimeOffset.Now);
    }

    private static bool IsMediaFile(string name)
        => name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".aac", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
}

public sealed record LibraryScanResult(Guid RootFolderId, string Path, bool Exists, int FolderCount, int FileCount, int MediaCount, long TotalBytes, DateTimeOffset? ScannedAt = null)
{
    public string TotalSizeText => FormatBytes(TotalBytes);
    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.0} GB";
    }
}
