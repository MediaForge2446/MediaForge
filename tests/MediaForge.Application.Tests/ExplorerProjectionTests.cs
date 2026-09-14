using MediaForge.Application.Explorer;
using MediaForge.Core.Enums;
using MediaForge.Core.Models;

namespace MediaForge.Application.Tests;

public sealed class ExplorerProjectionTests
{
    private readonly ExplorerProjectionService _projection = new();
    private static readonly DateTimeOffset Timestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProjectsCreateAndDownloadIntoCurrentDirectory()
    {
        var actual = new[]
        {
            new ExplorerEntry("Existing", "C:/Music/Existing", true, 0, Timestamp)
        };
        var operations = new[]
        {
            Op(OperationType.CreateDirectory, "C:/Music/Album", new StagingPayload(DirectoryPath: "C:/Music/Album"), 1),
            Op(OperationType.Download, "C:/Music/Album.mp3", new StagingPayload(DestinationPath: "C:/Music/Album.mp3"), 2)
        };

        var result = _projection.Project("C:/Music", actual, operations);

        Assert.Equal(
            ["Album", "Album.mp3", "Existing"],
            result.Select(x => x.Entry.Name).OrderBy(x => x));
        Assert.All(result.Where(x => x.Entry.Name is "Album" or "Album.mp3"), x => Assert.True(x.IsPending));
    }

    [Fact]
    public void ProjectsRenameMoveAndDeleteAsUserIntent()
    {
        var actual = new[]
        {
            new ExplorerEntry("Song.mp3", "C:/Music/Song.mp3", false, 123, Timestamp),
            new ExplorerEntry("Keep", "C:/Music/Keep", true, 0, Timestamp)
        };
        var operations = new[]
        {
            Op(OperationType.Rename, "C:/Music/Song.mp3", new StagingPayload(SourcePath: "C:/Music/Song.mp3", DestinationPath: "C:/Music/Renamed.mp3", NewName: "Renamed.mp3"), 1),
            Op(OperationType.Move, "C:/Music/Renamed.mp3", new StagingPayload(SourcePath: "C:/Music/Renamed.mp3", DestinationPath: "C:/Music/Archive/Renamed.mp3"), 2),
            Op(OperationType.Delete, "C:/Music/Keep", new StagingPayload(SourcePath: "C:/Music/Keep", Recursive: true), 3)
        };

        var result = _projection.Project("C:/Music", actual, operations);

        var keep = Assert.Single(result, x => x.Entry.Name == "Keep");
        Assert.True(keep.IsPending);
        Assert.True(keep.MarkedForDeletion);
        Assert.DoesNotContain(result, x => x.Entry.Name is "Song.mp3" or "Renamed.mp3");
    }

    private static StagingOperation Op(OperationType type, string target, StagingPayload payload, int seconds)
        => new(Guid.NewGuid(), Timestamp.AddSeconds(seconds), type, target, nameof(MediaState.Synced), nameof(MediaState.Pending), payload);
}
