using MediaForge.Core.Models;

namespace MediaForge.State;

public sealed class AppState
{
    public LibraryState Library { get; } = new();

    public PendingChangesState PendingChanges { get; } = new();

    public event EventHandler? Changed;

    public AppState()
    {
        Library.Changed += OnChildStateChanged;
        PendingChanges.Changed += OnChildStateChanged;
    }

    public bool HasPendingChanges => PendingChanges.PendingCount > 0;

    public void AddPendingChange(PendingChange change)
    {
        PendingChanges.Add(change);
    }

    private void OnChildStateChanged(object? sender, EventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
