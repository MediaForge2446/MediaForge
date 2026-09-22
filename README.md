# MediaForge

MediaForge is being rebuilt from the ground up as a premium, Windows-first media library and downloader.

## v1 reset status

The legacy application implementation has been intentionally removed from the rebuild branch. This is a design-first reset: no legacy WPF UI, no legacy application projects, no legacy test suite, and no legacy installer are being carried forward.

The new product direction is:

- **WinUI 3 + Windows App SDK**
- **.NET 10 LTS**
- Native Windows 11 visual language with Mica and composition-based motion
- Feature-oriented application architecture with strict UI/domain boundaries
- 20 first-class display languages, including RTL support for Hebrew and Arabic
- Media-first workflow: paste link → cinematic preview → choose format → download
- Local-first storage with explicit user control over files and library structure

Implementation starts only after the v1 blueprint and visual system are agreed.

See [docs/REBUILD-V1-BLUEPRINT.md](docs/REBUILD-V1-BLUEPRINT.md).
