# Intrinsic Border and Shadow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the `Border` and `Shadow` wrapper controls and make border and
shadow intrinsic `Control` capabilities without changing existing Button,
Window, showcase, or terminal-cell behavior.

**Architecture:** `ControlChrome`, `VisualBounds`, `ContentBounds`, and the
style-property registry already own border and shadow rendering. The base layout
pipeline will reserve `BorderThickness` before `Padding`; `Button` will drop the
padding class default that currently stands in for its border inset; the
temporary `Border` compatibility implementation will delegate reservation to the
base pipeline until the type is deleted. Shadow migrations explicitly preserve
the removed wrapper's `(2, 1)` offset and dim appearance.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly. Layout via
`Engine().Layout(root, size)`; rendering asserted with `Frame` + `FrameOracle`;
layout test helpers `LayoutProbe`/`ProbeControl`.

**Spec:** `docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md`.

**Execution base:** `codex/runtime-protocol-router` at `fb75af6`. Create an
isolated `codex/intrinsic-border-shadow` worktree before implementation. Do not
execute this plan on `text-markup-merge`; that branch contains separate work.

## Global Constraints

- .NET 10 / C# 14; file-scoped namespaces; `var` for locals in production (the
  test project uses explicit types + `new()` — follow that precedent); `using`
  after `namespace`.
- One public/named type per file, named exactly after the type (incl. test
  helpers). No nested named types. No primary constructors / positional records.
- XML docs on every public/internal type and member and every thrown exception.
  Validate public arguments before mutating state.
- Property changes invalidate only the required phase; the border reservation is
  a pure layout change and must leave zero-border layout **byte-for-byte
  unchanged** (regression).
- Border/shadow rendering (`ControlChrome.Render`, `RenderChrome`,
  `VisualBounds`, `ContentBounds`) is unchanged. A control that overrides
  `OnRender` without calling `RenderChrome` does not automatically draw
  intrinsic chrome; use an ordinary container such as `Dock` when migration
  needs a distinct frame node.
- `Button` currently gets its one-cell content inset from its `Padding = 1`
  class default. Remove that default in the same task that makes the base
  reserve its one-cell border; keep `OnPressedChanged`'s immediate
  pressed-content arrangement.
- The removed `Shadow` wrapper defaults to offset `(2, 1)` and dim attributes.
  Preserve both explicitly when porting its behavior; `HasShadow = true` alone
  is not equivalent.
- Keep the surviving `Border` wrapper green until deletion by removing its
  private border reservation in the same task as the base layout change.
- Commit only intentional files with explicit pathspecs. Never use `git add -A`,
  `git add .`, destructive restore/checkout/reset, or force operations.
- Quality gate before each commit: clean Release build plus the task's focused
  existing and new tests. The final isolated-worktree gate is `make format`,
  `make lint`, `make build`, and `make test`.

## Canonical inset (used across Tasks 1–2)

The canonical content inset is border **then** padding, exactly as
`Control.ContentBounds` already composes it (`Control.cs:1104`:
`Padding.Deflate(BorderThickness.Deflate(Bounds))`). This plan makes the measure
and arrange pipelines reserve the same border+padding inset.

---

## Task 1: Base layout reserves `BorderThickness` without moving existing chrome

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs` (`Control.Arrange` line ~591;
  `CreateContentConstraint` lines 1345-1347; `ResolveDesiredSize` lines
  1349-1365)
- Modify: `src/SharpVision/Controls/Button.cs` (remove the padding class
  default)
- Modify: `src/SharpVision/Controls/Border.cs` (temporary compatibility until
  Task 4 deletes the type)
- Test: `tests/SharpVision.Tests/Controls/ControlBorderReservationTests.cs`
  (create)
- Test: `tests/SharpVision.Tests/Controls/ButtonTests.cs` (add committed-bounds
  characterization)

**Interfaces:**

- Consumes: `Thickness.Horizontal`/`Vertical` (int), `Thickness.Deflate(Rect)`,
  `BorderThickness`/`Padding` (on `Control`).
- Produces: after this task, any control with `BorderThickness != default`
  insets its content (measure + arrange) by border+padding; zero-border controls
  are unchanged.

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

`LayoutProbe` (in `tests/SharpVision.Tests/Support/LayoutProbe.cs`) measures the
children-union and arranges each child to the slot it receives;
`ProbeControl(new Size(w,h))` reports an intrinsic size. Both already exist from
the scrolling work.

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*ControlBorderReservationTests" --timeout 120s
```

