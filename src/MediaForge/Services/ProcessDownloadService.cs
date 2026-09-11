using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using MediaForge.ViewModels;

namespace MediaForge.Services;

public sealed class ProcessDownloadService
{
    private static readonly Regex ProgressPattern = new(@"(?<p>\d+(?:\.\d+)?)%", RegexOptions.Compiled|RegexOptions.CultureInvariant);
    private static string ToolsDirectory=>Path.Combine(AppContext.BaseDirectory,"Tools");
    public async Task DownloadAsync(string url,string target,MediaFormat format,Action<double> progress,CancellationToken token)
    {
        var exe=Path.Combine(ToolsDirectory,"yt-dlp.exe");
        if(!File.Exists(exe))throw new FileNotFoundException("yt-dlp.exe is missing from the application installation.",exe);
        var ffmpeg=Path.Combine(ToolsDirectory,"ffmpeg.exe");
        var ffprobe=Path.Combine(ToolsDirectory,"ffprobe.exe");
        if(!File.Exists(ffmpeg))throw new FileNotFoundException("FFmpeg is missing from the application installation.",ffmpeg);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var stem=Path.Combine(Path.GetDirectoryName(target)!,Path.GetFileNameWithoutExtension(target)+".%(ext)s");
        var ffArgs=File.Exists(ffprobe)?$" --ffmpeg-location \"{ToolsDirectory}\"":"";
        var args=format==MediaFormat.Mp3?$"--newline --no-playlist --extract-audio --audio-format mp3{ffArgs} -o \"{stem}\" \"{url}\"":$"--newline --no-playlist --merge-output-format mp4{ffArgs} -o \"{stem}\" \"{url}\"";
        using var process=new Process{StartInfo=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        process.Start();
        var stdoutTask=ReadOutputAsync(process.StandardOutput,progress,token);
        var stderrTask=process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        await stdoutTask;
        var stderr=await stderrTask;
        if(process.ExitCode!=0)throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr)?$"yt-dlp exited with code {process.ExitCode}.":stderr.Trim());
        var file=Directory.EnumerateFiles(Path.GetDirectoryName(target)!,Path.GetFileNameWithoutExtension(target)+".*").Where(x=>!x.EndsWith(".part",StringComparison.OrdinalIgnoreCase)&&!x.EndsWith(".ytdl",StringComparison.OrdinalIgnoreCase)).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if(file is null)throw new FileNotFoundException("yt-dlp completed but no output file was found.",target);
        File.Move(file,target,true);progress(1);
    }
    private static async Task ReadOutputAsync(StreamReader reader,Action<double> progress,CancellationToken token){while(!reader.EndOfStream){token.ThrowIfCancellationRequested();var line=await reader.ReadLineAsync(token);if(line is null)continue;var match=ProgressPattern.Match(line);if(match.Success&&double.TryParse(match.Groups["p"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var p))progress(Math.Clamp(p/100d,0,1));}}
}
