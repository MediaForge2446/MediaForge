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

    public string ToolsDirectory => _toolsDirectory;

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
            return await CheckForUpdatesCoreAsync(RuntimeInformation.ProcessArchitecture, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ToolStatus>> UpdateAllAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var architecture = RuntimeInformation.ProcessArchitecture;
            var current = await CheckForUpdatesCoreAsync(architecture, cancellationToken).ConfigureAwait(false);
            var releases = new[]
            {
                await GetYoutubeDlReleaseAsync(cancellationToken).ConfigureAwait(false),
                await GetFfmpegReleaseAsync(cancellationToken).ConfigureAwait(false)
            };

            for (var index = 0; index < releases.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var release = releases[index];
                var installed = current.First(status => status.Name.Equals(release.Name, StringComparison.OrdinalIgnoreCase));

                if (installed.IsInstalled && !installed.UpdateAvailable)
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
                    SetStatus(UpdateFailedStatus(release.Name, exception));
                    throw;
                }

                progress?.Report((double)(index + 1) / releases.Length);
            }

            return await CheckForUpdatesCoreAsync(architecture, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<ToolStatus>> CheckForUpdatesCoreAsync(Architecture architecture, CancellationToken cancellationToken)
    {
        try
        {
            var result = new[]
            {
                await InspectYoutubeDlAsync(architecture, cancellationToken).ConfigureAwait(false),
                await InspectFfmpegAsync(architecture, cancellationToken).ConfigureAwait(false)
            };
            SetStatus(result);
            return result;
        }
        catch (Exception exception)
        {
            var provider = new YtDlpPathProvider(_toolsDirectory);
            var failed = new[]
            {
                new ToolStatus
                {
                    Name = "yt-dlp",
                    InstalledVersion = ReadInstalledMarker("yt-dlp") ?? "Unknown",
                    IsInstalled = File.Exists(provider.YoutubeDLPath),
                    UpdateAvailable = false,
                    ErrorMessage = exception.Message
                },
                new ToolStatus
                {
                    Name = "FFmpeg",
                    InstalledVersion = ReadInstalledMarker("FFmpeg") ?? "Unknown",
                    IsInstalled = File.Exists(Path.Combine(_toolsDirectory, "ffmpeg.exe")),
                    UpdateAvailable = false,
                    ErrorMessage = exception.Message
                }
            };
            SetStatus(failed);
            throw;
        }
    }

    private async Task<ToolRelease> GetYoutubeDlReleaseAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync($"https://api.github.com/repos/{YoutubeDlRepository}/releases/latest", cancellationToken).ConfigureAwait(false);
        var tag = document.RootElement.GetProperty("tag_name").GetString()
            ?? throw new InvalidOperationException("yt-dlp latest release did not contain a tag name.");
        var assets = document.RootElement.GetProperty("assets");
        var executableName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe";
        return new ToolRelease(
            "yt-dlp",
            tag,
            FindAsset(assets, executableName),
            FindAsset(assets, "SHA2-256SUMS"),
            false,
            executableName);
    }

    private async Task<ToolRelease> GetFfmpegReleaseAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync($"https://api.github.com/repos/{FfmpegRepository}/releases/latest", cancellationToken).ConfigureAwait(false);
        var publishedAt = document.RootElement.GetProperty("published_at").GetString()
            ?? throw new InvalidOperationException("FFmpeg release did not contain a published date.");
        var zipName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "ffmpeg-master-latest-winarm64-gpl.zip"
            : "ffmpeg-master-latest-win64-gpl.zip";
        var assets = document.RootElement.GetProperty("assets");
        return new ToolRelease(
            "FFmpeg",
            publishedAt,
            FindAsset(assets, zipName),
            FindAsset(assets, "checksums.sha256"),
            true,
            zipName);
    }

    private async Task<ToolStatus> InspectYoutubeDlAsync(Architecture architecture, CancellationToken cancellationToken)
    {
        var release = await GetYoutubeDlReleaseAsync(cancellationToken).ConfigureAwait(false);
        var path = Path.Combine(_toolsDirectory, architecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe");
        var installed = File.Exists(path);
        var installedVersion = installed ? await TryGetProcessVersionAsync(path, "--version", cancellationToken).ConfigureAwait(false) : null;
        return new ToolStatus
        {
            Name = "yt-dlp",
            InstalledVersion = installedVersion ?? "Not installed",
            LatestVersion = release.Version,
            IsInstalled = installed,
            UpdateAvailable = !installed || !string.Equals(installedVersion?.Trim(), release.Version, StringComparison.OrdinalIgnoreCase)
        };
    }

    private async Task<ToolStatus> InspectFfmpegAsync(Architecture architecture, CancellationToken cancellationToken)
    {
        var release = await GetFfmpegReleaseAsync(cancellationToken).ConfigureAwait(false);
        var path = Path.Combine(_toolsDirectory, "ffmpeg.exe");
        var installed = File.Exists(path);
        var installedVersion = installed ? ReadInstalledMarker("FFmpeg") : null;
        if (installed && string.IsNullOrWhiteSpace(installedVersion))
        {
            installedVersion = await TryGetProcessVersionAsync(path, "-version", cancellationToken).ConfigureAwait(false);
        }
        return new ToolStatus
        {
            Name = "FFmpeg",
            InstalledVersion = installedVersion ?? "Not installed",
            LatestVersion = release.Version,
            IsInstalled = installed,
            UpdateAvailable = !installed || !string.Equals(installedVersion, release.Version, StringComparison.OrdinalIgnoreCase)
        };
    }

    private async Task InstallAsync(ToolRelease release, Architecture architecture, CancellationToken cancellationToken)
    {
        var staging = Path.Combine(_toolsDirectory, ".staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var downloadPath = Path.Combine(staging, release.AssetName);
            await DownloadToFileAsync(release.DownloadUrl, downloadPath, cancellationToken).ConfigureAwait(false);
            var expectedHash = await DownloadExpectedHashAsync(release.ChecksumUrl, release.ExpectedChecksumFileName, cancellationToken).ConfigureAwait(false);
            await VerifySha256Async(downloadPath, expectedHash, cancellationToken).ConfigureAwait(false);

            if (release.IsArchive)
            {
                var extracted = Path.Combine(staging, "extracted");
                Directory.CreateDirectory(extracted);
                ZipFile.ExtractToDirectory(downloadPath, extracted, overwriteFiles: false);
                var ffmpeg = Directory.EnumerateFiles(extracted, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new FileNotFoundException("The FFmpeg archive did not contain ffmpeg.exe.");
                cancellationToken.ThrowIfCancellationRequested();
                ReplaceFileWithBackup(ffmpeg, Path.Combine(_toolsDirectory, "ffmpeg.exe"));
                WriteInstalledMarker("FFmpeg", release.Version);
            }
            else
            {
                var destination = Path.Combine(_toolsDirectory, architecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe");
                ReplaceFileWithBackup(downloadPath, destination);
                WriteInstalledMarker("yt-dlp", release.Version);
            }
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    private static void ReplaceFileWithBackup(string sourcePath, string destinationPath)
    {
        var backup = destinationPath + ".backup";
        TryDelete(backup);
        try
        {
            if (File.Exists(destinationPath))
            {
                File.Move(destinationPath, backup);
            }
            File.Move(sourcePath, destinationPath);
            TryDelete(backup);
        }
        catch
        {
            TryDelete(destinationPath);
            if (File.Exists(backup))
            {
                File.Move(backup, destinationPath);
            }
            throw;
        }
    }

    private async Task<string> DownloadExpectedHashAsync(string checksumUrl, string assetName, CancellationToken cancellationToken)
    {
        var text = await _httpClient.GetStringAsync(checksumUrl, cancellationToken).ConfigureAwait(false);
        foreach (var rawLine in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = rawLine.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && string.Equals(parts[^1].TrimStart('*'), assetName, StringComparison.OrdinalIgnoreCase))
            {
                return parts[0];
            }
        }
        throw new InvalidOperationException($"No SHA-256 checksum was found for {assetName}.");
    }

    private static async Task VerifySha256Async(string filePath, string expectedHash, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actual = Convert.ToHexString(hash);
        if (!string.Equals(actual, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Downloaded tool checksum verification failed.");
        }
    }

    private async Task DownloadToFileAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
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

    private static async Task<string?> TryGetProcessVersionAsync(string path, string arguments, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
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
            }
        };
        try
        {
            if (!process.Start()) return null;
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
            var output = outputTask.Result.Trim();
            return string.IsNullOrWhiteSpace(output) ? null : output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private static JsonElement FindAsset(JsonElement assets, string name)
    {
        foreach (var asset in assets.EnumerateArray())
        {
            if (string.Equals(asset.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
                return asset;
        }
        throw new InvalidOperationException($"Release asset not found: {name}");
    }

    private void RefreshLocalStatus()
    {
        var provider = new YtDlpPathProvider(_toolsDirectory);
        SetStatus(new[]
        {
            new ToolStatus { Name = "yt-dlp", InstalledVersion = File.Exists(provider.YoutubeDLPath) ? "Installed" : "Not installed", IsInstalled = File.Exists(provider.YoutubeDLPath) },
            new ToolStatus { Name = "FFmpeg", InstalledVersion = File.Exists(Path.Combine(_toolsDirectory, "ffmpeg.exe")) ? "Installed" : "Not installed", IsInstalled = File.Exists(Path.Combine(_toolsDirectory, "ffmpeg.exe")) }
        });
    }

    private ToolStatus[] UpdateFailedStatus(string toolName, Exception exception) => Status.Select(status => status.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase) ? status with { ErrorMessage = exception.Message, UpdateAvailable = true } : status).ToArray();

    private void SetStatus(IEnumerable<ToolStatus> statuses)
    {
        lock (_statusLock) _status = statuses.ToList();
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? ReadInstalledMarker(string toolName)
    {
        var path = Path.Combine(_toolsDirectory, toolName.Equals("FFmpeg", StringComparison.OrdinalIgnoreCase) ? "ffmpeg.version" : "yt-dlp.version");
        try { return File.Exists(path) ? File.ReadAllText(path).Trim() : null; }
        catch { return null; }
    }

    private void WriteInstalledMarker(string toolName, string version)
    {
        var path = Path.Combine(_toolsDirectory, toolName.Equals("FFmpeg", StringComparison.OrdinalIgnoreCase) ? "ffmpeg.version" : "yt-dlp.version");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, version.Trim());
        File.Move(temporary, path, overwrite: true);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _gate.Dispose();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
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
    }
}
