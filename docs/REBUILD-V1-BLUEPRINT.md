# MediaForge v1 — Rebuild Blueprint

Status: **Design approved for implementation planning; no application logic is defined here.**

## 1. Product principle

MediaForge v1 is not a WPF modernization.

It is a new Windows-first product with one simple promise:

> Turn a media link into an organized local library with almost no friction.

The application should feel closer to a polished first-party Windows product or a premium creative tool than to a traditional download manager.

Design priorities, in order:

1. Clarity
2. Speed of understanding
3. Calm visual hierarchy
4. Safe control over local files
5. Premium motion and feedback
6. Internationalization from day one

## 2. UI stack decision

### Decision

**WinUI 3 + Windows App SDK 2.5.1 Stable + .NET 10 LTS + C#**

This is a deliberate Windows-first decision.

Microsoft currently recommends WinUI 3 with the Windows App SDK for new native Windows desktop applications. Windows App SDK 2.5.1 is the current stable release as of September 16, 2026, and .NET 10 is the active LTS release.

### Why not Electron?

Electron embeds Chromium and Node.js in the application and uses a Chromium-style multi-process model. It is excellent when cross-platform web technology is the primary product requirement, but that is not MediaForge's primary constraint.

MediaForge needs deep Windows integration:

- native file picking and file-system interaction
- Windows drag and drop
- shell integration
- notifications
- modern Windows window chrome
- first-class accessibility and keyboard behavior
- predictable Windows packaging

Electron would trade some of that native alignment for maximum web UI flexibility.

### Why not Tauri?

Tauri is a strong alternative and materially lighter than Electron because it uses the operating system WebView instead of shipping a full browser runtime. The cost for MediaForge is architectural: UI is still a WebView application bridged to a native Rust backend.

That model is attractive for a future cross-platform MediaForge, but v1 is intentionally Windows-first. We should not introduce a web/native boundary unless it solves a product requirement we actually have.

### Why WinUI 3 can still deliver the “crazy” visual layer

WinUI 3 is not a visual compromise.

The Windows UI stack supports:

- Mica system backdrop
- custom integrated title bars
- NavigationView-based shells
- modern Fluent controls
- compositor-driven animations
- high-DPI rendering
- independent composition-thread animation

The premium look will come from our design system, spacing, typography, transitions, visual hierarchy, and custom media surfaces—not from forcing a web framework into a Windows application.

## 3. Application architecture

We use a **modular monolith with vertical feature boundaries**.

The objective is to get the simplicity of one desktop application without recreating a tangled “everything references everything” solution.

### Logical layers

`App`
- WinUI 3 shell
- windows, navigation, visual states
- page/view composition
- resource dictionaries and localization bindings
- accessibility presentation

`Domain`
- media, library, folder, download, source, and operation concepts
- business invariants only
- no UI
- no WinUI types
- no direct file-system or process access

`Application`
- user-facing use cases
- resolve media
- build download plans
- queue and pause/resume orchestration
- apply library changes
- import, organize, rename, move, delete
- update and migration orchestration

`Infrastructure`
- local file system
- SQLite persistence
- yt-dlp integration
- FFmpeg integration
- Windows-specific services
- thumbnails/cache
- updater transport
- logging and diagnostics

### Feature boundaries

The application is organized around product capabilities rather than technical folders alone:

`Home`
- global paste/download composer
- recent activity
- library health summary

`Library`
- library roots
- folders
- media grid/list
- search and filters
- local organization

`Downloads`
- active queue
- completed history
- retry/pause/resume
- per-item diagnostics

`Media Preview`
- metadata preview surface
- format and quality selection
- destination selection
- playlist review

`Settings`
- language
- appearance
- storage
- downloader tools
- updates
- privacy

`System`
- notifications
- tray
- startup behavior
- crash-safe recovery
- update/restart

## 4. Main window visual architecture

### Canvas

Reference design size: **1440 × 900**.

Supported minimum: approximately **1180 × 760** without destroying hierarchy.

The window is a single continuous visual composition.

### Window material

Use **Mica** on the main window.

Important rule: page backgrounds stay visually transparent so the Mica surface remains visible. Opaque surfaces are reserved for cards, popovers, dialogs, and high-contrast content.

Use Acrylic-like translucent surfaces only for transient overlays and elevated panels.

### Title bar

Height target: **52–56 px**.

The title bar visually merges with the shell instead of looking like an operating-system strip glued on top.

Contents:

- MediaForge mark
- current section title where useful
- optional compact status indicator
- Windows caption buttons aligned naturally with the shell

