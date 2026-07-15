# Intrinsic Border and Shadow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the `Border` and `Shadow` wrapper controls and make border a layout-reserved intrinsic property of every `Control` (shadow is already intrinsic via `HasShadow`).

**Architecture:** `Control` already draws border+shadow (`ControlChrome.Render`), expands `VisualBounds` for the shadow, and computes `ContentBounds` as border+padding-deflated. The single gap is that the base layout pipeline reserves `Padding` but not `BorderThickness`. Close that gap in three base sites (measure constraint, desired-size, arrange), reconcile the one control that reserves border itself (`Button`), then delete `Shadow`/`Border` and migrate usages to the intrinsic properties.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly. Layout via `Engine().Layout(root, size)`; rendering asserted with `Frame` + `FrameOracle`; layout test helpers `LayoutProbe`/`ProbeControl`.

**Spec:** `docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md`.

**Base commit:** `a8796a4`. Branch: `codex/runtime-protocol-router` (SHARED — a concurrent showcase-rewrite/theming effort is live; Tasks 3–5 delete controls + migrate the showcase and WILL collide with it — sequence those when that effort is quiescent, or coordinate).

## Global Constraints

- .NET 10 / C# 14; file-scoped namespaces; `var` for locals in production (the test project uses explicit types + `new()` — follow that precedent); `using` after `namespace`.
- One public/named type per file, named exactly after the type (incl. test helpers). No nested named types. No primary constructors / positional records.
- XML docs on every public/internal type and member and every thrown exception. Validate public arguments before mutating state.
- Property changes invalidate only the required phase; the border reservation is a pure layout change and must leave zero-border layout **byte-for-byte unchanged** (regression).
- Border/shadow rendering (`ControlChrome.Render`, `RenderChrome`, `VisualBounds`, `ContentBounds`) is NOT changed by this plan — only layout reservation and the deletion/migration.
- Commit ONLY your own files with an explicit pathspec (`git commit -- <paths>`). NEVER `git add -A`/`restore`/`checkout`/`stash`/`format` — the tree is shared with a live concurrent effort. If a file you must edit shows uncommitted concurrent changes (`git status --short <file>` = `M`), STOP and surface it.
- Quality gate before each commit: build clean (0 warnings/errors) + the task's focused tests. Known: repo-level `make lint`/one showcase interaction test may be red from the concurrent effort — distinguish your-scope green from concurrent noise; never "fix" concurrent files.

## Canonical inset (used across Tasks 1–2)

The canonical content inset is border **then** padding, exactly as `Control.ContentBounds` already composes it (`Control.cs:1104`: `Padding.Deflate(BorderThickness.Deflate(Bounds))`). This plan makes the measure and arrange pipelines reserve the same border+padding inset.

---

## Task 1: Base layout reserves `BorderThickness`

**Files:**
- Modify: `src/SharpVision/Controls/Control.cs` (`Control.Arrange` line ~591; `CreateContentConstraint` lines 1345-1347; `ResolveDesiredSize` lines 1349-1365)
- Test: `tests/SharpVision.Tests/Controls/ControlBorderReservationTests.cs` (create)

**Interfaces:**
- Consumes: `Thickness.Horizontal`/`Vertical` (int), `Thickness.Deflate(Rect)`, `BorderThickness`/`Padding` (on `Control`).
- Produces: after this task, any control with `BorderThickness != default` insets its content (measure + arrange) by border+padding; zero-border controls are unchanged.

- [ ] **Step 1: Write the failing test**

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Tests.Support;

/// <summary>Verifies the base layout pipeline reserves BorderThickness (with Padding).</summary>
public sealed class ControlBorderReservationTests
{
    /// <summary>Verifies a bordered container insets its child by the border on every edge.</summary>
    [Fact]
    public void Arrange_WhenContainerHasBorder_InsetsChildByBorder()
    {
        ProbeControl child = new(new Size(4, 2));
        LayoutProbe container = new() { BorderThickness = new Thickness(1) };
        container.Children.Add(child);

        new Engine().Layout(container, new Size(20, 10));

        // Child arranged inside the 1-cell border on all edges.
        child.Bounds.ShouldBe(new Rect(1, 1, 18, 8));
    }

