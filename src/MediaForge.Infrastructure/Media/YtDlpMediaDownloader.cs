using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Infrastructure.Media;

public sealed class YtDlpMediaDownloader : IMediaDownloader
{
    private readonly IYtDlpRunner _runner;

    public YtDlpMediaDownloader(IYtDlpRunner runner)
    {
        _runner = runner;
    }

    public async Task DownloadAsync(
        string sourceUrl,
        string outputPath,
        MediaFormat format,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var extension = format switch
        {
            MediaFormat.Mp3 => ".mp3",
            MediaFormat.Mp4 => ".mp4",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        var finalPath = Path.GetExtension(outputPath).Equals(extension, StringComparison.OrdinalIgnoreCase)
            ? outputPath
            : Path.ChangeExtension(outputPath, extension);

        var tempPath = finalPath + ".part";
        TryDelete(tempPath);

        try
        {
            progress?.Report(new DownloadProgress(0, "מתחיל הורדה"));
            await _runner.RunAsync(sourceUrl, tempPathWithoutExtension(tempPath), new Progress<double>(percent =>
            {
                progress?.Report(new DownloadProgress(percent, "מוריד"));
            }), cancellationToken).ConfigureAwait(false);

            var produced = ResolveProducedPath(tempPathWithoutExtension(tempPath), extension);
            if (produced is null)
                throw new FileNotFoundException("The downloader completed without producing an output file.");

            Directory.CreateDirectory(Path.GetDirectoryName(finalPath) ?? ".");
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(produced, finalPath);
            progress?.Report(new DownloadProgress(100, "הושלם"));
        }
        catch
        {
            TryDelete(tempPath);
            foreach (var candidate in Directory.Exists(Path.GetDirectoryName(tempPath) ?? string.Empty)
                         ? Directory.GetFiles(Path.GetDirectoryName(tempPath)!, Path.GetFileNameWithoutExtension(tempPath) + "*")
                         : [])
            {
                TryDelete(candidate);
            }

            throw;
        }
    }

    private static string tempPathWithoutExtension(string path)
        => Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path));

    private static string? ResolveProducedPath(string requestedBasePath, string extension)
    {
        var expected = requestedBasePath + extension;
        if (File.Exists(expected))
            return expected;

        var directory = Path.GetDirectoryName(requestedBasePath);
        if (directory is null || !Directory.Exists(directory))
            return null;

        var prefix = Path.GetFileName(requestedBasePath);
        return Directory.GetFiles(directory, prefix + ".*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup after a failed/cancelled download.
        }
    }
}
