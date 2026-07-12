# Showcase Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the runnable SharpVision showcase a colored documentation
dashboard with proven terminal mouse navigation.

**Architecture:** `Program` supplies an explicit, app-level cell-mouse
capability override to the existing terminal session. `Gallery` replaces the
unstyled list with a framed navigation surface made of public controls, and
`Page` adds colored header, example, and property-card chrome. The running
application test proves bytes, decoded input, selection, and screen output
together.

**Tech Stack:** .NET 10, C# 14, SharpVision controls and styling, xUnit v3,
Shouldly, `tmux`, and the existing terminal capture script.

---

## Tasks

### Task 1: Specify the observable dashboard contract

**Files:**

- Create: `docs/superpowers/specs/2026-07-12-showcase-dashboard-design.md`
- Create: `docs/superpowers/plans/2026-07-12-showcase-dashboard.md`
- Modify: `docs/architecture/showcase.md`
- Modify: `docs/testing/showcase.md`

- [ ] **Step 1: Record the explicit application-level mouse override.**

State that only the showcase supplies `Settings { CellMouse = true }`, while the
terminal library retains its conservative environment-hint policy.

- [ ] **Step 2: Record the dashboard's public-control composition.**

Describe the fixed sidebar `Border`, stateful `Pressable` navigation entries,
and the independently scrolling documentation pane.

- [ ] **Step 3: Record the test and capture evidence.**

Require exact mode bytes, raw SGR click routing, colored virtual-screen cells,
tiny-size containment, and the live `tmux` capture.

### Task 2: Prove the current startup and screen deficiencies

**Files:**

- Modify: `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`

- [ ] **Step 1: Write a failing startup-mode test.**

Start `Program`'s extracted startup-options factory with a fake terminal and
assert its first mode output contains the typed SGR enable command:

```csharp
terminal.Writes.SelectMany(static bytes => bytes)
    .ShouldContain((byte) '\u001b');
```

Assert the complete written sequence contains `"[?1000h\u001b[?1006h"` before
the first frame.

- [ ] **Step 2: Write a failing raw-click navigation test.**

Build raw SGR press/release bytes from the arranged Button entry's bounds and
assert the selected page changes through its typed activation event.

- [ ] **Step 3: Write failing dashboard render assertions.**

Render at 80 by 24 cells and assert `SHARP VISION`, `Components`, the selected
page marker, and a non-default foreground/background cell are present. Render at
30 by 8 and 140 by 40 to retain the existing containment proof.

- [ ] **Step 4: Run the focused showcase test project and confirm the new tests
      fail for missing startup configuration and dashboard chrome.**

Run: `dotnet test --project tests/SharpVision.Showcase.Tests --timeout 60s`

Expected: the newly added assertions fail; existing tests remain discoverable.

### Task 3: Configure mouse support for the executable showcase

**Files:**

- Create: `src/SharpVision.Showcase/StartupOptions.cs`
- Modify: `src/SharpVision.Showcase/Program.cs`

- [ ] **Step 1: Add a testable startup-options factory.**

Create
`StartupOptions.Create(IReadOnlyDictionary<string, string?> environment)`. It
calls
`Detector.Detect(environment, overrides: new Settings { CellMouse = true })` and
returns `Runtime.Options` with that capability profile,
`Tracking = MouseTracking.Press`, and `Coordinates = MouseCoordinates.Sgr`.

- [ ] **Step 2: Pass the factory output to `Application`.**

Copy process environment values into a `Dictionary<string, string?>`, call the
factory once, and pass the resulting options as the final constructor argument.

- [ ] **Step 3: Run the focused startup/click tests and confirm they pass.**

Run:

```bash
dotnet test --project tests/SharpVision.Showcase.Tests \
  --filter-class "*GalleryInteractionTests" --timeout 60s
```

Expected: exact mode bytes and raw pointer navigation pass.

### Task 4: Build the colored navigation and documentation surfaces

**Files:**

- Create: `src/SharpVision.Showcase/Palette.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Modify: `src/SharpVision.Showcase/Page.cs`
- Modify: `src/SharpVision.Showcase/Examples.cs`

- [ ] **Step 1: Define one internal palette file.**

Expose semantic indexed colors for canvas, panel, accent, highlight, text, muted
text, success, warning, and card borders. Use no raw color literals in gallery
or page composition outside this file.

- [ ] **Step 2: Replace the `List` sidebar with composed public controls.**

Create a 28-cell `Border` panel whose child is a `Dock`: an identity/header, a
scrollable button stack, and keyboard/pointer help. Each button's click handler
calls one index-validated gallery selection method. Replace style resources for
selected and unselected entries after selection changes.

- [ ] **Step 3: Style page chrome without changing page content semantics.**

Use colored `RichText` runs for title, metadata, and section headings. Wrap live
examples and properties in bordered card surfaces with readable spacing; keep
every named type in its own file and use only public controls.

- [ ] **Step 4: Run dashboard rendering and interaction tests.**

Run: `dotnet test --project tests/SharpVision.Showcase.Tests --timeout 60s`

Expected: all showcase tests pass with sidebar identity, colors, click routing,
scrolling, and resize coverage.

### Task 5: Align specifications and produce release evidence

**Files:**

- Modify: `docs/architecture/showcase.md`
- Modify: `docs/testing/showcase.md`
- Modify: `docs/images/showcase-dashboard.png`

- [ ] **Step 1: Update the architecture and testing contracts.**

Replace the old list-sidebar description with the dashboard layout and explain
that the executable app explicitly authorizes cell mouse reporting.

- [ ] **Step 2: Regenerate and inspect the live capture.**

Run: `scripts/capture-showcase.sh`

Expected: a valid 1280 by 900 PNG shows the framed, colored sidebar and the page
chrome from the Release application.

- [ ] **Step 3: Run repository quality gates.**

Run:

```bash
make format
make lint
make build
make test
```

Expected: zero format/lint errors, zero build warnings/errors, and all tests
passing.
