# DownTrack Architecture

## Boundaries

UI -> Application -> Core
Infrastructure implements Application contracts.

UI never talks directly to SQLite, yt-dlp, FFmpeg, or the file system.

## State

DownTrack models the difference between actual state, desired state, and the operations needed to reconcile them.

A staged change is representable without mutating the user's files.

## Commit

Validate -> Plan -> Prepare -> Execute -> Verify -> Finalize -> Persist

External processes are cancellable and bounded.

Downloads land in transaction-scoped temporary storage before finalization.

## UI

One large content surface. Navigation is contextual. Panels are transient. There is no permanent navigation sidebar.

## Distribution

The existing 0.2.0 artifact remains immutable. New releases are versioned separately.
