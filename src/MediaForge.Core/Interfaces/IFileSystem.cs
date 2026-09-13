using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IFileSystem
{
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, bool recursive = false, CancellationToken cancellationToken = default);
    Task RenameAsync(string path, string newName, CancellationToken cancellationToken = default);
    Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
}
