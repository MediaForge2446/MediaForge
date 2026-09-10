using MediaForge.Core.Enums;
using MediaForge.Core.Models;
using MediaForge.Services;

namespace MediaForge.IntegrationTests;

public sealed class StagingHistoryTests
{
    [Fact]
    public void RemoveLastRestoresOnlyTheRequestedStageAndLeavesOrderIntact()
    {
        var history = new StagingHistory();
        var first = CreateChange(ChangeType.Rename, "A", "B");
        var second = CreateChange(ChangeType.Move, "C", "D");
        var third = CreateChange(ChangeType.Delete, "E", null);

        history.Record(first);
        history.Record(second);
        history.Record(third);

        Assert.True(history.Remove(second.Id, out var removed));
        Assert.Equal(second, removed);
        Assert.Equal(new[] { first.Id, third.Id }, history.Snapshot.Select(change => change.Id));
    }

    [Fact]
    public void RestoreDoesNotDuplicateAnExistingChange()
    {
        var history = new StagingHistory();
        var change = CreateChange(ChangeType.CreateFolder, "Folder", null);

        history.Record(change);
        history.Restore(change);

        Assert.Single(history.Snapshot);
        Assert.Equal(change.Id, history.Snapshot[0].Id);
    }

    [Fact]
    public void ClearRemovesAllStagedHistory()
    {
        var history = new StagingHistory();
        history.Record(CreateChange(ChangeType.Rename, "A", "B"));
        history.Record(CreateChange(ChangeType.Delete, "C", null));

        history.Clear();

        Assert.Empty(history.Snapshot);
    }

    private static PendingChange CreateChange(ChangeType type, string source, string? target) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Status = ChangeStatus.Pending,
            SourcePath = source,
            TargetPath = target,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
}
