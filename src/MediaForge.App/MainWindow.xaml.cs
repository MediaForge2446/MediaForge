using System.Windows;
using MediaForge.App.Localization;
using MediaForge.App.ViewModels;

namespace MediaForge.App;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        LocalizationService.Instance.ApplyToWindow(this);
        DataContext = viewModel;
    }
}