Expected: FAIL — `Arrange_WhenContainerHasBorder...` gives `Rect(0,0,20,10)` and
`Measure...` gives `(4,2)` (border not reserved).

- [ ] **Step 3: Reserve border in the arrange content box**

In `src/SharpVision/Controls/Control.cs`, `Arrange` (line ~591), change the
`padded` computation:

```csharp
// before:
Rect padded = Padding.Deflate(bounds);
// after:
Rect padded = Padding.Deflate(BorderThickness.Deflate(bounds));
```

Leave the following two lines (`ArrangeOverride(ResolveContentSlot(padded));`
and `ArrangeOverlays(padded);`) unchanged — the scroll layer and bar chrome now
operate inside the border, which is correct.

- [ ] **Step 4: Reserve border in the measure content constraint and desired
      size**

In `CreateContentConstraint` (lines 1345-1347), add the border to the padding
argument on each axis:

```csharp
private Constraint CreateContentConstraint(Constraint constraint) => new(
    ResolveContentAxis(Width, constraint.Width, Margin.Horizontal, Padding.Horizontal + BorderThickness.Horizontal),
    ResolveContentAxis(Height, constraint.Height, Margin.Vertical, Padding.Vertical + BorderThickness.Vertical));
```

In `ResolveDesiredSize` (lines 1349-1365), add the border to the padding
argument passed to `ResolveMeasureAxis` on each axis:

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

(`ResolveContentAxis`/`ResolveMeasureAxis` treat their `padding` parameter as
the amount to reserve inside the border box; adding the border reserves both. No
signature change.)

- [ ] **Step 5: Preserve Button's current one-cell content position**

Before changing `Button`, add the following characterization to `ButtonTests.cs`
and run it once. It must pass on the old implementation:

```csharp
/// <summary>Verifies default Button chrome keeps content one cell inside its frame.</summary>
[Fact]
public void Arrange_WhenDefaultChromeIsUsed_PreservesOneCellContentInset()
{
    ControlText label = new("Go");
    Button button = new() { Content = label };

    new Engine().Layout(button, new Size(10, 3));

    button.Bounds.ShouldBe(new Rect(0, 0, 10, 3));
    label.Bounds.ShouldBe(new Rect(1, 1, 8, 1));
}
```

Then delete only this class-default registration from `Button`:

```csharp
_ = PaddingProperty.RegisterClassDefault<Button>(new Thickness(1));
```

Change `Constructor_WhenCreated_UsesDocumentedDefaults` to expect
`button.Padding.ShouldBe(default)` and update the `Button` constructor XML
summary so it no longer claims a default internal padding value.

Do not alter `FaceContentBounds` or the immediate content arrangement in
`OnPressedChanged`. The new base border inset replaces the old padding inset, so
released and pressed content remain in the same cells.

- [ ] **Step 6: Keep the surviving Border wrapper reserved exactly once**

After the base change, run
`BorderTests.Layout_WhenChildHasMarginPaddingAndBorder_ComputesExactBounds` and
observe the expected double-inset failure. Then make `Border` delegate box-model
arithmetic to the base:

```csharp
protected override Size MeasureOverride(Constraint constraint)
{
    Control? child = Child;

    if (child is null)
    {
        return default;
    }

    child.Measure(constraint);
    return new Size(
        Add(child.DesiredSize.Width, child.Margin.Horizontal),
        Add(child.DesiredSize.Height, child.Margin.Vertical));
}

protected override void ArrangeOverride(Rect bounds) =>
    Child?.Arrange(bounds, widthResolved: true, heightResolved: true);
```

Delete the now-unused `Subtract` helper. Keep `Add` and `OnRender` unchanged;
Task 4 deletes the complete type.

