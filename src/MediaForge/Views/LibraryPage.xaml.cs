using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MediaForge.ViewModels;

namespace MediaForge.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; set; } = null!;

    public LibraryPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void OnAddFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");

            if (App.Current is not App app || app.MainWindow is null)
            {
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(app.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                ViewModel.AddRootFolder(folder.Path, folder.Name);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnAddMediaClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "Add media",
                Content = new TextBlock { Text = "Media downloader dialog will open here." },
                CloseButtonText = "Close",
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception)
        {
        }
    }
}
