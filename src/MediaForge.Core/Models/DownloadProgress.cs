namespace MediaForge.Core.Models;

public sealed record DownloadProgress(
    double Percent,
    string? Status = null,
    double? SpeedBytesPerSecond = null,
    TimeSpan? Eta = null);
