using MediaForge.Core.Enums;

namespace MediaForge.Core.Models;

public sealed record DownloadTask
{
    public required Guid Id { get; init; }

    public required string SourceUrl { get; init; }

    public required string TargetPath { get; init; }

    public MediaFormat Format { get; init; } = MediaFormat.Mp3;

    public double Progress { get; init; }

    public string? ErrorMessage { get; init; }
}
