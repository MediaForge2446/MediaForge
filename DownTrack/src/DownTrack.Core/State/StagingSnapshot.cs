using DownTrack.Core.Models;

namespace DownTrack.Core.State;

public sealed record StagingSnapshot(
    IReadOnlyList<LibraryNode> Nodes,
    IReadOnlyList<StageOperation> Operations)
{
    public static StagingSnapshot Empty { get; } =
        new([], []);
}
