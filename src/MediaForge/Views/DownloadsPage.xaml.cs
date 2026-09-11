using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MediaForge.ViewModels;

namespace MediaForge.Views;

public sealed partial class DownloadsPage : Page
{
    public PendingChangesViewModel ViewModel { get; set; } = null!;

    public DownloadsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OnOpenExplorerClick(object sender, RoutedEventArgs e)
    {
        if (App.Current is App app && app.MainWindow is MainWindow window)
        {
            window.NavigateToExplorer();
        }
    }
}