    /// <summary>Verifies a bordered container's measured desired size includes the border.</summary>
    [Fact]
    public void Measure_WhenContainerHasBorder_DesiredSizeIncludesBorder()
    {
        ProbeControl child = new(new Size(4, 2));
        LayoutProbe container = new() { BorderThickness = new Thickness(1) };
        container.Children.Add(child);

        container.Measure(new Constraint(width: null, height: null));

        // content (4,2) + 1-cell border on each edge.
        container.DesiredSize.ShouldBe(new Size(6, 4));
    }

    /// <summary>Verifies a zero-border container's layout is unchanged (regression).</summary>
    [Fact]
    public void Arrange_WhenNoBorder_LeavesChildAtFullSlot()
    {
        ProbeControl child = new(new Size(4, 2));
        LayoutProbe container = new();
        container.Children.Add(child);

        new Engine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(0, 0, 20, 10));
    }
}
```

`LayoutProbe` (in `tests/SharpVision.Tests/Support/LayoutProbe.cs`) measures the children-union and arranges each child to the slot it receives; `ProbeControl(new Size(w,h))` reports an intrinsic size. Both already exist from the scrolling work.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/SharpVision.Tests --filter-class "*ControlBorderReservationTests" --timeout 120s`
Expected: FAIL — `Arrange_WhenContainerHasBorder...` gives `Rect(0,0,20,10)` and `Measure...` gives `(4,2)` (border not reserved).

- [ ] **Step 3: Reserve border in the arrange content box**

In `src/SharpVision/Controls/Control.cs`, `Arrange` (line ~591), change the `padded` computation:

```csharp
// before:
Rect padded = Padding.Deflate(bounds);
// after:
Rect padded = Padding.Deflate(BorderThickness.Deflate(bounds));
```
Leave the following two lines (`ArrangeOverride(ResolveContentSlot(padded));` and `ArrangeOverlays(padded);`) unchanged — the scroll layer and bar chrome now operate inside the border, which is correct.

- [ ] **Step 4: Reserve border in the measure content constraint and desired size**

In `CreateContentConstraint` (lines 1345-1347), add the border to the padding argument on each axis:

```csharp
private Constraint CreateContentConstraint(Constraint constraint) => new(
    ResolveContentAxis(Width, constraint.Width, Margin.Horizontal, Padding.Horizontal + BorderThickness.Horizontal),
    ResolveContentAxis(Height, constraint.Height, Margin.Vertical, Padding.Vertical + BorderThickness.Vertical));
```

In `ResolveDesiredSize` (lines 1349-1365), add the border to the padding argument passed to `ResolveMeasureAxis` on each axis:

```csharp
private Size ResolveDesiredSize(Constraint constraint, Size content) => new(
    ResolveMeasureAxis(
        Width,
        constraint.Width,
        Margin.Horizontal,
        Padding.Horizontal + BorderThickness.Horizontal,
        content.Width,
        MinWidth,
        MaxWidth),
    ResolveMeasureAxis(
        Height,
        constraint.Height,
        Margin.Vertical,
        Padding.Vertical + BorderThickness.Vertical,
        content.Height,
        MinHeight,
        MaxHeight));
```

