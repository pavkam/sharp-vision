# Showcase Follow-up Interaction Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox syntax for tracking.

**Goal:** Make the showcase fill its workspace, preserve layered backgrounds,
teach Popup and Window through triggered stages, and provide word selection and
clipboard shortcuts in hosted text inputs.

**Architecture:** Keep layout and example composition in SharpVision.Showcase,
correct passive text opacity in Text, synthesize multi-click gestures once in
CaptureManager, and let Application own the per-run clipboard buffer. Every
behavior starts with a focused failing test.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, SharpVision routed
input/layout/rendering, Markdown specifications.

---

## Task 1: Gallery fill and transparent Text

**Files:**

- Modify: `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TextTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ListTests.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Modify: `src/SharpVision/Controls/Text.cs`
- Modify: `docs/controls/display/text.md`

- [ ] Write tests asserting the selected page/header/body fill the region after
      the sidebar and themed Text preserves a prepared Surface at glyph cells.
      Extend List hover coverage to a glyph cell and trailing row cell.
- [ ] Run the focused tests and verify they fail for intrinsic Stack width and
      opaque Text.

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*TextTests|*ListTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --filter-class "*GalleryRenderingTests" --timeout 60s
```

- [ ] Replace the main Stack with a Dock and make unmarked Text opaque only when
      FillMode is Opaque.

```csharp
private BackgroundMode ResolveBackgroundMode(StyleSpan span) =>
    span.Background.HasValue || FillMode == FillMode.Opaque
        ? BackgroundMode.Opaque
        : BackgroundMode.Transparent;
```

- [ ] Update the Text contract, rerun both tests for GREEN, and commit.

```bash
git add src/SharpVision/Controls/Text.cs src/SharpVision.Showcase/Gallery.cs tests/SharpVision.Tests/Controls/TextTests.cs tests/SharpVision.Tests/Controls/ListTests.cs tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs docs/controls/display/text.md
git commit -m "fix(rendering): preserve parent surfaces beneath text"
```

## Task 2: Multi-click and word selection

**Files:**

- Create: `tests/SharpVision.Tests/Input/ManualTimeProvider.cs`
- Modify: `tests/SharpVision.Tests/Input/PointerTests.cs`
- Modify: `tests/SharpVision.Tests/Text/EditTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TextInputTests.cs`
- Modify: `src/SharpVision/Input/PointerEventArgs.cs`
- Modify: `src/SharpVision/Input/CaptureManager.cs`
- Modify: `src/SharpVision/Text/Edit.cs`
- Modify: `src/SharpVision/Controls/TextInput.cs`
- Modify: `docs/concepts/input-routing.md`
- Modify: `docs/controls/input/text-input.md`

- [ ] Write a manual-clock test for click counts 1, 2, reset-by-time,
      reset-by-cell, reset-by-target, and zero on release.
- [ ] Write pure Edit tests for Unicode word, punctuation-grapheme, and
      source-end ranges plus a routed TextInput double-click test that retains
      selection after release.
- [ ] Run the focused test and verify RED.

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*PointerTests|*EditTests|*TextInputTests" --timeout 60s
```

- [ ] Add PointerEventArgs.ClickCount and optional TimeProvider gesture
      synthesis in CaptureManager using same target/button/cell and a 500 ms
      threshold.
- [ ] Add Edit.SelectWord and use a cluster-under-pointer mapping for TextInput
      double-click without starting drag capture.
- [ ] Update contracts, verify GREEN, and commit.

```bash
git add src/SharpVision/Input/PointerEventArgs.cs src/SharpVision/Input/CaptureManager.cs src/SharpVision/Text/Edit.cs src/SharpVision/Controls/TextInput.cs tests/SharpVision.Tests/Input/ManualTimeProvider.cs tests/SharpVision.Tests/Input/PointerTests.cs tests/SharpVision.Tests/Text/EditTests.cs tests/SharpVision.Tests/Controls/TextInputTests.cs docs/concepts/input-routing.md docs/controls/input/text-input.md
git commit -m "feat(input): select words on deterministic double click"
```

## Task 3: Hosted clipboard shortcuts and Ctrl+Q

**Files:**

- Modify: `tests/SharpVision.Tests/Runtime/ApplicationTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryExitTests.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`
- Modify: `src/SharpVision/Controls/TextInput.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Modify: `src/SharpVision.Showcase/Program.cs`
- Modify: `docs/controls/input/text-input.md`
- Modify: `docs/concepts/hosting.md`

- [ ] Write Application tests for Ctrl+C then Ctrl+V across two inputs, Ctrl+X,
      read-only, password, empty buffer, history, and supported terminal
      clipboard output.
