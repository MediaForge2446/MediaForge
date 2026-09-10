using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface ICommitService
{
    Task<IReadOnlyList<PendingChange>> CommitAsync(
        IReadOnlyList<PendingChange> changes,
        CancellationToken cancellationToken = default);
}
