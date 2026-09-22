using DownTrack.Core.Models;

namespace DownTrack.Core.Tests;

public sealed class LibraryRootTests
{
    [Fact]
    public void Create_normalizes_path_and_uses_folder_name()
    {
        var path = Path.Combine(Path.GetTempPath(), "DownTrack-Test");

        var root = LibraryRoot.Create(path);

        Assert.Equal(Path.GetFullPath(path), root.Path);
        Assert.Equal("DownTrack-Test", root.DisplayName);
        Assert.NotEqual(Guid.Empty, root.Id);
    }
}
