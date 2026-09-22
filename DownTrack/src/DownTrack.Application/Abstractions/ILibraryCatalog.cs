using DownTrack.Core.Models;

namespace DownTrack.Application.Abstractions;

public interface ILibraryCatalog
{
    IReadOnlyList<LibraryRoot> Roots { get; }

    bool ContainsPath(string path);

    Task<LibraryRoot> AddRootAsync(string path, CancellationToken cancellationToken = default);
}
