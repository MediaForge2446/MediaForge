# MediaForge

MediaForge is a Windows media manager/downloader built around a staged desired-state workflow: users organize folders and media first, review pending changes, then apply the complete set with **Save Changes**.

## Product experience

The primary flow is intentionally simple:

1. Open MediaForge.
2. Choose or add a root folder.
3. Work inside a clean, light Explorer-style workspace.
4. Add folders, rename/delete items, or use **Add Media** to open the floating media dialog.
5. Review yellow pending changes.
6. Click **Save Changes** once to apply them.

The app includes local folder scanning, persistent staging, undo, duplicate protection, background downloads, search and sorting, and a quick action to open the current location in Windows Explorer.

## Architecture

```
WPF UI
  -> ViewModels
     -> Application
        -> Core contracts/models
           -> Infrastructure
              -> filesystem / persistence / yt-dlp / FFmpeg
```

Long-running work is asynchronous, staging is persisted locally, and the UI does not write desired-state changes to disk until the user commits them.

## Distribution

**Primary distribution target: WinGet Community Repository.**

The CI produces a self-contained Windows x64 build and a single Inno Setup installer, `Setup.exe`. The installer is published to the GitHub Release for the corresponding version and is suitable for a WinGet `exe` installer manifest.

The release pipeline intentionally does not build, sign, or validate MSIX packages. This removes the Microsoft Store/MSIX signing dependency from the WinGet release path.

## Current implementation

- .NET 8 WPF application
- MVVM with CommunityToolkit.Mvvm
- Core domain model: Library -> RootFolder -> Folder -> MediaItem
- Persistent desired-state staging and undo
- Background commit/download engine with bounded concurrency
- Atomic JSON persistence and local media index
- yt-dlp + FFmpeg integration
- MP3 default: 128 kbps stereo
- Explorer search, sorting, refresh and Windows Explorer shortcut
- Light Windows-style UI with rounded surfaces and MediaForge branding
