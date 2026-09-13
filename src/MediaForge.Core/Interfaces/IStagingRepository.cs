using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IStagingRepository
{
    Task<IReadOnlyList<StagingOperation>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<StagingOperation> operations, CancellationToken cancellationToken = default);
}
