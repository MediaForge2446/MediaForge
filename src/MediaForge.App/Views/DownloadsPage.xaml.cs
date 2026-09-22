using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaForge.App.ViewModels;

namespace MediaForge.App.Views;

public partial class DownloadsPage : UserControl
{
    private Point _dragStartPoint;
    private DownloadQueueItemViewModel? _draggedItem;

    public DownloadsPage()
    {
        InitializeComponent();
    }

    private void QueueList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(QueueList);
        _draggedItem = FindListItem(e.OriginalSource as DependencyObject)?.DataContext
            as DownloadQueueItemViewModel;
    }

    private void QueueList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem is null)
            return;

        var position = e.GetPosition(QueueList);
        var horizontal = Math.Abs(position.X - _dragStartPoint.X);
        var vertical = Math.Abs(position.Y - _dragStartPoint.Y);

        if (horizontal < SystemParameters.MinimumHorizontalDragDistance &&
            vertical < SystemParameters.MinimumVerticalDragDistance)
            return;

        var item = _draggedItem;
        _draggedItem = null;
        DragDrop.DoDragDrop(QueueList, item, DragDropEffects.Move);
    }

    private void QueueList_OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not DownloadsViewModel viewModel ||
            e.Data.GetData(typeof(DownloadQueueItemViewModel)) is not DownloadQueueItemViewModel source)
            return;

        var targetContainer = FindListItem(e.OriginalSource as DependencyObject);
        if (targetContainer?.DataContext is not DownloadQueueItemViewModel target)
            return;

        var targetIndex = viewModel.QueueItems.IndexOf(target);
        if (targetIndex < 0)
            return;

        var sourceIndex = viewModel.QueueItems.IndexOf(source);
        if (sourceIndex < 0)
            return;

        if (sourceIndex < targetIndex)
            targetIndex--;

        viewModel.MoveQueueItem(source.OperationId, targetIndex);
        e.Handled = true;
    }

    private void ShowLog_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not DownloadQueueItemViewModel item)
            return;

        var title = string.IsNullOrWhiteSpace(item.ErrorMessage)
            ? item.Title
            : $"{item.Title} — {item.ErrorMessage}";

        MessageBox.Show(
            Window.GetWindow(this),
            string.IsNullOrWhiteSpace(item.LogText)
                ? "No diagnostic log is available for this item."
                : item.LogText,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static ListViewItem? FindListItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListViewItem item)
                return item;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
