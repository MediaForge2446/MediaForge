using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface ICommitService
{
    Task<IReadOnlyList<PendingChange>> CommitAsync(
        IReadOnlyList<PendingChange> changes,
        IProgress<CommitProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
