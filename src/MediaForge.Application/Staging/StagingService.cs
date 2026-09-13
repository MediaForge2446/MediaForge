using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

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

    public IReadOnlyList<StagingOperation> Operations => _operations.AsReadOnly();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            var loaded = await _repository.LoadAsync(cancellationToken).ConfigureAwait(false);
            _operations.Clear();
            _operations.AddRange(loaded);
            _history.Clear();
            foreach (var operation in _operations)
                _history.Add(operation);

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
            if (_operations.Any(x => x.OperationId == operation.OperationId))
                throw new InvalidOperationException("A staging operation with the same ID already exists.");

            _operations.Add(operation);
            _history.Add(operation);
            await PersistAsync(cancellationToken).ConfigureAwait(false);
            return operation;
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
            var index = _operations.FindLastIndex(x => x.OperationId == operationId);
            if (index < 0)
                return false;

            _operations.RemoveAt(index);
            _history.TryUndo(operationId, out _);
            await PersistAsync(cancellationToken).ConfigureAwait(false);
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
            _operations.RemoveAll(x => x.OperationId == operationId);
            _history.TryUndo(operationId, out _);
            await PersistAsync(cancellationToken).ConfigureAwait(false);
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
            _operations.Clear();
            _history.Clear();
            await PersistAsync(cancellationToken).ConfigureAwait(false);
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
        => _repository.SaveAsync(_operations.ToArray(), cancellationToken);
}
