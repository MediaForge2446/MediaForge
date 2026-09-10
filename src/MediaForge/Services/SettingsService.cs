using System.Text.Json;
using System.Text.Json.Serialization;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _settingsPath;
    private AppSettings _current;

    public AppSettings Current => _current;

    public event EventHandler? Changed;

    public SettingsService(string? applicationDataDirectory = null)
    {
        var root = string.IsNullOrWhiteSpace(applicationDataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediaForge")
            : Path.GetFullPath(applicationDataDirectory);

        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");
        _current = Load();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var temporaryPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
            _current = settings;
        }
        finally
        {
            _gate.Release();
            TryDelete(temporaryPath);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
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
}
