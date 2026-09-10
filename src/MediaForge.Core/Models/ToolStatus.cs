namespace MediaForge.Core.Models;

public sealed record ToolStatus
{
    public required string Name { get; init; }

    public required string InstalledVersion { get; init; }

    public string? LatestVersion { get; init; }

    public bool IsInstalled { get; init; }

    public bool UpdateAvailable { get; init; }

    public string? ErrorMessage { get; init; }
}
