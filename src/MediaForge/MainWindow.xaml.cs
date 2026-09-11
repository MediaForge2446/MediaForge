using MediaForge.ViewModels;
using MediaForge.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MediaForge;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            RootNavigation.DataContext = ViewModel;
            ContentFrame.Navigated += OnContentFrameNavigated;
            RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
            _ = StartBackgroundMaintenanceAsync();
        }
        catch
        {
            ViewModel.Dispose();
            throw;
        }
    }

    public void NavigateToExplorer()
    {
        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
        {
            if (string.Equals(item.Tag as string, "Explorer", StringComparison.Ordinal))
            {
                RootNavigation.SelectedItem = item;
                return;
            }
        }
    }

    private async Task StartBackgroundMaintenanceAsync()
    {
        try
        {
            await ViewModel.Settings.StartBackgroundMaintenanceAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            ViewModel.ReportError(exception);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args) => ViewModel.Dispose();

    private void OnClearErrorClick(object sender, RoutedEventArgs e) => ViewModel.ClearError();

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
            case DownloadsPage downloadsPage:
                downloadsPage.ViewModel = ViewModel.PendingChanges;
                break;
        }
    }
}
