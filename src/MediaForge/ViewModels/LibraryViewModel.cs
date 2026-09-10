using System.Collections.ObjectModel;
using MediaForge.Core.Models;
using MediaForge.State;

namespace MediaForge.ViewModels;

public sealed class LibraryViewModel : ViewModelBase
{
    private readonly LibraryState _state;
    private readonly MainViewModel _main;
    private MediaFolder? _selectedRootFolder;

    public ObservableCollection<MediaFolder> RootFolders { get; } = new();

    public MediaFolder? SelectedRootFolder
    {
        get => _selectedRootFolder;
        set => SetProperty(ref _selectedRootFolder, value);
    }

    public MainViewModel Main => _main;

    public LibraryViewModel(LibraryState state, MainViewModel main)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _main = main ?? throw new ArgumentNullException(nameof(main));
        Refresh();
        _state.Changed += OnStateChanged;
    }

    public void Refresh()
    {
        var selectedId = SelectedRootFolder?.Id;
        RootFolders.Clear();
        foreach (var folder in _state.RootFolders)
        {
            RootFolders.Add(folder);
        }

        SelectedRootFolder = RootFolders.FirstOrDefault(folder => folder.Id == selectedId);
    }

    public void AddRootFolder(string path, string displayName)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

            _state.AddRootFolder(new MediaFolder
            {
                Id = Guid.NewGuid().ToString("N"),
                Path = Path.GetFullPath(path),
                DisplayName = displayName.Trim(),
                AddedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        Refresh();
    }
}
