using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Infrastructure.Persistence;

public sealed class JsonMediaIndex : IMediaIndex
{
    private sealed record Snapshot(IReadOnlyList<MediaIndexEntry> Entries);

    private readonly AtomicJsonStore _store;
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<MediaIndexEntry> _entries = [];
    private bool _initialized;

    public JsonMediaIndex(AtomicJsonStore store, LocalAppPaths paths)
        : this(store, paths.MediaIndexFilePath)
    {
    }

    public JsonMediaIndex(AtomicJsonStore store, string path)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _path = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("A media index path is required.", nameof(path)) : path;
    }

    public IReadOnlyList<MediaIndexEntry> Entries
    {
        get
        {
            lock (_entries) return _entries.ToArray();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return;
            var snapshot = await _store.LoadAsync<Snapshot>(_path, cancellationToken).ConfigureAwait(false);
            lock (_entries)
            {
                _entries.Clear();
                if (snapshot?.Entries is not null) _entries.AddRange(snapshot.Entries);
            }
            _initialized = true;
        }
        finally { _gate.Release(); }
    }

    public async Task<MediaIndexEntry?> FindByVideoIdAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId)) return null;
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        lock (_entries) return _entries.FirstOrDefault(x => string.Equals(x.VideoId, videoId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpsertAsync(MediaIndexEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.VideoId)) throw new ArgumentException("VideoId is required.", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.PhysicalPath)) throw new ArgumentException("PhysicalPath is required.", nameof(entry));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureInitializedCoreAsync(cancellationToken).ConfigureAwait(false);
            lock (_entries)
            {
                var index = _entries.FindIndex(x => string.Equals(x.VideoId, entry.VideoId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) _entries[index] = entry;
                else _entries.Add(entry);
            }
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId)) return;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureInitializedCoreAsync(cancellationToken).ConfigureAwait(false);
            var changed = false;
            lock (_entries)
            {
                changed = _entries.RemoveAll(x => string.Equals(x.VideoId, videoId, StringComparison.OrdinalIgnoreCase)) > 0;
            }
            if (changed) await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task EnsureInitializedCoreAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        var snapshot = await _store.LoadAsync<Snapshot>(_path, cancellationToken).ConfigureAwait(false);
        lock (_entries)
        {
            _entries.Clear();
            if (snapshot?.Entries is not null) _entries.AddRange(snapshot.Entries);
        }
        _initialized = true;
    }

    private Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        MediaIndexEntry[] snapshot;
        lock (_entries) snapshot = _entries.ToArray();
        return _store.SaveAsync(_path, new Snapshot(snapshot), cancellationToken);
    }
}
