using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.App.Localization;
using MediaForge.App.Services;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;

namespace MediaForge.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IToolManager _toolManager;
    private readonly LocalizationService _localization;
    private readonly UserPreferencesService _preferences;
    private readonly GitHubAppUpdateService _updateService;
    private bool _suppressLanguageChange;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isUpdateBusy;
    [ObservableProperty] private string _toolStatus = string.Empty;
    [ObservableProperty] private string _ytDlpPath = string.Empty;
    [ObservableProperty] private string _ffmpegPath = string.Empty;
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private MediaFormat _defaultFormat;
    [ObservableProperty] private ThemePreference _theme = ThemePreference.System;
    [ObservableProperty] private bool _isMp3FormatSelected;
    [ObservableProperty] private bool _isMp4FormatSelected;
    [ObservableProperty] private bool _isWavFormatSelected;
    [ObservableProperty] private bool _isM4aFormatSelected;
    [ObservableProperty] private bool _isSystemThemeSelected;
    [ObservableProperty] private bool _isLightThemeSelected;
    [ObservableProperty] private bool _isDarkThemeSelected;
    [ObservableProperty] private AppUpdateInfo? _availableUpdate;
    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private double _updateProgress;

    public ObservableCollection<LanguageOption> LanguageOptions { get; }

    public IReadOnlyList<MediaFormat> DefaultFormats { get; } =
        [MediaFormat.Mp3, MediaFormat.Mp4, MediaFormat.Wav, MediaFormat.M4a];

    public string CurrentVersionText =>
        _updateService.CurrentVersion.ToString(3);

    public bool IsUpdateAvailable => AvailableUpdate is not null;
    public string AvailableVersionText =>
        AvailableUpdate?.VersionLabel is { Length: > 0 } version
            ? string.Format(
                _localization.CurrentCulture,
                _localization.Get("Settings_UpdateAvailableVersion"),
                version)
            : string.Empty;

    public SettingsViewModel(
        IToolManager toolManager,
        LocalizationService localization,
        UserPreferencesService preferences,
        GitHubAppUpdateService updateService)
    {
        _toolManager = toolManager ?? throw new ArgumentNullException(nameof(toolManager));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));

        LanguageOptions = new ObservableCollection<LanguageOption>(_localization.SupportedLanguages);

        _localization.CultureChanged += OnCultureChanged;
        SelectedLanguage = LanguageOptions.FirstOrDefault(x =>
            string.Equals(
                x.CultureName,
                _localization.CurrentCulture.Name,
                StringComparison.OrdinalIgnoreCase));

        _defaultFormat = _preferences.DefaultFormat;
        _theme = _preferences.Theme;
        RefreshThemeSelection();
        RefreshFormatSelection();
        ToolStatus = _localization.Get("Status_Ready");
        UpdateStatus = _localization.Get("Settings_CheckingUpdates");
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_suppressLanguageChange || value is null)
            return;

        _ = ApplyLanguageAsync(value.CultureName);
    }

    partial void OnThemeChanged(ThemePreference value)
    {
        _preferences.SetTheme(value);
        RefreshThemeSelection();
    }

    partial void OnDefaultFormatChanged(MediaFormat value)
    {
        if (!Enum.IsDefined(value))
            return;

        _preferences.SetDefaultFormat(value);
        RefreshFormatSelection();
    }

    partial void OnAvailableUpdateChanged(AppUpdateInfo? value)
    {
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(AvailableVersionText));
    }

    private async Task ApplyLanguageAsync(string cultureName)
    {
        try
        {
            await _localization
                .SetCultureAsync(cultureName)
                .ConfigureAwait(true);

            ToolStatus = _localization.Get("Status_Ready");
            if (AvailableUpdate is null)
                UpdateStatus = _localization.Get("Settings_NoUpdate");
            OnPropertyChanged(nameof(AvailableVersionText));
        }
        catch (OperationCanceledException)
        {
            ToolStatus = _localization.Get("Status_Canceled");
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        var selected = LanguageOptions.FirstOrDefault(x =>
            string.Equals(
                x.CultureName,
                _localization.CurrentCulture.Name,
                StringComparison.OrdinalIgnoreCase));

        if (selected is null)
            return;

        _suppressLanguageChange = true;
        try
        {
            SelectedLanguage = selected;
        }
        finally
        {
            _suppressLanguageChange = false;
        }

        ToolStatus = _localization.Get("Status_Ready");
        UpdateStatus = AvailableUpdate is null
            ? _localization.Get("Settings_NoUpdate")
            : _localization.Get("Settings_UpdateReady");
        OnPropertyChanged(nameof(AvailableVersionText));
    }

    private void RefreshThemeSelection()
    {
        IsSystemThemeSelected = Theme == ThemePreference.System;
        IsLightThemeSelected = Theme == ThemePreference.Light;
        IsDarkThemeSelected = Theme == ThemePreference.Dark;
    }

    private void RefreshFormatSelection()
    {
        IsMp3FormatSelected = DefaultFormat == MediaFormat.Mp3;
        IsMp4FormatSelected = DefaultFormat == MediaFormat.Mp4;
        IsWavFormatSelected = DefaultFormat == MediaFormat.Wav;
        IsM4aFormatSelected = DefaultFormat == MediaFormat.M4a;
    }

    private void SetDefaultFormatCore(MediaFormat format)
    {
        DefaultFormat = format;
    }

    [RelayCommand]
    private void SetSystemTheme() => Theme = ThemePreference.System;

    [RelayCommand]
    private void SetLightTheme() => Theme = ThemePreference.Light;

    [RelayCommand]
    private void SetDarkTheme() => Theme = ThemePreference.Dark;

    [RelayCommand]
    private void SetMp3DefaultFormat() => SetDefaultFormatCore(MediaFormat.Mp3);

    [RelayCommand]
    private void SetMp4DefaultFormat() => SetDefaultFormatCore(MediaFormat.Mp4);

    [RelayCommand]
    private void SetWavDefaultFormat() => SetDefaultFormatCore(MediaFormat.Wav);

    [RelayCommand]
    private void SetM4aDefaultFormat() => SetDefaultFormatCore(MediaFormat.M4a);

    [RelayCommand]
    private async Task VerifyToolsAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ToolStatus = _localization.Get("Settings_VerifyingTools");

        try
        {
            var tools = await _toolManager
                .EnsureToolsReadyAsync(cancellationToken)
                .ConfigureAwait(true);

            YtDlpPath = tools.YtDlpExecutablePath;
            FfmpegPath = tools.FfmpegExecutablePath;
            ToolStatus = _localization.Get("Status_ToolsReady");
        }
        catch (OperationCanceledException)
        {
            ToolStatus = _localization.Get("Status_Canceled");
        }
        catch (Exception ex)
        {
            ToolStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        if (IsUpdateBusy)
            return;

        IsUpdateBusy = true;
        UpdateProgress = 0;
        UpdateStatus = _localization.Get("Settings_CheckingUpdates");

        try
        {
            AvailableUpdate = await _updateService
                .CheckForUpdateAsync(cancellationToken)
                .ConfigureAwait(true);

            UpdateStatus = AvailableUpdate is null
                ? _localization.Get("Settings_NoUpdate")
                : _localization.Get("Settings_UpdateReady");

            OnPropertyChanged(nameof(AvailableVersionText));
        }
        catch (OperationCanceledException)
        {
            UpdateStatus = _localization.Get("Status_Canceled");
        }
        catch (Exception ex)
        {
            AvailableUpdate = null;
            UpdateStatus = string.Format(
                _localization.CurrentCulture,
                _localization.Get("Settings_UpdateCheckFailed"),
                ex.Message);
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync(CancellationToken cancellationToken)
    {
        if (IsUpdateBusy || AvailableUpdate is null)
            return;

        IsUpdateBusy = true;
        UpdateProgress = 0;
        UpdateStatus = _localization.Get("Settings_DownloadingUpdate");

        try
        {
            var progress = new Progress<double>(value => UpdateProgress = value);
            await _updateService
                .DownloadAndLaunchInstallerAsync(
                    AvailableUpdate,
                    progress,
                    cancellationToken)
                .ConfigureAwait(true);

            UpdateStatus = _localization.Get("Settings_UpdateLaunching");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus = _localization.Get("Status_Canceled");
        }
        catch (Exception ex)
        {
            UpdateStatus = string.Format(
                _localization.CurrentCulture,
                _localization.Get("Settings_UpdateInstallFailed"),
                ex.Message);
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }
}
