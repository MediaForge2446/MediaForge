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
    StageOperationState State = StageOperationState.Pending,
    string? Error = null);