Do not repeat the application name again as a giant page heading.

### Sidebar

Desktop expanded width: **248 px**.

Collapsed width: **72–80 px**.

The sidebar is a calm navigation rail, not a dense menu.

Top:

- MediaForge symbol
- optional “Quick add” action

Primary navigation:

- Home
- Library
- Downloads

Secondary navigation:

- Favorites
- History

Bottom:

- Settings
- Help / About
- profile-independent system state if later required

Visual behavior:

- soft hover fill
- selected state is a low-contrast pill
- icon size around 20–22 px
- label baseline aligned with icon
- no permanent heavy borders
- keyboard focus always visible

### Main content

Horizontal page padding: **40–48 px**.

Vertical page padding: **32–40 px**.

Maximum readable content width: around **1360–1480 px** depending on viewport.

The content area uses a **12-column responsive grid**.

Spacing system:

- 4 px micro spacing
- 8 px base
- 12 px compact
- 16 px standard
- 24 px card
- 32 px section
- 48 px major section
- 64+ px hero breathing room

The interface should deliberately feel under-filled rather than cramped.

## 5. Design language

### Color

Base dark canvas:

- background: near-black blue-gray
- primary text: soft white
- secondary text: cool gray
- tertiary text: subdued gray

Accent:

- one restrained brand accent
- used for primary actions, active states, progress, and selection
- never used to color every surface

Semantic status:

- success: green
- pending: amber
- error: red
- informational: blue

Do not turn the application into a rainbow dashboard.

### Surfaces

Card radius target: **16–20 px**.

Interactive control radius: **10–14 px**.

Large media hero radius: **24 px**.

Borders are subtle and low-contrast. Depth should come from material, elevation, spacing, and blur—not thick outlines.

### Typography

Use the system's **Segoe UI Variable** family and let the operating system handle language-specific fallback.

Target hierarchy:

- hero headline: 48–64 px
- page title: 30–36 px
- section title: 20–24 px
- card title: 16–18 px
- body: 14–16 px
- metadata: 12–14 px

The large headings are intentionally generous. A page should usually have one dominant thought, not five competing headings.

## 6. Media cards

Media cards are information-rich but visually quiet.

### Standard media row

Target:

- height: **88–104 px**
- thumbnail: **64 × 64 px**
- internal horizontal gap: **16 px**
- title and artist left aligned
- format and destination metadata near the right edge
- progress or status beneath title
- contextual actions revealed on hover

### Media grid card

Target:

- width: **220–280 px**
- artwork ratio: **1:1**
- artwork radius: **14–18 px**
- title max: 2 lines
- artist and metadata below
- hover lift: very small
- focus state: obvious and accessible

### Download card

Target:

- min height: **104–124 px**
- artwork or source icon
- title
- artist/source
- format
- speed
- ETA
- progress
- one primary action
- overflow menu for secondary actions

The progress area must communicate state at a glance without becoming a permanent dashboard.

## 7. Home / Command Center

The Home page is the fastest path through the product.

### Top hero

Large headline:

**“What are we downloading?”**

Below it:

A single oversized URL composer.

The composer should visually behave like a premium command bar:

- paste
- type
- drag URL
- focus glow
- subtle loading state

Primary action is integrated, not presented as a row of three competing buttons.

### Below the composer

A compact “Recent” area:

- last downloads
- current queue
- library destination

No charts for the sake of charts.

The user should be able to understand the application in under five seconds.

## 8. Cinematic media preview

The preview window/page is the signature MediaForge experience.

### Visual composition

The source artwork becomes the visual anchor.

Layering:

1. blurred artwork atmosphere in the background
2. dark readability gradient
3. sharp artwork card
4. title and artist
5. duration / source metadata
6. format chips
7. quality selector
8. destination
9. single primary download action

The artwork background should move very subtly when the preview enters. No aggressive parallax.

### Playlist preview

For playlists:

- large cover/collection image
- title
- creator
- item count
- scrollable track rail
- per-track selection
- “apply format to all” as a secondary accelerator

The first screen should not look like a spreadsheet.

## 9. Minimal-click user flow

### First run

1. Open MediaForge
2. Choose the default library folder
3. Arrive directly at Home

One decision. No tutorial carousel.

### Single media

1. Paste URL
2. Preview resolves automatically
3. Choose format/quality if the default is not correct
4. Press **Download**

Target: **2–3 meaningful user actions** after pasting.

### Playlist

1. Paste URL
2. Preview identifies playlist
3. Review/select tracks
4. Set format/quality once
5. Press **Download playlist**

### After download

The completed item animates from Downloads into its actual library location.

