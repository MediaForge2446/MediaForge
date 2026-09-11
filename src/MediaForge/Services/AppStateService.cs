using System.Text.Json;
using MediaForge.ViewModels;

namespace MediaForge.Services;

public sealed class AppStateService
{
    private sealed record Snapshot(List<LibraryFolder> Folders, List<PendingChange> Pending);
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediaForge", "state.json");
    public (IReadOnlyList<LibraryFolder> Folders, IReadOnlyList<PendingChange> Pending) Load()
    {
        if (!File.Exists(_path)) return (Array.Empty<LibraryFolder>(), Array.Empty<PendingChange>());
        try { var data = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(_path)); return data is null ? (Array.Empty<LibraryFolder>(), Array.Empty<PendingChange>()) : (data.Folders, data.Pending); }
        catch { return (Array.Empty<LibraryFolder>(), Array.Empty<PendingChange>()); }
    }
    public void Save(IEnumerable<LibraryFolder> folders, IEnumerable<PendingChange> pending)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new Snapshot(folders.ToList(), pending.ToList()), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, true);
    }
}
