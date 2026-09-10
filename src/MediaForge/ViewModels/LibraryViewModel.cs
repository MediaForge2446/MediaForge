using System.Collections.ObjectModel;
using MediaForge.Core.Models;
using MediaForge.State;

namespace MediaForge.ViewModels;

public sealed class LibraryViewModel : ViewModelBase
{
    private readonly LibraryState _state;
    private readonly MainViewModel _main;

    public ObservableCollection<MediaFolder> RootFolders { get; } = new();

    public LibraryViewModel(LibraryState state, MainViewModel main)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _main = main ?? throw new ArgumentNullException(nameof(main));
        Refresh();
        _state.Changed += OnStateChanged;
    }

    public void Refresh()
    {
        RootFolders.Clear();
        foreach (var folder in _state.RootFolders)
        {
            RootFolders.Add(folder);
        }
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
