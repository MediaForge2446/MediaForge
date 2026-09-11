using MediaForge.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MediaForge;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Nav.DataContext = ViewModel;
        ContentFrame.Navigate(typeof(LibraryPage));
        Nav.SelectedItem = Nav.MenuItems[0];
        Closed += (_, _) => ViewModel.Dispose();
    }

    public void NavigateToExplorer()
    {
        foreach (var item in Nav.MenuItems.OfType<NavigationViewItem>())
        {
            if (string.Equals(item.Tag?.ToString(), "Explorer", StringComparison.Ordinal))
            {
                Nav.SelectedItem = item;
                return;
            }
        }
    }

    private void ClearError(object sender, RoutedEventArgs e) => ViewModel.ClearError();

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs e)
    {
        try
        {
            var page = e.IsSettingsSelected
                ? typeof(SettingsPage)
                : string.Equals(e.SelectedItemContainer?.Tag?.ToString(), "Explorer", StringComparison.Ordinal)
                    ? typeof(ExplorerPage)
                    : typeof(LibraryPage);

            ContentFrame.Navigate(page);
        }
        catch (Exception ex)
        {
            ViewModel.ReportError(ex);
        }
    }
}
