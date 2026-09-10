using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IStagingHistory
{
    IReadOnlyList<PendingChange> Snapshot { get; }
    void Record(PendingChange change);
    bool Remove(Guid changeId, out PendingChange? removedChange);
    void Restore(PendingChange change);
    void Clear();
}
