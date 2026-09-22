namespace DownTrack.Core.Models;

public sealed record LibraryRoot(
    Guid Id,
    string Path,
    string DisplayName)
{
    public static LibraryRoot Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A library path is required.", nameof(path));
        }

        var normalized = System.IO.Path.TrimEndingDirectorySeparator(
            System.IO.Path.GetFullPath(path));

        return new LibraryRoot(
            Guid.NewGuid(),
            normalized,
            new DirectoryInfo(normalized).Name);
    }
}