- [ ] **Step 7: Run focused tests for the atomic box-model change**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ControlBorderReservationTests|*ButtonTests|*BorderTests" --timeout 180s
dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Layout" --timeout 180s
dotnet build SharpVision.slnx --configuration Release --no-incremental
```

Expected: all selected tests pass and the Release build reports zero warnings
and zero errors.

- [ ] **Step 8: Commit**

```bash
git commit -m "feat(layout): reserve BorderThickness in the base measure/arrange pipeline" -- \
  src/SharpVision/Controls/Control.cs \
  src/SharpVision/Controls/Button.cs \
  src/SharpVision/Controls/Border.cs \
  tests/SharpVision.Tests/Controls/ControlBorderReservationTests.cs \
  tests/SharpVision.Tests/Controls/ButtonTests.cs
```

---

## Task 2: Prove Button's pressed content still follows the translated face

**Files:**

- Test: `tests/SharpVision.Tests/Controls/ButtonTests.cs`

Task 1 deliberately keeps `OnPressedChanged`'s immediate content arrangement.
This task records that behavior so a later cleanup cannot reintroduce the old
plan's incorrect “redundant” diagnosis.

- [ ] **Step 1: Add the pressed-content characterization**

```csharp
/// <summary>Verifies a pressed shadowed Button immediately translates content with its face.</summary>
[Fact]
public void Arrange_WhenPressedWithShadow_TranslatesContentByShadowOffset()
{
    ControlText label = new("Go");
    Button button = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Width = Length.Cells(6),
        Height = Length.Cells(3),
        Content = label,
    };
    new Engine().Layout(button, new Size(10, 6));

    Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
        Code.Character,
        new Rune(' '),
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press)));

    label.Bounds.ShouldBe(new Rect(2, 2, 4, 1));
}
```

- [ ] **Step 2: Audit and verify**

Run:

```bash
rg -n "BorderThickness(Property)?" src/SharpVision/Controls --glob '*.cs'
dotnet test --project tests/SharpVision.Tests --filter-class "*ButtonTests" --timeout 180s
```

Expected: non-zero class-default border registration remains only on `Button`;
the temporary `Border` compatibility type references its own property for
rendering only; all Button tests pass.

- [ ] **Step 3: Commit**

```bash
git commit -m "test(controls): preserve pressed Button content geometry" -- \
  tests/SharpVision.Tests/Controls/ButtonTests.cs
```

---

## Task 3: Delete `Shadow`; shadow is intrinsic via `HasShadow`

**Files:**

- Delete: `src/SharpVision/Controls/Shadow.cs`,
  `src/SharpVision.Showcase/Panes/ShadowPane.cs`,
  `tests/SharpVision.Tests/Controls/ShadowTests.cs`
- Create: `tests/SharpVision.Tests/Controls/IntrinsicShadowTests.cs` (port the
  `Shadow` render contract onto a plain control)
- Modify: `tests/SharpVision.Tests/Controls/AmbiguousWidthControlTests.cs` and
  the showcase inventory (`Gallery.cs` +
  `GalleryTests`/`GalleryRenderingTests`/`TmuxSmokeTests`)

**Interfaces:**

- Consumes:
  `Control.HasShadow`/`ShadowMode`/`ShadowOffset`/`ShadowGlyph`/`ShadowAttributes`
  (all pre-existing).
- Produces: no `Shadow` type anywhere.

This is atomic (deleting `Shadow.cs` breaks every referencing file, so all
migrations land in one commit).

- [ ] **Step 1: Find every reference**

Run:

```bash
rg -n "new Shadow\b|ShouldBeOfType<Shadow>|Find<Shadow>|\bShadow shadow\b" src tests --glob '*.cs'
```

Expected on the execution base: `ShadowPane.cs`, `ShadowTests.cs`, and the
ambiguous-width control test. Re-run immediately before deletion so a newly
added call site cannot escape migration.

- [ ] **Step 2: Port the Shadow render contract to `IntrinsicShadowTests.cs`**

Read the deleted-target `tests/SharpVision.Tests/Controls/ShadowTests.cs` and
port its render cases (`Render_WhenModeIsBlockGlyph_DrawsTurboVisionFootprint`,
`Render_WhenModeIsComposite_PreservesUnderlyingGlyphs`,
`Render_WhenShadowTouchesWideGlyph_StylesCompleteOwner`,
`Render_WhenOffsetIsNegative_DrawsVisualOverflowWithoutHitTarget`,
`Render_WhenAncestorCanvasClipsShadow_DoesNotEscapeClip`, and
`Layout_WhenChildIsPresent_DoesNotReserveShadowOffset`) to assertions on a plain
`LayoutProbe` with `HasShadow = true` and the relevant intrinsic properties. Use
an ordinary container because its inherited `OnRender` exercises `RenderChrome`;
do not use a leaf that overrides `OnRender` without calling it. Preserve the
removed wrapper defaults explicitly in the shared fixture:

```csharp
private static LayoutProbe CreateArranged(ShadowMode mode, Point offset)
{
    LayoutProbe control = new()
    {
        HasShadow = true,
        ShadowMode = mode,
        ShadowOffset = offset,
        ShadowAttributes = Attributes.Dim,
    };
    control.Children.Add(new ProbeControl(new Size(3, 2)));
    new Engine().Layout(control, new Size(3, 2));
    return control;
}
```

Port the invalid-mode and invalid-glyph cases to the same `LayoutProbe` public
properties. Keep the existing `Frame`/`FrameOracle`, clipping, wide-owner, and
hit-testing assertions unchanged.

- [ ] **Step 3: Migrate production/showcase `Shadow` usages**

At this base there is no shipped production construction outside the page being
deleted. In `AmbiguousWidthControlTests`, replace the wrapper with a
`LayoutProbe` configured with `HasShadow = true`, block-glyph mode, offset
`(1, 1)`, and a `ProbeControl` child. Preserve its `#` fallback assertion.

