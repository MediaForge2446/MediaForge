using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.Application.Abstractions;
using MediaForge.Application.Commit;
using MediaForge.Application.Downloads;
using MediaForge.Application.Library;
using MediaForge.Application.Staging;
using MediaForge.App.Services;
using MediaForge.Core.Models;

namespace MediaForge.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly LibraryScanService _libraryScanService;
    private readonly IFolderPicker _folderPicker;
    private readonly StagingService _stagingService;
    private readonly ICommitEngine _commitEngine;
    [ObservableProperty] private string _pageTitle = "הספרייה שלי";
    [ObservableProperty] private string _activeSection = "home";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "מוכן";
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private object? _currentPage;
    public ObservableCollection<RootFolderViewModel> RootFolders { get; } = [];
    public ExplorerViewModel Explorer { get; }
    public DownloadsViewModel Downloads { get; }
    public SettingsViewModel Settings { get; }
    public bool HasLibrary => RootFolders.Count > 0;
    public bool HasPendingChanges => PendingCount > 0;

    public MainViewModel(LibraryService libraryService, LibraryScanService libraryScanService, IFolderPicker folderPicker, StagingService stagingService, ICommitEngine commitEngine, ExplorerViewModel explorer, DownloadsViewModel downloads, SettingsViewModel settings)
    { _libraryService = libraryService; _libraryScanService = libraryScanService; _folderPicker = folderPicker; _stagingService = stagingService; _commitEngine = commitEngine; Explorer = explorer; Downloads = downloads; Settings = settings; CurrentPage = this; _stagingService.Changed += OnStagingChanged; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true; StatusText = "טוען את הספרייה…";
        try
        {
            var library = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(true);
            RootFolders.Clear(); foreach (var root in library.RootFolders) RootFolders.Add(new RootFolderViewModel(root));
            OnPropertyChanged(nameof(HasLibrary));
            await _stagingService.InitializeAsync(cancellationToken).ConfigureAwait(true); RefreshPendingCount();
            Downloads.SetDefaultDestination(RootFolders.FirstOrDefault()?.Path ?? string.Empty);
            CurrentPage = this;
            await RefreshLibraryCoreAsync(cancellationToken).ConfigureAwait(true);
            await Explorer.InitializeAsync(RootFolders.FirstOrDefault()?.Path, cancellationToken).ConfigureAwait(true);
            StatusText = RootFolders.Count == 0 ? "הספרייה שלך עדיין ריקה" : "הספרייה מסונכרנת עם מצב הדיסק";
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshLibraryCoreAsync(CancellationToken cancellationToken)
    { foreach (var root in RootFolders) await RefreshRootAsync(root, cancellationToken).ConfigureAwait(true); OnPropertyChanged(nameof(HasLibrary)); }

    [RelayCommand]
    private async Task RefreshLibraryAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return; IsBusy = true; StatusText = "סורק מחדש את התיקיות במחשב…";
        try { await RefreshLibraryCoreAsync(cancellationToken).ConfigureAwait(true); await Explorer.ReloadAsync(cancellationToken).ConfigureAwait(true); StatusText = "הספרייה עודכנה לפי מצב הדיסק"; }
        catch (OperationCanceledException) { StatusText = "הסריקה בוטלה"; }
        catch (Exception ex) { StatusText = $"הסריקה נכשלה: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task RefreshRootAsync(RootFolderViewModel root, CancellationToken cancellationToken)
    { var model = new RootFolder { Id = root.Id, Path = root.Path, Name = root.Name }; root.ApplyScan(await _libraryScanService.ScanAsync(model, cancellationToken).ConfigureAwait(true)); }

    [RelayCommand]
    private async Task NavigateAsync(string section, CancellationToken cancellationToken)
    {
        if (IsBusy) return; ActiveSection = section; PageTitle = section switch { "home" => "הספרייה שלי", "explorer" => "סייר הקבצים", "downloads" => "הורדת מדיה", "settings" => "הגדרות וכלים", _ => "הספרייה שלי" };
        switch (section) { case "home": CurrentPage = this; break; case "explorer": if (string.IsNullOrWhiteSpace(Explorer.CurrentPath)) await Explorer.InitializeAsync(RootFolders.FirstOrDefault()?.Path, cancellationToken).ConfigureAwait(true); CurrentPage = Explorer; break; case "downloads": CurrentPage = Downloads; break; case "settings": CurrentPage = Settings; break; }
    }

    [RelayCommand]
    private async Task AddRootFolderAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return; var path = await _folderPicker.PickFolderAsync(cancellationToken).ConfigureAwait(true); if (string.IsNullOrWhiteSpace(path)) return; IsBusy = true; StatusText = "מוסיף וסורק תיקייה…";
        try { var library = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(true); if (library.RootFolders.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase))) { StatusText = "התיקייה כבר קיימת בספרייה"; return; } var root = await _libraryService.AddRootFolderAsync(library, path, cancellationToken: cancellationToken).ConfigureAwait(true); var vm = new RootFolderViewModel(root); RootFolders.Add(vm); await RefreshRootAsync(vm, cancellationToken).ConfigureAwait(true); if (string.IsNullOrWhiteSpace(Downloads.DestinationDirectory)) Downloads.SetDefaultDestination(root.Path); if (string.IsNullOrWhiteSpace(Explorer.CurrentPath)) await Explorer.SetInitialPathAsync(root.Path, cancellationToken).ConfigureAwait(true); OnPropertyChanged(nameof(HasLibrary)); StatusText = "התיקייה נוספה ונבדקה מול הדיסק"; }
        catch (OperationCanceledException) { StatusText = "הפעולה בוטלה"; } catch (Exception ex) { StatusText = $"לא ניתן להוסיף את התיקייה: {ex.Message}"; } finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveRootFolderAsync(RootFolderViewModel? folder, CancellationToken cancellationToken)
    { if (folder is null || IsBusy) return; IsBusy = true; try { var library = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(true); await _libraryService.RemoveRootFolderAsync(library, folder.Id, cancellationToken).ConfigureAwait(true); RootFolders.Remove(folder); OnPropertyChanged(nameof(HasLibrary)); StatusText = "התיקייה הוסרה מהספרייה בלבד — הקבצים במחשב לא נמחקו"; } catch (OperationCanceledException) { StatusText = "הפעולה בוטלה"; } catch (Exception ex) { StatusText = ex.Message; } finally { IsBusy = false; } }

    [RelayCommand]
    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    { if (IsBusy || !HasPendingChanges) return; IsBusy = true; StatusText = "מבצע שינויים בדיסק…"; try { var operations = _stagingService.Operations.ToArray(); var progress = new Progress<CommitProgress>(value => StatusText = value.Percent >= 100 ? "מאמת…" : $"{value.Status} · {value.Percent:0}%"); var result = await _commitEngine.CommitAsync(operations, progress, cancellationToken).ConfigureAwait(true); RefreshPendingCount(); StatusText = result.Success ? "כל השינויים נשמרו ואומתו בדיסק" : $"השמירה הסתיימה עם {result.Items.Count(x => !x.Success)} שגיאות"; await Explorer.ReloadAsync(cancellationToken).ConfigureAwait(true); await RefreshLibraryCoreAsync(cancellationToken).ConfigureAwait(true); } catch (OperationCanceledException) { StatusText = "השמירה בוטלה — שינויים שלא הושלמו נשארו ממתינים"; } catch (Exception ex) { StatusText = $"השמירה נכשלה: {ex.Message}"; } finally { IsBusy = false; } }
    public void RefreshPendingCount() { PendingCount = _stagingService.Operations.Count; OnPropertyChanged(nameof(HasPendingChanges)); }
    private void OnStagingChanged(object? sender, EventArgs e) => RefreshPendingCount();
}

