using MediaForge.Core.Interfaces;
using CoreLibrary = MediaForge.Core.Models.Library;
using MediaForge.Core.Models;

namespace MediaForge.Application.Library;

public sealed class LibraryService
{
    private readonly ILibraryRepository _repository;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LibraryService(ILibraryRepository repository)
    {
        _repository = repository;
    }

    public async Task<CoreLibrary> LoadAsync(CancellationToken cancellationToken = default)
        => await _repository.LoadAsync(cancellationToken).ConfigureAwait(false) ?? new CoreLibrary();

    public async Task<RootFolder> AddRootFolderAsync(
        CoreLibrary library,
        string path,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A root folder path is required.", nameof(path));

        var normalizedPath = Path.GetFullPath(path.Trim());
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (library.RootFolders.Any(x => string.Equals(x.Path, normalizedPath, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("The root folder is already in the library.");

            var folder = new RootFolder
            {
                Path = normalizedPath,
                Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(normalizedPath.TrimEnd(Path.DirectorySeparatorChar)) : name.Trim()
            };

            library.RootFolders.Add(folder);
            await _repository.SaveAsync(library, cancellationToken).ConfigureAwait(false);
            return folder;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveRootFolderAsync(
        CoreLibrary library,
        Guid rootFolderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(library);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var removed = library.RootFolders.RemoveAll(x => x.Id == rootFolderId) > 0;
            if (!removed)
                throw new KeyNotFoundException("The root folder was not found.");

            await _repository.SaveAsync(library, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
