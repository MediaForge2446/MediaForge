# Windows Validation root cause

The Windows validation failure was caused by two source-level issues, not by the runner path or warning policy:

- `MediaForge.Core.Tests` targeted `net10.0` and referenced only `MediaForge.Core`, while its tests use `MediaForge.Services`, `MediaForge.State`, and `MediaForge.ViewModels` from the Windows application project.
- `MainWindow.xaml` declared `Loaded="OnLoaded"`, but WinUI 3 `Window` does not expose that XAML event.

The validation pipeline uses `windows-latest`, restores the solution, builds the full solution, publishes self-contained x64, builds the installer, runs install/uninstall smoke tests, and uploads `Setup.exe`.
