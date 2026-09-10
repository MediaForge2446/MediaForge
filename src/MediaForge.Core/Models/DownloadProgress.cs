namespace MediaForge.Core.Models;

public sealed record DownloadProgress
{
    public required Guid DownloadId { get; init; }

    public required double Progress { get; init; }

    public int Attempt { get; init; }

    public int MaxAttempts { get; init; }

    public bool IsRetrying { get; init; }

    public string? Message { get; init; }

    public Exception? Error { get; init; }
}
