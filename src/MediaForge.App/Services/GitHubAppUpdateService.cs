using System.Windows;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace MediaForge.App.Services;

public sealed record AppUpdateInfo(
    Version Version,
    string VersionLabel,
    string ReleaseName,
    string ReleaseNotes,
    string ReleaseUrl,
    string DownloadUrl,
    string Sha256);

public sealed class GitHubAppUpdateService
{
    private const string Repository = "MediaForge2446/MediaForge";
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/MediaForge2446/MediaForge/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly Version _currentVersion;

    public GitHubAppUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MediaForge-Updater/1.0");

        _currentVersion = Assembly.GetEntryAssembly()?.GetName().Version
            ?? new Version(0, 2, 0);
    }

    public Version CurrentVersion => _currentVersion;

    public async Task<AppUpdateInfo?> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .GetAsync(LatestReleaseUrl, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var release = await response.Content
            .ReadFromJsonAsync<GitHubRelease>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("GitHub returned an empty release response.");

        if (release.Draft || release.Prerelease)
            return null;

        var versionLabel = NormalizeVersionLabel(release.TagName);
        if (!Version.TryParse(versionLabel, out var latestVersion))
            return null;

        var installer = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, "Setup.exe", StringComparison.OrdinalIgnoreCase));

        if (installer is null || string.IsNullOrWhiteSpace(installer.BrowserDownloadUrl))
            return latestVersion > _currentVersion
                ? throw new InvalidDataException("The latest MediaForge release does not contain Setup.exe.")
                : null;

        if (latestVersion <= _currentVersion)
            return null;

        var digest = NormalizeSha256(installer.Digest);
        if (string.IsNullOrWhiteSpace(digest))
            throw new InvalidDataException("The latest MediaForge installer has no SHA-256 digest.");

        return new AppUpdateInfo(
            latestVersion,
            versionLabel,
            string.IsNullOrWhiteSpace(release.Name)
                ? $"MediaForge {versionLabel}"
                : release.Name,
            release.Body ?? string.Empty,
            release.HtmlUrl ?? $"https://github.com/{Repository}/releases",
            installer.BrowserDownloadUrl,
            digest);
    }

    public async Task<string> DownloadAndLaunchInstallerAsync(
        AppUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var installerPath = Path.Combine(
            Path.GetTempPath(),
            $"MediaForge-Setup-{update.VersionLabel}-{Guid.NewGuid():N}.exe");

        try
        {
            using var response = await _httpClient
                .GetAsync(
                    update.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            await using var input = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var output = new FileStream(
                installerPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 128 * 1024,
                useAsync: true);

            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            long totalRead = 0;

            while (true)
            {
                var read = await input
                    .ReadAsync(buffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                    break;

                await output
                    .WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);

                hasher.AppendData(buffer, 0, read);
                totalRead += read;

                if (contentLength is > 0)
                    progress?.Report(totalRead * 100d / contentLength.Value);
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);

            var actualHash = Convert.ToHexString(hasher.GetHashAndReset());
            if (!actualHash.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded installer failed SHA-256 verification.");

            progress?.Report(100d);

            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            Application.Current?.Dispatcher.BeginInvoke(
                new Action(() => Application.Current.Shutdown()));

            return installerPath;
        }
        catch
        {
            TryDelete(installerPath);
            throw;
        }
    }

    private static string NormalizeVersionLabel(string? tagName)
        => (tagName ?? string.Empty).Trim().TrimStart('v', 'V');

    private static string? NormalizeSha256(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
            return null;

        var value = digest.Trim();
        const string prefix = "sha256:";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            value = value[prefix.Length..];

        return value.Length == 64 &&
               value.All(character => Uri.IsHexDigit(character))
            ? value
            : null;
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
            // Best-effort cleanup of a cancelled or failed updater download.
        }
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] GitHubReleaseAsset[] Assets);

    private sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest);
}
