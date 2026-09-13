using MediaForge.Core.Interfaces;

namespace MediaForge.Infrastructure.FileSystem;

public sealed class WindowsFileSystem : IFileSystem
{
    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.Run(() => File.Exists(path), cancellationToken);

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.Run(() => Directory.Exists(path), cancellationToken);

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(path);
        }, cancellationToken);

    public Task DeleteAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                File.Delete(path);
                return;
            }

            if (Directory.Exists(path))
                Directory.Delete(path, recursive);
        }, cancellationToken);

    public Task RenameAsync(string path, string newName, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("The new name is invalid.", nameof(newName));

            var parent = Path.GetDirectoryName(path)
                ?? throw new IOException("The source path has no parent directory.");
            var destination = Path.Combine(parent, newName.Trim());

            if (File.Exists(path))
                File.Move(path, destination);
            else if (Directory.Exists(path))
                Directory.Move(path, destination);
            else
                throw new FileNotFoundException("The source path does not exist.", path);
        }, cancellationToken);

    public Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(sourcePath))
                File.Move(sourcePath, destinationPath);
            else if (Directory.Exists(sourcePath))
                Directory.Move(sourcePath, destinationPath);
            else
                throw new FileNotFoundException("The source path does not exist.", sourcePath);
        }, cancellationToken);
}
