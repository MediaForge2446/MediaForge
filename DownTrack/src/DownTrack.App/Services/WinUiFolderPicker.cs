using DownTrack.Application.Abstractions;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DownTrack.App.Services;

public sealed class WinUiFolderPicker : IFolderPicker
{
    public async Task<string?> PickFolderAsync(
        nint ownerHandle,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, ownerHandle);

        var folder = await picker.PickSingleFolderAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return folder?.Path;
    }
}
