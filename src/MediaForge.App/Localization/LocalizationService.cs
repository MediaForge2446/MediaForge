using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace MediaForge.App.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string SettingsDirectoryName = "MediaForge";
    private const string SettingsFileName = "locale.json";
    private const string ResourceFileName = "Languages.xaml";

    private static readonly Lazy<LocalizationService> LazyInstance = new(() => new LocalizationService());

    private static readonly IReadOnlyList<LanguageOption> Supported = new[]
    {
        new LanguageOption("en-US", "English"),
        new LanguageOption("he-IL", "עברית"),
        new LanguageOption("es-ES", "Español"),
        new LanguageOption("fr-FR", "Français"),
        new LanguageOption("de-DE", "Deutsch"),
        new LanguageOption("it-IT", "Italiano"),
        new LanguageOption("pt-BR", "Português (Brasil)"),
        new LanguageOption("pt-PT", "Português (Portugal)"),
        new LanguageOption("nl-NL", "Nederlands"),
        new LanguageOption("pl-PL", "Polski"),
        new LanguageOption("tr-TR", "Türkçe"),
        new LanguageOption("sv-SE", "Svenska"),
        new LanguageOption("da-DK", "Dansk"),
        new LanguageOption("nb-NO", "Norsk bokmål"),
        new LanguageOption("fi-FI", "Suomi"),
        new LanguageOption("cs-CZ", "Čeština"),
        new LanguageOption("uk-UA", "Українська"),
        new LanguageOption("ja-JP", "日本語"),
        new LanguageOption("ko-KR", "한국어"),
        new LanguageOption("zh-CN", "简体中文"),
        new LanguageOption("ar-SA", "العربية"),
    };

    private CultureInfo _culture = CultureInfo.GetCultureInfo("en-US");

    public static LocalizationService Instance => LazyInstance.Value;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CultureChanged;

    public IReadOnlyList<LanguageOption> SupportedLanguages => Supported;
    public CultureInfo CurrentCulture => _culture;
    public bool IsRtl => IsRtlCulture(_culture);
    public FlowDirection CurrentFlowDirection => IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public void Initialize()
    {
        ApplyCulture(ResolveCulture(ReadPersistedCulture()), persist: false);
    }

    public string Get(string key)
    {
        if (Application.Current?.Resources[key] is string value)
            return value;

        return key;
    }

    public void ApplyToWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.FlowDirection = CurrentFlowDirection;
        window.Language = System.Windows.Markup.XmlLanguage.GetLanguage(_culture.Name);
    }

    public async Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplyCulture(ResolveCulture(cultureName), persist: true);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private void ApplyCulture(CultureInfo culture, bool persist)
    {
        _culture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;

        ReplaceLanguageDictionary();
        ApplyToOpenWindows();

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRtl)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentFlowDirection)));
        CultureChanged?.Invoke(this, EventArgs.Empty);

        if (persist)
            PersistCulture(culture);
    }

    private void ReplaceLanguageDictionary()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ReplaceLanguageDictionary);
            return;
        }

        var dictionaries = Application.Current?.Resources.MergedDictionaries;
        if (dictionaries is null)
            return;

        var catalog = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/MediaForge;component/Localization/{ResourceFileName}",
                UriKind.Absolute)
        };

        var english = catalog["en-US"] as ResourceDictionary;
        var selected = catalog[_culture.Name] as ResourceDictionary;
        var active = new ResourceDictionary();

        CopyEntries(english, active);
        if (selected is not null && !ReferenceEquals(selected, english))
            CopyEntries(selected, active);

        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (source is not null && source.Contains("/Localization/" + ResourceFileName, StringComparison.OrdinalIgnoreCase))
                dictionaries.RemoveAt(i);
        }

        dictionaries.Add(active);
    }

    private static void CopyEntries(ResourceDictionary? source, ResourceDictionary target)
    {
        if (source is null)
            return;

        foreach (DictionaryEntry entry in source)
            target[entry.Key] = entry.Value;
    }

    private static CultureInfo ResolveCulture(string? persisted)
    {
        if (!string.IsNullOrWhiteSpace(persisted) &&
            !string.Equals(persisted, "system", StringComparison.OrdinalIgnoreCase))
        {
            var exact = Supported.FirstOrDefault(x =>
                string.Equals(x.CultureName, persisted, StringComparison.OrdinalIgnoreCase));

            if (exact is not null)
                return exact.Culture;
        }

        var system = CultureInfo.InstalledUICulture;
        return Supported.FirstOrDefault(x =>
            string.Equals(x.CultureName, system.Name, StringComparison.OrdinalIgnoreCase))?.Culture
            ?? Supported.FirstOrDefault(x =>
                x.Culture.TwoLetterISOLanguageName.Equals(system.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))?.Culture
            ?? CultureInfo.GetCultureInfo("en-US");
    }

    private static bool IsRtlCulture(CultureInfo culture) =>
        culture.Name.StartsWith("he-", StringComparison.OrdinalIgnoreCase) ||
        culture.Name.StartsWith("ar-", StringComparison.OrdinalIgnoreCase) ||
        culture.Name.StartsWith("fa-", StringComparison.OrdinalIgnoreCase) ||
        culture.Name.StartsWith("ur-", StringComparison.OrdinalIgnoreCase);

    private static string GetSettingsPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            SettingsDirectoryName);

        Directory.CreateDirectory(directory);
        return Path.Combine(directory, SettingsFileName);
    }

    private static string? ReadPersistedCulture()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
                return "system";

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<LocalizationSettings>(stream)?.Culture;
        }
        catch
        {
            return "system";
        }
    }

    private static void PersistCulture(CultureInfo culture)
    {
        try
        {
            var path = GetSettingsPath();
            var temp = path + ".tmp";
            var json = JsonSerializer.Serialize(new LocalizationSettings(culture.Name), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(temp, json);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
        catch
        {
            // Never let a preference write failure break language switching.
        }
    }

    private static void ApplyToOpenWindows()
    {
        var application = Application.Current;
        var dispatcher = application?.Dispatcher;
        if (application is null || dispatcher is null)
            return;

        void Apply()
        {
            foreach (Window window in application.Windows)
                Instance.ApplyToWindow(window);
        }

        if (dispatcher.CheckAccess())
            Apply();
        else
            dispatcher.Invoke(Apply, DispatcherPriority.DataBind);
    }

    private sealed record LocalizationSettings(string Culture);
}

public sealed class LanguageOption
{
    public LanguageOption(string cultureName, string displayName)
    {
        CultureName = cultureName;
        DisplayName = displayName;
        Culture = CultureInfo.GetCultureInfo(cultureName);
    }

    public string CultureName { get; }
    public string DisplayName { get; }
    public CultureInfo Culture { get; }

    public override string ToString() => DisplayName;
}
