using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace MediaForge.Services;

public sealed class DownloadService : IDownloadService
{
    private readonly YtDlpPathProvider _paths;
    private readonly object _sync = new();

    public DownloadService(YtDlpPathProvider? paths = null)
    {
        _paths = paths ?? new YtDlpPathProvider();
    }

    public async Task<string> DownloadAsync(
        DownloadTask task,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(task.SourceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(task.TargetPath);

        if (!Uri.TryCreate(task.SourceUrl.Trim(), UriKind.Absolute, out var sourceUri) ||
            (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The source URL must be a valid HTTP or HTTPS URL.", nameof(task));
        }

        cancellationToken.ThrowIfCancellationRequested();
        _paths.EnsureReady();

        var targetPath = Path.GetFullPath(task.TargetPath);
        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException("The target path must include a destination directory.");
        }

        Directory.CreateDirectory(targetDirectory);

        var targetFileName = Path.GetFileName(targetPath);
        if (string.IsNullOrWhiteSpace(targetFileName))
        {
            throw new InvalidOperationException("The target path must include a file name.");
        }

        if (File.Exists(targetPath))
        {
            throw new IOException($"The destination file already exists: {targetPath}");
        }

        var ytdl = new YoutubeDL
        {
            YoutubeDLPath = _paths.YoutubeDLPath,
            FFmpegPath = _paths.FFmpegPath,
            OutputFolder = targetDirectory,
            OutputFileTemplate = BuildOutputTemplate(task.Format, targetFileName)
        };

        var downloadProgress = new Progress<DownloadProgress>(state =>
        {
            if (state.Progress >= 0 && state.Progress <= 1)
            {
                progress?.Report(state.Progress);
            }
        });

        var result = task.Format switch
        {
            MediaFormat.Mp3 => await ytdl.RunAudioDownload(
                sourceUri.ToString(),
                AudioConversionFormat.Mp3,
                progress: downloadProgress,
                ct: cancellationToken).ConfigureAwait(false),

            MediaFormat.Mp4 => await ytdl.RunVideoDownload(
                sourceUri.ToString(),
                progress: downloadProgress,
                ct: cancellationToken,
                overrideOptions: new OptionSet
                {
                    Format = "bestvideo+bestaudio/best",
                    MergeOutputFormat = "mp4",
                    NoPlaylist = true
                }).ConfigureAwait(false),

            _ => throw new ArgumentOutOfRangeException(nameof(task.Format), task.Format, "Unsupported media format.")
        };

        if (!result.Success)
        {
            var details = result.ErrorOutput is { Length: > 0 }
                ? string.Join(Environment.NewLine, result.ErrorOutput)
                : "yt-dlp reported an unknown download failure.";

            throw new InvalidOperationException(details);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var producedPath = result.Data;
        if (string.IsNullOrWhiteSpace(producedPath) || !File.Exists(producedPath))
        {
            producedPath = FindProducedFile(targetDirectory, targetFileName);
        }

        if (string.IsNullOrWhiteSpace(producedPath) || !File.Exists(producedPath))
        {
            throw new FileNotFoundException("yt-dlp completed without producing the expected output file.", targetPath);
        }

        producedPath = Path.GetFullPath(producedPath);
        if (!string.Equals(producedPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            MoveProducedFile(producedPath, targetPath);
        }

        progress?.Report(1d);
        return targetPath;
    }

    private static string BuildOutputTemplate(MediaFormat format, string targetFileName)
    {
        var extension = Path.GetExtension(targetFileName);
        var stem = targetFileName[..^extension.Length];
        return format == MediaFormat.Mp4
            ? $"{stem}.%(ext)s"
            : $"{stem}.mp3";
    }

    private static string? FindProducedFile(string directory, string targetFileName)
    {
        var expectedName = Path.GetFileNameWithoutExtension(targetFileName);
        var extension = Path.GetExtension(targetFileName);

        return Directory.EnumerateFiles(directory, $"{expectedName}.*", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault(path => string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase));
    }

    private static void MoveProducedFile(string sourcePath, string targetPath)
    {
        lock (typeof(DownloadService))
        {
            if (File.Exists(targetPath))
            {
                throw new IOException($"The destination file already exists: {targetPath}");
            }

            File.Move(sourcePath, targetPath);
        }
    }
}
