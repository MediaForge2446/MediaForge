namespace MediaForge.Core.Models;

public sealed record AppSettings
{
    public bool AutomaticToolUpdates { get; init; } = true;

    public string? DownloadDirectory { get; init; }

    public bool StartInLibrary { get; init; } = true;
}
