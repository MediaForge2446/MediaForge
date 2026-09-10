namespace MediaForge.Core.Models;

public sealed record AppStateSnapshot
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<MediaFolder> RootFolders { get; init; } = Array.Empty<MediaFolder>();

    public IReadOnlyList<PendingChange> PendingChanges { get; init; } = Array.Empty<PendingChange>();

    public DateTimeOffset SavedAtUtc { get; init; }
}
