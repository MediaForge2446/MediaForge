using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IExplorerService
{
    Task<IReadOnlyList<ExplorerEntry>> ListAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);
}
