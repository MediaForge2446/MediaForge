using System;
using System.Windows;
using MediaForge.App.ViewModels;

namespace MediaForge.App.Views;

public partial class AddMediaWindow : Window
{
    private readonly DownloadsViewModel _viewModel;

    public AddMediaWindow(DownloadsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.MediaStaged += OnMediaStagedAsync;
    }

    private Task OnMediaStagedAsync()
    {
        if (!Dispatcher.HasShutdownStarted)
            Dispatcher.BeginInvoke(new Action(Close));

        return Task.CompletedTask;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.MediaStaged -= OnMediaStagedAsync;
        base.OnClosed(e);
    }
}