namespace MediaForge.Core.Interfaces;

public interface IToolManager
{
    Task EnsureToolsReadyAsync(CancellationToken cancellationToken = default);
}
