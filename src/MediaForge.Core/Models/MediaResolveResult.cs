using MediaForge.Core.Enums;

namespace MediaForge.Core.Models;

public sealed record ResolvedMediaItem(
    string VideoId,
    string SourceUrl,
    MediaMetadata Metadata,
    MediaFormat DesiredFormat = MediaFormat.Mp3);

public sealed record MediaResolveResult(
    bool IsPlaylist,
    string? CollectionTitle,
    IReadOnlyList<ResolvedMediaItem> Items);
