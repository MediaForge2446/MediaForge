using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MediaForge.ViewModels;

namespace MediaForge;

public sealed partial class MediaDialog : ContentDialog
{
    public ObservableCollection<MediaItem> Media { get; } = new();
    public MainViewModel Model => ((MainWindow)App.MainWindow!).ViewModel;
    private CancellationTokenSource? _resolveCts;
    public MediaDialog(){InitializeComponent(); DataContext=this;}
    private void UrlChanged(object sender, TextChangedEventArgs e){Summary.Text=string.IsNullOrWhiteSpace(UrlBox.Text)?"Paste a URL to begin":"Ready to resolve";}
    private async void Resolve(object sender,RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(UrlBox.Text))return;
        try
        {
            _resolveCts?.Cancel(); _resolveCts?.Dispose(); _resolveCts=new CancellationTokenSource();
            Busy.IsActive=true; IsPrimaryButtonEnabled=false; Media.Clear();
            var items=await Model.Resolver.ResolveAsync(UrlBox.Text.Trim(),_resolveCts.Token);
            foreach(var item in items)Media.Add(item);
            Summary.Text=items.Count==1?"1 video resolved":$"{items.Count} playlist items resolved";
            Destination.Text=Model.CurrentPath;
        }
        catch(OperationCanceledException) when(_resolveCts?.IsCancellationRequested==true){}
        catch(Exception ex){Model.ReportError(ex); Summary.Text="Resolve failed";}
        finally{Busy.IsActive=false; IsPrimaryButtonEnabled=true;}
    }
    private void SelectAll(object s,RoutedEventArgs e){foreach(var x in Media)x.IsSelected=true;}
    private void ClearAll(object s,RoutedEventArgs e){foreach(var x in Media)x.IsSelected=false;}
    private void AllMp3(object s,RoutedEventArgs e){foreach(var x in Media)x.Format=MediaFormat.Mp3;}
    private void AllMp4(object s,RoutedEventArgs e){foreach(var x in Media)x.Format=MediaFormat.Mp4;}
    private void CommitDialog(ContentDialog sender,ContentDialogButtonClickEventArgs args)
    {
        try
        {
            var folder=Model.CurrentPath;
            if(string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { args.Cancel=true; Model.ReportError(new InvalidOperationException("Open a destination folder before adding media.")); return; }
            foreach(var item in Media.Where(x=>x.IsSelected))
            {
                var ext=item.Format==MediaFormat.Mp4?".mp4":".mp3";
                var file=Path.Combine(folder,Sanitize(item.Title)+ext);
                Model.Stage(new PendingChange{Kind=ChangeKind.Download,SourcePath=item.Url,SourceUrl=item.Url,TargetPath=file,DisplayName=item.Title,Format=item.Format});
            }
        }
        catch(Exception ex){args.Cancel=true; Model.ReportError(ex);}
    }
    protected override void OnClosed(ContentDialogClosedEventArgs args){_resolveCts?.Cancel();_resolveCts?.Dispose();base.OnClosed(args);}
    private static string Sanitize(string name)
    {
        foreach(var c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
        name=name.Trim().TrimEnd('.'); return string.IsNullOrWhiteSpace(name)?"Media":name.Length>120?name[..120]:name;
    }
}