If the Step 1 search finds a new construction, migrate it to a chrome-rendering
container and explicitly preserve every non-base wrapper default it relied on:
`HasShadow = true`, `ShadowOffset = new Point(2, 1)`, and
`ShadowAttributes = Attributes.Dim`.

- [ ] **Step 4: Delete `Shadow.cs`, `ShadowPane.cs`, `ShadowTests.cs`; drop the
      `Shadow` showcase page**

```bash
git rm src/SharpVision/Controls/Shadow.cs src/SharpVision.Showcase/Panes/ShadowPane.cs tests/SharpVision.Tests/Controls/ShadowTests.cs
```

Remove the `ShadowPane` entry from `Gallery.cs`'s page list, and remove
`"Shadow"` from the inventory arrays/assertions in
`tests/SharpVision.Showcase.Tests/GalleryTests.cs`, `GalleryRenderingTests.cs`,
and the `Down`-count in `TmuxSmokeTests.cs` (change 18 to 17 because Text moves
one catalog position earlier).

- [ ] **Step 5: Build + verify**

Run:

```bash
rg -n "new Shadow\b|ShouldBeOfType<Shadow>|Find<Shadow>|\bShadow shadow\b" src tests --glob '*.cs'
dotnet build SharpVision.slnx --configuration Release --no-incremental
dotnet test --project tests/SharpVision.Tests --filter-class "*IntrinsicShadowTests|*AmbiguousWidthControlTests" --timeout 180s
dotnet test --project tests/SharpVision.Showcase.Tests --timeout 300s
```

Expected: the search is silent; build and both test commands pass.

- [ ] **Step 6: Commit**

```bash
git commit -m "refactor: remove Shadow control; shadow is intrinsic via HasShadow" -- \
  src/SharpVision/Controls/Shadow.cs \
  src/SharpVision.Showcase/Gallery.cs \
  src/SharpVision.Showcase/Panes/ShadowPane.cs \
  tests/SharpVision.Tests/Controls/ShadowTests.cs \
  tests/SharpVision.Tests/Controls/IntrinsicShadowTests.cs \
  tests/SharpVision.Tests/Controls/AmbiguousWidthControlTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs \
  tests/SharpVision.Showcase.Tests/TmuxSmokeTests.cs
```

---

## Task 4: Delete `Border`; border is intrinsic via `BorderThickness`/`BorderGlyphs`

**Files:**

- Delete: `src/SharpVision/Controls/Border.cs`,
  `src/SharpVision.Showcase/Panes/BorderPane.cs`,
  `tests/SharpVision.Tests/Controls/BorderTests.cs`
- Create: `tests/SharpVision.Tests/Controls/IntrinsicBorderTests.cs`
- Modify: `Gallery.cs`, `CanvasPane.cs`, `Doc.cs`, `DockPane.cs`, `GridPane.cs`,
  `MenuPane.cs`, `OverlayPane.cs`, `RadioButtonPane.cs`, `RichTextPane.cs`,
  `StackPane.cs`, `ThemingPane.cs`, and `WindowPane.cs`.
