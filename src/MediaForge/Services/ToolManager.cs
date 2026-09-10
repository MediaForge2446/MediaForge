using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Services;

public sealed class ToolManager : IToolManager, IDisposable
{
    private const string YoutubeDlRepository = "yt-dlp/yt-dlp";
    private const string FfmpegRepository = "BtbN/FFmpeg-Builds";
    private const int HttpTimeoutSeconds = 120;

    private readonly HttpClient _httpClient;
    private readonly string _toolsDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _statusLock = new();
    private List<ToolStatus> _status = new();

    public IReadOnlyList<ToolStatus> Status
    {
        get
        {
            lock (_statusLock)
            {
                return _status.ToArray();
            }
        }
    }

    public event EventHandler? StatusChanged;

    public ToolManager(string? toolsDirectory = null, HttpClient? httpClient = null)
    {
        _toolsDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(toolsDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "Tools")
                : toolsDirectory);

        Directory.CreateDirectory(_toolsDirectory);

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MediaForge/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        RefreshLocalStatus();
    }

    public async Task<IReadOnlyList<ToolStatus>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var architecture = RuntimeInformation.ProcessArchitecture;
            var result = new[]
            {
                await InspectYoutubeDlAsync(architecture, cancellationToken).ConfigureAwait(false),
                await InspectFfmpegAsync(architecture, cancellationToken).ConfigureAwait(false)
            };

            SetStatus(result);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ToolStatus>> UpdateAllAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var architecture = RuntimeInformation.ProcessArchitecture;
            var releases = new[]
            {
                await GetYoutubeDlReleaseAsync(cancellationToken).ConfigureAwait(false),
                await GetFfmpegReleaseAsync(cancellationToken).ConfigureAwait(false)
            };

            for (var index = 0; index < releases.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var release = releases[index];
                var local = release.Name == "yt-dlp"
                    ? Path.Combine(_toolsDirectory, architecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe")
                    : Path.Combine(_toolsDirectory, "ffmpeg.exe");

                if (release.IsCurrent(local))
                {
                    progress?.Report((double)(index + 1) / releases.Length);
                    continue;
                }

                try
                {
                    await InstallAsync(release, architecture, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    SetStatus(UpdateFailedStatus(release, exception));
                    throw;
                }

                progress?.Report((double)(index + 1) / releases.Length);
            }

            RefreshLocalStatus();
            await CheckForUpdatesCoreAsync(architecture, cancellationToken).ConfigureAwait(false);
            return Status;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CheckForUpdatesCoreAsync(Architecture architecture, CancellationToken cancellationToken)
    {
        var result = new[]
        {
            await InspectYoutubeDlAsync(architecture, cancellationToken).ConfigureAwait(false),
            await InspectFfmpegAsync(architecture, cancellationToken).ConfigureAwait(false)
        };

        SetStatus(result);
    }

    private async Task<ToolRelease> GetYoutubeDlReleaseAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            $"https://api.github.com/repos/{YoutubeDlRepository}/releases/latest",
            cancellationToken).ConfigureAwait(false);

        var tag = document.RootElement.GetProperty("tag_name").GetString()
            ?? throw new InvalidOperationException("yt-dlp latest release did not contain a tag name.");

        var assets = document.RootElement.GetProperty("assets");
        var architecture = RuntimeInformation.ProcessArchitecture;
        var executableName = architecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe";
        var checksumAsset = FindAsset(assets, "SHA2-256SUMS");
        var executableAsset = FindAsset(assets, executableName);

        return new ToolRelease(
            "yt-dlp",
            tag,
            executableAsset,
            checksumAsset,
            isArchive: false,
            expectedChecksumFileName: executableName);
    }

    private async Task<ToolRelease> GetFfmpegReleaseAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            $"https://api.github.com/repos/{FfmpegRepository}/releases/latest",
            cancellationToken).ConfigureAwait(false);

        var publishedAt = document.RootElement.GetProperty("published_at").GetString()
            ?? throw new InvalidOperationException("FFmpeg release did not contain a published date.");

        var architecture = RuntimeInformation.ProcessArchitecture;
        var zipName = architecture == Architecture.Arm64
            ? "ffmpeg-master-latest-winarm64-gpl.zip"
            : "ffmpeg-master-latest-win64-gpl.zip";

        var assets = document.RootElement.GetProperty("assets");
        var archiveAsset = FindAsset(assets, zipName);
        var checksumAsset = FindAsset(assets, "checksums.sha256");

        return new ToolRelease(
            "FFmpeg",
            publishedAt,
            archiveAsset,
            checksumAsset,
            isArchive: true,
            expectedChecksumFileName: zipName);
    }

    private async Task<ToolStatus> InspectYoutubeDlAsync(
        Architecture architecture,
        CancellationToken cancellationToken)
    {
        var release = await GetYoutubeDlReleaseAsync(cancellationToken).ConfigureAwait(false);
        var path = Path.Combine(_toolsDirectory, architecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe");
        var installedVersion = await TryGetProcessVersionAsync(path, "--version", cancellationToken).ConfigureAwait(false) ?? "Not installed";
        var installed = File.Exists(path);

        return new ToolStatus
        {
            Name = "yt-dlp",
            InstalledVersion = installedVersion,
            LatestVersion = release.Version,
            IsInstalled = installed,
            UpdateAvailable = installedVersion != release.Version,
            ErrorMessage = null
        };
    }

    private async Task<ToolStatus> InspectFfmpegAsync(
        Architecture architecture,
        CancellationToken cancellationToken)
    {
        var release = await GetFfmpegReleaseAsync(cancellationToken).ConfigureAwait(false);
        var path = Path.Combine(_toolsDirectory, "ffmpeg.exe");
        var installedVersion = await TryGetProcessVersionAsync(path, "-version", cancellationToken).ConfigureAwait(false) ?? "Not installed";
        var installed = File.Exists(path);

        return new ToolStatus
        {
            Name = "FFmpeg",
            InstalledVersion = installedVersion,
            LatestVersion = release.Version,
            IsInstalled = installed,
            UpdateAvailable = !installed || !installedVersion.Contains(release.Version, StringComparison.OrdinalIgnoreCase),
            ErrorMessage = null
        };
    }

    private async Task InstallAsync(
        ToolRelease release,
        Architecture architecture,
        CancellationToken cancellationToken)
    {
        var stagingDirectory = Path.Combine(_toolsDirectory, ".staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var archivePath = Path.Combine(stagingDirectory, release.AssetName);
            await DownloadToFileAsync(release.DownloadUrl, archivePath, cancellationToken).ConfigureAwait(false);
            var expectedHash = await DownloadExpectedHashAsync(release.ChecksumUrl, release.ExpectedChecksumFileName, cancellationToken).ConfigureAwait(false);
            await VerifySha256Async(archivePath, expectedHash, cancellationToken).ConfigureAwait(false);

            if (release.IsArchive)
            {
                await InstallFfmpegArchiveAsync(archivePath, stagingDirectory, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var destinationName = architecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe";
                await ReplaceFileWithBackupAsync(archivePath, Path.Combine(_toolsDirectory, destinationName), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private async Task InstallFfmpegArchiveAsync(
        string archivePath,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        var extractedDirectory = Path.Combine(stagingDirectory, "extracted");
        Directory.CreateDirectory(extractedDirectory);
        ZipFile.ExtractToDirectory(archivePath, extractedDirectory, overwriteFiles: false);

        var ffmpegPath = Directory.EnumerateFiles(extractedDirectory, "ffmpeg.exe", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (ffmpegPath is null)
        {
            throw new FileNotFoundException("The FFmpeg archive did not contain ffmpeg.exe.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        await ReplaceFileWithBackupAsync(ffmpegPath, Path.Combine(_toolsDirectory, "ffmpeg.exe"), cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReplaceFileWithBackupAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var backupPath = destinationPath + ".backup";
        TryDelete(backupPath);

        try
        {
            if (File.Exists(destinationPath))
            {
                File.Move(destinationPath, backupPath);
            }

            File.Move(sourcePath, destinationPath);
            TryDelete(backupPath);
        }
        catch
        {
            TryDelete(destinationPath);
            if (File.Exists(backupPath))
            {
                File.Move(backupPath, destinationPath);
            }

            throw;
        }

        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<string> DownloadExpectedHashAsync(
        string checksumUrl,
        string assetName,
        CancellationToken cancellationToken)
    {
        var text = await _httpClient.GetStringAsync(checksumUrl, cancellationToken).ConfigureAwait(false);
        foreach (var rawLine in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var name = parts[^1].TrimStart('*');
                if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
                {
                    return parts[0];
                }
            }
        }

        throw new InvalidOperationException($"No SHA-256 checksum was found for {assetName}.");
    }

    private static async Task VerifySha256Async(
        string filePath,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actualHash = Convert.ToHexString(hash);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualHash),
                Convert.FromHexString(expectedHash.Trim())))
        {
            throw new InvalidDataException("Downloaded tool checksum verification failed.");
        }
    }

    private async Task DownloadToFileAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(
            targetPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        await source.CopyToAsync(destination, 128 * 1024, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> TryGetProcessVersionAsync(
        string path,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        try
        {
            if (!process.Start())
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken).AsTask();
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken).AsTask();
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

            var output = outputTask.Result.Trim();
            return output.Length == 0 ? null : output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        }
        catch
        {
            return null;
        }
    }

    private static JsonElement FindAsset(JsonElement assets, string name)
    {
        foreach (var asset in assets.EnumerateArray())
        {
            if (string.Equals(asset.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }
        }

        throw new InvalidOperationException($"Release asset not found: {name}");
    }

    private void RefreshLocalStatus()
    {
        var architecture = RuntimeInformation.ProcessArchitecture;
        var ytPath = Path.Combine(_toolsDirectory, architecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe");
        var ffmpegPath = Path.Combine(_toolsDirectory, "ffmpeg.exe");

        SetStatus(new[]
        {
            new ToolStatus { Name = "yt-dlp", InstalledVersion = File.Exists(ytPath) ? "Installed" : "Not installed", IsInstalled = File.Exists(ytPath) },
            new ToolStatus { Name = "FFmpeg", InstalledVersion = File.Exists(ffmpegPath) ? "Installed" : "Not installed", IsInstalled = File.Exists(ffmpegPath) }
        });
    }

    private ToolStatus[] UpdateFailedStatus(ToolRelease release, Exception exception) =>
        Status.Select(status => status.Name.Equals(release.Name, StringComparison.OrdinalIgnoreCase)
            ? status with { ErrorMessage = exception.Message, UpdateAvailable = true }
            : status).ToArray();

    private void SetStatus(IEnumerable<ToolStatus> statuses)
    {
        lock (_statusLock)
        {
            _status = statuses.ToList();
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _gate.Dispose();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
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
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record ToolRelease(
        string Name,
        string Version,
        JsonElement Asset,
        JsonElement ChecksumAsset,
        bool IsArchive,
        string ExpectedChecksumFileName)
    {
        public string AssetName => Asset.GetProperty("name").GetString()!;
        public string DownloadUrl => Asset.GetProperty("browser_download_url").GetString()!;
        public string ChecksumUrl => ChecksumAsset.GetProperty("browser_download_url").GetString()!;

        public bool IsCurrent(string path) => File.Exists(path) && !IsArchive;
    }
}
