namespace MediaForge.Services;

public sealed class YtDlpPathProvider
{
    public string ToolsDirectory { get; }

    public string YoutubeDLPath => Path.Combine(ToolsDirectory, "yt-dlp.exe");

    public string FFmpegPath => Path.Combine(ToolsDirectory, "ffmpeg.exe");

    public YtDlpPathProvider(string? toolsDirectory = null)
    {
        ToolsDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(toolsDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "Tools")
                : toolsDirectory);
    }

    public void EnsureReady()
    {
        if (!File.Exists(YoutubeDLPath))
        {
            throw new FileNotFoundException(
                "yt-dlp.exe was not found. Place the executable in the application's Tools directory.",
                YoutubeDLPath);
        }

        if (!File.Exists(FFmpegPath))
        {
            throw new FileNotFoundException(
                "ffmpeg.exe was not found. Place the executable in the application's Tools directory.",
                FFmpegPath);
        }
    }
}
