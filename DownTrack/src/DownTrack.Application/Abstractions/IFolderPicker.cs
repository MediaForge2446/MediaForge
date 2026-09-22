namespace DownTrack.Application.Abstractions;

public interface IFolderPicker
{
    Task<string?> PickFolderAsync(nint ownerHandle, CancellationToken cancellationToken = default);
}
