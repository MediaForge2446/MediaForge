# MediaForge

MediaForge is a Windows desktop media manager and downloader.

## Architecture

The solution is built in layers. The domain core is intentionally independent of WPF, the filesystem, YouTube clients, yt-dlp, and FFmpeg.

Current stage: **Core Architecture & Domain Models**.

- `src/MediaForge.Core/Models` — domain model
- `src/MediaForge.Core/Enums` — state and operation enums
- `src/MediaForge.Core/State` — staging history
- `src/MediaForge.Core/Interfaces` — infrastructure/application boundaries
- `src/MediaForge.Core/Results` — operation results

Target framework: .NET 8.