The UI shows:

- completion state
- destination
- optional “Open folder”
- undo/recover action where applicable

No success modal is required for routine downloads.

## 10. File organization model

MediaForge treats organization as a plan, not as a sequence of destructive UI actions.

The visual system distinguishes:

- **Synced**
- **Pending**
- **Applying**
- **Failed**

But the product language should be human:

- “Ready”
- “Will move”
- “Applying”
- “Needs attention”

Users should never need to understand internal staging terminology.

### Change center

Instead of forcing every user through a global “Save Changes” step, the application exposes a **Changes** drawer for structural file operations.

Simple downloads may commit directly after the user explicitly presses Download.

Advanced organization changes can be reviewed together and applied as a single plan.

This removes an unnecessary click from the common path while preserving control over destructive file operations.

## 11. Download architecture

The downloader remains a separate application service.

Conceptually:

**URL → Resolver → Preview Model → Download Plan → Queue → Media Processor → Final File → Library Index**

Responsibilities stay separated:

- resolver understands metadata
- queue controls concurrency and lifecycle
- media processor handles yt-dlp/FFmpeg
- file system layer owns local files
- index owns searchable library state
- UI renders state and intent

No page directly owns a process, a file stream, or a downloader implementation.

## 12. State model

Every long-running operation has an explicit state.

Download states:

- Queued
- Resolving
- Downloading
- Paused
- Retrying
- Processing
- Completed
- Failed
- Cancelled

Library operation states:

- Ready
- Pending
- Applying
- Completed
- Failed

The UI is a projection of state. It should never infer state from button text or visual color alone.

## 13. Persistence

Local-first.

Primary structured store:

**SQLite**

Stores:

- libraries
- roots
- folders
- media metadata
- download history
- queued operations
- preferences
- migrations

File data remains where the user asked MediaForge to store it.

Thumbnail/cache data uses a dedicated local cache and can be rebuilt.

Writes are atomic and recoverable.

## 14. Localization foundation — 20 languages

Localization is part of the platform, not a page-level feature.

### Supported v1 languages

1. en-US — English
2. he-IL — Hebrew
3. es-ES — Spanish
4. fr-FR — French
5. de-DE — German
6. it-IT — Italian
7. pt-BR — Portuguese (Brazil)
8. pt-PT — Portuguese (Portugal)
9. nl-NL — Dutch
10. pl-PL — Polish
11. tr-TR — Turkish
12. sv-SE — Swedish
13. da-DK — Danish
14. nb-NO — Norwegian
15. fi-FI — Finnish
16. uk-UA — Ukrainian
17. ja-JP — Japanese
18. ko-KR — Korean
19. zh-CN — Simplified Chinese
20. ar-SA — Arabic

### Resource system

Use native Windows App SDK localization through **MRT Core / .resw** resources.

Principles:

- no hard-coded user-facing strings
- stable resource identifiers
- translator comments for context
- language-specific resources from the beginning
- runtime language switching
- resource fallback to English
- manifest/package language declarations when applicable

### Runtime switching

The language picker changes the resource context and refreshes the visual tree without requiring a reinstall.

The preference is persisted locally.

### RTL

Hebrew and Arabic are first-class RTL experiences.

RTL is not a “flip the whole window” switch.

The system must respect:

- logical start/end alignment
- icon mirroring where semantically required
- numeric formatting
- breadcrumb ordering
- navigation transitions
- mixed RTL/LTR content such as URLs and artist names

### International layout rules

Every screen must survive:

- 30–40% longer strings
- short translated labels
- long person / artist names
- CJK typography
- mixed scripts
- narrow widths
- RTL layout

Localization QA is part of the design acceptance criteria.

## 15. Motion system

The motion language is subtle, physical, and fast.

Targets:

- micro interaction: **120–180 ms**
- normal control transition: **180–240 ms**
- panel/page transition: **240–360 ms**
- major hero transition: up to **450 ms**

Motion rules:

- animate opacity + transform before expensive layout changes
- prefer composition-layer animation
- no infinite decorative animations
- no excessive bouncing
- no animation that blocks input
- respect Windows reduced-motion/accessibility settings

Signature transitions:

- URL composer → preview
- thumbnail → full preview
- download card → completed library card
- sidebar selection → page content
- changes drawer → committed state

## 16. Accessibility

Premium means accessible.

Requirements:

- full keyboard navigation
- visible focus
- high contrast support
- reduced-motion respect
- semantic labels
- screen-reader friendly state changes
- minimum touch/click targets that remain comfortable
- no information conveyed only by color