- Modify: `AmbiguousWidthControlTests.cs`, `PopupTests.cs`,
  `DisplayPanelTests.cs`, `DisplayPanelPerformanceTests.cs`, `GalleryTests.cs`,
  `GalleryRenderingTests.cs`, `ThemeSwatchLiveThemeTests.cs`, and
  `TmuxSmokeTests.cs`.

**Interfaces:**

- Consumes: base border reservation (Task 1);
  `Control.BorderThickness`/`BorderGlyphs`; `ControlChrome.DrawPartialBorder`
  (already used by `RenderChrome`).
- Produces: no `Border` type anywhere.

Atomic (like Task 3). Requires Task 1 (base reserves border) already merged.
NOTE: a broad `Border` search also matches the `Border` color role /
`ThemeColors.Border` / `ColorRole.Border` / `Glyphs` — those are NOT the
control; migrate only `Border`-_control_ constructions.

- [ ] **Step 1: Find every Border-control reference**

Run:

```bash
rg -n "new Border\b|ShouldBeOfType<Border>|Find<Border>|\bBorder (border|chip|cover|frame|surface|sidebar)\b" src tests --glob '*.cs'
```

Expected: the deleted files plus the production and test files enumerated in
this task. Re-run immediately before deletion.

- [ ] **Step 2: Port the Border render/layout contract to
      `IntrinsicBorderTests.cs`**

Read the deleted-target `tests/SharpVision.Tests/Controls/BorderTests.cs` and
port its cases to an intrinsic-bordered control (a `LayoutProbe`/`Stack` with
`BorderThickness`/`BorderGlyphs` set):

- `Layout_WhenChildHasMarginPaddingAndBorder_ComputesExactBounds` → assert child
  `Bounds` inset by margin+border+padding (this is Task 1's contract at full
  generality).
- `Render_WhenBorderIsComplete_WritesCornersEdgesAndChild`,
  `Render_WhenEdgesArePartial_UsesOnlyActiveCustomGlyphsAndStyles`,
  `Render_WhenBoundsAreTiny_RemainsContained` → assert the per-side border
  glyphs/corners via `Frame`/`FrameOracle` on the intrinsic-bordered control
  (the default `OnRender` → `RenderChrome` → `ControlChrome.DrawPartialBorder`
  draws them).
- `Glyphs_WhenPresetIsSelected_UsesExactRunes`,
  `BorderThickness_WhenAnEdgeExceedsOne_Throws`,
  `Constructor_WhenGlyphIsNotPrintableNarrow_Throws` → move to
  `BorderGlyphs`/`BorderThickness` property-setter tests on `Control` (these
  validators already live on `Control`).

- [ ] **Step 3: Migrate `Border`-control usages**

Use an ordinary `Dock` whenever the old wrapper supplied a distinct ownership or
rendering node:

```csharp
Dock frame = new()
{
    BorderThickness = new Thickness(1),
    BorderGlyphs = glyphs,
    Padding = padding,
    Children = { child },
};
```

Do not put border properties directly on a leaf whose custom `OnRender` omits
`RenderChrome`. Change `Doc.Card` to the exact shared idiom:

```csharp
internal static Dock Card(Control child)
{
    ArgumentNullException.ThrowIfNull(child);
    return new Dock
    {
        BorderThickness = new Thickness(1),
        BorderGlyphs = Glyphs.Rounded,
        Padding = new Thickness(1, 0),
        Children = { child },
    };
}
```

Apply the same conversion to every pane-local `Card`/`Frame`: return `Dock`,
rename `Glyphs =` to `BorderGlyphs =`, and replace `Child = value` with
`Children = { value }`. Use `Dock` for the opaque popup cover, integration
display-panel frame, performance navigation cards, and theme swatch chip. In
`ThemeSwatchLiveThemeTests`, change the helper parameter type and comments from
`Border` to `Dock`/“intrinsic control surface.”

- [ ] **Step 4: Delete `Border.cs`, `BorderPane.cs`, `BorderTests.cs`; drop the
      `Border` showcase page**

