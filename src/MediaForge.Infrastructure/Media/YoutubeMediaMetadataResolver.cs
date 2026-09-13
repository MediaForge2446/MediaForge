using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace MediaForge.Infrastructure.Media;

public sealed class YoutubeMediaMetadataResolver : IMediaMetadataResolver
{
    private readonly YoutubeClient _youtube;

    public YoutubeMediaMetadataResolver(YoutubeClient? youtube = null)
    {
        _youtube = youtube ?? new YoutubeClient();
    }

    public async Task<MediaResolveResult> ResolveAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(sourceUrl?.Trim(), UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Please enter a valid YouTube URL.", nameof(sourceUrl));
        }

        var normalized = uri.ToString();
        if (normalized.Contains("list=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/playlist", StringComparison.OrdinalIgnoreCase))
        {
            var playlist = await _youtube.Playlists.GetAsync(normalized, cancellationToken).ConfigureAwait(false);
            var items = new List<ResolvedMediaItem>();

            await foreach (var video in _youtube.Playlists.GetVideosAsync(playlist.Id, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var thumbnail = video.Thumbnails.Count == 0 ? null : video.Thumbnails.GetWithHighestResolution().Url;
                items.Add(new ResolvedMediaItem(
                    video.Id.Value,
                    $"https://www.youtube.com/watch?v={video.Id.Value}",
                    new MediaMetadata(
                        video.Title.Trim(),
                        video.Author?.ChannelTitle,
                        ThumbnailUrl: thumbnail,
                        Duration: video.Duration)));
            }

            if (items.Count == 0)
                throw new InvalidOperationException("The playlist does not contain any accessible videos.");

            return new MediaResolveResult(true, playlist.Title.Trim(), items);
        }

        var videoInfo = await _youtube.Videos.GetAsync(normalized, cancellationToken).ConfigureAwait(false);
        var videoThumbnail = videoInfo.Thumbnails.Count == 0 ? null : videoInfo.Thumbnails.GetWithHighestResolution().Url;
        return new MediaResolveResult(
            false,
            null,
            [new ResolvedMediaItem(
                videoInfo.Id.Value,
                $"https://www.youtube.com/watch?v={videoInfo.Id.Value}",
                new MediaMetadata(
                    videoInfo.Title.Trim(),
                    videoInfo.Author?.ChannelTitle,
                    ThumbnailUrl: videoThumbnail,
                    Duration: videoInfo.Duration))]);
    }
}
