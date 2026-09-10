using MediaForge.Core.Models;

namespace MediaForge.State;

public sealed class LibraryState
{
    private readonly object _sync = new();
    private readonly List<MediaFolder> _rootFolders = new();

    public event EventHandler? Changed;

    public IReadOnlyList<MediaFolder> RootFolders
    {
        get
        {
            lock (_sync)
            {
                return _rootFolders.ToArray();
            }
        }
    }

    public void AddRootFolder(MediaFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        lock (_sync)
        {
            if (_rootFolders.Any(existing => string.Equals(existing.Path, folder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _rootFolders.Add(folder);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool RemoveRootFolder(string folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return false;
        }

        var removed = false;

        lock (_sync)
        {
            var index = _rootFolders.FindIndex(folder => string.Equals(folder.Id, folderId, StringComparison.Ordinal));
            if (index >= 0)
            {
                _rootFolders.RemoveAt(index);
                removed = true;
            }
        }

        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }
}
