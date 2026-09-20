using MediaForge.Core.Enums;

namespace MediaForge.Infrastructure.Media;

/// <summary>Builds deterministic yt-dlp arguments so format behavior can be tested without network access.</summary>
public static class YtDlpArgumentBuilder
{
    // Balanced default for everyday listening: good enough quality while keeping MP3 files compact.
    public const string DefaultMp3AudioQuality = "128K";

    public static IReadOnlyList<string> Build(string outputPath, string ffmpegPath, MediaFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);

        var arguments = new List<string>
        {
            "--newline",
            "--no-playlist",
            "--no-warnings",
            "--ffmpeg-location", ffmpegPath
        };

        switch (format)
        {
            case MediaFormat.Mp3:
            case MediaFormat.Wav:
            case MediaFormat.M4a:
                arguments.Add("-x");
                arguments.Add("--audio-format");
                arguments.Add(format switch
                {
                    MediaFormat.Mp3 => "mp3",
                    MediaFormat.Wav => "wav",
                    _ => "m4a"
                });
                arguments.Add("--audio-quality");
                arguments.Add(format == MediaFormat.Mp3 ? DefaultMp3AudioQuality : "0");
                if (format is MediaFormat.Mp3 or MediaFormat.M4a)
                {
                    arguments.Add("--embed-thumbnail");
                    arguments.Add("--add-metadata");
                }
                break;

            case MediaFormat.Mp4:
                arguments.Add("-f");
                arguments.Add("bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]");
                arguments.Add("--merge-output-format");
                arguments.Add("mp4");
                arguments.Add("--add-metadata");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }

        arguments.Add("-o");
        arguments.Add(outputPath);
        return arguments;
    }
}
