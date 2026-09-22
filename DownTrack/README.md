# DownTrack

A fresh Windows-native media workspace created independently from the legacy MediaForge code.

Technology:
- WinUI 3
- Windows App SDK 2.5.1
- .NET 10 LTS
- x64
- CommunityToolkit.Mvvm
- Microsoft.Extensions.Hosting
- Microsoft.Data.Sqlite
- CliWrap
- Inno Setup

Non-negotiables:
- no permanent sidebar
- no legacy WPF code reuse
- local-first architecture
- staged desired-state operations
- deterministic tests
- bounded external processes
- self-contained x64 release

The legacy MediaForge 0.2.0 artifact remains outside this workspace and is not modified by this branch.
