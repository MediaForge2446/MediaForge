namespace MediaForge.Core.Models;

public sealed class RootFolder
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Path { get; init; }
    public required string Name { get; set; }
    public Folder Root { get; init; } = new() { Name = string.Empty };
}
