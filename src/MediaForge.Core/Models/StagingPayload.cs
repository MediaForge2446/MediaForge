using MediaForge.Core.Enums;

namespace MediaForge.Core.Models;

public sealed record StagingPayload(
    string? SourceUrl = null,
    string? SourcePath = null,
    string? DestinationPath = null,
    string? DirectoryPath = null,
    string? NewName = null,
    bool Recursive = false,
    MediaFormat? DesiredFormat = null,
    MediaQuality? DesiredQuality = null,
    MediaVideoQuality? DesiredVideoQuality = null,
    string? VideoId = null,
    bool? IsDirectory = null,
    string? Title = null,
    string? Artist = null,
    string? ThumbnailUrl = null);
