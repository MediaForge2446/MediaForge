using MediaForge.Core.Models;
using MediaForge.Services;

namespace MediaForge.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task SaveAsync_RoundTripsSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MediaForgeTests", Guid.NewGuid().ToString("N"));

        try
        {
            var first = new SettingsService(directory);
            var expected = new AppSettings
            {
                AutomaticToolUpdates = false,
                DownloadDirectory = Path.Combine(directory, "Music"),
                StartInLibrary = false
            };

            await first.SaveAsync(expected);

            var second = new SettingsService(directory);

            Assert.Equal(expected.AutomaticToolUpdates, second.Current.AutomaticToolUpdates);
            Assert.Equal(expected.DownloadDirectory, second.Current.DownloadDirectory);
            Assert.Equal(expected.StartInLibrary, second.Current.StartInLibrary);
        }
        finally
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

    [Fact]
    public void SettingsService_DefaultsToAutomaticUpdates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MediaForgeTests", Guid.NewGuid().ToString("N"));

        try
        {
            var service = new SettingsService(directory);
            Assert.True(service.Current.AutomaticToolUpdates);
            Assert.True(service.Current.StartInLibrary);
        }
        finally
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
}
