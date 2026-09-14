using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IMediaMetadataResolver
{
    Task<MediaResolveResult> ResolveAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default);
}
