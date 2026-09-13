namespace MediaForge.Core.Models;

public sealed record ResolvedMediaItem(
    string VideoId,
    string SourceUrl,
    MediaMetadata Metadata);

public sealed record MediaResolveResult(
    bool IsPlaylist,
    string? CollectionTitle,
    IReadOnlyList<ResolvedMediaItem> Items);
