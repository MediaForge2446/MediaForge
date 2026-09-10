# MediaForge Windows Release

## Local build

Install the .NET 10 SDK and Inno Setup 7.1.0, then run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build-installer.ps1 -RuntimeIdentifier win-x64 -Version 1.0.0
```

The installer is created under `artifacts/installer/`.

For Windows on ARM64:

```powershell
.\scripts\build-installer.ps1 -RuntimeIdentifier win-arm64 -Version 1.0.0
```

## CI release

Push a version tag such as `v1.0.0`. The Windows release workflow builds and tests the solution, publishes self-contained x64 and ARM64 binaries, runs the smoke test, builds both installers, and attaches them to the GitHub release.

## Runtime tools

`yt-dlp` and `FFmpeg` are managed by the application after installation and are not committed to the repository. Their release payloads are downloaded and checksum-verified by the application's Tool Manager.
