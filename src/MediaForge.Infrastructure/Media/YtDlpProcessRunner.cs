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
    private static readonly Regex ProgressRegex = new(@"(?<percent>\d+(?:\.\d+)?)%", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("A source URL is required.", nameof(url));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("An output path is required.", nameof(outputPath));

        var tools = await _toolManager.EnsureToolsReadyAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? _paths.AppDirectory);

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

        process.StartInfo.ArgumentList.Add("--newline");
        process.StartInfo.ArgumentList.Add("--no-playlist");
        process.StartInfo.ArgumentList.Add("--no-warnings");
        process.StartInfo.ArgumentList.Add("--ffmpeg-location");
        process.StartInfo.ArgumentList.Add(tools.FfmpegExecutablePath);

        switch (format)
        {
            case MediaFormat.Mp3:
                process.StartInfo.ArgumentList.Add("-x");
                process.StartInfo.ArgumentList.Add("--audio-format");
                process.StartInfo.ArgumentList.Add("mp3");
                process.StartInfo.ArgumentList.Add("--audio-quality");
                process.StartInfo.ArgumentList.Add("0");
                break;
            case MediaFormat.Mp4:
                process.StartInfo.ArgumentList.Add("--merge-output-format");
                process.StartInfo.ArgumentList.Add("mp4");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }

        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(outputPath);
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
                // The process may exit between HasExited and Kill.
            }
        });

        var stderr = new List<string>();
        var stdoutTask = ConsumeAsync(process.StandardOutput, progress, cancellationToken, null);
        var stderrTask = ConsumeAsync(process.StandardError, progress: null, cancellationToken, stderr);

        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
        {
            var detail = string.Join(Environment.NewLine, stderr.Where(x => !string.IsNullOrWhiteSpace(x)).TakeLast(8));
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                ? $"yt-dlp exited with code {process.ExitCode}."
                : detail);
        }

        progress?.Report(100d);
    }

    private static async Task ConsumeAsync(
        StreamReader reader,
        IProgress<double>? progress,
        CancellationToken cancellationToken,
        ICollection<string>? lines)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines?.Add(line);
            if (progress is null)
                continue;

            var match = ProgressRegex.Match(line);
            if (match.Success &&
                double.TryParse(match.Groups["percent"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                progress.Report(Math.Clamp(percent, 0d, 100d));
            }
        }
    }
}
