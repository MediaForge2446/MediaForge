using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaForge.App.ViewModels;

namespace MediaForge.App.Views;

public partial class ExplorerPage : UserControl
{
    public ExplorerPage()
    {
        InitializeComponent();
    }

    private async void FolderTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not ExplorerViewModel viewModel || e.NewValue is not ExplorerTreeNodeViewModel node)
            return;

        await viewModel.NavigateToPathAsync(node.FullPath);
    }

    private async void Entry_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ExplorerViewModel viewModel || sender is not ListViewItem item || item.Content is not ExplorerEntryViewModel entry)
            return;

        await viewModel.OpenFromDoubleClickAsync(entry);
    }
}
