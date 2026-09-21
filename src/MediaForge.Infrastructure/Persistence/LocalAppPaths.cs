namespace MediaForge.Infrastructure.Persistence;

public sealed class LocalAppPaths
{
    public string AppDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MediaForge");

    public string LibraryFilePath => Path.Combine(AppDirectory, "library.json");
    public string StagingFilePath => Path.Combine(AppDirectory, "staging.json");
    public string MediaIndexFilePath => Path.Combine(AppDirectory, "media-index.json");
    public string TermsAcceptanceFilePath => Path.Combine(AppDirectory, "terms-acceptance.json");
    public string ToolsDirectory => Path.Combine(AppDirectory, "tools");
    public string BundledToolsDirectory => Path.Combine(AppContext.BaseDirectory, "tools");
}
