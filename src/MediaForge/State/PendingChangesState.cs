using MediaForge.Core.Enums;
using MediaForge.Core.Models;

namespace MediaForge.State;

public sealed class PendingChangesState
{
    private readonly object _sync = new();
    private readonly List<PendingChange> _changes = new();

    public event EventHandler? Changed;

    public IReadOnlyList<PendingChange> Changes
    {
        get
        {
            lock (_sync)
            {
                return _changes.ToArray();
            }
        }
    }

    public void Add(PendingChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        lock (_sync)
        {
            _changes.Add(change);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(Guid changeId)
    {
        var removed = false;

        lock (_sync)
        {
            var index = _changes.FindIndex(change => change.Id == changeId);
            if (index >= 0)
            {
                _changes.RemoveAt(index);
                removed = true;
            }
        }

        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    public void Replace(IEnumerable<PendingChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        lock (_sync)
        {
            _changes.Clear();
            _changes.AddRange(changes);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public int PendingCount => Changes.Count(change => change.Status == ChangeStatus.Pending);
}
