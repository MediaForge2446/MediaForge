using MediaForge.Core.Enums;

namespace MediaForge.Core.Models;

public sealed record MediaItem
{
    public required string Id { get; init; }

    public required string SourceUrl { get; init; }

    public string? Title { get; init; }

    public string? Artist { get; init; }

    public Uri? ThumbnailUrl { get; init; }

    public MediaFormat Format { get; init; } = MediaFormat.Mp3;

    public bool IsSelected { get; init; } = true;

    public string? TargetFolderPath { get; init; }
}