## 17. Error experience

Errors are not popups by default.

Preferred pattern:

- inline state
- human explanation
- concrete next action
- expandable diagnostic details

Example:

**“We couldn’t download this item.”**

Secondary detail:

**“The source stopped responding.”**

Action:

**Retry**

Advanced:

**View details**

The application should not expose raw stack traces to ordinary users.

## 18. Settings

Settings should be short and calm.

Sections:

### General
- language
- startup
- confirmation behavior

### Appearance
- dark/light/system
- motion
- compactness

### Downloads
- default format
- quality
- naming convention
- concurrency
- destination rules

### Library
- default roots
- organization rules
- scan behavior

### Tools
- yt-dlp status
- FFmpeg status
- update tools

### Updates
- current version
- check for updates
- release notes

### Privacy
- diagnostics
- analytics preference

Analytics are opt-in.

## 19. Technical rules for implementation

These are architectural rules, not implementation tasks.

1. The App layer never owns downloader or file-system internals.
2. Domain code never references WinUI.
3. UI state is derived from application state.
4. Long-running work is cancellable.
5. File operations are atomic where practical.
6. Every destructive operation has a recoverable failure path.
7. Network and process execution happen outside the UI thread.
8. All user-facing strings are localized resources.
9. Tests validate application behavior without requiring network access.
10. Integration tests explicitly isolate the real file system and external binaries.
11. CI has hard timeouts for every external process.
12. Every release build is reproducible from a clean checkout.

## 20. Proposed v1 repository structure

```
MediaForge/
├─ src/
│  ├─ MediaForge.App/
│  │  ├─ Shell/
│  │  ├─ Features/
│  │  │  ├─ Home/
│  │  │  ├─ Library/
│  │  │  ├─ Downloads/
│  │  │  ├─ Preview/
│  │  │  └─ Settings/
│  │  ├─ Design/
│  │  ├─ Strings/
│  │  └─ Assets/
│  │
│  ├─ MediaForge.Domain/
│  ├─ MediaForge.Application/
│  └─ MediaForge.Infrastructure/
│
├─ tests/
│  ├─ MediaForge.Domain.Tests/
│  ├─ MediaForge.Application.Tests/
│  ├─ MediaForge.Infrastructure.Tests/
│  └─ MediaForge.App.Tests/
│
├─ packaging/
├─ build/
├─ docs/
│  ├─ REBUILD-V1-BLUEPRINT.md
│  ├─ DESIGN-SYSTEM.md
│  └─ ARCHITECTURE.md
│
└─ .github/
   └─ workflows/
```

This is a target structure. It is intentionally not implemented yet.

## 21. Implementation gates

Development starts in these gates:

### Gate A — Design system
- typography
- spacing
- colors
- surfaces
- controls
- motion tokens
- RTL rules

### Gate B — Shell
- WinUI 3 app
- title bar
- Mica
- sidebar
- navigation
- theme switching

### Gate C — Home + Preview
- universal URL composer
- cinematic preview
- media cards

### Gate D — Downloads
- queue
- progress
- pause/resume
- failure/retry

### Gate E — Library
- roots
- folder navigation
- media index
- local operations

### Gate F — Internationalization
- all 20 language resource packs
- runtime switching
- RTL
- localization QA

### Gate G — Packaging
- self-contained Windows build
- signed installer
- clean-machine smoke test
- update path

No gate advances because the previous UI “looks okay.” Each gate has explicit acceptance criteria.

## 22. Acceptance vision

The rebuilt MediaForge should feel:

- native on Windows
- quiet when nothing needs attention
- expressive when media is being resolved
- obvious during downloads
- safe around files
- excellent with keyboard and mouse
- correct in RTL
- comfortable in every supported language

The defining interaction is:

**Paste → Preview → Download**

Everything else exists to support that experience without getting in its way.

## References

- Microsoft WinUI 3: https://learn.microsoft.com/windows/apps/winui/winui3/
- Windows App SDK downloads: https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads
- Modern WinUI app structure: https://learn.microsoft.com/windows/apps/develop/ui/windows-app-sdk-app-structure
- WinUI animation guidance: https://learn.microsoft.com/windows/apps/develop/performance/optimize-animations-and-media
- Windows App SDK localization: https://learn.microsoft.com/windows/apps/winui/winui3/localize-winui3-app
- MRT Core: https://learn.microsoft.com/windows/apps/windows-app-sdk/mrtcore/mrtcore-overview
- Tauri architecture: https://v2.tauri.app/concept/architecture/
- Electron documentation: https://www.electronjs.org/docs/latest
