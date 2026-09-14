using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface ILibraryRepository
{
    Task<Library?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Library library, CancellationToken cancellationToken = default);
}
