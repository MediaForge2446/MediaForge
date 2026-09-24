using MediaForge.Core.Enums;
using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IYtDlpRunner
{
    Task RunAsync(
        string url,
        string outputPath,
        MediaFormat format,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        MediaQuality quality = MediaQuality.High192K,
        MediaVideoQuality videoQuality = MediaVideoQuality.Balanced720p);
}
