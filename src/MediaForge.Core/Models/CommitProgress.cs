namespace MediaForge.Core.Models;

public sealed record CommitProgress
{
    public required Guid ChangeId { get; init; }

    public required double Progress { get; init; }

    public required int CompletedCount { get; init; }

    public required int TotalCount { get; init; }

    public string? Message { get; init; }

    public bool IsRetrying { get; init; }

    public int RetryAttempt { get; init; }

    public Exception? Error { get; init; }
}
