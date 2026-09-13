namespace MediaForge.Core.Models;

public sealed record DownloadProgress(double Percent, string? Status = null);
