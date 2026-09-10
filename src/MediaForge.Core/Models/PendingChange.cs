using MediaForge.Core.Enums;

namespace MediaForge.Core.Models;

public sealed record PendingChange
{
    public required Guid Id { get; init; }

    public required ChangeType Type { get; init; }

    public required ChangeStatus Status { get; init; }

    public required string SourcePath { get; init; }

    public string? TargetPath { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
