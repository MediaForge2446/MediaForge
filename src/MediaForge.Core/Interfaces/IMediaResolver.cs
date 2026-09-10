using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IMediaResolver
{
    Task<IReadOnlyList<MediaItem>> ResolveAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default);
}