public sealed partial class RootFolderViewModel : ObservableObject
{
    private readonly RootFolder _model;
    [ObservableProperty] private bool _exists; [ObservableProperty] private int _folderCount; [ObservableProperty] private int _fileCount; [ObservableProperty] private int _mediaCount; [ObservableProperty] private string _totalSizeText = "0 B"; [ObservableProperty] private string _scanStatus = "לא נבדק"; [ObservableProperty] private string _scanSummary = "";
    public Guid Id => _model.Id; public string Name => _model.Name; public string Path => _model.Path; public string DisplayPath => _model.Path.Replace('\\', '/');
    public RootFolderViewModel(RootFolder model) => _model = model;
    public void ApplyScan(LibraryScanResult scan) { Exists = scan.Exists; FolderCount = scan.FolderCount; FileCount = scan.FileCount; MediaCount = scan.MediaCount; TotalSizeText = scan.TotalSizeText; ScanStatus = scan.Exists ? "מסונכרן עם הדיסק" : "התיקייה לא נמצאה"; ScanSummary = scan.Exists ? $"{scan.MediaCount} קבצי מדיה · {scan.FileCount} קבצים · {scan.FolderCount} תיקיות · {scan.TotalSizeText}" : "יש לעדכן את הנתיב או לחבר את הכונן"; }
}
