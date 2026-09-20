using MediaForge.Application.Abstractions;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Core.State;

namespace MediaForge.Application.Staging;

public sealed class StagingService : IStagingService
{
    private readonly IStagingRepository _repository;
    private readonly IStagingHistory _history;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<StagingOperation> _operations = [];
    private bool _initialized;

    public StagingService(IStagingRepository repository, IStagingHistory history)
    {
        _repository = repository;
        _history = history;
    }

    public IReadOnlyList<StagingOperation> Operations
    {
        get
        {
            lock (_operations)
                return _operations.ToArray();
        }
    }

    public event EventHandler? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            var loaded = await _repository.LoadAsync(cancellationToken).ConfigureAwait(false);
            lock (_operations)
            {
                _operations.Clear();
                _operations.AddRange(loaded);
                _history.Clear();
                foreach (var operation in _operations)
                    _history.Add(operation);
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<StagingOperation> StageAsync(
        StagingOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_operations)
            {
                if (_operations.Any(x => x.OperationId == operation.OperationId))
                    throw new InvalidOperationException("A staging operation with the same ID already exists.");

                _operations.Add(operation);
                _history.Add(operation);
            }

            await PersistAsync(cancellationToken).ConfigureAwait(false);
            Changed?.Invoke(this, EventArgs.Empty);
            return operation;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<StagingOperation>> StageManyAsync(
        IReadOnlyCollection<StagingOperation> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0)
            return [];

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var additions = operations.ToArray();
            if (additions.Any(x => x is null))
                throw new ArgumentException("A staging operation cannot be null.", nameof(operations));

            var duplicateIds = additions
                .GroupBy(x => x.OperationId)
                .FirstOrDefault(x => x.Count() > 1);
            if (duplicateIds is not null || additions.Any(x => _operations.Any(existing => existing.OperationId == x.OperationId)))
                throw new InvalidOperationException("A staging operation with the same ID already exists.");

            lock (_operations)
            {
                _operations.AddRange(additions);
                foreach (var operation in additions)
                    _history.Add(operation);
            }

            try
            {
                await PersistAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (_operations)
                {
                    foreach (var operation in additions)
                        _operations.RemoveAll(x => x.OperationId == operation.OperationId);
                    foreach (var operation in additions.Reverse())
                        _history.TryUndo(operation.OperationId, out _);
                }
                throw;
            }

            Changed?.Invoke(this, EventArgs.Empty);
            return additions;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> UndoAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_operations)
            {
                var index = _operations.FindLastIndex(x => x.OperationId == operationId);
                if (index < 0)
                    return false;

                _operations.RemoveAt(index);
                _history.TryUndo(operationId, out _);
            }

            await PersistAsync(cancellationToken).ConfigureAwait(false);
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int removed;
            lock (_operations)
            {
                removed = _operations.RemoveAll(x => x.OperationId == operationId);
                _history.TryUndo(operationId, out _);
            }

            await PersistAsync(cancellationToken).ConfigureAwait(false);
            if (removed > 0)
                Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteManyAsync(
        IReadOnlyCollection<Guid> operationIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationIds);
        if (operationIds.Count == 0)
            return;

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ids = operationIds.ToHashSet();
            int removed;
            lock (_operations)
            {
                removed = _operations.RemoveAll(x => ids.Contains(x.OperationId));
                foreach (var operationId in ids)
                    _history.TryUndo(operationId, out _);
            }

            if (removed > 0)
            {
                await PersistAsync(cancellationToken).ConfigureAwait(false);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_operations)
            {
                _operations.Clear();
                _history.Clear();
            }

            await PersistAsync(cancellationToken).ConfigureAwait(false);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task PersistAsync(CancellationToken cancellationToken)
    {
        StagingOperation[] snapshot;
        lock (_operations)
            snapshot = _operations.ToArray();

        return _repository.SaveAsync(snapshot, cancellationToken);
    }
}
