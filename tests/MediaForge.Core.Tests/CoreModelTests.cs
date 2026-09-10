using MediaForge.Core.Enums;
using MediaForge.Core.Models;

namespace MediaForge.Core.Tests;

public sealed class CoreModelTests
{
    [Fact]
    public void MediaItem_DefaultsToSelectedMp3()
    {
        var item = new MediaItem
        {
            Id = "test-id",
            SourceUrl = "https://example.com/media"
        };

        Assert.True(item.IsSelected);
        Assert.Equal(MediaFormat.Mp3, item.Format);
    }

    [Fact]
    public void FileItem_DefaultsToSynced()
    {
        var item = new FileItem
        {
            Name = "song.mp3",
            FullPath = "C:\\Media\\song.mp3",
            Kind = FileItemKind.File
        };

        Assert.Equal(ChangeStatus.Synced, item.Status);
    }
}
