using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MediaForge.ViewModels;

namespace MediaForge.Views;

public sealed partial class DownloadsPage : Page
{
    private PendingChangesViewModel? _viewModel;

    public PendingChangesViewModel ViewModel
    {
        get => _viewModel ?? throw new InvalidOperationException("Downloads view model is not initialized.");
        set
        {
            _viewModel = value ?? throw new ArgumentNullException(nameof(value));
            DataContext = this;
        }
    }

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
