using System;
using System.Windows;
using MediaForge.App.Localization;
using MediaForge.App.ViewModels;
using Wpf.Ui.Controls;

namespace MediaForge.App.Views;

public partial class AddMediaWindow : FluentWindow
{
    private readonly DownloadsViewModel _viewModel;
    private readonly LocalizationService _localization;

    public AddMediaWindow(DownloadsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _localization = LocalizationService.Instance;

        InitializeComponent();
        _localization.ApplyToWindow(this);
        DataContext = viewModel;
        _viewModel.MediaStaged += OnMediaStagedAsync;
    }

    private Task OnMediaStagedAsync()
    {
        if (!Dispatcher.HasShutdownStarted)
            Dispatcher.BeginInvoke(new Action(Close));

        return Task.CompletedTask;
    }

    private void PasteButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText())
                return;

            _viewModel.SourceUrl = Clipboard.GetText().Trim();
            _viewModel.StatusText = _localization.Get("AddMedia_PasteReady");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _viewModel.StatusText = _localization.Get("AddMedia_ClipboardUnavailable");
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.MediaStaged -= OnMediaStagedAsync;
        base.OnClosed(e);
    }
}