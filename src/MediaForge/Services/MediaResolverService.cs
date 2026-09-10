using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Services;

public sealed class MediaResolverService : IMediaResolver
{
    public Task<IReadOnlyList<MediaItem>> ResolveAsync(
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
        var isPlaylist = IsPlaylist(uri);
        var itemCount = isPlaylist ? 8 : 1;
        var baseTitle = GetBaseTitle(uri);
        var items = Enumerable.Range(1, itemCount)
            .Select(index => new MediaItem
            {
                Id = $"{Guid.NewGuid():N}",
                SourceUrl = normalizedUrl,
                Title = itemCount == 1 ? baseTitle : $"{baseTitle} {index}",
                Artist = null,
                ThumbnailUrl = null,
                Format = MediaForge.Core.Enums.MediaFormat.Mp3,
                IsSelected = true
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<MediaItem>>(items);
    }

    private static bool IsPlaylist(Uri uri)
    {
        var query = uri.Query;
        return query.Contains("list=", StringComparison.OrdinalIgnoreCase)
            || query.Contains("playlist", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("playlist", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBaseTitle(Uri uri)
    {
        var segment = uri.Segments.LastOrDefault(segment => !string.IsNullOrWhiteSpace(segment));
        if (!string.IsNullOrWhiteSpace(segment))
        {
            var title = Uri.UnescapeDataString(segment.Trim('/'));
            if (title.Length > 0)
            {
                return title;
            }
        }

        return "Media item";
    }
}
