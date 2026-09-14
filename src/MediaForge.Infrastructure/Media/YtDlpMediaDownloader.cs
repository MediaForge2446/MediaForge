using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Infrastructure.Media;

public sealed class YtDlpMediaDownloader : IMediaDownloader
{
    private readonly IYtDlpRunner _runner;
    public YtDlpMediaDownloader(IYtDlpRunner runner) => _runner = runner;

    public async Task DownloadAsync(string sourceUrl, string outputPath, MediaFormat format, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl)) throw new ArgumentException("A source URL is required.", nameof(sourceUrl));
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("An output path is required.", nameof(outputPath));
        var extension = GetExtension(format);
        var finalPath = Path.GetExtension(outputPath).Equals(extension, StringComparison.OrdinalIgnoreCase) ? outputPath : Path.ChangeExtension(outputPath, extension);
        var stagingBase = finalPath + ".mediaforge-temp";
        var parentDirectory = Path.GetDirectoryName(finalPath) ?? ".";
        TryDeleteMatching(stagingBase);
        try
        {
            Directory.CreateDirectory(parentDirectory);
            progress?.Report(new DownloadProgress(0, "מתחיל הורדה"));
            await _runner.RunAsync(sourceUrl, stagingBase, format,
                new Progress<double>(percent => progress?.Report(new DownloadProgress(percent, "מוריד"))), cancellationToken).ConfigureAwait(false);
            var produced = ResolveProducedPath(stagingBase, extension);
            if (produced is null) throw new FileNotFoundException("ההורדה הסתיימה ללא קובץ פלט תקין.");
            cancellationToken.ThrowIfCancellationRequested();
            var replacement = finalPath + ".replace";
            TryDelete(replacement);
            File.Move(produced, replacement);
            try
            {
                if (File.Exists(finalPath)) File.Replace(replacement, finalPath, null, ignoreMetadataErrors: true);
                else File.Move(replacement, finalPath);
            }
            finally { TryDelete(replacement); }
            progress?.Report(new DownloadProgress(100, "הושלם"));
        }
        catch { TryDeleteMatching(stagingBase); throw; }
    }

    private static string GetExtension(MediaFormat format) => format switch
    {
        MediaFormat.Mp3 => ".mp3", MediaFormat.Mp4 => ".mp4", MediaFormat.Wav => ".wav", MediaFormat.M4a => ".m4a",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static string? ResolveProducedPath(string stagingBase, string extension)
    {
        var expected = stagingBase + extension;
        if (File.Exists(expected)) return expected;
        var directory = Path.GetDirectoryName(stagingBase);
        if (directory is null || !Directory.Exists(directory)) return null;
        var prefix = Path.GetFileName(stagingBase);
        return Directory.GetFiles(directory, prefix + ".*", SearchOption.TopDirectoryOnly).FirstOrDefault(path => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static void TryDeleteMatching(string prefix)
    {
        var directory = Path.GetDirectoryName(prefix);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        foreach (var path in Directory.GetFiles(directory, Path.GetFileName(prefix) + "*", SearchOption.TopDirectoryOnly)) TryDelete(path);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
