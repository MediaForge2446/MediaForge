using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using MediaForge.ViewModels;

namespace MediaForge;

public sealed partial class ExplorerPage : Page
{
    private MainViewModel Model => ((MainWindow)App.MainWindow!).ViewModel;
    public ExplorerPage(){InitializeComponent(); DataContext=Model;}
    private void FolderChanged(object sender, SelectionChangedEventArgs e){if(FolderPicker.SelectedItem is LibraryFolder f) _=Model.LoadFolderAsync(f.Path,CancellationToken.None);}
    private void OpenItem(object sender, ItemClickEventArgs e){if(e.ClickedItem is FileEntry f && f.Kind==FileKind.Folder) _=Model.LoadFolderAsync(f.Path,CancellationToken.None);}
    private async void NewFolder(object sender,RoutedEventArgs e){try{var box=new TextBox{PlaceholderText="Folder name",MinWidth=320};var d=new ContentDialog{Title="New folder",Content=box,PrimaryButtonText="Stage",CloseButtonText="Cancel",XamlRoot=XamlRoot};if(await d.ShowAsync()==ContentDialogResult.Primary){var path=Path.Combine(Model.CurrentPath,box.Text.Trim());Model.Stage(new PendingChange{Kind=ChangeKind.CreateFolder,SourcePath=path,DisplayName=box.Text.Trim()});}}catch(Exception ex){Model.ReportError(ex);}}
    private async void Rename(object sender,RoutedEventArgs e){try{if(FileList.SelectedItem is not FileEntry item)return;var box=new TextBox{Text=item.Name,MinWidth=320};var d=new ContentDialog{Title="Rename",Content=box,PrimaryButtonText="Stage",CloseButtonText="Cancel",XamlRoot=XamlRoot};if(await d.ShowAsync()==ContentDialogResult.Primary){var target=Path.Combine(Path.GetDirectoryName(item.Path)!,box.Text.Trim());Model.Stage(new PendingChange{Kind=ChangeKind.Rename,SourcePath=item.Path,TargetPath=target,DisplayName=$"{item.Name} → {box.Text.Trim()}"});}}catch(Exception ex){Model.ReportError(ex);}}
    private async void Delete(object sender,RoutedEventArgs e){try{if(FileList.SelectedItem is not FileEntry item)return;var d=new ContentDialog{Title="Stage deletion",Content=$"{item.Name} will be deleted when you save changes.",PrimaryButtonText="Stage delete",CloseButtonText="Cancel",XamlRoot=XamlRoot};if(await d.ShowAsync()==ContentDialogResult.Primary)Model.Stage(new PendingChange{Kind=ChangeKind.Delete,SourcePath=item.Path,DisplayName=item.Name});}catch(Exception ex){Model.ReportError(ex);}}
    private async void Move(object sender,RoutedEventArgs e){try{if(FileList.SelectedItem is not FileEntry item)return;var picker=new FolderPicker();picker.FileTypeFilter.Add("*");InitializeWithWindow.Initialize(picker,WindowNative.GetWindowHandle(App.MainWindow));var folder=await picker.PickSingleFolderAsync();if(folder is not null){var target=Path.Combine(folder.Path,item.Name);Model.Stage(new PendingChange{Kind=ChangeKind.Move,SourcePath=item.Path,TargetPath=target,DisplayName=$"{item.Name} → {folder.Path}"});}}catch(Exception ex){Model.ReportError(ex);}}
    private void Back(object sender,RoutedEventArgs e){var parent=Directory.GetParent(Model.CurrentPath);if(parent is not null)_=_Load(parent.FullName);}
    private Task _Load(string p)=>Model.LoadFolderAsync(p,CancellationToken.None);
    private void Refresh(object sender,RoutedEventArgs e)=>Model.FilesRefresh();
    private void UndoItem(object sender,RoutedEventArgs e){if(sender is Button b&&b.Tag is Guid id)Model.Undo(id);}
    private void UndoLast(object sender,RoutedEventArgs e){if(Model.Pending.LastOrDefault() is { } x)Model.Undo(x.Id);}
    private void CancelAll(object sender,RoutedEventArgs e){foreach(var x in Model.Pending.ToArray())Model.Undo(x.Id);}
    private async void Save(object sender,RoutedEventArgs e){await Model.SaveChangesAsync(CancellationToken.None);}
}