- [ ] Change the showcase exit regression to Ctrl+Q and prove Ctrl+C on a
      focused input does not close.
- [ ] Run focused tests and verify RED.

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*ApplicationTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --filter-class "*GalleryExitTests" --timeout 60s
```

- [ ] Register an Application root preview handler for exact Control+C/X/V.
      Store non-empty copy/cut text per Application, mirror it through
      Terminal.Clipboard.Write, and paste through an internal TextInput entry
      point that delegates to Insert.
- [ ] Rebind Gallery exit to Ctrl+Q and run the showcase with
      TreatControlCAsInput.
- [ ] Update contracts, verify GREEN, and commit.

```csharp
var status = await ConsoleApplication.RunAsync(
    new Gallery(),
    static builder => builder.TreatControlCAsInput());
```

```bash
git add src/SharpVision/Runtime/Application.cs src/SharpVision/Controls/TextInput.cs src/SharpVision.Showcase/Gallery.cs src/SharpVision.Showcase/Program.cs tests/SharpVision.Tests/Runtime/ApplicationTests.cs tests/SharpVision.Showcase.Tests/GalleryExitTests.cs docs/controls/input/text-input.md docs/concepts/hosting.md
git commit -m "feat(input): route hosted clipboard shortcuts"
```

## Task 4: Trigger Popup specimens

**Files:**

- Modify: `tests/SharpVision.Showcase.Tests/LayerPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs`
- Modify: `src/SharpVision.Showcase/Panes/PopupPane.cs`

- [ ] Write tests asserting every fresh Popup is closed with empty
      SurfaceBounds, then activate each trigger and assert only its Popup opens
      over the stage.
- [ ] Run focused tests and verify RED.

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --filter-class "*LayerPaneTests|*GalleryInteractionTests" --timeout 60s
```

- [ ] Remove constructor IsOpen assignments, add explicit toggle/open handlers
      for edge, lifecycle, style, resize, and placement, and populate shallow
      stages with opaque content.
- [ ] Verify GREEN and commit.

```bash
git add src/SharpVision.Showcase/Panes/PopupPane.cs tests/SharpVision.Showcase.Tests/LayerPaneTests.cs tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs
git commit -m "fix(showcase): trigger popup specimens on demand"
```

## Task 5: Window stages and sidebar utilities

**Files:**

- Modify: `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/LayerPaneTests.cs`
- Modify: `src/SharpVision.Showcase/Panes/WindowPane.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`

- [ ] Write tests for Window child containment, contained shadows, one
      project-stage frame, no two-cell visual specimen, and a vertical footer
      with full-width picker/Quit rows.
- [ ] Run focused tests and verify RED.

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --filter-class "*GalleryRenderingTests|*LayerPaneTests" --timeout 60s
```

- [ ] Remove the redundant Window frame, contain the main shadow, widen/wrap
      styled content, center composition over a full backdrop, and replace the
      tiny visual with a readable minimum.
- [ ] Replace the footer Grid with a padded Stack containing a palette heading,
      full-width ComboBox, and a full-width Quit Button whose Dock content
      aligns Quit left and Ctrl+Q right.
- [ ] Verify GREEN and commit.

```bash
git add src/SharpVision.Showcase/Panes/WindowPane.cs src/SharpVision.Showcase/Gallery.cs tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs tests/SharpVision.Showcase.Tests/LayerPaneTests.cs
git commit -m "fix(showcase): clarify windows and sidebar utilities"
```

## Task 6: Normative showcase documentation

**Files:**

- Modify: `docs/architecture/showcase.md`
- Modify: `docs/testing/showcase.md`

- [ ] Document the fill Dock, vertical utility group, Ctrl+Q, closed-first
      Popups, readable Window stages, and transparent Text ownership.
- [ ] Require bounds fill, triggered popups, footer hierarchy, and
      clipboard/word-selection runtime proofs.
- [ ] Validate and commit.

```bash
npm run lint:markdown
npm run lint:links
git diff --check
git add docs/architecture/showcase.md docs/testing/showcase.md
git commit -m "docs(showcase): specify layered interaction polish"
```

## Task 7: Full verification

- [ ] Run all repository gates.

```bash
make format
make lint
make build
make test
```

Expected: every command exits zero; build reports zero warnings and errors;
tests meet the configured discovery minimum with no failures or timeouts.

- [ ] Audit the final tree.

```bash
git status --short
git diff --check
git log --oneline -10
```

Only intentional files may remain changed. Commit any formatter-only changes as
`style: apply repository formatting`.
