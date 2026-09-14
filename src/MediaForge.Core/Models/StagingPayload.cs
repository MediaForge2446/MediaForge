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
    string? VideoId = null,
    bool? IsDirectory = null);
