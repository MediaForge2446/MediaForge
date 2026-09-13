namespace MediaForge.Core.Interfaces;

public interface IYtDlpRunner
{
    Task RunAsync(string url, string outputPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