```bash
git rm src/SharpVision/Controls/Border.cs src/SharpVision.Showcase/Panes/BorderPane.cs tests/SharpVision.Tests/Controls/BorderTests.cs
```

In `Gallery`, remove `BorderPane`; change `Sidebar` and the unbordered `surface`
wrapper to `Dock`; expose `public Dock Sidebar { get; }`; and preserve the
sidebar width, rounded glyphs, border thickness, child, and key handler.

In `GalleryTests`, remove `"Border"`, assert the sidebar is `Dock`, assert the
initial page is `"Button"`, and change the index-1 selection expectation to
`"Canvas"`. In `GalleryRenderingTests`, replace page-number selections with the
existing `IndexOf` helper where the page is named, change `Find<Border>` to
`Find<Dock>`, and replace
`screen.Count("Border").ShouldBeGreaterThanOrEqualTo(2)` with
`screen.Text.ShouldContain("Button")`. Change the tmux Down count from 17 to 16
because Text moves one additional catalog position earlier.

- [ ] **Step 5: Build + verify**

Run:

```bash
rg -n "new Border\b|ShouldBeOfType<Border>|Find<Border>|\bBorder (border|chip|cover|frame|surface|sidebar)\b" src tests --glob '*.cs'
dotnet build SharpVision.slnx --configuration Release --no-incremental
dotnet test --project tests/SharpVision.Tests --filter-class "*IntrinsicBorderTests|*AmbiguousWidthControlTests|*PopupTests|*DisplayPanelTests" --timeout 300s
dotnet test --project tests/SharpVision.Showcase.Tests --timeout 300s
```

Expected: the wrapper-type search is silent; build and focused suites pass.
`ColorRole.Border`, `ThemeColors.Border`, and `BorderGlyphs` remain valid.

- [ ] **Step 6: Commit**

```bash
git commit -m "refactor: remove Border control; border is intrinsic via BorderThickness" -- \
  src/SharpVision/Controls/Border.cs \
  src/SharpVision.Showcase/Gallery.cs \
  src/SharpVision.Showcase/Panes/BorderPane.cs \
  src/SharpVision.Showcase/Panes/CanvasPane.cs \
  src/SharpVision.Showcase/Panes/Doc.cs \
  src/SharpVision.Showcase/Panes/DockPane.cs \
  src/SharpVision.Showcase/Panes/GridPane.cs \
  src/SharpVision.Showcase/Panes/MenuPane.cs \
  src/SharpVision.Showcase/Panes/OverlayPane.cs \
  src/SharpVision.Showcase/Panes/RadioButtonPane.cs \
  src/SharpVision.Showcase/Panes/RichTextPane.cs \
  src/SharpVision.Showcase/Panes/StackPane.cs \
  src/SharpVision.Showcase/Panes/ThemingPane.cs \
  src/SharpVision.Showcase/Panes/WindowPane.cs \
  tests/SharpVision.Tests/Controls/BorderTests.cs \
  tests/SharpVision.Tests/Controls/IntrinsicBorderTests.cs \
  tests/SharpVision.Tests/Controls/AmbiguousWidthControlTests.cs \
  tests/SharpVision.Tests/Controls/PopupTests.cs \
  tests/SharpVision.Tests/Integration/DisplayPanelTests.cs \
  tests/SharpVision.Tests/Performance/DisplayPanelPerformanceTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs \
  tests/SharpVision.Showcase.Tests/ThemeSwatchLiveThemeTests.cs \
  tests/SharpVision.Showcase.Tests/TmuxSmokeTests.cs
```

---

## Task 5: Docs, `AGENTS.md`, and quality gate

**Files:**

- Modify: `docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md`,
  `docs/concepts/layout.md`, `docs/concepts/styling.md`,
  `docs/controls/control.md`, `docs/controls/index.md`,
  `docs/controls/input/button.md`, `docs/architecture/rendering-pipeline.md`,
  `docs/architecture/memory-ownership.md`,
  `docs/testing/controls-integration.md`, `docs/testing/showcase.md`, and
  `AGENTS.md`.
- Delete: `docs/controls/display/border.md` and
  `docs/controls/display/shadow.md`.

- [ ] **Step 1: Update docs**

