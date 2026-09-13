using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Infrastructure.Persistence;

public sealed class JsonStagingRepository : IStagingRepository
{
    private readonly AtomicJsonStore _store;
    private readonly string _path;

    public JsonStagingRepository(AtomicJsonStore store, LocalAppPaths paths)
    {
        _store = store;
        _path = paths.StagingFilePath;
    }

    public async Task<IReadOnlyList<StagingOperation>> LoadAsync(CancellationToken cancellationToken = default)
        => await _store.LoadAsync<List<StagingOperation>>(_path, cancellationToken).ConfigureAwait(false) ?? [];

    public Task SaveAsync(IReadOnlyList<StagingOperation> operations, CancellationToken cancellationToken = default)
        => _store.SaveAsync(_path, operations.ToList(), cancellationToken);
}
