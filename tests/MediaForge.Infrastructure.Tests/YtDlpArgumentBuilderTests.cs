using MediaForge.Core.Enums;
using MediaForge.Infrastructure.Media;

namespace MediaForge.Infrastructure.Tests;

public sealed class YtDlpArgumentBuilderTests
{
    [Theory]
    [InlineData(MediaFormat.Mp3, "mp3", true)]
    [InlineData(MediaFormat.Wav, "wav", false)]
    [InlineData(MediaFormat.M4a, "m4a", true)]
    public void AudioFormatsUseExtractionAndExpectedCodec(MediaFormat format, string codec, bool embedsMetadata)
    {
        var args = YtDlpArgumentBuilder.Build(@"C:\\temp\\song.tmp", @"C:\\tools\\ffmpeg.exe", format);

        Assert.Contains("-x", args);
        Assert.Contains("--audio-format", args);
        Assert.Contains(codec, args);
        Assert.Contains("--audio-quality", args);
        Assert.Equal(embedsMetadata, args.Contains("--add-metadata") && args.Contains("--embed-thumbnail"));
    }

    [Fact]
    public void Mp3UsesCompact128KAudioQuality()
    {
        var args = YtDlpArgumentBuilder.Build(@"C:\temp\song.tmp", @"C:\tools\ffmpeg.exe", MediaFormat.Mp3);

        var qualityIndex = Array.IndexOf(args.ToArray(), "--audio-quality");

        Assert.True(qualityIndex >= 0);
        Assert.Equal(YtDlpArgumentBuilder.DefaultMp3AudioQuality, args[qualityIndex + 1]);
    }

    [Fact]
    public void Mp4UsesMp4VideoAndMerge()
    {
        var args = YtDlpArgumentBuilder.Build(@"C:\\temp\\video.tmp", @"C:\\tools\\ffmpeg.exe", MediaFormat.Mp4);

        Assert.Contains("-f", args);
        Assert.Contains("bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]", args);
        Assert.Contains("--merge-output-format", args);
        Assert.Contains("mp4", args);
        Assert.DoesNotContain("-x", args);
    }

    [Fact]
    public void CommonSafetyArgumentsAreAlwaysPresent()
    {
        var args = YtDlpArgumentBuilder.Build(@"C:\\temp\\output.tmp", @"C:\\tools\\ffmpeg.exe", MediaFormat.Mp3);

        Assert.Equal("--newline", args[0]);
        Assert.Contains("--no-playlist", args);
        Assert.Contains("--no-warnings", args);
        Assert.Contains("--ffmpeg-location", args);
        Assert.Contains(@"C:\\tools\\ffmpeg.exe", args);
        Assert.Contains("-o", args);
        Assert.Contains(@"C:\\temp\\output.tmp", args);
    }
}
