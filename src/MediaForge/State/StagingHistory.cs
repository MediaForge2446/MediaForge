using MediaForge.Core.Models;

namespace MediaForge.State;

public sealed class StagingHistory : IStagingHistory
{
    private readonly object _sync = new();
    private readonly List<PendingChange> _history = new();

    public void Record(PendingChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        lock (_sync)
        {
            _history.RemoveAll(existing => existing.Id == change.Id);
            _history.Add(change);
        }
    }

    public bool Remove(Guid changeId, out PendingChange? change)
    {
        lock (_sync)
        {
            for (var index = _history.Count - 1; index >= 0; index--)
            {
                if (_history[index].Id != changeId)
                {
                    continue;
                }

                change = _history[index];
                _history.RemoveAt(index);
                return true;
            }
        }

        change = null;
        return false;
    }

    public bool TryGetLastPending(out PendingChange? change)
    {
        lock (_sync)
        {
            for (var index = _history.Count - 1; index >= 0; index--)
            {
                var candidate = _history[index];
                if (candidate.Status == Core.Enums.ChangeStatus.Pending)
                {
                    change = candidate;
                    return true;
                }
            }
        }

        change = null;
        return false;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _history.Clear();
        }
    }
}