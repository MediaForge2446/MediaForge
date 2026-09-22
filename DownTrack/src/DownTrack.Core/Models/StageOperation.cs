namespace DownTrack.Core.Models;

public enum StageOperationKind
{
    CreateFolder,
    Delete,
    Rename,
    Move,
    Download
}

public enum StageOperationState
{
    Pending,
    Executing,
    Completed,
    Failed,
    Cancelled
}

public sealed record StageOperation(
    Guid Id,
    StageOperationKind Kind,
    string TargetPath,
    string? SourcePath = null,
    Guid? NodeId = null,
    DateTimeOffset? CreatedAt = null,
    StageOperationState State = StageOperationState.Pending,
    string? Error = null,
    IReadOnlyList<Guid>? DependsOn = null)
{
    public DateTimeOffset CreatedAtUtc => CreatedAt ?? DateTimeOffset.UtcNow;

    public IReadOnlyList<Guid> Dependencies => DependsOn ?? [];
}
