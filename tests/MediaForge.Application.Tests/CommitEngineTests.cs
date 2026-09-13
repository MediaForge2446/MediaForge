using MediaForge.Application.Commit;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Core.State;
using MediaForge.Application.Staging;

namespace MediaForge.Application.Tests;

public sealed class CommitEngineTests
{
    [Fact]
    public async Task CommitCreateDirectory_CompletesAndRemovesStagedOperation()
    {
        var fileSystem = new InMemoryFileSystem();
        var repository = new InMemoryStagingRepository();
        var staging = new StagingService(repository, new StagingHistory());
        await staging.InitializeAsync();

        var operation = new StagingOperation(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            OperationType.CreateDirectory,
            "Music",
            nameof(MediaState.Missing),
            nameof(MediaState.Pending),
            new StagingPayload(DirectoryPath: "C:\\Music"));
        await staging.StageAsync(operation);

        var engine = new CommitEngine(fileSystem, new FakeMediaDownloader(fileSystem), staging);
        var result = await engine.CommitAsync([operation]);

        Assert.True(result.Success);
        Assert.True(await fileSystem.DirectoryExistsAsync("C:\\Music"));
        Assert.Empty(staging.Operations);
    }

    [Fact]
    public async Task CommitDownload_VerifiesOutputFileBeforeCompleting()
    {
        var fileSystem = new InMemoryFileSystem();
        var repository = new InMemoryStagingRepository();
        var staging = new StagingService(repository, new StagingHistory());
        await staging.InitializeAsync();

        var operation = new StagingOperation(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            OperationType.Download,
            "song.mp3",
            nameof(MediaState.Missing),
            nameof(MediaState.Pending),
            new StagingPayload(
                SourceUrl: "https://youtube.example/video",
                DestinationPath: "C:\\Music\\song.mp3",
                DesiredFormat: MediaFormat.Mp3));
        await staging.StageAsync(operation);

        var engine = new CommitEngine(fileSystem, new FakeMediaDownloader(fileSystem), staging, new DownloadQueue(1));
        var result = await engine.CommitAsync([operation]);

        Assert.True(result.Success);
        Assert.True(await fileSystem.FileExistsAsync("C:\\Music\\song.mp3"));
        Assert.Empty(staging.Operations);
    }
}
