using DownTrack.Application.Abstractions;
using DownTrack.Core.Models;

namespace DownTrack.Application.Services;

public sealed class InMemoryLibraryCatalog : ILibraryCatalog
{
    private readonly List<LibraryRoot> _roots = [];

    public IReadOnlyList<LibraryRoot> Roots => _roots;

    public bool ContainsPath(string path)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return _roots.Any(root =>
            string.Equals(root.Path, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public Task<LibraryRoot> AddRootAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        if (!Directory.Exists(normalized))
        {
            throw new DirectoryNotFoundException(normalized);
        }

        if (ContainsPath(normalized))
        {
            throw new InvalidOperationException("This folder is already in the library.");
        }

        var root = LibraryRoot.Create(normalized);
        _roots.Add(root);

        return Task.FromResult(root);
    }
}
