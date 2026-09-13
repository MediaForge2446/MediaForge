using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IToolManager
{
    Task<ToolPaths> EnsureToolsReadyAsync(CancellationToken cancellationToken = default);
}
