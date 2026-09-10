using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MediaForge.ViewModels;
using MediaForge.Views;

namespace MediaForge;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        ContentFrame.Navigated += OnContentFrameNavigated;
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        _ = StartBackgroundMaintenanceAsync();
    }

    private async Task StartBackgroundMaintenanceAsync()
    {
        try
        {
            await ViewModel.Settings.StartBackgroundMaintenanceAsync();
        }
        catch (Exception exception)
        {
            ViewModel.ReportError(exception);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        ContentFrame.Navigated -= OnContentFrameNavigated;
        ViewModel.Dispose();
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        try
        {
            if (args.IsSettingsSelected)
            {
                if (ContentFrame.CurrentSourcePageType != typeof(SettingsPage))
                {
                    ContentFrame.Navigate(typeof(SettingsPage));
                }
                return;
            }

            if (args.SelectedItemContainer?.Tag is not string tag)
            {
                return;
            }

            var pageType = tag switch
            {
                "Library" => typeof(LibraryPage),
                "Explorer" => typeof(ExplorerPage),
                "Downloads" => typeof(DownloadsPage),
                _ => null
            };

            if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }
        catch (Exception exception)
        {
            ViewModel.ReportError(exception);
        }
    }

    private void OnContentFrameNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        switch (ContentFrame.Content)
        {
            case LibraryPage libraryPage:
                libraryPage.ViewModel = ViewModel.Library;
                break;
            case ExplorerPage explorerPage:
                explorerPage.ViewModel = ViewModel.Explorer;
                explorerPage.PendingViewModel = ViewModel.PendingChanges;
                break;
        }
    }
}
