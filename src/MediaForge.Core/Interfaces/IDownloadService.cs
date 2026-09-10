using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IDownloadService
{
    Task<string> DownloadAsync(
        DownloadTask task,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
