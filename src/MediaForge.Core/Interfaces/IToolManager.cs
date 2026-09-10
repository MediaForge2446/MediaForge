using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IToolManager
{
    event EventHandler? StatusChanged;

    IReadOnlyList<ToolStatus> Status { get; }

    string ToolsDirectory { get; }

    Task<IReadOnlyList<ToolStatus>> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ToolStatus>> UpdateAllAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
