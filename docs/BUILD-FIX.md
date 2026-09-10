# Build fix

The Core project generates XML documentation while warnings are treated as errors globally. CS1591 is excluded from the Core project because its public API is intentionally documented incrementally without breaking the build pipeline.

The Windows validation workflow also builds and smoke-tests the self-contained x64 installer and publishes `MediaForge-Setup-x64compatible.exe` as a workflow artifact.
