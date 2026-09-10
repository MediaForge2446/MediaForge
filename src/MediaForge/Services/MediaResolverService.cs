using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace MediaForge.Services;

public sealed class MediaResolverService : IMediaResolver
{
    private readonly YoutubeClient _youtube;

    public MediaResolverService(YoutubeClient? youtubeClient = null)
    {
        _youtube = youtubeClient ?? new YoutubeClient();
    }

    public async Task<IReadOnlyList<MediaItem>> ResolveAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);

        if (!Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The media URL must be a valid HTTP or HTTPS URL.", nameof(sourceUrl));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedUrl = uri.ToString();
        return LooksLikePlaylist(uri)
            ? await ResolvePlaylistAsync(normalizedUrl, cancellationToken).ConfigureAwait(false)
            : new[] { await ResolveVideoAsync(normalizedUrl, null, cancellationToken).ConfigureAwait(false) };
    }

    private async Task<IReadOnlyList<MediaItem>> ResolvePlaylistAsync(
        string playlistUrl,
        CancellationToken cancellationToken)
    {
        var playlist = await _youtube.Playlists.GetAsync(playlistUrl, cancellationToken).ConfigureAwait(false);
        var items = new List<MediaItem>();
        var playlistTitle = NullIfWhiteSpace(playlist.Title);

        await foreach (var video in _youtube.Playlists.GetVideosAsync(playlist.Id)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            items.Add(CreateMediaItem(
                video.Id.Value,
                video.Url,
                video.Title,
                video.Author.ChannelTitle,
                video.Thumbnails.GetWithHighestResolution().Url,
                video.Duration,
                playlistTitle));
        }

        return items;
    }

    private async Task<MediaItem> ResolveVideoAsync(
        string videoUrl,
        string? playlistTitle,
        CancellationToken cancellationToken)
    {
        var video = await _youtube.Videos.GetAsync(videoUrl, cancellationToken).ConfigureAwait(false);

        return CreateMediaItem(
            video.Id.Value,
            video.Url,
            video.Title,
            video.Author.ChannelTitle,
            video.Thumbnails.GetWithHighestResolution().Url,
            video.Duration,
            playlistTitle);
    }

    private static MediaItem CreateMediaItem(
        string id,
        string sourceUrl,
        string? title,
        string? artist,
        string? thumbnailUrl,
        TimeSpan? duration,
        string? playlistTitle)
    {
        return new MediaItem
        {
            Id = id,
            SourceUrl = sourceUrl,
            Title = NullIfWhiteSpace(title),
            Artist = NullIfWhiteSpace(artist),
            ThumbnailUrl = Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out var uri) ? uri : null,
            Duration = duration,
            PlaylistTitle = playlistTitle,
            Format = MediaFormat.Mp3,
            IsSelected = true
        };
    }

    private static bool LooksLikePlaylist(Uri uri) =>
        uri.Query.Contains("list=", StringComparison.OrdinalIgnoreCase) ||
        uri.AbsolutePath.Contains("/playlist", StringComparison.OrdinalIgnoreCase);

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
