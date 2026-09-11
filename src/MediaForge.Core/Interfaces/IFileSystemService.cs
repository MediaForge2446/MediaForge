using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IFileSystemService
{
    IReadOnlyList<FileItem> GetDirectoryItems(string directoryPath);
    Task<IReadOnlyList<FileItem>> GetDirectoryItemsAsync(string directoryPath, CancellationToken cancellationToken = default);
    bool DirectoryExists(string directoryPath);
    bool FileExists(string filePath);
    void CreateDirectory(string directoryPath);
    void Delete(string path);
    void Move(string sourcePath, string destinationPath);
    void Rename(string sourcePath, string destinationPath);
}
