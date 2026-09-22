using DownTrack.App.ViewModels;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace DownTrack.App;

public sealed partial class MainWindow : Window
{
    public HomeViewModel ViewModel { get; }

    public MainWindow(HomeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.OwnerHandle = WindowNative.GetWindowHandle(this);
        FoldersGrid.ItemsSource = ViewModel.Roots;
    }
}
