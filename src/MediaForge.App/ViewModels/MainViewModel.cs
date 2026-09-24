using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.Application.Abstractions;
using MediaForge.Application.Commit;
using MediaForge.Application.Library;
using MediaForge.Application.Staging;
using MediaForge.App.Services;
using MediaForge.App.Localization;
using MediaForge.Core.Models;

namespace MediaForge.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly LibraryScanService _libraryScanService;
    private readonly IFolderPicker _folderPicker;
    private readonly StagingService _stagingService;
    private readonly ICommitEngine _commitEngine;
    private readonly LocalizationService _localization;

    [ObservableProperty] private string _pageTitle = string.Empty;
    [ObservableProperty] private string _activeSection = "home";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private double _commitProgressPercent;
    [ObservableProperty] private string _commitProgressStatus = string.Empty;
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
        SettingsViewModel settings,
        LocalizationService localization)
    {
        _libraryService = libraryService;
        _libraryScanService = libraryScanService;
        _folderPicker = folderPicker;
        _stagingService = stagingService;
        _commitEngine = commitEngine;
        _localization = localization;
        Explorer = explorer;
        Downloads = downloads;
        Settings = settings;
        CurrentPage = this;
        _localization.CultureChanged += OnCultureChanged;
        ApplyHomeText();

        _stagingService.Changed += OnStagingChanged;
        Explorer.AddMediaRequested += OnAddMediaRequested;
        Downloads.RetryRequested += RetryDownloadAsync;
    }

    private void OnAddMediaRequested(string path)
    {
        Downloads.PrepareForFolder(path);
        StatusText = $"{_localization.Get("Status_AddMediaTo")} {path}";
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (ActiveSection == "home")
            ApplyHomeText();
        else if (ActiveSection == "settings")
        {
            PageTitle = _localization.Get("Settings_Title");
            StatusText = _localization.Get("Settings_Description");
        }
    }

    private void ApplyHomeText()
    {
        PageTitle = _localization.Get("Nav_Home");
        StatusText = RootFolders.Count == 0
            ? _localization.Get("Status_AddLibrary")
            : _localization.Get("Status_ChooseLibrary");
    }

    [RelayCommand]
    private void NavigateHome()
    {
        ActiveSection = "home";
        CurrentPage = this;
        ApplyHomeText();
    }

    public void NavigateHomeFromView() => NavigateHome();

    [RelayCommand]
    private async Task NavigateExplorerAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Explorer.CurrentPath))
        {
            var first = RootFolders.FirstOrDefault();
            if (first is null)
            {
                StatusText = _localization.Get("Status_AddLibrary");
                return;
            }

            await OpenRootFolderAsync(first, cancellationToken).ConfigureAwait(true);
            return;
        }

        ActiveSection = "explorer";
        PageTitle = Path.GetFileName(Explorer.CurrentPath.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name
            ? name
            : _localization.Get("Nav_Explorer");
        CurrentPage = Explorer;
        StatusText = Explorer.CurrentPath;
    }

    [RelayCommand]
    private void NavigateDownloads()
    {
        if (IsBusy) return;
        ActiveSection = "downloads";
        PageTitle = _localization.Get("Downloads_Title");
        CurrentPage = Downloads;
        StatusText = _localization.Get("Status_DownloadsDescription");
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        if (IsBusy) return;
        ActiveSection = "settings";
        PageTitle = _localization.Get("Settings_Title");
        CurrentPage = Settings;
        StatusText = _localization.Get("Settings_Description");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        StatusText = _localization.Get("Status_LoadingLibrary");
        try
        {
            var library = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(true);
            RootFolders.Clear();
            foreach (var root in library.RootFolders)
                RootFolders.Add(new RootFolderViewModel(root, _localization));

            OnPropertyChanged(nameof(HasLibrary));
            await _stagingService.InitializeAsync(cancellationToken).ConfigureAwait(true);
            RefreshPendingCount();
            Downloads.SyncFromStaging(_stagingService.Operations);
            CurrentPage = this;
            await RefreshLibraryCoreAsync(cancellationToken).ConfigureAwait(true);

            if (RootFolders.Count == 1 && RootFolders[0].Exists)
            {
                await Explorer.SetInitialPathAsync(
                    RootFolders[0].Path,
                    cancellationToken).ConfigureAwait(true);
                ActiveSection = "explorer";
                PageTitle = RootFolders[0].Name;
                CurrentPage = Explorer;
                StatusText = RootFolders[0].Path;
            }
            else
            {
                ApplyHomeText();
            }
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
        StatusText = _localization.Get("Status_AddingLibrary");
        try
        {
            var library = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(true);
            if (library.RootFolders.Any(x => string.Equals(Path.GetFullPath(x.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
            {
                StatusText = _localization.Get("Status_DuplicateRoot");
                return;
            }

            var root = await _libraryService.AddRootFolderAsync(library, path, cancellationToken: cancellationToken).ConfigureAwait(true);
            var vm = new RootFolderViewModel(root, _localization);
            RootFolders.Add(vm);
            await RefreshRootAsync(vm, cancellationToken).ConfigureAwait(true);
            OnPropertyChanged(nameof(HasLibrary));
            StatusText = _localization.Get("Status_RootAdded");
        }
        catch (OperationCanceledException) { StatusText = _localization.Get("Status_Canceled"); }
        catch (Exception ex) { StatusText = $"{_localization.Get("Status_AddLibraryFailed")}: {ex.Message}"; }
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
        StatusText = _localization.Get("Status_Scanning");
        try
        {
            await RefreshLibraryCoreAsync(cancellationToken).ConfigureAwait(true);
            await Explorer.ReloadAsync(cancellationToken).ConfigureAwait(true);
            StatusText = _localization.Get("Status_LibraryUpdated");
        }
        catch (OperationCanceledException) { StatusText = _localization.Get("Status_Canceled"); }
        catch (Exception ex) { StatusText = $"{_localization.Get("Status_ScanFailed")}: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || !HasPendingChanges) return;
        IsBusy = true;
        StatusText = _localization.Get("Status_SaveChanges");
        try
        {
            var order = Downloads.GetOrderedOperationIds()
                .Select((id, index) => (id, index))
                .ToDictionary(x => x.id, x => x.index);

            var operations = _stagingService.Operations
                .OrderBy(x => order.TryGetValue(x.OperationId, out var index) ? index : int.MaxValue)
                .ToArray();
            CommitProgressPercent = 0;
            CommitProgressStatus = _localization.Get("Status_SavePreparing");

            var progress = new Progress<CommitProgress>(value =>
            {
                CommitProgressPercent = Math.Clamp(value.Percent, 0, 100);
                CommitProgressStatus = value.Status;
                Downloads.ApplyCommitProgress(value);
                Explorer.ApplyCommitProgress(value);
                StatusText = value.Percent >= 100 ? _localization.Get("Status_VerifyingChanges") : $"{value.Status} · {value.Percent:0}%";
            });

            var result = await _commitEngine.CommitAsync(operations, progress, cancellationToken).ConfigureAwait(true);
            var successful = result.Items.Where(x => x.Success).Select(x => x.OperationId).ToArray();
            var failed = result.Items.Where(x => !x.Success).Select(x => x.OperationId).ToArray();
            await Explorer.ApplyCommitResultsAsync(successful, failed, cancellationToken).ConfigureAwait(true);
            Downloads.ApplyCommitResults(result.Items);
            RefreshPendingCount();
            Downloads.SyncFromStaging(_stagingService.Operations);
            StatusText = result.Success ? _localization.Get("Status_ChangesSaved") : $"{_localization.Get("Status_SaveFailed")}: {failed.Length}";
            await Explorer.ReloadAsync(cancellationToken).ConfigureAwait(true);
            await RefreshLibraryCoreAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { StatusText = _localization.Get("Status_SaveCanceled"); }
        catch (Exception ex) { StatusText = $"{_localization.Get("Status_SaveFailed")}: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task RetryDownloadAsync(Guid operationId)
    {
        if (IsBusy)
            return;

        var operation = _stagingService.Operations
            .FirstOrDefault(x => x.OperationId == operationId && x.OperationType == MediaForge.Core.Enums.OperationType.Download);

        if (operation is null)
            return;

        IsBusy = true;
        StatusText = _localization.Get("Queue_Retrying");

        try
        {
            var progress = new Progress<CommitProgress>(value =>
            {
                CommitProgressPercent = Math.Clamp(value.Percent, 0, 100);
                CommitProgressStatus = value.Status;
                Downloads.ApplyCommitProgress(value);
            });

            var result = await _commitEngine
                .CommitAsync(new[] { operation }, progress, CancellationToken.None)
                .ConfigureAwait(true);

            Downloads.ApplyCommitResults(result.Items);
            RefreshPendingCount();
            Downloads.SyncFromStaging(_stagingService.Operations);

            if (result.Success)
            {
                await Explorer.ApplyCommitResultsAsync([operationId], [], CancellationToken.None).ConfigureAwait(true);
                await Explorer.ReloadAsync(CancellationToken.None).ConfigureAwait(true);
                await RefreshLibraryCoreAsync(CancellationToken.None).ConfigureAwait(true);
                StatusText = _localization.Get("Status_ChangesSaved");
            }
            else
            {
                StatusText = result.Items.FirstOrDefault()?.Error
                    ?? _localization.Get("Status_SaveFailed");
            }
        }
        catch (Exception ex)
        {
            Downloads.ApplyCommitResults([new CommitItemResult(operationId, false, ex.Message)]);
            StatusText = $"{_localization.Get("Status_SaveFailed")}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DiscardPendingChangesAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || !HasPendingChanges)
            return;

        IsBusy = true;
        StatusText = _localization.Get("Status_Discarding");
        try
        {
            await _stagingService.ClearAsync(cancellationToken).ConfigureAwait(true);
            RefreshPendingCount();
            Downloads.SyncFromStaging(_stagingService.Operations);
            await Explorer.RefreshFromStagingAsync(cancellationToken).ConfigureAwait(true);
            StatusText = _localization.Get("Status_Discarded");
        }
        catch (OperationCanceledException)
        {
            StatusText = _localization.Get("Status_Canceled");
        }
        catch (Exception ex)
        {
            StatusText = $"{_localization.Get("Status_DiscardFailed")}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UndoPendingAsync(Guid? operationId, CancellationToken cancellationToken)
    {
        if (operationId is not Guid id || IsBusy) return;
        await _stagingService.UndoAsync(id, cancellationToken).ConfigureAwait(true);
        RefreshPendingCount();
        Downloads.SyncFromStaging(_stagingService.Operations);
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
        if (dispatcher is null)
            return;

        if (dispatcher.CheckAccess())
        {
            _ = RefreshAfterStagingChangedAsync();
            return;
        }

        _ = dispatcher.InvokeAsync(() => _ = RefreshAfterStagingChangedAsync());
    }

    private async Task RefreshAfterStagingChangedAsync()
    {
        try
        {
            RefreshPendingCount();
            Downloads.SyncFromStaging(_stagingService.Operations);
            if (ActiveSection == "explorer" && !string.IsNullOrWhiteSpace(Explorer.CurrentPath))
                await Explorer.RefreshFromStagingAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = $"{_localization.Get("Status_RefreshFailed")}: {ex.Message}";
        }
    }
}

public sealed partial class RootFolderViewModel : ObservableObject
{
    private readonly RootFolder _model;
    private readonly LocalizationService _localization;
    private LibraryScanResult? _lastScan;
    [ObservableProperty] private bool _exists;
    [ObservableProperty] private int _folderCount;
    [ObservableProperty] private int _fileCount;
    [ObservableProperty] private int _mediaCount;
    [ObservableProperty] private string _totalSizeText = "0 B";
    [ObservableProperty] private string _scanStatus = string.Empty;
    [ObservableProperty] private string _scanSummary = string.Empty;

    public Guid Id => _model.Id;
    public string Name => _model.Name;
    public string Path => _model.Path;
    public string DisplayPath => _model.Path;
    public RootFolderViewModel(RootFolder model, LocalizationService localization)
    {
        _model = model;
        _localization = localization;
        _localization.CultureChanged += OnCultureChanged;
        UpdateScanText(null);
    }

    private void OnCultureChanged(object? sender, EventArgs e) => UpdateScanText(_lastScan);

    private void UpdateScanText(LibraryScanResult? scan)
    {
        _lastScan = scan;
        if (scan is null)
        {
            ScanStatus = _localization.Get("Status_NotChecked");
            ScanSummary = string.Empty;
            return;
        }

        ScanStatus = scan.Exists
            ? _localization.Get("Status_Synced")
            : _localization.Get("Status_RootMissing");

        ScanSummary = scan.Exists
            ? string.Format(
                _localization.CurrentCulture,
                "{0} media · {1} files · {2} folders · {3}",
                scan.MediaCount,
                scan.FileCount,
                scan.FolderCount,
                scan.TotalSizeText)
            : _localization.Get("Status_CheckPath");
    }

    public void ApplyScan(LibraryScanResult scan)
    {
        Exists = scan.Exists;
        FolderCount = scan.FolderCount;
        FileCount = scan.FileCount;
        MediaCount = scan.MediaCount;
        TotalSizeText = scan.TotalSizeText;
        UpdateScanText(scan);
    }
}