namespace MediaForge.Infrastructure.Persistence;

public sealed class LocalAppPaths
{
    public string AppDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MediaForge");

    public string LibraryFilePath => Path.Combine(AppDirectory, "library.json");
    public string StagingFilePath => Path.Combine(AppDirectory, "staging.json");
}
