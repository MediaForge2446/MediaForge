using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MediaForge.Core.Enums;
using MediaForge.Core.Models;
using MediaForge.ViewModels;

namespace MediaForge.Views;

public sealed partial class ExplorerPage : Page
{
    private ExplorerViewModel? _viewModel;
    private PendingChangesViewModel? _pendingViewModel;

    public ExplorerViewModel ViewModel
    {
        get => _viewModel ?? throw new InvalidOperationException("Explorer view model is not initialized.");
        set
        {
            _viewModel = value ?? throw new ArgumentNullException(nameof(value));
            DataContext = this;
        }
    }

    public PendingChangesViewModel PendingViewModel
    {
        get => _pendingViewModel ?? throw new InvalidOperationException("Pending changes view model is not initialized.");
        set
        {
            _pendingViewModel = value ?? throw new ArgumentNullException(nameof(value));
            DataContext = this;
        }
    }

    public ExplorerPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OnRootFolderChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (RootFolderPicker.SelectedItem is MediaFolder folder)
            {
                ViewModel.OpenFolder(folder.Path);
            }
        }
        catch (Exception exception)
        {
            Report(exception);
        }
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is FileItem item && item.Kind == FileItemKind.Folder)
            {
                ViewModel.OpenFolder(item.FullPath);
            }
        }
        catch (Exception exception)
        {
            Report(exception);
        }
    }

    private async void OnNewFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var textBox = new TextBox { PlaceholderText = "Folder name", MinWidth = 340 };
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
            Report(exception);
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

            var textBox = new TextBox { Text = item.Name, MinWidth = 340 };
            var dialog = new ContentDialog
            {
                Title = "Rename item",
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
            Report(exception);
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

            if (App.Current is not App app || app.MainWindow is null)
            {
                return;
            }

            var picker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder
            };
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(app.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var destination = await picker.PickSingleFolderAsync();
            if (destination is not null)
            {
                ViewModel.Move(item, destination.Path);
            }
        }
        catch (Exception exception)
        {
            Report(exception);
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ExplorerList.SelectedItem is not FileItem item)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Stage deletion?",
                Content = $"{item.Name} will be marked for deletion. Nothing is removed until Save Changes.",
                PrimaryButtonText = "Stage delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.Delete(item);
            }
        }
        catch (Exception exception)
        {
            Report(exception);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ViewModel.CurrentPath))
            {
                return;
            }

            var parent = Directory.GetParent(ViewModel.CurrentPath);
            if (parent is not null)
            {
                ViewModel.OpenFolder(parent.FullName);
            }
        }
        catch (Exception exception)
        {
            Report(exception);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => ViewModel.Refresh();

    private void OnUndoChangeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button && button.Tag is Guid changeId)
            {
                PendingViewModel.Undo(changeId);
                ViewModel.Refresh();
            }
        }
        catch (Exception exception)
        {
            Report(exception);
        }
    }

    private void OnUndoLastClick(object sender, RoutedEventArgs e)
    {
        try
        {
            PendingViewModel.UndoLast();
            ViewModel.Refresh();
        }
        catch (Exception exception)
        {
            Report(exception);
        }
    }

    private async void OnCancelAllClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "Cancel staged changes?",
                Content = "All pending changes will be removed from the staging list. Your files on disk will not be touched.",
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
            Report(exception);
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
            Report(exception);
        }
    }

    private void Report(Exception exception)
    {
        if (App.Current is App app && app.MainWindow is MainWindow window)
        {
            window.ViewModel.ReportError(exception);
        }
    }
}
