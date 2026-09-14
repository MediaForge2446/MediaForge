using MediaForge.Core.Models;

namespace MediaForge.Core.State;

public sealed class StagingHistory : IStagingHistory
{
    private readonly List<StagingOperation> _operations = [];

    public IReadOnlyList<StagingOperation> Operations => _operations;

    public void Add(StagingOperation operation) => _operations.Add(operation);

    public bool TryUndo(Guid operationId, out StagingOperation? operation)
    {
        var index = _operations.FindLastIndex(x => x.OperationId == operationId);
        if (index < 0)
        {
            operation = null;
            return false;
        }

        operation = _operations[index];
        _operations.RemoveAt(index);
        return true;
    }

    public void Clear() => _operations.Clear();
}
