using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Tests;

internal sealed class InMemoryStagingRepository : IStagingRepository
{
    private IReadOnlyList<StagingOperation> _operations = [];

    public Task<IReadOnlyList<StagingOperation>> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_operations);

    public Task SaveAsync(IReadOnlyList<StagingOperation> operations, CancellationToken cancellationToken = default)
    {
        _operations = operations.ToArray();
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryFileSystem : IFileSystem
{
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(_files.Contains(path));

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(_directories.Contains(path));

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        _directories.Add(path);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        _files.Remove(path);
        _directories.Remove(path);
        return Task.CompletedTask;
    }

    public Task RenameAsync(string path, string newName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (_files.Remove(sourcePath))
            _files.Add(destinationPath);
        if (_directories.Remove(sourcePath))
            _directories.Add(destinationPath);
        return Task.CompletedTask;
    }

    public void AddFile(string path) => _files.Add(path);
}

internal sealed class FakeMediaDownloader : IMediaDownloader
{
    private readonly InMemoryFileSystem _fileSystem;

    public FakeMediaDownloader(InMemoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Task DownloadAsync(
        string sourceUrl,
        string outputPath,
        MediaFormat format,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new DownloadProgress(100, "Completed"));
        _fileSystem.AddFile(outputPath);
        return Task.CompletedTask;
    }
}
