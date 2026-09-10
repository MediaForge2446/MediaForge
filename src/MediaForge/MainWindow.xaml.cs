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
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        try
        {
            var pageType = tag switch
            {
                "Library" => typeof(LibraryPage),
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
        if (ContentFrame.Content is LibraryPage libraryPage)
        {
            libraryPage.ViewModel = ViewModel.Library;
        }
    }
}
