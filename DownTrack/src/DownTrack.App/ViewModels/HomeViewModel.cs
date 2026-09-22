using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DownTrack.Application.Abstractions;
using DownTrack.Core.Models;

namespace DownTrack.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ILibraryCatalog _catalog;
    private readonly IFolderPicker _folderPicker;

    public HomeViewModel(
        ILibraryCatalog catalog,
        IFolderPicker folderPicker)
    {
        _catalog = catalog;
        _folderPicker = folderPicker;
        Roots = new ObservableCollection<LibraryRoot>(_catalog.Roots);
    }

    public ObservableCollection<LibraryRoot> Roots { get; }

    public nint OwnerHandle { get; set; }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? statusMessage;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AddFolderAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = null;

            var path = await _folderPicker.PickFolderAsync(OwnerHandle);

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var root = await _catalog.AddRootAsync(path);
            Roots.Add(root);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation.
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
