using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Infrastructure.Persistence;

namespace MediaForge.Infrastructure.Tools;

public sealed class ManagedToolManager : IToolManager
{
    private const string YtDlpVersion = "2026.08.19";
    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/yt-dlp.exe";
    private const string YtDlpSha256 = "66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a";

    private const string FfmpegVersion = "n9.0.1-29-gad500d59cb";
    private const string FfmpegArchiveFileName = "ffmpeg-n9.0.1-29-gad500d59cb-win64-gpl-shared-9.0.zip";
    private const string FfmpegArchiveUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-09-11-13-20/ffmpeg-n9.0.1-29-gad500d59cb-win64-gpl-shared-9.0.zip";
    private const string FfmpegChecksumsUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-09-11-13-20/checksums.sha256";

    private readonly LocalAppPaths _paths;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ManagedToolManager(LocalAppPaths paths, HttpClient? httpClient = null)
    {
        _paths = paths;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MediaForge/1.0");
    }

    public async Task<ToolPaths> EnsureToolsReadyAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.ToolsDirectory);
            var ytDlpPath = Path.Combine(_paths.ToolsDirectory, "yt-dlp.exe");
            var ffmpegPath = Path.Combine(_paths.ToolsDirectory, "ffmpeg", "bin", "ffmpeg.exe");

            var ytDlpReady = await IsSha256MatchAsync(ytDlpPath, YtDlpSha256, cancellationToken).ConfigureAwait(false);
            var ffmpegReady = File.Exists(ffmpegPath);

            if (!ytDlpReady)
                ytDlpReady = await SeedBundledYtDlpAsync(ytDlpPath, cancellationToken).ConfigureAwait(false);

            if (!ffmpegReady)
                ffmpegReady = await SeedBundledFfmpegAsync(Path.Combine(_paths.ToolsDirectory, "ffmpeg"), cancellationToken).ConfigureAwait(false);

            if (!ytDlpReady)
                await InstallYtDlpAsync(ytDlpPath, cancellationToken).ConfigureAwait(false);

            if (!ffmpegReady)
                await InstallFfmpegAsync(ffmpegPath, cancellationToken).ConfigureAwait(false);

            return new ToolPaths(ytDlpPath, ffmpegPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task<bool> SeedBundledYtDlpAsync(string destinationPath, CancellationToken cancellationToken)
    {
        var bundledPath = Path.Combine(_paths.BundledToolsDirectory, "yt-dlp.exe");
        return SeedBundledFileAsync(bundledPath, destinationPath, YtDlpSha256, cancellationToken);
    }

    private async Task<bool> SeedBundledFfmpegAsync(string destinationDirectory, CancellationToken cancellationToken)
    {
        var bundledDirectory = Path.Combine(_paths.BundledToolsDirectory, "ffmpeg");
        var bundledExecutable = Path.Combine(bundledDirectory, "bin", "ffmpeg.exe");
        if (!File.Exists(bundledExecutable))
            return false;

        var bundledHash = await ComputeSha256Async(bundledExecutable, cancellationToken).ConfigureAwait(false);
        var existingExecutable = Path.Combine(destinationDirectory, "bin", "ffmpeg.exe");
        if (File.Exists(existingExecutable) && string.Equals(bundledHash, await ComputeSha256Async(existingExecutable, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
            return true;

        var stagingDirectory = destinationDirectory + ".bundle";
        TryDeleteDirectory(stagingDirectory);
        CopyDirectory(bundledDirectory, stagingDirectory);

        try
        {
            ReplaceDirectoryAtomically(stagingDirectory, destinationDirectory);
            return File.Exists(existingExecutable);
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    private static async Task<bool> SeedBundledFileAsync(
        string sourcePath,
        string destinationPath,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
            return false;

        if (!string.Equals(
                await ComputeSha256Async(sourcePath, cancellationToken).ConfigureAwait(false),
                expectedHash,
                StringComparison.OrdinalIgnoreCase))
            return false;

        var tempPath = destinationPath + ".bundle";
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, tempPath, overwrite: true);
        ReplaceAtomically(tempPath, destinationPath);
        return true;
    }

    private async Task InstallYtDlpAsync(string destinationPath, CancellationToken cancellationToken)
    {
        var tempPath = destinationPath + ".download";
        await DownloadAsync(YtDlpUrl, tempPath, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await IsSha256MatchAsync(tempPath, YtDlpSha256, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException($"yt-dlp {YtDlpVersion} failed SHA-256 verification.");

            ReplaceAtomically(tempPath, destinationPath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private async Task InstallFfmpegAsync(string destinationPath, CancellationToken cancellationToken)
    {
        var archivePath = Path.Combine(_paths.ToolsDirectory, $"ffmpeg-{FfmpegVersion}.zip.download");
        var extractionPath = Path.Combine(_paths.ToolsDirectory, "ffmpeg.download");

        await DownloadAsync(FfmpegArchiveUrl, archivePath, cancellationToken).ConfigureAwait(false);

        try
        {
            var checksums = await _httpClient.GetStringAsync(FfmpegChecksumsUrl, cancellationToken).ConfigureAwait(false);
            var expectedHash = FindSha256(checksums, FfmpegArchiveFileName)
                ?? throw new InvalidDataException($"No checksum was published for {FfmpegArchiveFileName}.");

            if (!await IsSha256MatchAsync(archivePath, expectedHash, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("FFmpeg archive failed SHA-256 verification.");

            TryDeleteDirectory(extractionPath);
            ZipFile.ExtractToDirectory(archivePath, extractionPath);

            var extractedRoot = Directory.GetDirectories(extractionPath).SingleOrDefault()
                ?? throw new InvalidDataException("FFmpeg archive layout is invalid.");
            var extractedExecutable = Path.Combine(extractedRoot, "bin", "ffmpeg.exe");
            if (!File.Exists(extractedExecutable))
                throw new InvalidDataException("FFmpeg executable was not found in the verified archive.");

            ReplaceDirectoryAtomically(extractedRoot, Path.Combine(_paths.ToolsDirectory, "ffmpeg"));
        }
        catch
        {
            TryDelete(archivePath);
            TryDeleteDirectory(extractionPath);
            throw;
        }
        finally
        {
            TryDelete(archivePath);
            TryDeleteDirectory(extractionPath);
        }
    }

    private async Task DownloadAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var responseStream = await _httpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        await using var outputStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true);
        await responseStream.CopyToAsync(outputStream, 128 * 1024, cancellationToken).ConfigureAwait(false);
        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsSha256MatchAsync(string path, string expectedHash, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return false;

        var hash = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
        return hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var target = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string? FindSha256(string checksums, string fileName)
    {
        foreach (var line in checksums.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line.Trim(), @"^(?<hash>[0-9a-fA-F]{64})\s+\*?(?<file>.+)$");
            if (match.Success && string.Equals(Path.GetFileName(match.Groups["file"].Value.Trim()), fileName, StringComparison.OrdinalIgnoreCase))
                return match.Groups["hash"].Value;
        }

        return null;
    }

    private static void ReplaceAtomically(string sourcePath, string destinationPath)
    {
        var backupPath = destinationPath + ".bak";
        if (File.Exists(destinationPath))
            File.Replace(sourcePath, destinationPath, backupPath, ignoreMetadataErrors: true);
        else
            File.Move(sourcePath, destinationPath);

        TryDelete(backupPath);
    }

    private static void ReplaceDirectoryAtomically(string sourceDirectory, string destinationDirectory)
    {
        var backupDirectory = destinationDirectory + ".bak";
        TryDeleteDirectory(backupDirectory);

        if (Directory.Exists(destinationDirectory))
            Directory.Move(destinationDirectory, backupDirectory);

        Directory.Move(sourceDirectory, destinationDirectory);
        TryDeleteDirectory(backupDirectory);
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
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
