namespace DownTrack.Core.Models;

public enum LibraryNodeKind
{
    Folder,
    Media
}

public enum LibraryNodeState
{
    Synced,
    Pending,
    Failed,
    Missing
}

public sealed record LibraryNode(
    Guid Id,
    Guid? ParentId,
    string Name,
    string RelativePath,
    LibraryNodeKind Kind,
    LibraryNodeState State = LibraryNodeState.Synced)
{
    public static LibraryNode CreateFolder(
        Guid? parentId,
        string name,
        string relativePath)
    {
        ValidateName(name);

        return new LibraryNode(
            Guid.NewGuid(),
            parentId,
            name.Trim(),
            NormalizeRelativePath(relativePath),
            LibraryNodeKind.Folder,
            LibraryNodeState.Pending);
    }

    public static LibraryNode CreateMedia(
        Guid? parentId,
        string name,
        string relativePath)
    {
        ValidateName(name);

        return new LibraryNode(
            Guid.NewGuid(),
            parentId,
            name.Trim(),
            NormalizeRelativePath(relativePath),
            LibraryNodeKind.Media,
            LibraryNodeState.Pending);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A node name is required.", nameof(name));
        }

        if (name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("A node name cannot contain path separators.", nameof(name));
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A relative path is required.", nameof(path));
        }

        return path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);
    }
}
