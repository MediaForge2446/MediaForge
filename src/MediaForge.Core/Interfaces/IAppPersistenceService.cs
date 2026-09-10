using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IAppPersistenceService
{
    Task<AppStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppStateSnapshot snapshot, CancellationToken cancellationToken = default);
}
