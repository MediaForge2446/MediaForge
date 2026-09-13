using MediaForge.Core.Enums;

namespace MediaForge.Core.Models;

public sealed class MediaItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required MediaSource Source { get; init; }
    public required MediaMetadata Metadata { get; set; }
    public required MediaTarget Target { get; set; }
    public MediaFormat DesiredFormat { get; set; } = MediaFormat.Mp3;
    public MediaState DesiredState { get; set; } = MediaState.Synced;
    public MediaState ActualState { get; set; } = MediaState.Missing;
    public string? LastError { get; set; }
}