- Correct the sibling design spec: `Button` removes its padding class default;
  `OnPressedChanged` remains intentional; wrapper-default shadow offset/dim
  styling are preserved explicitly; ordinary containers provide distinct
  intrinsic frame nodes for custom-rendering leaves.
- `layout.md`: width/height describe the border box; measure/arrange remove
  margin, then border, then padding; border edges reserve cells and saturation
  prevents negative geometry.
- `styling.md` and `control.md`: document the full intrinsic property surface,
  defaults, validation, invalidation, layout, rendering, visual overflow,
  clipping, and hit-testing. State that a custom `OnRender` calls `RenderChrome`
  when it opts into chrome.
- `button.md`: remove the inaccurate default-internal-padding claim and document
  the preserved one-cell border inset.
- Remove the retired control specs and catalog entries. Update rendering,
  memory, integration-test, and showcase-test docs so they no longer name
  `Border`/`Shadow` as concrete controls or require a dedicated Shadow page.
- `AGENTS.md`: add that border and shadow are intrinsic `Control` properties and
  there are no dedicated `Border`/`Shadow` controls.

- [ ] **Step 2: Validate documentation and retired names**

```bash
rg -n "display/border|display/shadow|new Border|new Shadow|Border control|Shadow control" docs AGENTS.md
npm run lint:markdown
npm run lint:links
npm run test:docs
```

Expected: no retired API link/example remains and every documentation command
passes. Remaining “border” and “shadow” text describes intrinsic chrome, colors,
or historical migration context.

- [ ] **Step 3: Commit**

```bash
git commit -m "docs: intrinsic border/shadow; remove Border/Shadow control docs" -- \
  AGENTS.md \
  docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md \
  docs/concepts/layout.md \
  docs/concepts/styling.md \
  docs/controls/control.md \
  docs/controls/index.md \
  docs/controls/input/button.md \
  docs/controls/display/border.md \
  docs/controls/display/shadow.md \
  docs/architecture/rendering-pipeline.md \
  docs/architecture/memory-ownership.md \
  docs/testing/controls-integration.md \
  docs/testing/showcase.md
```

- [ ] **Step 4: Run the full isolated-worktree gate**

```bash
make format
make lint
make build
make test
git diff --check
```

Expected: every command exits zero, build reports zero warnings and zero errors,
test discovery meets the configured minimum, Markdown/link checks pass, and
`git diff --check` is silent. Inspect formatting changes before committing them;
do not absorb unrelated paths.

---

## Self-Review

**Spec coverage:** Decision 1 (delete Shadow) → Task 3; Decision 2 (delete
Border) → Task 4; Decision 3 (base reserves BorderThickness) → Task 1; Decision
4 (reserve exactly once) → Task 1's atomic base/Button/temporary-Border change
plus Task 2's pressed-content proof; Decision 5 (idiom) → the explicit `Dock`
frame migration in Task 4; Decision 6 (showcase pages removed) → Tasks 3 and 4.
Normative docs and full gates are Task 5.

**Defects corrected:** the plan removes Button's redundant padding class
default, preserves the Shadow wrapper's offset and dim appearance explicitly,
keeps `OnPressedChanged`'s intentional immediate arrangement, prevents a red
double-reserving Border commit, enumerates the missed production/test call
sites, updates both showcase index shifts, and runs all gates in an isolated
worktree.

**Placeholder scan:** No implementation step contains a conditional edit,
inferred commit path list, or unspecified migration. The pre-deletion searches
are drift guards, not substitutes for the enumerated call sites and exact
mechanical conversions.

**Type consistency:**
`BorderThickness`/`BorderGlyphs`/`HasShadow`/`ShadowMode`/`ShadowOffset`/`ShadowGlyph`/`ShadowAttributes`,
`Thickness.Horizontal`/`Vertical`/`Deflate`, `LayoutProbe`/`ProbeControl`, and
the `Padding.Horizontal + BorderThickness.Horizontal` inset are consistent
across tasks and match the current code (`Control.cs:591,1104,1345-1365`).

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between
   tasks.
2. **Inline Execution** — execute tasks in this session with checkpoints.

Use the subagent-driven option from an isolated `codex/intrinsic-border-shadow`
worktree based on `codex/runtime-protocol-router`. Do not begin while any target
path has uncommitted changes in that worktree.
