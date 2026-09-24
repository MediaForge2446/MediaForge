using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Infrastructure.Persistence;

namespace MediaForge.Infrastructure.Media;

public sealed class YtDlpProcessRunner : IYtDlpRunner
{
    private static readonly Regex PercentRegex =
        new(@"(?<percent>\d+(?:\.\d+)?)%", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SpeedRegex =
        new(@"(?<speed>\d+(?:[.,]\d+)?)\s*(?<unit>[KMGTP]?i?B/s|B/s)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EtaRegex =
        new(@"ETA\s+(?<eta>\d{2}:\d{2}(?::\d{2})?)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TotalSizeRegex =
        new(@"of\s+~?\s*(?<size>\d+(?:[.,]\d+)?)\s*(?<unit>[KMGTPE]?i?B)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IToolManager _toolManager;
    private readonly LocalAppPaths _paths;

    public YtDlpProcessRunner(IToolManager toolManager, LocalAppPaths paths)
    {
        _toolManager = toolManager;
        _paths = paths;
    }

    public async Task RunAsync(
        string url,
        string outputPath,
        MediaFormat format,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        MediaQuality quality = MediaQuality.High192K,
        MediaVideoQuality videoQuality = MediaVideoQuality.Balanced720p)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("A source URL is required.", nameof(url));

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("An output path is required.", nameof(outputPath));

        var tools = await _toolManager
            .EnsureToolsReadyAsync(cancellationToken)
            .ConfigureAwait(false);

        Directory.CreateDirectory(
            Path.GetDirectoryName(outputPath) ?? _paths.AppDirectory);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = tools.YtDlpExecutablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(outputPath) ?? _paths.AppDirectory
            },
            EnableRaisingEvents = true
        };

        foreach (var argument in YtDlpArgumentBuilder.Build(
                     outputPath,
                     tools.FfmpegExecutablePath,
                     format, quality, videoQuality))
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.ArgumentList.Add(url);

        if (!process.Start())
            throw new InvalidOperationException("Unable to start yt-dlp.");

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort process cleanup during cancellation.
            }
        });

        var stderr = new List<string>();

        var stdoutTask = ConsumeAsync(
            process.StandardOutput,
            progress,
            cancellationToken,
            null);

        var stderrTask = ConsumeAsync(
            process.StandardError,
            null,
            cancellationToken,
            stderr);

        await Task.WhenAll(
                stdoutTask,
                stderrTask,
                process.WaitForExitAsync(cancellationToken))
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            var detail = string.Join(
                Environment.NewLine,
                stderr.Where(x => !string.IsNullOrWhiteSpace(x)).TakeLast(8));

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detail)
                    ? $"yt-dlp exited with code {process.ExitCode}."
                    : detail);
        }

        progress?.Report(new DownloadProgress(100, "Completed"));
    }

    private static async Task ConsumeAsync(
        StreamReader reader,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken,
        ICollection<string>? lines)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines?.Add(line);

            if (progress is null)
                continue;

            var parsed = ParseProgress(line);
            if (parsed is not null)
                progress.Report(parsed);
        }
    }

    private static DownloadProgress? ParseProgress(string line)
    {
        var percentMatch = PercentRegex.Match(line);
        if (!percentMatch.Success ||
            !double.TryParse(
                percentMatch.Groups["percent"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var percent))
        {
            return null;
        }

        double? speed = null;
        var speedMatch = SpeedRegex.Match(line);

        if (speedMatch.Success &&
            double.TryParse(
                speedMatch.Groups["speed"].Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var numericSpeed))
        {
            speed = numericSpeed * GetUnitMultiplier(speedMatch.Groups["unit"].Value);
        }

        TimeSpan? eta = null;
        var etaMatch = EtaRegex.Match(line);
        if (etaMatch.Success &&
            TimeSpan.TryParse(
                etaMatch.Groups["eta"].Value,
                CultureInfo.InvariantCulture,
                out var parsedEta))
        {
            eta = parsedEta;
        }

        long? totalBytes = null;
        var sizeMatch = TotalSizeRegex.Match(line);
        if (sizeMatch.Success &&
            double.TryParse(
                sizeMatch.Groups["size"].Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var sizeValue))
        {
            totalBytes = (long)Math.Round(
                sizeValue * GetUnitMultiplier(sizeMatch.Groups["unit"].Value));
        }

        long? downloadedBytes = totalBytes is > 0
            ? (long)Math.Round(totalBytes.Value * Math.Clamp(percent, 0d, 100d) / 100d)
            : null;

        return new DownloadProgress(
            Math.Clamp(percent, 0d, 100d),
            "Downloading",
            speed,
            eta,
            downloadedBytes,
            totalBytes);
    }

    private static double GetUnitMultiplier(string unit) =>
        unit.ToUpperInvariant() switch
        {
            "B/S" => 1d,
            "KB/S" => 1000d,
            "MB/S" => 1000d * 1000d,
            "GB/S" => 1000d * 1000d * 1000d,
            "TB/S" => 1000d * 1000d * 1000d * 1000d,
            "PB/S" => 1000d * 1000d * 1000d * 1000d * 1000d,
            "KB" => 1000d,
            "MB" => 1000d * 1000d,
            "GB" => 1000d * 1000d * 1000d,
            "TB" => 1000d * 1000d * 1000d * 1000d,
            "PB" => 1000d * 1000d * 1000d * 1000d * 1000d,
            "KIB" => 1024d,
            "MIB" => 1024d * 1024d,
            "GIB" => 1024d * 1024d * 1024d,
            "TIB" => 1024d * 1024d * 1024d * 1024d,
            "PIB" => 1024d * 1024d * 1024d * 1024d * 1024d,
            "KIB/S" => 1024d,
            "MIB/S" => 1024d * 1024d,
            "GIB/S" => 1024d * 1024d * 1024d,
            "TIB/S" => 1024d * 1024d * 1024d * 1024d,
            "PIB/S" => 1024d * 1024d * 1024d * 1024d * 1024d,
            _ => 1d
        };
}
