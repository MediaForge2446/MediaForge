using MediaForge.Core.Models;

namespace MediaForge.State;

public sealed class StagingHistory : IStagingHistory
{
    private readonly object _gate = new();
    private readonly List<PendingChange> _entries = new();

    public void Record(PendingChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        lock (_gate)
        {
            _entries.RemoveAll(entry => entry.Id == change.Id);
            _entries.Add(change);
        }
    }

    public bool Remove(Guid changeId, out PendingChange? change)
    {
        lock (_gate)
        {
            var index = _entries.FindIndex(entry => entry.Id == changeId);
            if (index < 0)
            {
                change = null;
                return false;
            }

            change = _entries[index];
            _entries.RemoveAt(index);
            return true;
        }
    }

    public bool TryGetLastPending(out PendingChange? change)
    {
        lock (_gate)
        {
            for (var index = _entries.Count - 1; index >= 0; index--)
            {
                var candidate = _entries[index];
                if (candidate.Status == MediaForge.Core.Enums.ChangeStatus.Pending)
                {
                    change = candidate;
                    return true;
                }
            }

            change = null;
            return false;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}
