using MediaForge.Services;

namespace MediaForge.Core.Tests;

public sealed class YtDlpPathProviderTests
{
    [Fact]
    public void DefaultToolsDirectory_IsUserWritableLocation()
    {
        var provider = new YtDlpPathProvider();
        var localAppData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var expectedPrefix = Path.Combine(localAppData, "MediaForge", "Tools");

        Assert.StartsWith(expectedPrefix, provider.ToolsDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".exe", provider.YoutubeDLPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("ffmpeg.exe", provider.FFmpegPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CustomToolsDirectory_IsPreserved()
    {
        var customDirectory = Path.Combine(Path.GetTempPath(), "MediaForgeTools", Guid.NewGuid().ToString("N"));
        var provider = new YtDlpPathProvider(customDirectory);

        Assert.Equal(Path.GetFullPath(customDirectory), provider.ToolsDirectory);
    }
}
