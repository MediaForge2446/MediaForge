using MediaForge.Application.Staging;
using MediaForge.Core.Enums;
using MediaForge.Core.Models;
using MediaForge.Core.State;

namespace MediaForge.Application.Tests;

public sealed class StagingServiceTests
{
    [Fact]
    public async Task StageThenUndo_RemovesOperationAndPersists()
    {
        var repository = new InMemoryStagingRepository();
        var history = new StagingHistory();
        var service = new StagingService(repository, history);
        await service.InitializeAsync();

        var operation = new StagingOperation(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            OperationType.CreateDirectory,
            "Music",
            nameof(MediaState.Missing),
            nameof(MediaState.Pending),
            new StagingPayload(DirectoryPath: "C:\\Music"));

        await service.StageAsync(operation);
        Assert.Single(service.Operations);
        Assert.Single(history.Operations);

        var undone = await service.UndoAsync(operation.OperationId);

        Assert.True(undone);
        Assert.Empty(service.Operations);
        Assert.Empty(history.Operations);
        Assert.Empty(await repository.LoadAsync());
    }

    [Fact]
    public async Task StageMany_PersistsAllOperationsInOneSnapshot()
    {
        var repository = new InMemoryStagingRepository();
        var service = new StagingService(repository, new StagingHistory());
        await service.InitializeAsync();

        var operations = new[]
        {
            new StagingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow, OperationType.CreateDirectory,
                "C:\\Music\\A", nameof(MediaState.Missing), nameof(MediaState.Pending),
                new StagingPayload(DirectoryPath: "C:\\Music\\A", IsDirectory: true)),
            new StagingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMilliseconds(1), OperationType.CreateDirectory,
                "C:\\Music\\B", nameof(MediaState.Missing), nameof(MediaState.Pending),
                new StagingPayload(DirectoryPath: "C:\\Music\\B", IsDirectory: true))
        };

        var staged = await service.StageManyAsync(operations);

        Assert.Equal(2, staged.Count);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(2, repository.LastSavedOperationCount);
        Assert.Equal(2, service.Operations.Count);
    }
}
