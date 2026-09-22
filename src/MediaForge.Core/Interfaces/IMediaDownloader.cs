using MediaForge.Core.Enums;
using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IMediaDownloader
{
    Task DownloadAsync(
        string sourceUrl,
        string outputPath,
        MediaFormat format,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        MediaQuality quality = MediaQuality.Standard128K);
}
