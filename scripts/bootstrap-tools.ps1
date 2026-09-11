$ErrorActionPreference='Stop'
$tools=Join-Path $env:LOCALAPPDATA 'MediaForge\Tools'
New-Item -ItemType Directory -Force -Path $tools | Out-Null
$yt=Join-Path $tools 'yt-dlp.exe'
Invoke-WebRequest 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile $yt
$zip=Join-Path $env:TEMP 'ffmpeg-mediaforge.zip'
Invoke-WebRequest 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $zip
$extract=Join-Path $env:TEMP 'ffmpeg-mediaforge'
if(Test-Path $extract){Remove-Item $extract -Recurse -Force}
Expand-Archive $zip $extract -Force
$bin=Get-ChildItem $extract -Directory -Recurse | Where-Object { Test-Path (Join-Path $_.FullName 'bin\ffmpeg.exe') } | Select-Object -First 1
Copy-Item (Join-Path $bin.FullName 'bin\ffmpeg.exe') (Join-Path $tools 'ffmpeg.exe') -Force
Copy-Item (Join-Path $bin.FullName 'bin\ffprobe.exe') (Join-Path $tools 'ffprobe.exe') -Force
Remove-Item $zip,$extract -Recurse -Force
Write-Host "MediaForge tools installed in $tools"
