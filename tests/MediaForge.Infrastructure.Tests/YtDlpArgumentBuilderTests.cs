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
    public void Mp3UsesBalanced192KAudioQuality()
    {
        var args = YtDlpArgumentBuilder.Build(@"C:\temp\song.tmp", @"C:\tools\ffmpeg.exe", MediaFormat.Mp3);

        var qualityIndex = Array.IndexOf(args.ToArray(), "--audio-quality");

        Assert.True(qualityIndex >= 0);
        Assert.Equal(YtDlpArgumentBuilder.DefaultMp3AudioQuality, args[qualityIndex + 1]);
    }

    [Theory]
    [InlineData(MediaQuality.Standard128K, "128K")]
    [InlineData(MediaQuality.High192K, "192K")]
    [InlineData(MediaQuality.VeryHigh256K, "256K")]
    [InlineData(MediaQuality.Maximum320K, "320K")]
    public void Mp3MapsQualityToRequestedBitrate(MediaQuality quality, string bitrate)
    {
        var args = YtDlpArgumentBuilder.Build(
            @"C:\temp\song.tmp",
            @"C:\tools\ffmpeg.exe",
            MediaFormat.Mp3,
            quality);

        var qualityIndex = Array.IndexOf(args.ToArray(), "--audio-quality");

        Assert.True(qualityIndex >= 0);
        Assert.Equal(bitrate, args[qualityIndex + 1]);
    }

    [Fact]
    public void Mp4UsesMp4VideoAndMerge()
    {
        var args = YtDlpArgumentBuilder.Build(@"C:\\temp\\video.tmp", @"C:\\tools\\ffmpeg.exe", MediaFormat.Mp4);

        Assert.Contains("-f", args);
        Assert.Contains("bestvideo[height<=720]+bestaudio/best[height<=720]/bestvideo+bestaudio/best", args);
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

    [Theory]
    [InlineData(MediaVideoQuality.DataSaver480p, "height<=480")]
    [InlineData(MediaVideoQuality.Balanced720p, "height<=720")]
    [InlineData(MediaVideoQuality.High1080p, "height<=1080")]
    public void Mp4AppliesVideoHeightCap(MediaVideoQuality quality, string heightFilter)
    {
        var args = YtDlpArgumentBuilder.Build(
            @"C:\temp\video.tmp",
            @"C:\tools\ffmpeg.exe",
            MediaFormat.Mp4,
            MediaQuality.High192K,
            quality);

        Assert.Contains(heightFilter, args.Single(x => x.Contains("bestvideo")));
    }

    [Fact]
    public void Mp4BestAvailableDoesNotApplyHeightCap()
    {
        var args = YtDlpArgumentBuilder.Build(
            @"C:\temp\video.tmp",
            @"C:\tools\ffmpeg.exe",
            MediaFormat.Mp4,
            MediaQuality.High192K,
            MediaVideoQuality.BestAvailable);

        Assert.Contains("bestvideo+bestaudio/best", args);
    }
