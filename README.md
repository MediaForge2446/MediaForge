# MediaForge

MediaForge is a Windows desktop media manager/downloader designed around a staged desired-state model: users organize folders and media first, then apply the complete set of pending changes with **Save Changes**.

## Architecture

```text
WPF Views
   -> ViewModels
      -> Application
         -> Core abstractions
            -> Infrastructure (filesystem, persistence, media tools)
```

The Core project contains domain models, state definitions, and contracts only. The Application layer owns staging, undo, commit orchestration, and bounded download concurrency. Infrastructure provides concrete persistence and Windows filesystem implementations.

## Current implementation stages

- .NET 8 solution foundation
- Core domain model: Library -> RootFolder -> Folder -> MediaItem
- Desired/actual media states and staged operations
- Persistent staging contracts and undo history
- Application staging service
- General Commit Engine for filesystem operations and downloads
- Bounded background download queue with cancellation and per-item progress
- Atomic JSON persistence with backup recovery
- Windows filesystem abstraction
- Unit tests for staging, commit orchestration, and persistence

The architecture follows the technical specification: long-running work is asynchronous, UI concerns stay outside Core/Application, and pending changes remain explicit until committed.
