using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Infrastructure.Persistence;

public sealed class JsonLibraryRepository : ILibraryRepository
{
    private readonly AtomicJsonStore _store;
    private readonly string _path;

    public JsonLibraryRepository(AtomicJsonStore store, LocalAppPaths paths)
    {
        _store = store;
        _path = paths.LibraryFilePath;
    }

    public async Task<Library?> LoadAsync(CancellationToken cancellationToken = default)
        => await _store.LoadAsync<Library>(_path, cancellationToken).ConfigureAwait(false);

    public Task SaveAsync(Library library, CancellationToken cancellationToken = default)
        => _store.SaveAsync(_path, library, cancellationToken);
}
