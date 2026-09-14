namespace MediaForge.Core.Models;

public sealed record ExplorerEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTimeOffset LastModifiedUtc);
