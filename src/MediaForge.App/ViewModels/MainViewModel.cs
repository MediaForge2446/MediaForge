using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.Application.Abstractions;
using MediaForge.Application.Commit;
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

    [ObservableProperty] private string _pageTitle = "הבית";
    [ObservableProperty] private string _activeSection = "home";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isMediaDialogOpen;
    [ObservableProperty] private string _statusText = "מוכן";
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private object? _currentPage;

    public ObservableCollection<RootFolderViewModel> RootFolders { get; } = [];
    public ObservableCollection<StagingOperation> PendingOperations { get; } = [];
    public ExplorerViewModel Explorer { get; }
    public DownloadsViewModel Downloads { get; }
    public SettingsViewModel Settings { get; }
    public bool HasLibrary => RootFolders.Count > 0;
    public bool HasPendingChanges => PendingCount > 0;

    public MainViewModel(
        LibraryService libraryService,
        LibraryScanService libraryScanService,
        IFolderPicker folderPicker,
        StagingService stagingService,
        ICommitEngine commitEngine,
        ExplorerViewModel explorer,
        DownloadsViewModel downloads,
        SettingsViewModel settings)
    {
        _libraryService = libraryService;
        _libraryScanService = libraryScanService;
        _folderPicker = folderPicker;
        _stagingService = stagingService;
        _commitEngine = commitEngine;
        Explorer = explorer;
        Downloads = downloads;
        Settings = settings;
        CurrentPage = this;

        _stagingService.Changed += OnStagingChanged;
        Explorer.AddMediaRequested += OnAddMediaRequested;
        Downloads.MediaStaged += OnMediaStaged;
    }

    private void OnAddMediaRequested(string path)
    {
        Downloads.PrepareForFolder(path);
        IsMediaDialogOpen = true;
        StatusText = $"הוספת מדיה אל {path}";
    }

    private async void OnMediaStaged()
    {
        IsMediaDialogOpen = false;
        RefreshPendingCount();
        await Explorer.ReloadAsync().ConfigureAwait(true);
        StatusText = "השירים נוספו כשינויים ממתינים — הדיסק עדיין לא השתנה";
    }

    [RelayCommand]
    private void CloseMediaDialog() => IsMediaDialogOpen = false;

    [RelayCommand]
    private void NavigateHome()
    {
        IsMediaDialogOpen = false;
        ActiveSection = "home";
        PageTitle = "הבית";
        CurrentPage = this;
        StatusText = RootFolders.Count == 0 ? "הוסף תיקיית מקור ראשית כדי להתחיל" : "בחר תיקייה ראשית כדי לפתוח את סביבת העבודה";
    }

    public void NavigateHomeFromView() => NavigateHome();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        StatusText = "טוען את הספרייה…";
        try
        {
            var library = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(true);
            RootFolders.Clear();
            foreach (var root in library.RootFolders)
                RootFolders.Add(new RootFolderViewModel(root));

            OnPropertyChanged(nameof(HasLibrary));
            await _stagingService.InitializeAsync(cancellationToken).ConfigureAwait(true);
            RefreshPendingCount();
            CurrentPage = this;
            await RefreshLibraryCoreAsync(cancellationToken).ConfigureAwait(true);
            StatusText = RootFolders.Count == 0 ? "הוסף תיקיית מקור ראשית כדי להתחיל" : "בחר תיקייה ראשית כדי להתחיל";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshLibraryCoreAsync(CancellationToken cancellationToken)
    {
        foreach (var root in RootFolders)
            await RefreshRootAsync(root, cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(HasLibrary));
    }

    [RelayCommand]
    private async Task AddRootFolderAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        var path = await _folderPicker.PickFolderAsync(cancellationToken).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path)) return;

        IsBusy = true;
        StatusText = "מוסיף את התיקייה הראשית…";
        try
        {
            var library = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(true);
            if (library.RootFolders.Any(x => string.Equals(Path.GetFullPath(x.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
            {
                StatusText = "התיקייה כבר קיימת בבית";
                return;
            }

            var root = await _libraryService.AddRootFolderAsync(library, path, cancellationToken: cancellationToken).ConfigureAwait(true);
            var vm = new RootFolderViewModel(root);
            RootFolders.Add(vm);
            await RefreshRootAsync(vm, cancellationToken).ConfigureAwait(true);
            OnPropertyChanged(nameof(HasLibrary));
            StatusText = "התיקייה הראשית נוספה";
        }
        catch (OperationCanceledException) { StatusText = "הפעולה בוטלה"; }
        catch (Exception ex) { StatusText = $"לא ניתן להוסיף את התיקייה: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task OpenRootFolderAsync(RootFolderViewModel? folder, CancellationToken cancellationToken)
    {
        if (folder is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await Explorer.SetInitialPathAsync(folder.Path, cancellationToken).ConfigureAwait(true);
            ActiveSection = "explorer";
            PageTitle = folder.Name;
            CurrentPage = Explorer;
            StatusText = folder.Path;
        }
        catch (Exception ex) { StatusText = $"לא ניתן לפתוח את התיקייה: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task RefreshRootAsync(RootFolderViewModel root, CancellationToken cancellationToken)
    {
        var model = new RootFolder { Id = root.Id, Path = root.Path, Name = root.Name };
        root.ApplyScan(await _libraryScanService.ScanAsync(model, cancellationToken).ConfigureAwait(true));
    }

    [RelayCommand]
    private async Task RefreshLibraryAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "סורק מחדש…";
        try
        {
            await RefreshLibraryCoreAsync(cancellationToken).ConfigureAwait(true);
            await Explorer.ReloadAsync(cancellationToken).ConfigureAwait(true);
            StatusText = "הספרייה עודכנה";
        }
        catch (OperationCanceledException) { StatusText = "הסריקה בוטלה"; }
        catch (Exception ex) { StatusText = $"הסריקה נכשלה: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || !HasPendingChanges) return;
        IsBusy = true;
        StatusText = "שומר שינויים בדיסק…";
        try
        {
            var operations = _stagingService.Operations.ToArray();
            var progress = new Progress<CommitProgress>(value =>
                StatusText = value.Percent >= 100 ? "מאמת שינויים…" : $"{value.Status} · {value.Percent:0}%");

            var result = await _commitEngine.CommitAsync(operations, progress, cancellationToken).ConfigureAwait(true);
            var successful = result.Items.Where(x => x.Success).Select(x => x.OperationId).ToArray();
            var failed = result.Items.Where(x => !x.Success).Select(x => x.OperationId).ToArray();
            await Explorer.ApplyCommitResultsAsync(successful, failed, cancellationToken).ConfigureAwait(true);
            RefreshPendingCount();
            StatusText = result.Success ? "כל השינויים נשמרו ואומתו" : $"השמירה הסתיימה עם {failed.Length} שגיאות";
            await Explorer.ReloadAsync(cancellationToken).ConfigureAwait(true);
            await RefreshLibraryCoreAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { StatusText = "השמירה בוטלה — שינויים שלא הושלמו נשארו ממתינים"; }
        catch (Exception ex) { StatusText = $"השמירה נכשלה: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UndoPendingAsync(Guid? operationId, CancellationToken cancellationToken)
    {
        if (operationId is not Guid id || IsBusy) return;
        await _stagingService.UndoAsync(id, cancellationToken).ConfigureAwait(true);
        RefreshPendingCount();
        await Explorer.ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    public void RefreshPendingCount()
    {
        PendingCount = _stagingService.Operations.Count;
        PendingOperations.Clear();
        foreach (var operation in _stagingService.Operations.OrderByDescending(x => x.CreatedAt))
            PendingOperations.Add(operation);
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    private void OnStagingChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            RefreshPendingCount();
            return;
        }

        _ = dispatcher.InvokeAsync(RefreshPendingCount);
    }
}

public sealed partial class RootFolderViewModel : ObservableObject
{
    private readonly RootFolder _model;
    [ObservableProperty] private bool _exists;
    [ObservableProperty] private int _folderCount;
    [ObservableProperty] private int _fileCount;
    [ObservableProperty] private int _mediaCount;
    [ObservableProperty] private string _totalSizeText = "0 B";
    [ObservableProperty] private string _scanStatus = "לא נבדק";
    [ObservableProperty] private string _scanSummary = string.Empty;

    public Guid Id => _model.Id;
    public string Name => _model.Name;
    public string Path => _model.Path;
    public string DisplayPath => _model.Path;
    public RootFolderViewModel(RootFolder model) => _model = model;

    public void ApplyScan(LibraryScanResult scan)
    {
        Exists = scan.Exists;
        FolderCount = scan.FolderCount;
        FileCount = scan.FileCount;
        MediaCount = scan.MediaCount;
        TotalSizeText = scan.TotalSizeText;
        ScanStatus = scan.Exists ? "מחובר ומסונכרן" : "התיקייה לא נמצאה";
        ScanSummary = scan.Exists
            ? $"{scan.MediaCount} מדיה · {scan.FileCount} קבצים · {scan.FolderCount} תיקיות · {scan.TotalSizeText}"
            : "בדוק את הנתיב או חבר את הכונן";
    }
}