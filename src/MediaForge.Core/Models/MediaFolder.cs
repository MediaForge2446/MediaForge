namespace MediaForge.Core.Models;

public sealed record MediaFolder
{
    public required string Id { get; init; }

    public required string Path { get; init; }

    public required string DisplayName { get; init; }

    public DateTimeOffset AddedAtUtc { get; init; }
}
