using System.IO;
using MediaForge.ViewModels;

namespace MediaForge.Services;

public sealed class FileSystemService
{
    public Task<IReadOnlyList<FileEntry>> ListAsync(string folder, CancellationToken token)
    {
        return Task.Run<IReadOnlyList<FileEntry>>(() =>
        {
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
            var info = new DirectoryInfo(folder);
            var result = new List<FileEntry>();
            foreach (var d in info.EnumerateDirectories())
            {
                token.ThrowIfCancellationRequested();
                result.Add(new FileEntry(d.Name, d.FullName, FileKind.Folder, 0, d.LastWriteTime, ChangeStatus.Synced));
            }
            foreach (var f in info.EnumerateFiles())
            {
                token.ThrowIfCancellationRequested();
                result.Add(new FileEntry(f.Name, f.FullName, FileKind.File, f.Length, f.LastWriteTime, ChangeStatus.Synced));
            }
            return (IReadOnlyList<FileEntry>)result.OrderBy(x => x.Kind == FileKind.File).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }, token);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken token) => Task.Run(() => Directory.CreateDirectory(path), token);
    public Task DeleteAsync(string path, CancellationToken token) => Task.Run(() =>
    {
        if (File.Exists(path)) { File.Delete(path); return; }
        if (Directory.Exists(path)) { Directory.Delete(path, true); return; }
        throw new FileNotFoundException(path);
    }, token);
    public Task MoveAsync(string source, string target, CancellationToken token) => Task.Run(() =>
    {
        source = Path.GetFullPath(source); target = Path.GetFullPath(target);
        if (File.Exists(target) || Directory.Exists(target)) throw new IOException($"Target already exists: {target}");
        var parent = Path.GetDirectoryName(target);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
        if (File.Exists(source)) File.Move(source, target); else if (Directory.Exists(source)) Directory.Move(source, target); else throw new FileNotFoundException(source);
    }, token);
}