(`ResolveContentAxis`/`ResolveMeasureAxis` treat their `padding` parameter as the amount to reserve inside the border box; adding the border reserves both. No signature change.)

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --project tests/SharpVision.Tests --filter-class "*ControlBorderReservationTests" --timeout 120s`
Expected: PASS (3/3).

- [ ] **Step 6: Regression — zero-border layout unchanged**

Run: `dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Layout" --timeout 180s`
Expected: PASS. (These suites use zero-border controls, so nothing shifts.)

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(layout): reserve BorderThickness in the base measure/arrange pipeline" -- src/SharpVision/Controls/Control.cs tests/SharpVision.Tests/Controls/ControlBorderReservationTests.cs
```

---

## Task 2: Reconcile `Button` (border reserved exactly once)

**Files:**
- Modify: `src/SharpVision/Controls/Button.cs` (only if the characterization test shows a double-inset or an inconsistency; see steps)
- Test: `tests/SharpVision.Tests/Controls/ButtonTests.cs` (add a content-inset test; adjust an existing render test only if the corrected position differs)

**Interfaces:**
- Consumes: base border reservation (Task 1).
- Produces: `Button` content sits inside its 1-cell border exactly once, in normal and pressed-with-shadow states.

`Button` has `BorderThickness = new Thickness(1)` as a class default (`Button.cs:15`) and is the only control with a non-zero default border. Its `ArrangeOverride` (`Button.cs:153-154`) arranges `Content` via `FaceContentBounds(bounds)`, where `FaceContentBounds` (`Button.cs:222`) only applies the pressed-shadow shift (it does NOT deflate border). Its `OnPressedChanged` (`Button.cs:186-192`) re-arranges content via `FaceContentBounds(ContentBounds)`. Before Task 1, `ArrangeOverride`'s `bounds` was padding-only-deflated (content overlapped the border) while `ContentBounds` was border+padding-deflated — the two disagreed. After Task 1, `ArrangeOverride`'s `bounds` is border+padding-deflated, matching `ContentBounds` — so `Button` becomes self-consistent and content sits inside the border.

- [ ] **Step 1: Write the characterization test**

```csharp
// tests/SharpVision.Tests/Controls/ButtonTests.cs — add:
/// <summary>Verifies button content is inset inside the 1-cell border exactly once.</summary>
[Fact]
public void Arrange_WhenButtonHasDefaultBorder_InsetsContentByOneCell()
{
    Text label = new("Go");
    Button button = new() { Content = label };

    new Engine().Layout(button, new Size(10, 3));

    button.Bounds.ShouldBe(new Rect(0, 0, 10, 3));
    // Content sits inside the 1-cell border: origin (1,1), size (8,1).
    label.Bounds.ShouldBe(new Rect(1, 1, 8, 1));
}
```

- [ ] **Step 2: Run test to verify current behavior**

Run: `dotnet test --project tests/SharpVision.Tests --filter-class "*ButtonTests" --filter-method "*InsetsContentByOneCell*" --timeout 120s`
Expected: With Task 1 in place, this likely PASSES already (the base reserves the border and `FaceContentBounds` no longer needs to). If it FAILS with content at `(2,2,6,-1)`-style double-inset, `Button` is deflating the border a second time — proceed to Step 3; otherwise skip to Step 4.

- [ ] **Step 3: Remove any double border reservation in Button (only if Step 2 failed)**

If Step 2 showed a double-inset, the cause is the redundant `OnPressedChanged` re-arrange or a stale deflate. Simplify so the border is reserved exactly once — `ArrangeOverride` already receives the border-deflated box from the base, so `FaceContentBounds(bounds)` must pass `bounds` straight through (only the pressed-shadow shift). Confirm `FaceContentBounds` (`Button.cs:222`) is `IsPressed && HasShadow ? ControlChrome.Shift(bounds, ShadowOffset) : bounds` (it is — no border deflate), and remove the now-redundant `content.Arrange(FaceContentBounds(ContentBounds), ...)` in `OnPressedChanged` (`Button.cs:190`) if it fights the committed arrange (replace with `Invalidate(Invalidation.Arrange)` alone, which re-runs `ArrangeOverride` with the correct box).

- [ ] **Step 4: Run the full ButtonTests to catch render shifts**

Run: `dotnet test --project tests/SharpVision.Tests --filter-class "*ButtonTests" --timeout 180s`
Expected: PASS. If a pre-existing render test asserted content one cell off (the old inconsistent position), update that test's expected coordinates to the corrected border-inset position and note it in the commit.

- [ ] **Step 5: Commit**

```bash
git commit -m "fix(controls): Button reserves its border exactly once via the base pipeline" -- src/SharpVision/Controls/Button.cs tests/SharpVision.Tests/Controls/ButtonTests.cs
```
(If no `Button.cs` change was needed, commit only the added test.)

---

## Task 3: Delete `Shadow`; shadow is intrinsic via `HasShadow`

**Files:**
- Delete: `src/SharpVision/Controls/Shadow.cs`, `src/SharpVision.Showcase/Panes/ShadowPane.cs`, `tests/SharpVision.Tests/Controls/ShadowTests.cs`
- Create: `tests/SharpVision.Tests/Controls/IntrinsicShadowTests.cs` (port the `Shadow` render contract onto a plain control)
- Modify: every `Shadow` usage (find in Step 1), the showcase inventory (`Gallery.cs` + `GalleryTests`/`GalleryRenderingTests`/`TmuxSmokeTests`)

**Interfaces:**
- Consumes: `Control.HasShadow`/`ShadowMode`/`ShadowOffset`/`ShadowGlyph`/`ShadowAttributes` (all pre-existing).
- Produces: no `Shadow` type anywhere.

This is atomic (deleting `Shadow.cs` breaks every referencing file, so all migrations land in one commit).

- [ ] **Step 1: Find every reference**

Run: `grep -rln --include="*.cs" "\bShadow\b" src tests | grep -vE "ShadowMode|ShadowOffset|ShadowGlyph|ShadowAttributes|HasShadow|DrawShadow|ShadowExcludeBounds|ShadowAppearanceSource|ShadowBounds"`
Expected: `Shadow.cs`, `ShadowPane.cs`, `ShadowTests.cs`, and any control/showcase/test that constructs a `Shadow`.

- [ ] **Step 2: Port the Shadow render contract to `IntrinsicShadowTests.cs`**

Read the deleted-target `tests/SharpVision.Tests/Controls/ShadowTests.cs` and port its render cases (`Render_WhenModeIsBlockGlyph_DrawsTurboVisionFootprint`, `Render_WhenModeIsComposite_PreservesUnderlyingGlyphs`, `Render_WhenShadowTouchesWideGlyph_StylesCompleteOwner`, `Render_WhenOffsetIsNegative_DrawsVisualOverflowWithoutHitTarget`, `Render_WhenAncestorCanvasClipsShadow_DoesNotEscapeClip`, and `Layout_WhenChildIsPresent_DoesNotReserveShadowOffset`) to assertions on a plain control (e.g. a `Text` or `LayoutProbe` child) with `HasShadow = true` + the relevant `ShadowMode`/`ShadowOffset`/`ShadowGlyph` set directly, instead of wrapping in `Shadow`. Same `Frame`/`FrameOracle` glyph/style assertions. The `Constructor_...`/`Properties_...WhenValueIsInvalid` cases move to whichever control's property setters they now target (or drop if already covered by `Control` style-property tests).

- [ ] **Step 3: Migrate production/showcase `Shadow` usages**

Replace each `new Shadow { Child = x, Mode = m, Offset = o, Glyph = g }` with setting the properties on `x` directly: `x.HasShadow = true;` (+ `x.ShadowMode = m; x.ShadowOffset = o; x.ShadowGlyph = g;` when they differ from defaults), and use `x` where the `Shadow` was used.

- [ ] **Step 4: Delete `Shadow.cs`, `ShadowPane.cs`, `ShadowTests.cs`; drop the `Shadow` showcase page**

```bash
git rm src/SharpVision/Controls/Shadow.cs src/SharpVision.Showcase/Panes/ShadowPane.cs tests/SharpVision.Tests/Controls/ShadowTests.cs
```
Remove the `ShadowPane` entry from `Gallery.cs`'s page list, and remove `"Shadow"` from the inventory arrays/assertions in `tests/SharpVision.Showcase.Tests/GalleryTests.cs`, `GalleryRenderingTests.cs`, and the `Down`-count in `TmuxSmokeTests.cs` (decrement by one, mirroring the `ScrollView` removal).

- [ ] **Step 5: Build + verify**

Run: `dotnet build` then `dotnet test --project tests/SharpVision.Tests --filter-class "*IntrinsicShadowTests" --timeout 120s` and `dotnet test --project tests/SharpVision.Showcase.Tests --timeout 300s`
Expected: build clean; intrinsic shadow tests pass; showcase inventory tests pass. `grep -rn --include="*.cs" "\bShadow\b" src tests` shows only the `ShadowMode`/`ShadowOffset`/… property names, no `Shadow` type.

- [ ] **Step 6: Commit**

```bash
git commit -m "refactor: remove Shadow control; shadow is intrinsic via HasShadow" -- <every file you changed/removed>
```

---

## Task 4: Delete `Border`; border is intrinsic via `BorderThickness`/`BorderGlyphs`

**Files:**
- Delete: `src/SharpVision/Controls/Border.cs`, `src/SharpVision.Showcase/Panes/BorderPane.cs`, `tests/SharpVision.Tests/Controls/BorderTests.cs`
- Create: `tests/SharpVision.Tests/Controls/IntrinsicBorderTests.cs`
- Modify: every `Border`-control usage (find in Step 1) — notably the showcase panes that frame content with `Border` (`GridPane`, `WindowPane`, `MenuPane`, `CanvasPane`, `ButtonPane`, `DockPane`, `RichTextPane`, `OverlayPane`, `StackPane`, `Doc.cs`, `Gallery.cs`) — plus the showcase inventory.

**Interfaces:**
- Consumes: base border reservation (Task 1); `Control.BorderThickness`/`BorderGlyphs`; `ControlChrome.DrawPartialBorder` (already used by `RenderChrome`).
- Produces: no `Border` type anywhere.

Atomic (like Task 3). Requires Task 1 (base reserves border) already merged. NOTE: `grep "\bBorder\b"` also matches the `Border` color role / `ThemeColors.Border` / `ColorRole.Border` / `Glyphs` — those are NOT the control; migrate only `Border`-*control* constructions.

- [ ] **Step 1: Find every Border-control reference**

Run: `grep -rln --include="*.cs" "new Border\b\|: Border\b\|\bBorder \|\bBorder(" src tests` and cross-check against the `Border`-role false positives in `Styling/*`.
Expected: `Border.cs`, `BorderPane.cs`, `BorderTests.cs`, and the showcase panes/tests that construct a `Border`.

- [ ] **Step 2: Port the Border render/layout contract to `IntrinsicBorderTests.cs`**

Read the deleted-target `tests/SharpVision.Tests/Controls/BorderTests.cs` and port its cases to an intrinsic-bordered control (a `LayoutProbe`/`Stack` with `BorderThickness`/`BorderGlyphs` set):
- `Layout_WhenChildHasMarginPaddingAndBorder_ComputesExactBounds` → assert child `Bounds` inset by margin+border+padding (this is Task 1's contract at full generality).
- `Render_WhenBorderIsComplete_WritesCornersEdgesAndChild`, `Render_WhenEdgesArePartial_UsesOnlyActiveCustomGlyphsAndStyles`, `Render_WhenBoundsAreTiny_RemainsContained` → assert the per-side border glyphs/corners via `Frame`/`FrameOracle` on the intrinsic-bordered control (the default `OnRender` → `RenderChrome` → `ControlChrome.DrawPartialBorder` draws them).
- `Glyphs_WhenPresetIsSelected_UsesExactRunes`, `BorderThickness_WhenAnEdgeExceedsOne_Throws`, `Constructor_WhenGlyphIsNotPrintableNarrow_Throws` → move to `BorderGlyphs`/`BorderThickness` property-setter tests on `Control` (these validators already live on `Control`).

- [ ] **Step 3: Migrate `Border`-control usages**

`new Border { Child = x, Glyphs = g }` becomes: set `x.BorderThickness = new Thickness(1); x.BorderGlyphs = g;` and use `x` where the `Border` was. When the bordered subject must remain wrapped in a distinct node (e.g. a showcase card framing arbitrary content), set the border on a single-child container: `new Dock { BorderThickness = new Thickness(1), BorderGlyphs = g, Children = { x } }` (or `Stack`/`Grid`). For the many showcase framing uses, prefer a small local helper in `Doc.cs` (e.g. a `Framed(Control, Glyphs)` returning a bordered `Dock`) to keep the panes terse — this replaces the `Border` framing idiom in one place.

- [ ] **Step 4: Delete `Border.cs`, `BorderPane.cs`, `BorderTests.cs`; drop the `Border` showcase page**

```bash
git rm src/SharpVision/Controls/Border.cs src/SharpVision.Showcase/Panes/BorderPane.cs tests/SharpVision.Tests/Controls/BorderTests.cs
```
Remove the `BorderPane` entry from `Gallery.cs`, and remove `"Border"` from the inventory arrays/assertions in `GalleryTests.cs`/`GalleryRenderingTests.cs` and the `TmuxSmokeTests.cs` count (decrement by one).

- [ ] **Step 5: Build + verify**

Run: `dotnet build`, then `dotnet test --project tests/SharpVision.Tests --filter-class "*IntrinsicBorderTests" --timeout 120s` and `dotnet test --project tests/SharpVision.Showcase.Tests --timeout 300s`.
Expected: build clean; intrinsic border tests pass; showcase tests pass. `grep -rn --include="*.cs" "new Border\b" src tests` returns nothing.

- [ ] **Step 6: Commit**

```bash
git commit -m "refactor: remove Border control; border is intrinsic via BorderThickness" -- <every file you changed/removed>
```

---

## Task 5: Docs, `AGENTS.md`, and quality gate

**Files:**
- Modify: `docs/concepts/layout.md`, `docs/concepts/styling.md`, `AGENTS.md`; remove `docs/controls/*` `Border`/`Shadow` specs (grep first) + fix any links they break (mirror the ScrollView doc removal).

- [ ] **Step 1: Update docs**

- `docs/concepts/layout.md`: note that `BorderThickness` reserves layout (border, then padding) and insets children, alongside the existing padding/margin description.
- `docs/concepts/styling.md` (or the chrome doc): border and shadow are set on any control via `BorderThickness`/`BorderGlyphs` and `HasShadow`/`ShadowMode`/`ShadowOffset`/`ShadowGlyph`/`ShadowAttributes`; there is no `Border`/`Shadow` control.
- Remove any `docs/controls/**/border*.md` and `docs/controls/**/shadow*.md` spec files (grep: `grep -rln "border\|shadow" docs/controls`); fix dangling links in `docs/controls/index.md` and anywhere referencing them (`grep -rn "border-\|shadow-" docs`).
- `AGENTS.md`: add that border/shadow are intrinsic `Control` properties (no dedicated control), mirroring the scrolling note.

- [ ] **Step 2: Quality gate (my-scope green; concurrent noise noted)**

Run: `dotnet build` (0/0), then `dotnet test --project tests/SharpVision.Tests --timeout 600s` (green for this scope), then `dotnet test --project tests/SharpVision.Showcase.Tests --timeout 300s`. Do NOT run `make format` (it mutates the shared tree). Run `make lint` and report which sub-checks pass on your files; any red confined to concurrent theme/showcase files is out of scope — do not fix.
Expected: build clean; `SharpVision.Tests` green; showcase inventory green.

- [ ] **Step 3: Commit**

```bash
git commit -m "docs: intrinsic border/shadow; remove Border/Shadow control docs" -- <the docs/AGENTS files you changed>
```

---

## Self-Review

**Spec coverage:** Decision 1 (delete Shadow) → Task 3; Decision 2 (delete Border) → Task 4; Decision 3 (base reserves BorderThickness) → Task 1; Decision 4 (reserve exactly once) → Task 1 (containers) + Task 2 (Button) + audit note; Decision 5 (idiom) → the migration shape in Tasks 3/4; Decision 6 (showcase pages removed) → Tasks 3/4 Step 4. §2 rendering-already-intrinsic → relied on in Tasks 3/4 (no render change). Testing/docs → Tasks 1–5. Risks (heavy showcase contention, double-inset, non-chrome OnRender containers, inventory change) → Global Constraints + Task 2 audit + Tasks 3/4/5.

**Placeholder scan:** Task 2 is intentionally conditional ("only if the characterization test fails") because whether `Button` needs an edit depends on Task 1's effect — the test and the exact reconciliation (remove the redundant `OnPressedChanged` re-arrange) are concrete, not a placeholder. Task 3/4 Step 1 (`grep`) and Step 3 (per-usage migration) can't enumerate every call site in advance because the concurrent showcase effort is still moving them — the grep + the exact mechanical substitution are the actionable instruction.

**Type consistency:** `BorderThickness`/`BorderGlyphs`/`HasShadow`/`ShadowMode`/`ShadowOffset`/`ShadowGlyph`/`ShadowAttributes`, `Thickness.Horizontal`/`Vertical`/`Deflate`, `LayoutProbe`/`ProbeControl`, and the `Padding.Horizontal + BorderThickness.Horizontal` inset are consistent across tasks and match the current code (`Control.cs:591,1104,1345-1365`).

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks.
2. **Inline Execution** — execute tasks in this session with checkpoints.

Which approach? (Note: Tasks 3–5 collide with the live concurrent showcase effort — consider running Tasks 1–2, the pure-library core, now and holding 3–5 until that effort is quiescent.)
