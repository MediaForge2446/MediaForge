using MediaForge.Core.Enums;

namespace MediaForge.Core.Interfaces;

public interface IYtDlpRunner
{
    Task RunAsync(
        string url,
        string outputPath,
        MediaFormat format,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
