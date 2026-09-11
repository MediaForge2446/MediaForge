using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MediaForge.Services;

namespace MediaForge.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppStateService _state = new();
    private string _currentPath="";
    private string? _lastError;
    private bool _busy;
    public ObservableCollection<LibraryFolder> Folders{get;}=new();
    public ObservableCollection<FileEntry> Files{get;}=new();
    public ObservableCollection<PendingChange> Pending{get;}=new();
    public FileSystemService FileSystem{get;}=new();
    public YouTubeResolver Resolver{get;}=new();
    public ProcessDownloadService Downloader{get;}=new();
    public string CurrentPath{get=>_currentPath;private set{if(_currentPath!=value){_currentPath=value;Changed();Changed(nameof(CurrentFolderName));}}}
    public string CurrentFolderName=>string.IsNullOrWhiteSpace(CurrentPath)?"Choose a root folder":new DirectoryInfo(CurrentPath).Name;
    public bool IsBusy{get=>_busy;private set{if(_busy!=value){_busy=value;Changed();Changed(nameof(CanSave));}}}
    public string? LastError{get=>_lastError;private set{if(_lastError!=value){_lastError=value;Changed();Changed(nameof(HasError));}}}
    public bool HasError=>!string.IsNullOrWhiteSpace(LastError);
    public string PendingSummary=>Pending.Count==0?"All changes saved":$"{Pending.Count} pending";
    public bool CanSave=>!IsBusy&&Pending.Any(x=>x.Status==ChangeStatus.Pending);
    public double PendingProgress=>Pending.Count==0?0:Pending.Average(x=>x.Status==ChangeStatus.Synced?1:x.Progress);

    public MainViewModel(){try{var s=_state.Load();foreach(var f in s.Folders)Folders.Add(f);foreach(var c in s.Pending)Pending.Add(c);}catch(Exception ex){ReportError(ex);}}
    public void AddFolder(string path){try{path=Path.GetFullPath(path);if(!Directory.Exists(path))throw new DirectoryNotFoundException(path);if(Folders.Any(x=>string.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase)))return;Folders.Add(new LibraryFolder(Guid.NewGuid(),new DirectoryInfo(path).Name,path));Persist();}catch(Exception ex){ReportError(ex);}}
    public async Task LoadFolderAsync(string path,CancellationToken token){try{IsBusy=true;var full=Path.GetFullPath(path);var physical=await FileSystem.ListAsync(full,token);CurrentPath=full;Files.Clear();foreach(var x in Project(full,physical))Files.Add(x);}catch(OperationCanceledException)when(token.IsCancellationRequested){}catch(Exception ex){ReportError(ex);}finally{IsBusy=false;}}
    public void Stage(PendingChange change){Pending.Add(change);Persist();Changed(nameof(PendingSummary));Changed(nameof(CanSave));Changed(nameof(PendingProgress));FilesRefresh();}
    public void Undo(Guid id){var x=Pending.FirstOrDefault(p=>p.Id==id);if(x is null)return;Pending.Remove(x);Persist();Changed(nameof(PendingSummary));Changed(nameof(CanSave));Changed(nameof(PendingProgress));FilesRefresh();}
    public void ClearError()=>LastError=null;
    public async Task SaveChangesAsync(CancellationToken token){if(!CanSave)return;try{IsBusy=true;foreach(var c in Pending.Where(x=>x.Status==ChangeStatus.Pending).ToArray()){try{await ExecuteAsync(c,token);c.Status=ChangeStatus.Synced;c.Progress=1;Changed(nameof(PendingProgress));}catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}catch(Exception ex){c.Status=ChangeStatus.Failed;c.Error=ex.Message;}}Persist();FilesRefresh();Changed(nameof(PendingSummary));Changed(nameof(PendingProgress));}catch(OperationCanceledException)when(token.IsCancellationRequested){ReportError(new InvalidOperationException("Save cancelled."));}catch(Exception ex){ReportError(ex);}finally{IsBusy=false;}}
    private async Task ExecuteAsync(PendingChange c,CancellationToken token){switch(c.Kind){case ChangeKind.CreateFolder:await FileSystem.CreateDirectoryAsync(c.SourcePath,token);break;case ChangeKind.Rename:case ChangeKind.Move:await FileSystem.MoveAsync(c.SourcePath,c.TargetPath!,token);break;case ChangeKind.Delete:await FileSystem.DeleteAsync(c.SourcePath,token);break;case ChangeKind.Download:await Downloader.DownloadAsync(c.SourceUrl!,c.TargetPath!,c.Format,p=>{c.Progress=p;Changed(nameof(PendingProgress));},token);break;}}
    private IReadOnlyList<FileEntry> Project(string folder,IReadOnlyList<FileEntry> physical)
    {
        var result=physical.ToList();
        foreach(var c in Pending.Where(x=>x.Status==ChangeStatus.Pending))
        {
            if(c.Kind==ChangeKind.Delete){result.RemoveAll(x=>string.Equals(x.Path,c.SourcePath,StringComparison.OrdinalIgnoreCase));continue;}
            if(c.Kind is ChangeKind.Rename or ChangeKind.Move)
            {
                var source=result.FirstOrDefault(x=>string.Equals(x.Path,c.SourcePath,StringComparison.OrdinalIgnoreCase));
                result.RemoveAll(x=>string.Equals(x.Path,c.SourcePath,StringComparison.OrdinalIgnoreCase));
                if(source is not null&&string.Equals(Path.GetDirectoryName(c.TargetPath),folder,StringComparison.OrdinalIgnoreCase))result.Add(source with{Name=Path.GetFileName(c.TargetPath),Path=c.TargetPath!,Status=ChangeStatus.Pending});
                continue;
            }
            if(c.Kind==ChangeKind.CreateFolder&&string.Equals(Path.GetDirectoryName(c.SourcePath),folder,StringComparison.OrdinalIgnoreCase))result.Add(new FileEntry(Path.GetFileName(c.SourcePath)!,c.SourcePath,FileKind.Folder,0,DateTime.Now,ChangeStatus.Pending));
            if(c.Kind==ChangeKind.Download&&string.Equals(Path.GetDirectoryName(c.TargetPath),folder,StringComparison.OrdinalIgnoreCase))result.Add(new FileEntry(Path.GetFileName(c.TargetPath)!,c.TargetPath!,FileKind.File,0,DateTime.Now,ChangeStatus.Pending));
        }
        foreach(var failed in Pending.Where(x=>x.Status==ChangeStatus.Failed))
            for(var i=0;i<result.Count;i++)if(string.Equals(result[i].Path,failed.SourcePath,StringComparison.OrdinalIgnoreCase)||string.Equals(result[i].Path,failed.TargetPath,StringComparison.OrdinalIgnoreCase))result[i]=result[i] with{Status=ChangeStatus.Failed};
        return result.OrderBy(x=>x.Kind==FileKind.File).ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public void FilesRefresh(){if(!string.IsNullOrWhiteSpace(CurrentPath))_=LoadFolderAsync(CurrentPath,CancellationToken.None);}
    private void Persist(){try{_state.Save(Folders,Pending);}catch(Exception ex){ReportError(ex);}}
    public void ReportError(Exception ex)=>LastError=ex.Message;
    public void Dispose(){Persist();Resolver.Dispose();}
    public event PropertyChangedEventHandler? PropertyChanged; private void Changed([CallerMemberName]string? n=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(n));
}
