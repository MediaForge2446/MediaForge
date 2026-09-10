using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Services;

public sealed class AppPersistenceService : IAppPersistenceService
{
    private const int CurrentSchemaVersion = 1;
    private const string FileName = "state.json";
    private const string TempSuffix = ".tmp";
    private const string BackupSuffix = ".bak";
    private const string HashSuffix = ".sha256";

    private readonly string _directory;
    private readonly string _statePath;
    private readonly string _tempPath;
    private readonly string _backupPath;
    private readonly string _hashPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppPersistenceService(string? directory = null)
    {
        _directory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MediaForge")
                : directory);

        _statePath = Path.Combine(_directory, FileName);
        _tempPath = _statePath + TempSuffix;
        _backupPath = _statePath + BackupSuffix;
        _hashPath = _statePath + HashSuffix;
    }

    public async Task<AppStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_directory);

            var candidates = new[]
            {
                _statePath,
                _backupPath
            };

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var snapshot = await TryReadSnapshotAsync(candidate, cancellationToken).ConfigureAwait(false);
                if (snapshot is null || snapshot.SchemaVersion != CurrentSchemaVersion)
                {
                    continue;
                }

                if (!string.Equals(candidate, _statePath, StringComparison.OrdinalIgnoreCase))
                {
                    await RestoreBackupAsync(candidate, cancellationToken).ConfigureAwait(false);
                }

                return Sanitize(snapshot);
            }

            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_directory);

            var normalized = Sanitize(snapshot with
            {
                SchemaVersion = CurrentSchemaVersion,
                SavedAtUtc = DateTimeOffset.UtcNow
            });

            var json = JsonSerializer.Serialize(normalized, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));

            await WriteBytesAsync(_tempPath, bytes, cancellationToken).ConfigureAwait(false);
            await WriteTextAsync(_hashPath + TempSuffix, hash, cancellationToken).ConfigureAwait(false);

            if (File.Exists(_statePath))
            {
                ReplaceWithBackup(_tempPath, _statePath, _backupPath);
            }
            else
            {
                File.Move(_tempPath, _statePath);
            }

            ReplaceHashFile(_hashPath + TempSuffix, _hashPath);
        }
        finally
        {
            TryDelete(_tempPath);
            TryDelete(_hashPath + TempSuffix);
            _gate.Release();
        }
    }

    private async Task<AppStateSnapshot?> TryReadSnapshotAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return null;
            }

            if (string.Equals(path, _statePath, StringComparison.OrdinalIgnoreCase) && !await VerifyHashAsync(bytes, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return JsonSerializer.Deserialize<AppStateSnapshot>(bytes, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task<bool> VerifyHashAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (!File.Exists(_hashPath))
        {
            return true;
        }

        var expected = (await File.ReadAllTextAsync(_hashPath, cancellationToken).ConfigureAwait(false)).Trim();
        if (expected.Length != 64)
        {
            return false;
        }

        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(_statePath))
        {
            TryDelete(_statePath);
        }

        File.Copy(backupPath, _statePath, overwrite: true);
        var bytes = await File.ReadAllBytesAsync(_statePath, cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(_hashPath, Convert.ToHexString(SHA256.HashData(bytes)), cancellationToken).ConfigureAwait(false);
    }

    private static AppStateSnapshot Sanitize(AppStateSnapshot snapshot)
    {
        var folders = snapshot.RootFolders
            .Where(folder => !string.IsNullOrWhiteSpace(folder.Id) && !string.IsNullOrWhiteSpace(folder.Path))
            .GroupBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(folder => folder.AddedAtUtc).First())
            .ToArray();

        var changes = snapshot.PendingChanges
            .Where(change => change.Id != Guid.Empty && !string.IsNullOrWhiteSpace(change.SourcePath))
            .GroupBy(change => change.Id)
            .Select(group => group.Last())
            .ToArray();

        return snapshot with
        {
            RootFolders = folders,
            PendingChanges = changes
        };
    }

    private static async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);

        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteTextAsync(string path, string text, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await WriteBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static void ReplaceWithBackup(string sourcePath, string destinationPath, string backupPath)
    {
        TryDelete(backupPath);
        File.Move(destinationPath, backupPath);

        try
        {
            File.Move(sourcePath, destinationPath);
        }
        catch
        {
            if (File.Exists(backupPath) && !File.Exists(destinationPath))
            {
                File.Move(backupPath, destinationPath);
            }
            throw;
        }
    }

    private static void ReplaceHashFile(string sourcePath, string destinationPath)
    {
        TryDelete(destinationPath);
        File.Move(sourcePath, destinationPath);
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

    public void Dispose() => _gate.Dispose();
}
