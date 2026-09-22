using MediaForge.Core.Enums;

namespace MediaForge.Core.Models;

public sealed record MediaIndexEntry(
    string VideoId,
    string SourceUrl,
    string PhysicalPath,
    MediaFormat Format,
    DateTimeOffset AddedAtUtc,
    DateTimeOffset LastVerifiedAtUtc,
    string? Title = null,
    string? Artist = null,
    string? ThumbnailUrl = null);
