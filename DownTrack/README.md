# DownTrack

A new Windows-native media workspace built from a clean architecture.

This directory is intentionally independent from the legacy MediaForge implementation.

Principles:
- Windows 11 first
- WinUI 3 + Windows App SDK 2.5.1
- .NET 10 LTS
- no permanent sidebar
- local-first core
- desired-state staging
- deterministic and cancellable operations
- external tools isolated behind interfaces
- CI has hard timeouts
- Inno Setup remains the release mechanism for future builds

The existing MediaForge 0.2.0 distribution is treated as immutable.
