using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.App.Localization;
using MediaForge.Core.Interfaces;

namespace MediaForge.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IToolManager _toolManager;
    private readonly LocalizationService _localization;
    private bool _suppressLanguageChange;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _toolStatus = string.Empty;

    [ObservableProperty]
    private string _ytDlpPath = string.Empty;

    [ObservableProperty]
    private string _ffmpegPath = string.Empty;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    public ObservableCollection<LanguageOption> LanguageOptions { get; }

    public SettingsViewModel(IToolManager toolManager, LocalizationService localization)
    {
        _toolManager = toolManager;
        _localization = localization;
        LanguageOptions = new ObservableCollection<LanguageOption>(_localization.SupportedLanguages);

        _localization.CultureChanged += OnCultureChanged;
        SelectedLanguage = LanguageOptions.FirstOrDefault(x =>
            string.Equals(x.CultureName, _localization.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase));
        ToolStatus = _localization.Get("Status_Ready");
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_suppressLanguageChange || value is null)
            return;

        _ = ApplyLanguageAsync(value.CultureName);
    }

    private async Task ApplyLanguageAsync(string cultureName)
    {
        try
        {
            await _localization.SetCultureAsync(cultureName).ConfigureAwait(true);
            ToolStatus = _localization.Get("Status_Ready");
        }
        catch (OperationCanceledException)
        {
            ToolStatus = _localization.Get("Status_Canceled");
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        var selected = LanguageOptions.FirstOrDefault(x =>
            string.Equals(x.CultureName, _localization.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase));

        if (selected is null)
            return;

        _suppressLanguageChange = true;
        try { SelectedLanguage = selected; }
        finally { _suppressLanguageChange = false; }
    }

    [RelayCommand]
    private async Task VerifyToolsAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ToolStatus = _localization.Get("Settings_Tools");
        try
        {
            var tools = await _toolManager.EnsureToolsReadyAsync(cancellationToken).ConfigureAwait(true);
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
}
