namespace MediaForge.Core.Interfaces;

public interface IFFmpegRunner
{
    Task ConvertAsync(string inputPath, string outputPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
