using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediaForge.Core.Enums;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MediaForge.App.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public sealed class UserPreferencesService
{
    private const string SettingsDirectoryName = "MediaForge";
    private const string SettingsFileName = "preferences.json";

    private readonly object _gate = new();
    private UserPreferences _preferences = new(
        ThemePreference.System,
        MediaFormat.Mp3);

    public ThemePreference Theme => _preferences.Theme;
    public MediaFormat DefaultFormat => _preferences.DefaultFormat;

    public void Initialize()
    {
        _preferences = ReadPreferences();
        ApplyTheme(_preferences.Theme);
    }

    public void SetTheme(ThemePreference preference)
    {
        _preferences = _preferences with { Theme = preference };
        PersistPreferences();
        ApplyTheme(preference);
    }

    public void SetDefaultFormat(MediaFormat format)
    {
        if (!Enum.IsDefined(format))
            return;

        _preferences = _preferences with { DefaultFormat = format };
        PersistPreferences();
    }

    private static void ApplyTheme(ThemePreference preference)
    {
        switch (preference)
        {
            case ThemePreference.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica);
                break;
            case ThemePreference.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }
    }

    private static UserPreferences ReadPreferences()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
                return new UserPreferences(ThemePreference.System, MediaFormat.Mp3);

            using var stream = File.OpenRead(path);
            var data = JsonSerializer.Deserialize<PersistedPreferences>(stream);
            if (data is null)
                return new UserPreferences(ThemePreference.System, MediaFormat.Mp3);

            var theme = Enum.TryParse<ThemePreference>(
                data.Theme,
                ignoreCase: true,
                out var parsedTheme)
                ? parsedTheme
                : ThemePreference.System;

            var format = Enum.TryParse<MediaFormat>(
                data.DefaultFormat,
                ignoreCase: true,
                out var parsedFormat) && Enum.IsDefined(parsedFormat)
                ? parsedFormat
                : MediaFormat.Mp3;

            return new UserPreferences(theme, format);
        }
        catch
        {
            return new UserPreferences(ThemePreference.System, MediaFormat.Mp3);
        }
    }

    private void PersistPreferences()
    {
        lock (_gate)
        {
            try
            {
                var path = GetSettingsPath();
                var temp = path + ".tmp";
                var persisted = new PersistedPreferences(
                    _preferences.Theme.ToString(),
                    _preferences.DefaultFormat.ToString());

                var json = JsonSerializer.Serialize(
                    persisted,
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(temp, json);

                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            catch
            {
                // Preferences are non-critical; never break the UI when persistence fails.
            }
        }
    }

    private static string GetSettingsPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            SettingsDirectoryName);

        Directory.CreateDirectory(directory);
        return Path.Combine(directory, SettingsFileName);
    }

    private sealed record UserPreferences(
        ThemePreference Theme,
        MediaFormat DefaultFormat);

    private sealed record PersistedPreferences(
        string Theme,
        string DefaultFormat);
}
