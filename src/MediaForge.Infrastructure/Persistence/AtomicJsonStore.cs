using System.Text.Json;

namespace MediaForge.Infrastructure.Persistence;

public sealed class AtomicJsonStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<T?> LoadAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            return default;

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException) when (File.Exists(path + ".bak"))
        {
            return await LoadBackupAsync<T>(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException) when (File.Exists(path + ".bak"))
        {
            return await LoadBackupAsync<T>(path, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The persistence file must have a parent directory.");
        Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        var backupPath = path + ".bak";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private async Task<T?> LoadBackupAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path + ".bak");
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
