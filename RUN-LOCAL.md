# Run MediaForge locally

## Requirements

- Windows 11
- .NET 10 SDK
- Visual Studio 2026 with Windows App SDK / WinUI development support

## Bootstrap

Open PowerShell in the repository root and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\bootstrap.ps1
```

The script restores dependencies and builds the x64 Debug application.

## Launch

After a successful build, run:

```powershell
.\src\MediaForge\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\MediaForge.exe
```

Or open `MediaForge.sln` in Visual Studio and start the `MediaForge` project.

The application downloads `yt-dlp` and `FFmpeg` into its per-user tools directory when Tool Manager maintenance is enabled.
