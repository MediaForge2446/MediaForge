using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MediaForge.Core.Models;
using MediaForge.ViewModels;

namespace MediaForge.Views;

public sealed partial class ExplorerPage : Page
{
    public ExplorerViewModel ViewModel { get; set; } = null!;
    public PendingChangesViewModel PendingViewModel { get; set; } = null!;

    public ExplorerPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OnRootFolderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RootFolderPicker.SelectedItem is MediaFolder folder)
        {
            ViewModel.OpenFolder(folder.Path);
        }
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not FileItem item || item.Kind != MediaForge.Core.Enums.FileItemKind.Folder)
        {
            return;
        }

        ViewModel.OpenFolder(item.FullPath);
    }

    private async void OnNewFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var textBox = new TextBox { PlaceholderText = "Folder name", MinWidth = 320 };
            var dialog = new ContentDialog
            {
                Title = "New folder",
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = textBox,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.CreateFolder(textBox.Text);
            }
        }
        catch (Exception exception)
        {
            ReportUiError(exception);
        }
    }

    private async void OnRenameClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ExplorerList.SelectedItem is not FileItem item)
            {
                return;
            }

            var textBox = new TextBox { Text = item.Name, MinWidth = 320 };
            var dialog = new ContentDialog
            {
                Title = "Rename",
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = textBox,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.Rename(item, textBox.Text);
            }
        }
        catch (Exception exception)
        {
            ReportUiError(exception);
        }
    }

    private async void OnMoveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ExplorerList.SelectedItem is not FileItem item)
            {
                return;
            }

            var textBox = new TextBox
            {
                Text = ViewModel.CurrentPath ?? string.Empty,
                PlaceholderText = "Destination folder path",
                MinWidth = 420
            };

            var dialog = new ContentDialog
            {
                Title = "Move item",
                PrimaryButtonText = "Stage move",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = textBox,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.Move(item, textBox.Text);
            }
        }
        catch (Exception exception)
        {
            ReportUiError(exception);
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ExplorerList.SelectedItem is FileItem item)
            {
                ViewModel.Delete(item);
            }
        }
        catch (Exception exception)
        {
            ReportUiError(exception);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.CurrentPath))
        {
            return;
        }

        var parent = System.IO.Directory.GetParent(ViewModel.CurrentPath);
        if (parent is not null)
        {
            ViewModel.OpenFolder(parent.FullName);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Refresh();
    }

    private void OnUndoChangeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid changeId })
        {
            PendingViewModel.Undo(changeId);
            ViewModel.Refresh();
        }
    }

    private void OnUndoLastClick(object sender, RoutedEventArgs e)
    {
        PendingViewModel.UndoLast();
        ViewModel.Refresh();
    }

    private async void OnCancelAllClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!PendingViewModel.CanCancel)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Cancel pending changes?",
                Content = "All unsaved changes will be removed from the staging list. Nothing on disk will be changed.",
                PrimaryButtonText = "Cancel changes",
                CloseButtonText = "Keep changes",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                PendingViewModel.CancelAll();
                ViewModel.Refresh();
            }
        }
        catch (Exception exception)
        {
            ReportUiError(exception);
        }
    }

    private async void OnSaveChangesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await PendingViewModel.SaveChangesAsync();
            ViewModel.Refresh();
        }
        catch (Exception exception)
        {
            ReportUiError(exception);
        }
    }

    private void ReportUiError(Exception exception)
    {
        if (App.Current is App app && app.MainWindow is MainWindow window)
        {
            window.ViewModel.ReportError(exception);
        }
    }
}
