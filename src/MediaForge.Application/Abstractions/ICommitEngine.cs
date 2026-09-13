using MediaForge.Core.Models;

namespace MediaForge.Application.Abstractions;

public interface ICommitEngine
{
    Task<CommitResult> CommitAsync(
        IReadOnlyCollection<StagingOperation> operations,
        IProgress<CommitProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record CommitItemResult(
    Guid OperationId,
    bool Success,
    string? Error = null);

public sealed record CommitResult(IReadOnlyList<CommitItemResult> Items)
{
    public bool Success => Items.All(item => item.Success);
}
