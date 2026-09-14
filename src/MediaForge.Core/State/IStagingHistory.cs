using MediaForge.Core.Models;

namespace MediaForge.Core.State;

public interface IStagingHistory
{
    IReadOnlyList<StagingOperation> Operations { get; }
    void Add(StagingOperation operation);
    bool TryUndo(Guid operationId, out StagingOperation? operation);
    void Clear();
}
