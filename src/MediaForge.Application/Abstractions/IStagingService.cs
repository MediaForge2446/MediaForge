using MediaForge.Core.Models;

namespace MediaForge.Application.Abstractions;

public interface IStagingService
{
    IReadOnlyList<StagingOperation> Operations { get; }
    event EventHandler? Changed;
    Task<StagingOperation> StageAsync(
        StagingOperation operation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StagingOperation>> StageManyAsync(
        IReadOnlyCollection<StagingOperation> operations,
        CancellationToken cancellationToken = default);
    Task<bool> UndoAsync(Guid operationId, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid operationId, CancellationToken cancellationToken = default);

    Task CompleteManyAsync(
        IReadOnlyCollection<Guid> operationIds,
        CancellationToken cancellationToken = default);
}
