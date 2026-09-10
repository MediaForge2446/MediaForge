using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Services;

public sealed class StagingHistory : IStagingHistory
{
    private readonly object _sync = new();
    private readonly List<PendingChange> _history = new();

    public IReadOnlyList<PendingChange> Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _history.ToArray();
            }
        }
    }

    public void Record(PendingChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        lock (_sync)
        {
            _history.Add(change);
        }
    }

    public bool Remove(Guid changeId, out PendingChange? removedChange)
    {
        lock (_sync)
        {
            var index = _history.FindLastIndex(change => change.Id == changeId);
            if (index < 0)
            {
                removedChange = null;
                return false;
            }

            removedChange = _history[index];
            _history.RemoveAt(index);
            return true;
        }
    }

    public void Restore(PendingChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        lock (_sync)
        {
            if (_history.Any(existing => existing.Id == change.Id))
            {
                return;
            }

            _history.Add(change);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _history.Clear();
        }
    }
}
