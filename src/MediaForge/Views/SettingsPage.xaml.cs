using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MediaForge.ViewModels;

namespace MediaForge.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        if (App.Current is not App app || app.MainWindow is not MainWindow window)
        {
            throw new InvalidOperationException("The application window is not initialized.");
        }

        ViewModel = window.ViewModel.Settings;
        InitializeComponent();
        DataContext = this;
    }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = this;
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.CheckForUpdatesAsync();
        }
        catch (Exception exception)
        {
            if (App.Current is App app && app.MainWindow is MainWindow window)
            {
                window.ViewModel.ReportError(exception);
            }
        }
    }

    private async void OnUpdateAllClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.UpdateAllAsync();
        }
        catch (Exception exception)
        {
            if (App.Current is App app && app.MainWindow is MainWindow window)
            {
                window.ViewModel.ReportError(exception);
            }
        }
    }
}
