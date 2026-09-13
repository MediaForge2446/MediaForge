namespace MediaForge.Core.Models;

public sealed record MediaSource(string Url, string? Provider = null, string? VideoId = null);
