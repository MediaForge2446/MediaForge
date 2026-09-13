namespace MediaForge.Core.Models;

public sealed record MediaMetadata(
    string Title,
    string? Artist = null,
    string? Album = null,
    string? ThumbnailUrl = null,
    TimeSpan? Duration = null);
