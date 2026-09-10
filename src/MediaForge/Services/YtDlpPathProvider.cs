using System.Runtime.InteropServices;

namespace MediaForge.Services;

public sealed class YtDlpPathProvider
{
    public string ToolsDirectory { get; }

    public string YoutubeDLPath => Path.Combine(
        ToolsDirectory,
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe");

    public string FFmpegPath => Path.Combine(ToolsDirectory, "ffmpeg.exe");

    public YtDlpPathProvider(string? toolsDirectory = null)
    {
        ToolsDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(toolsDirectory)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MediaForge",
                    "Tools")
                : toolsDirectory);
    }

    public void EnsureReady()
    {
        if (!File.Exists(YoutubeDLPath))
        {
            throw new FileNotFoundException(
                "The required yt-dlp executable was not found.",
                YoutubeDLPath);
        }

        if (!File.Exists(FFmpegPath))
        {
            throw new FileNotFoundException(
                "The required FFmpeg executable was not found.",
                FFmpegPath);
        }
    }
}
