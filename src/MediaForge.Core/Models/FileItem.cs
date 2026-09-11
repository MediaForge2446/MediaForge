using MediaForge.Core.Enums;

namespace MediaForge.Core.Models;

public sealed record FileItem
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public required FileItemKind Kind { get; init; }

    public long? SizeBytes { get; init; }

    public DateTimeOffset LastModifiedUtc { get; init; }

    public ChangeStatus Status { get; init; } = ChangeStatus.Synced;

    public string Glyph => Kind == FileItemKind.Folder ? "\uE8B7" : "\uE8A5";
}
