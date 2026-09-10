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
    public void MediaItem_PreservesResolvedMetadata()
    {
        var thumbnail = new Uri("https://example.com/cover.jpg");
        var item = new MediaItem
        {
            Id = "video-id",
            SourceUrl = "https://www.youtube.com/watch?v=video-id",
            Title = "Song Title",
            Artist = "Artist",
            ThumbnailUrl = thumbnail,
            Duration = TimeSpan.FromMinutes(3),
            PlaylistTitle = "Playlist"
        };

        Assert.Equal("Song Title", item.Title);
        Assert.Equal("Artist", item.Artist);
        Assert.Equal(thumbnail, item.ThumbnailUrl);
        Assert.Equal(TimeSpan.FromMinutes(3), item.Duration);
        Assert.Equal("Playlist", item.PlaylistTitle);
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
