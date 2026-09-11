using MediaForge.Core.Models;

namespace MediaForge.State;

public interface IStagingHistory
{
    void Record(PendingChange change);

    bool Remove(Guid changeId, out PendingChange? change);

    bool TryGetLastPending(out PendingChange? change);

    void Clear();
}