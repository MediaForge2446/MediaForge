using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Infrastructure.FileSystem;

public sealed class WindowsExplorerService : IExplorerService
{
    public Task<IReadOnlyList<ExplorerEntry>> ListAsync(string directoryPath, CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<ExplorerEntry>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(directoryPath.Trim());
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
            var entries = new List<ExplorerEntry>();
            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new DirectoryInfo(directory);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                entries.Add(new ExplorerEntry(info.Name, info.FullName, true, 0, info.LastWriteTimeUtc));
            }
            foreach (var file in Directory.EnumerateFiles(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(file);
                entries.Add(new ExplorerEntry(info.Name, info.FullName, false, info.Length, info.LastWriteTimeUtc));
            }
            return entries.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }, cancellationToken);
}
