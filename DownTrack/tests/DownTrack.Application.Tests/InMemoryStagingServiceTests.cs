using DownTrack.Application.Services;
using DownTrack.Core.Models;

namespace DownTrack.Application.Tests;

public sealed class InMemoryStagingServiceTests
{
    [Fact]
    public void StageCreateFolder_adds_pending_node_and_operation()
    {
        var staging = new InMemoryStagingService();

        var operationId = staging.StageCreateFolder(
            null,
            "Concerts",
            "Music/Concerts");

        var snapshot = staging.Snapshot;

        Assert.True(staging.HasPendingChanges);
        Assert.Contains(snapshot.Nodes, node =>
            node.Name == "Concerts" &&
            node.State == LibraryNodeState.Pending);
        Assert.Contains(snapshot.Operations, op =>
            op.Id == operationId &&
            op.Kind == StageOperationKind.CreateFolder &&
            op.State == StageOperationState.Pending);
    }

    [Fact]
    public void Undo_removes_the_staged_node_and_operation()
    {
        var staging = new InMemoryStagingService();

        var operationId = staging.StageDownload(
            null,
            "track.mp3",
            "Music/track.mp3");

        Assert.True(staging.Undo(operationId));
        Assert.False(staging.HasPendingChanges);
        Assert.Empty(staging.Snapshot.Nodes);
        Assert.Empty(staging.Snapshot.Operations);
    }

    [Fact]
    public void Clear_removes_all_staged_work()
    {
        var staging = new InMemoryStagingService();

        staging.StageCreateFolder(null, "A", "A");
        staging.StageDownload(null, "B.mp3", "A/B.mp3");

        staging.Clear();

        Assert.False(staging.HasPendingChanges);
        Assert.Empty(staging.Snapshot.Nodes);
        Assert.Empty(staging.Snapshot.Operations);
    }
}
