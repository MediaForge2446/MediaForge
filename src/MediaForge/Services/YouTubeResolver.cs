using YoutubeExplode;
using YoutubeExplode.Common;
using MediaForge.ViewModels;
namespace MediaForge.Services;
public sealed class YouTubeResolver
{
    private readonly YoutubeClient _client=new();
    public async Task<IReadOnlyList<MediaItem>> ResolveAsync(string url,CancellationToken token)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||(uri.Scheme!=Uri.UriSchemeHttps&&uri.Scheme!=Uri.UriSchemeHttp))throw new ArgumentException("Enter a valid YouTube URL.");
        if(url.Contains("list=",StringComparison.OrdinalIgnoreCase))
        {
            var playlist=await _client.Playlists.GetAsync(url,token);var items=new List<MediaItem>();
            await foreach(var video in _client.Playlists.GetVideosAsync(playlist.Id).WithCancellation(token))items.Add(new MediaItem{Url=video.Url,Title=video.Title,Artist=video.Author.ChannelTitle,ThumbnailUrl=video.Thumbnails.GetWithHighestResolution().Url,Duration=video.Duration});
            return items;
        }
        var v=await _client.Videos.GetAsync(url,token);return new[]{new MediaItem{Url=v.Url,Title=v.Title,Artist=v.Author.ChannelTitle,ThumbnailUrl=v.Thumbnails.GetWithHighestResolution().Url,Duration=v.Duration}};
    }
    public void Dispose(){}
}
