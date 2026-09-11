using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace MediaForge.ViewModels;
public enum ChangeStatus{Synced,Pending,Failed}
public enum ChangeKind{Rename,Delete,Move,CreateFolder,Download}
public enum MediaFormat{Mp3,Mp4}
public enum FileKind{File,Folder}
public sealed record LibraryFolder(Guid Id,string Name,string Path);
public sealed record FileEntry(string Name,string Path,FileKind Kind,long Size,DateTime LastWriteTime,ChangeStatus Status)
{
 public string SizeText=>Kind==FileKind.Folder?"Folder":Size switch{<1024=>$"{Size:N0} B",<1048576=>$"{Size/1024d:N1} KB",<1073741824=>$"{Size/1048576d:N1} MB",_=>$"{Size/1073741824d:N1} GB"};
 public string ModifiedText=>LastWriteTime.ToString("g");
 public string Glyph=>Kind==FileKind.Folder?"\uE8B7":"\uE8A5";
 public string StatusText=>Status switch{ChangeStatus.Synced=>"Synced",ChangeStatus.Pending=>"Pending",ChangeStatus.Failed=>"Failed",_=>"Unknown"};
}
public sealed class PendingChange:INotifyPropertyChanged
{
 public Guid Id{get;init;}=Guid.NewGuid(); public ChangeKind Kind{get;init;} public string SourcePath{get;init;}=""; public string? TargetPath{get;init;} public string? SourceUrl{get;init;} public string? DisplayName{get;init;} public MediaFormat Format{get;init;}=MediaFormat.Mp3;
 private ChangeStatus _status=ChangeStatus.Pending; private double _progress; private string? _error;
 public ChangeStatus Status{get=>_status;set{if(_status!=value){_status=value;Changed();Changed(nameof(StatusText));}}}
 public double Progress{get=>_progress;set{value=Math.Clamp(value,0,1);if(Math.Abs(_progress-value)>0.0001){_progress=value;Changed();}}}
 public string? Error{get=>_error;set{if(_error!=value){_error=value;Changed();Changed(nameof(StatusText));}}}
 public string StatusText=>Status==ChangeStatus.Failed?Error??"Failed":Status==ChangeStatus.Pending?"Pending":"Saved";
 public event PropertyChangedEventHandler? PropertyChanged; private void Changed([CallerMemberName]string? n=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(n));
}
public sealed class MediaItem:INotifyPropertyChanged
{
 public string Url{get;init;}=""; public string ThumbnailUrl{get;init;}=""; public string Artist{get;init;}=""; public TimeSpan? Duration{get;init;}
 private string _title=""; private bool _selected=true; private MediaFormat _format=MediaFormat.Mp3;
 public string Title{get=>_title;set{if(_title!=value){_title=value;Changed();}}}
 public bool IsSelected{get=>_selected;set{if(_selected!=value){_selected=value;Changed();}}}
 public MediaFormat Format{get=>_format;set{if(_format!=value){_format=value;Changed();Changed(nameof(FormatIndex));}}}
 public int FormatIndex{get=>(int)Format;set{if(value is 0 or 1)Format=(MediaFormat)value;}}
 public string DurationText=>Duration?.ToString(@"mm\:ss")??"";
 public event PropertyChangedEventHandler? PropertyChanged; private void Changed([CallerMemberName]string? n=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(n));
}
