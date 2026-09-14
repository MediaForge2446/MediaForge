using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.Core.Interfaces;

namespace MediaForge.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IToolManager _toolManager;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _toolStatus = "הכלים עדיין לא אומתו";

    [ObservableProperty]
    private string _ytDlpPath = string.Empty;

    [ObservableProperty]
    private string _ffmpegPath = string.Empty;

    public SettingsViewModel(IToolManager toolManager)
    {
        _toolManager = toolManager;
    }

    [RelayCommand]
    private async Task VerifyToolsAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ToolStatus = "מאמת ומתקן כלים מנוהלים…";
        try
        {
            var tools = await _toolManager.EnsureToolsReadyAsync(cancellationToken).ConfigureAwait(true);
            YtDlpPath = tools.YtDlpExecutablePath;
            FfmpegPath = tools.FfmpegExecutablePath;
            ToolStatus = "הכלים מוכנים לשימוש";
        }
        catch (OperationCanceledException)
        {
            ToolStatus = "הפעולה בוטלה";
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
