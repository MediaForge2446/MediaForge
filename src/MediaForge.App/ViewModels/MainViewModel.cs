using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.Application.Library;
using MediaForge.Application.Staging;
using MediaForge.App.Services;
using MediaForge.Core.Models;

namespace MediaForge.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly IFolderPicker _folderPicker;
    private readonly StagingService _stagingService;

    [ObservableProperty]
    private string _pageTitle = "הספרייה שלי";

    [ObservableProperty]
    private string _activeSection = "home";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "מוכן";

    [ObservableProperty]
    private int _pendingCount;

    public ObservableCollection<RootFolderViewModel> RootFolders { get; } = [];

    public bool HasLibrary => RootFolders.Count > 0;

    public bool HasPendingChanges => PendingCount > 0;

    public MainViewModel(
        LibraryService libraryService,
        IFolderPicker folderPicker,
        StagingService stagingService)
    {
        _libraryService = libraryService;
        _folderPicker = folderPicker;
        _stagingService = stagingService;
    }

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

            await _stagingService.InitializeAsync(cancellationToken).ConfigureAwait(true);
            PendingCount = _stagingService.Operations.Count;
            StatusText = RootFolders.Count == 0 ? "הספרייה שלך עדיין ריקה" : "הספרייה מסונכרנת";
            OnPropertyChanged(nameof(HasLibrary));
            OnPropertyChanged(nameof(HasPendingChanges));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Navigate(string section)
    {
        ActiveSection = section;
        PageTitle = section switch
        {
            "home" => "הספרייה שלי",
            "explorer" => "סייר הקבצים",
            "downloads" => "ההורדות שלי",
            "settings" => "הגדרות",
            _ => "הספרייה שלי"
        };
    }

    [RelayCommand]
    private async Task AddRootFolderAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        var path = await _folderPicker.PickFolderAsync(cancellationToken).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
            return;

        IsBusy = true;
        StatusText = "מוסיף תיקייה…";

        try
        {
            var library = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(true);
            var root = await _libraryService.AddRootFolderAsync(library, path, cancellationToken: cancellationToken).ConfigureAwait(true);
            RootFolders.Add(new RootFolderViewModel(root));
            OnPropertyChanged(nameof(HasLibrary));
            StatusText = "התיקייה נוספה לספרייה";
        }
        catch (Exception ex)
        {
            StatusText = $"לא ניתן להוסיף את התיקייה: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveRootFolderAsync(RootFolderViewModel? folder, CancellationToken cancellationToken)
    {
        if (folder is null || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var library = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(true);
            await _libraryService.RemoveRootFolderAsync(library, folder.Id, cancellationToken).ConfigureAwait(true);
            RootFolders.Remove(folder);
            OnPropertyChanged(nameof(HasLibrary));
            StatusText = "התיקייה הוסרה מהספרייה";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class RootFolderViewModel : ObservableObject
{
    private readonly RootFolder _model;

    public Guid Id => _model.Id;
    public string Name => _model.Name;
    public string Path => _model.Path;
    public string DisplayPath => _model.Path.Replace('\\', '/');

    public RootFolderViewModel(RootFolder model)
    {
        _model = model;
    }
}
