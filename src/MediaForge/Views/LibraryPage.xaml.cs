using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MediaForge;

public sealed partial class LibraryPage : Page
{
    public MainViewModel Model => ((MainWindow)App.MainWindow!).ViewModel;
    public LibraryPage(){InitializeComponent(); DataContext=Model;}
    private async void AddFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker=new FolderPicker(); picker.FileTypeFilter.Add("*"); InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var folder=await picker.PickSingleFolderAsync(); if(folder is not null) Model.AddFolder(folder.Path);
        }
        catch(Exception ex){Model.ReportError(ex);}
    }
    private async void OpenFolder(object sender, ItemClickEventArgs e)
    {
        if(e.ClickedItem is not LibraryFolder folder)return;
        await Model.LoadFolderAsync(folder.Path, CancellationToken.None);
        ((MainWindow)App.MainWindow!).NavigateToExplorer();
    }
    private async void AddMedia(object sender, RoutedEventArgs e)
    {
        try { var dialog=new MediaDialog{XamlRoot=XamlRoot, DataContext=Model}; await dialog.ShowAsync(); Model.FilesRefresh(); }
        catch(Exception ex){Model.ReportError(ex);}
    }
}
