using MediaForge.Core.Enums;
using MediaForge.Core.Models;
using MediaForge.Services;

namespace MediaForge.Core.Tests;

public sealed class AppPersistenceServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsLibraryAndPendingChanges()
    {
        var directory = CreateTempDirectory();
        try
        {
            using var persistence = new AppPersistenceService(directory);
            var folder = new MediaFolder
            {
                Id = "folder-1",
                Path = Path.Combine(directory, "Music"),
                DisplayName = "Music",
                AddedAtUtc = DateTimeOffset.UtcNow
            };

            var change = CreatePendingChange("https://example.com/song", Path.Combine(directory, "song.mp3"));
            var snapshot = new AppStateSnapshot
            {
                RootFolders = new[] { folder },
                PendingChanges = new[] { change }
            };

            await persistence.SaveAsync(snapshot);
            var loaded = await persistence.LoadAsync();

            var result = Assert.NotNull(loaded);
            var loadedFolder = Assert.Single(result.RootFolders);
            var loadedChange = Assert.Single(result.PendingChanges);
            Assert.Equal(folder.Path, loadedFolder.Path);
            Assert.Equal(change.Id, loadedChange.Id);
            Assert.Equal(change.SourcePath, loadedChange.SourcePath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Load_WhenCurrentStateIsCorrupt_RestoresLastKnownGoodBackup()
    {
        var directory = CreateTempDirectory();
        try
        {
            using var persistence = new AppPersistenceService(directory);
            var firstChange = CreatePendingChange("https://example.com/first", Path.Combine(directory, "first.mp3"));
            var secondChange = CreatePendingChange("https://example.com/second", Path.Combine(directory, "second.mp3"));

            await persistence.SaveAsync(new AppStateSnapshot { PendingChanges = new[] { firstChange } });
            await persistence.SaveAsync(new AppStateSnapshot { PendingChanges = new[] { secondChange } });

            await File.WriteAllTextAsync(Path.Combine(directory, "state.json"), "{ not valid json");

            var loaded = await persistence.LoadAsync();

            var result = Assert.NotNull(loaded);
            var restoredChange = Assert.Single(result.PendingChanges);
            Assert.Equal(firstChange.Id, restoredChange.Id);
            Assert.Equal(firstChange.SourcePath, restoredChange.SourcePath);
            Assert.True(File.Exists(Path.Combine(directory, "state.json.sha256")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Save_ReusesExistingTemporaryFileLeftByInterruptedWrite()
    {
        var directory = CreateTempDirectory();
        try
        {
            using var persistence = new AppPersistenceService(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "state.json.tmp"), "stale temporary data");

            var change = CreatePendingChange("https://example.com/current", Path.Combine(directory, "current.mp3"));
            await persistence.SaveAsync(new AppStateSnapshot { PendingChanges = new[] { change } });

            var loaded = await persistence.LoadAsync();
            var result = Assert.NotNull(loaded);
            var loadedChange = Assert.Single(result.PendingChanges);
            Assert.Equal(change.Id, loadedChange.Id);
            Assert.False(File.Exists(Path.Combine(directory, "state.json.tmp")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Save_FiltersDuplicateFoldersAndChanges()
    {
        var directory = CreateTempDirectory();
        try
        {
            using var persistence = new AppPersistenceService(directory);
            var folder = new MediaFolder
            {
                Id = "folder-1",
                Path = Path.Combine(directory, "Music"),
                DisplayName = "Music",
                AddedAtUtc = DateTimeOffset.UtcNow
            };
            var duplicateFolder = folder with { Id = "folder-2", AddedAtUtc = folder.AddedAtUtc.AddMinutes(1) };
            var change = CreatePendingChange("https://example.com/song", Path.Combine(directory, "song.mp3"));
            var duplicateChange = change with { ErrorMessage = "latest" };

            await persistence.SaveAsync(new AppStateSnapshot
            {
                RootFolders = new[] { folder, duplicateFolder },
                PendingChanges = new[] { change, duplicateChange }
            });

            var loaded = Assert.NotNull(await persistence.LoadAsync());
            Assert.Single(loaded.RootFolders);
            var loadedChange = Assert.Single(loaded.PendingChanges);
            Assert.Equal("latest", loadedChange.ErrorMessage);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static PendingChange CreatePendingChange(string sourceUrl, string targetPath) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = ChangeType.Download,
            Status = ChangeStatus.Pending,
            SourcePath = sourceUrl,
            TargetPath = targetPath,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MediaForgePersistenceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
