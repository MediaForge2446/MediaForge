using System.Windows;
using MediaForge.App.ViewModels;

namespace MediaForge.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}