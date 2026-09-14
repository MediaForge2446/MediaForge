namespace MediaForge.Core.Models;

public sealed class Folder
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public List<Folder> Children { get; init; } = [];
    public List<MediaItem> Media { get; init; } = [];
}
