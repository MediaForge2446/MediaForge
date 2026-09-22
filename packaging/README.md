# Windows installer packaging

MediaForge uses Inno Setup to create a standard Windows `Setup.exe` installer from the self-contained x64 publish output.

## Build flow

The release workflow:

1. Publishes the WPF app self-contained for `win-x64`.
2. Bundles and verifies yt-dlp and FFmpeg with SHA-256 checks.
3. Installs a pinned Inno Setup 7 compiler on the Windows runner.
4. Compiles `installer/MediaForge.iss` into `artifacts/Setup.exe`.
5. Runs a silent install/uninstall smoke test.
6. Publishes `Setup.exe` to the matching GitHub Release.

The resulting EXE is a normal Windows installer; no MSIX certificate or SignTool step is involved in this release path.

## Local build

From a Windows development machine with Inno Setup installed:

```powershell
dotnet publish src/MediaForge.App/MediaForge.App.csproj --configuration Release --runtime win-x64 --self-contained true --output publish

& "C:\Program Files\Inno Setup 7\ISCC.exe" `
  "/DMyAppVersion=0.2.0" `
  "/O$PWD\artifacts" `
  "$PWD\installer\MediaForge.iss"
```

The installer is written to `artifacts/Setup.exe`.
