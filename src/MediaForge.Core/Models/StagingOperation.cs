using MediaForge.Core.Enums;

namespace MediaForge.Core.Models;

public sealed record StagingOperation(
    Guid OperationId,
    DateTimeOffset CreatedAt,
    OperationType OperationType,
    string Target,
    string? PreviousState,
    string? DesiredState);
