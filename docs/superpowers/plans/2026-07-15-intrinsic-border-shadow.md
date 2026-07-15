# Intrinsic Border and Shadow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the `Border` and `Shadow` wrapper controls and make border and
shadow intrinsic `Control` capabilities while preserving intended Button,
Window, showcase, and terminal-cell behavior and correcting Button's latent
pressed-content double inset.

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

**Execution prerequisite:** Continue in the current component-architecture
worktree from the verified Foundation tip. Begin only after the
[component-foundation plan](2026-07-15-component-foundation.md) is committed and
its complete repository gates pass. The prerequisite plan and gates define the
base; no branch name or commit hash is part of this contract.

## Global Constraints

- .NET 10 / C# 14; file-scoped namespaces; `var` for locals in production and
  tests; `using` after `namespace`.
- One public/named type per file, named exactly after the type (incl. test
  helpers). No nested named types. No primary constructors / positional records.
- XML docs on every public/internal type and member and every thrown exception.
  Validate public arguments before mutating state.
- Property changes invalidate only the required phase; the border reservation is
  a pure layout change and must leave zero-border layout **byte-for-byte
  unchanged** (regression).
- Border/shadow rendering (`ControlChrome.Render`, `RenderChrome`,
  `VisualBounds`, `ContentBounds`) is unchanged. A control that overrides
  `OnRender` without calling `RenderChrome` reserves intrinsic border layout but
  does not draw that chrome. Call `RenderChrome` from a custom renderer when the
  chrome belongs to that control; use an ordinary chrome-rendering container
  such as `Dock` when migration must preserve a distinct styling, layout,
  ownership, or event-routing node.
- `Button` currently gets its one-cell content inset from its `Padding = 1`
  class default. Remove that default in the same task that makes the base
  reserve its one-cell border; keep `OnPressedChanged`'s immediate
  pressed-content arrangement.
- The removed `Shadow` wrapper defaults to offset `(2, 1)` and dim attributes.
  Preserve both explicitly when porting its behavior. Moving `HasShadow` onto an
  arbitrary child is not equivalent when that child owns custom rendering,
  margins, different styling, or different bounds; retain an ordinary
  chrome-rendering container in those cases.
- Keep the surviving `Border` wrapper green until deletion by removing its
  private border reservation in the same task as the base layout change.
- Keep both compatibility wrappers until all non-wrapper call sites and ported
  intrinsic tests compile and pass. Delete each wrapper only in its own final,
  atomic retirement slice.
- Commit only intentional files with explicit pathspecs. Never use `git add -A`,
  `git add .`, destructive restore/checkout/reset, or force operations.
- Quality gate before each commit: clean Release build plus the task's focused
  existing and new tests. The final plan gate is `make format`, `make lint`,
  `make build`, and `make test`.

## Canonical inset

The canonical content inset is border **then** padding, exactly as
`Control.ContentBounds` already composes it:
`Padding.Deflate(BorderThickness.Deflate(Bounds))`. Measure combines the two
axis extents with saturating arithmetic; arrange deflates them sequentially.
This prevents valid near-`int.MaxValue` padding plus a one-cell border from
overflowing while keeping measure and arrange equivalent.

---

## Task 1: Base layout reserves `BorderThickness` and corrects Button press geometry

**Files:**

- Modify: `src/SharpVision/Controls/Control.StyleProperties.cs` (document the
  validated `BorderThickness` setter exception)
- Modify: `src/SharpVision/Controls/Control.cs` (`Control.Arrange`,
  `CreateContentConstraint`, and `ResolveDesiredSize`)
- Modify: `src/SharpVision/Controls/Container.cs` (`OnMeasuredDesired` and the
  AutoSize axis helper must include border as well as padding)
- Modify: `src/SharpVision/Controls/Button.cs` (remove the padding class
  default)
- Modify: `src/SharpVision/Controls/Border.cs` (temporary compatibility until
  Task 3 deletes the type)
- Test: `tests/SharpVision.Tests/Controls/ControlBorderReservationTests.cs`
  (create)
- Test: `tests/SharpVision.Tests/Controls/ButtonTests.cs` (add committed-bounds
  characterization)
- Test: `tests/SharpVision.Tests/Controls/ContainerAutoSizeTests.cs`
- Test: `tests/SharpVision.Tests/Controls/ContainerScrollGeometryTests.cs`
- Test: `tests/SharpVision.Tests/Controls/TextInputTests.cs` (editor, caret, and
  private rail reserve-once regressions)
- Test: `tests/SharpVision.Tests/Styling/StylePropertyTests.cs` (local change
  impact and equivalent-assignment no-op)
- Test: `tests/SharpVision.Tests/Styling/StyleTests.cs`
- Test: `tests/SharpVision.Consumer.Tests/ExternalContractTests.cs`
- Modify: `docs/concepts/layout.md`, `docs/controls/control.md`, and
  `docs/controls/input/button.md`
- Modify: `docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md` and
  `docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md`

**Interfaces:**

- Consumes: `Thickness.Horizontal`/`Vertical` (int), `Thickness.Deflate(Rect)`,
  the existing saturating integer helpers, and `BorderThickness`/`Padding` on
  `Control`.
- Produces: after this task, any control with `BorderThickness != default`
  insets its content (measure + arrange) by border+padding; zero-border controls
  are unchanged. `Container.AutoSize`, intrinsic scrolling, theme-resolved
  border thickness, and externally derived containers obey the same contract.

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
        var child = new ProbeControl(new Size(4, 2))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var container = new LayoutProbe
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(1),
        };
        container.Children.Add(child);

        new Engine().Layout(container, new Size(20, 10));

        // Child arranged inside the 1-cell border on all edges.
        child.Bounds.ShouldBe(new Rect(1, 1, 18, 8));
    }

    /// <summary>Verifies a bordered container's measured desired size includes the border.</summary>
    [Fact]
    public void Measure_WhenContainerHasBorder_DesiredSizeIncludesBorder()
    {
        var child = new ProbeControl(new Size(4, 2))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var container = new LayoutProbe() { BorderThickness = new Thickness(1) };
        container.Children.Add(child);

        container.Measure(new Constraint(width: null, height: null));

        // content (4,2) + 1-cell border on each edge.
        container.DesiredSize.ShouldBe(new Size(6, 4));
    }

    /// <summary>Verifies a zero-border container's layout is unchanged (regression).</summary>
    [Fact]
    public void Arrange_WhenNoBorder_LeavesChildAtFullSlot()
    {
        var child = new ProbeControl(new Size(4, 2))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var container = new LayoutProbe
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        container.Children.Add(child);

        new Engine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(0, 0, 20, 10));
    }

    /// <summary>Verifies independently active edges reserve only their physical sides.</summary>
    [Fact]
    public void Arrange_WhenBorderEdgesArePartial_ReservesOnlyActiveEdges()
    {
        var child = new ProbeControl(new Size(4, 2))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var container = new LayoutProbe()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(1, 1, 0, 0),
        };
        container.Children.Add(child);

        new Engine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(1, 1, 19, 9));
    }

    /// <summary>Verifies valid extreme padding plus a border saturates instead of overflowing.</summary>
    [Fact]
    public void Measure_WhenCombinedInsetExceedsInteger_SaturatesWithoutThrowing()
    {
        var child = new ProbeControl(new Size(1, 1));
        var container = new LayoutProbe
        {
            Padding = new Thickness(int.MaxValue - 1, 0, 0, 0),
            BorderThickness = new Thickness(1, 0, 1, 0),
        };
        container.Children.Add(child);

        Should.NotThrow(() => container.Measure(new Constraint(10, 10)));

        child.MeasureConstraints[^1].Width.ShouldBe(0);
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
  --filter-class "*ControlBorderReservationTests" --parallel none --timeout 120s
```

Expected: FAIL — `Arrange_WhenContainerHasBorder...` gives `Rect(0,0,20,10)` and
`Measure...` gives `(4,2)` (border not reserved).

- [ ] **Step 3: Add focused AutoSize, scrolling, specialized-control, style,
      theme, and external proofs**

Add these cases before implementation:

```csharp
// ContainerAutoSizeTests.cs
[Fact]
public void AutoSize_WhenBorderAndPaddingAreSet_IncludesCompleteContentInset()
{
    var container = new LayoutProbe
    {
        AutoSize = true,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(1),
    };
    container.Children.Add(new ProbeControl(new Size(5, 3)));

    new Engine().Layout(container, new Size(40, 40));

    container.Bounds.Width.ShouldBe(9);
    container.Bounds.Height.ShouldBe(7);
}

// ContainerScrollGeometryTests.cs
[Fact]
public void Layout_WhenBorderPaddingAndBothBarsArePresent_ContainsViewportAndBars()
{
    var child = new ProbeControl(new Size(20, 10));
    var container = new LayoutProbe
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Both,
        HorizontalBarVisibility = ScrollBarVisibility.Always,
        VerticalBarVisibility = ScrollBarVisibility.Always,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(1),
    };
    container.Children.Add(child);

    new Engine().Layout(container, new Size(10, 6));

    container.Viewport.ShouldBe(new Size(5, 1));
    new Point(child.Bounds.X, child.Bounds.Y).ShouldBe(new Point(2, 2));
    _ = container.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>();
    _ = container.HitTest(new Point(7, 2)).ShouldBeOfType<ScrollBar>();
}

// StyleTests.cs
[Fact]
public async Task Theme_WhenBorderThicknessChanges_ReflowsContentAsync()
{
    await using var dispatcher = Dispatcher.Start();

    await dispatcher.InvokeAsync(() =>
    {
        var style = new ControlStyle<LayoutProbe>();
        style.Set(Control.BorderThicknessProperty, State.Normal, default);
        var theme = new Theme();
        theme.SetStyle(style);
        var child = new ProbeControl(new Size(2, 1))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var container = new LayoutProbe
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        container.Children.Add(child);
        ThemeTestSupport.ApplyTheme(container, theme);
        theme.Changed += (_, args) =>
        {
            ThemeTestSupport.RefreshTheme(container, theme);
            InvalidateThemeDependents(container, args.Impact);
        };
        container.Attach(dispatcher);
        var engine = new Engine();
        engine.Layout(container, new Size(10, 4));
        child.Bounds.ShouldBe(new Rect(0, 0, 10, 4));

        style.Set(Control.BorderThicknessProperty, State.Normal, new Thickness(1));
        engine.Layout(container, new Size(10, 4));

        child.Bounds.ShouldBe(new Rect(1, 1, 8, 2));
    }, TestContext.Current.CancellationToken);
}
```

In `TextInputTests`, add reserve-once regressions that position editor text and
the focused caret one cell inside a configured border and keep its private
vertical rail inside that same border. In `StylePropertyTests`, set a local
`BorderThickness`, assert `ChangeImpact.Measure` requests all layout phases and
publishes once, then assign the equivalent thickness and assert no invalidation
or notification.

In `ExternalContractTests`, add an application-level case using the existing
unfriended `FlowPanel`:

```csharp
[Fact]
public async Task Layout_WhenExternalContainerHasBorder_InsetsOwnedLeavesAsync()
{
    await using var terminal = new ConsumerTerminal();
    terminal.QueueResize(new Dimensions(new Size(20, 4)));
    var first = new Gauge { Value = 7 };
    var second = new Gauge { Value = 100 };
    var panel = new FlowPanel { BorderThickness = new Thickness(1) };
    panel.Children.Add(first);
    panel.Children.Add(second);
    await using var application = new Application(
        panel,
        terminal,
        terminal,
        TerminalOptions.Minimal);

    await application.StartAsync(TestContext.Current.CancellationToken);

    first.Bounds.X.ShouldBe(panel.Bounds.X + 1);
    first.Bounds.Y.ShouldBe(panel.Bounds.Y + 1);
    second.Bounds.Right.ShouldBeLessThanOrEqualTo(panel.Bounds.Right - 1);
    second.Bounds.Bottom.ShouldBeLessThanOrEqualTo(panel.Bounds.Bottom - 1);

    await application.StopAsync(TestContext.Current.CancellationToken);
}
```

This proves the behavior is inherited by a real third-party container without
internal access.

- [ ] **Step 4: Reserve border in the arrange content box**

In `src/SharpVision/Controls/Control.cs`, change `Arrange`'s `padded`
computation:

```csharp
// before:
var padded = Padding.Deflate(bounds);
// after:
var padded = Padding.Deflate(BorderThickness.Deflate(bounds));
```

Leave the following two lines (`ArrangeOverride(ResolveContentSlot(padded));`
and `ArrangeOverlays(padded);`) unchanged — the scroll layer and bar chrome now
operate inside the border, which is correct.

- [ ] **Step 5: Reserve border in the measure content constraint and desired
      size**

In `CreateContentConstraint`, combine border and padding through the existing
saturating helper before resolving each axis:

```csharp
private Constraint CreateContentConstraint(Constraint constraint)
{
    var horizontalInset = SaturatingAdd(Padding.Horizontal, BorderThickness.Horizontal);
    var verticalInset = SaturatingAdd(Padding.Vertical, BorderThickness.Vertical);
    return new Constraint(
        ResolveContentAxis(Width, constraint.Width, Margin.Horizontal, horizontalInset),
        ResolveContentAxis(Height, constraint.Height, Margin.Vertical, verticalInset));
}
```

Use the same saturated values in `ResolveDesiredSize`:

```csharp
private Size ResolveDesiredSize(Constraint constraint, Size content)
{
    var horizontalInset = SaturatingAdd(Padding.Horizontal, BorderThickness.Horizontal);
    var verticalInset = SaturatingAdd(Padding.Vertical, BorderThickness.Vertical);
    return new Size(
        ResolveMeasureAxis(
            Width,
            constraint.Width,
            Margin.Horizontal,
            horizontalInset,
            content.Width,
            MinWidth,
            MaxWidth),
        ResolveMeasureAxis(
            Height,
            constraint.Height,
            Margin.Vertical,
            verticalInset,
            content.Height,
            MinHeight,
            MaxHeight));
}
```

Rename the private `padding` parameters to `inset` so their meaning stays true.
Update the XML summaries for `OnMeasuringContent`, `ResolveContentSlot`,
`ArrangeOverlays`, and `ContentBounds` to say border-and-padding-deflated rather
than padding-only. Add the missing `ArgumentOutOfRangeException` documentation
to the public `BorderThickness` setter because any edge above one cell is
already rejected by its registered validator.

- [ ] **Step 6: Preserve AutoSize border reservation**

`Container.OnMeasuredDesired` replaces the base desired size when `AutoSize` is
enabled, so it must include the same inset explicitly:

```csharp
internal override Size OnMeasuredDesired(Size desired) => !AutoSize
    ? desired
    : new Size(
        AutoSizeAxis(
            ContentExtent.Width,
            Add(Padding.Horizontal, BorderThickness.Horizontal),
            Width,
            MinWidth,
            MaxWidth),
        AutoSizeAxis(
            ContentExtent.Height,
            Add(Padding.Vertical, BorderThickness.Vertical),
            Height,
            MinHeight,
            MaxHeight));
```

Rename the helper's `padding` parameter to `inset`; its existing `long` addition
and `Add` helper keep the calculation saturated.

- [ ] **Step 7: Preserve Button's released inset and correct pressed geometry**

Before changing `Button`, add the following characterization to `ButtonTests.cs`
and run it once. It must pass on the old implementation:

```csharp
/// <summary>Verifies default Button chrome keeps content one cell inside its frame.</summary>
[Fact]
public void Arrange_WhenDefaultChromeIsUsed_PreservesOneCellContentInset()
{
    var label = new ControlText("Go");
    var button = new Button
    {
        Width = Length.Cells(10),
        Height = Length.Cells(3),
        Content = label,
    };

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
released content remains one cell inside the frame. This also corrects an
existing inconsistency: before the base change, `OnPressedChanged` applies
border plus padding to content that released arrangement inset by padding alone,
then shifts it. Add the following regression before changing production code;
the old immediate `(3,3,2,0)` result is the expected RED diagnosis, not behavior
to preserve:

```csharp
/// <summary>Verifies pressed content has immediate and post-layout face parity.</summary>
[Fact]
public void Arrange_WhenPressedWithShadow_TranslatesContentByShadowOffset()
{
    var content = new ProbeControl
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
    var button = new Button
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Width = Length.Cells(6),
        Height = Length.Cells(3),
        Content = content,
    };
    new Engine().Layout(button, new Size(10, 6));
    var released = content.Bounds;

    Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
        Code.Character,
        new Rune(' '),
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press)));

    released.ShouldBe(new Rect(1, 1, 4, 1));
    content.Bounds.ShouldBe(new Rect(2, 2, 4, 1));

    new Engine().Layout(button, new Size(10, 6));

    content.Bounds.ShouldBe(new Rect(2, 2, 4, 1));
}
```

- [ ] **Step 8: Keep the surviving Border wrapper reserved exactly once**

After the base change, run
`BorderTests.Layout_WhenChildHasMarginPaddingAndBorder_ComputesExactBounds` and
observe the expected double-inset failure. Then make `Border` delegate box-model
arithmetic to the base:

```csharp
protected override Size MeasureOverride(Constraint constraint)
{
    var child = Child;

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
Task 3 deletes the complete type.

- [ ] **Step 9: Update the normative box-model documentation**

Update `layout.md` and `control.md` with margin → border → padding ordering,
saturating inset arithmetic, AutoSize/AutoScroll behavior, theme-driven layout
invalidation, and the `RenderChrome` obligation for custom renderers. Update
`button.md` to remove the padding-default claim while preserving the one-cell
frame inset and corrected pressed-face translation. Reconcile the sibling design
spec and this implementation plan with the same correction, actual control
taxonomy, Task 1 file inventory, and verification commands.

- [ ] **Step 10: Run focused tests for the atomic box-model change**

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*ControlBorderReservationTests" "*ButtonTests" "*BorderTests" \
  "*ContainerAutoSizeTests" "*ContainerScrollGeometryTests" "*ControlChromeTests" \
  "*TextInputTests" "*StylePropertyTests" \
  --parallel none --timeout 180s
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*StyleTests" --parallel none --timeout 180s
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Layout" --parallel none --timeout 180s
dotnet test --project tests/SharpVision.Consumer.Tests \
  --filter-class "*ExternalContractTests" --parallel none --timeout 180s
npx prettier --write \
  docs/concepts/layout.md \
  docs/controls/control.md \
  docs/controls/input/button.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md \
  docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md
npx prettier --check \
  docs/concepts/layout.md \
  docs/controls/control.md \
  docs/controls/input/button.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md \
  docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md
dotnet build SharpVision.slnx --configuration Release --no-incremental
```

Expected: all selected tests pass and the Release build reports zero warnings
and zero errors.

- [ ] **Step 11: Commit**

```bash
git add -- \
  src/SharpVision/Controls/Control.StyleProperties.cs \
  src/SharpVision/Controls/Control.cs \
  src/SharpVision/Controls/Container.cs \
  src/SharpVision/Controls/Button.cs \
  src/SharpVision/Controls/Border.cs \
  tests/SharpVision.Tests/Controls/ControlBorderReservationTests.cs \
  tests/SharpVision.Tests/Controls/ButtonTests.cs \
  tests/SharpVision.Tests/Controls/ContainerAutoSizeTests.cs \
  tests/SharpVision.Tests/Controls/ContainerScrollGeometryTests.cs \
  tests/SharpVision.Tests/Controls/TextInputTests.cs \
  tests/SharpVision.Tests/Styling/StylePropertyTests.cs \
  tests/SharpVision.Tests/Styling/StyleTests.cs \
  tests/SharpVision.Consumer.Tests/ExternalContractTests.cs \
  docs/concepts/layout.md \
  docs/controls/control.md \
  docs/controls/input/button.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md \
  docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md
git commit -m "feat(layout): reserve BorderThickness in the base measure/arrange pipeline"
```

---

## Task 2: Port intrinsic shadow behavior, then retire `Shadow`

**Files:**

- Create: `tests/SharpVision.Tests/Controls/IntrinsicShadowTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/AmbiguousWidthControlTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/ControlChromeTests.cs`
- Delete in the retirement slice: `src/SharpVision/Controls/Shadow.cs`,
  `src/SharpVision.Showcase/Panes/ShadowPane.cs`, and
  `tests/SharpVision.Tests/Controls/ShadowTests.cs`
- Modify in the retirement slice: `src/SharpVision/Controls/ShadowMode.cs`,
  `src/SharpVision.Showcase/Gallery.cs`,
  `tests/SharpVision.Showcase.Tests/GalleryTests.cs`,
  `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`, and
  `tests/SharpVision.Showcase.Tests/TmuxSmokeTests.cs`
- Delete in the retirement slice: `docs/controls/display/shadow.md`
- Modify in the retirement slice: `docs/controls/index.md`,
  `docs/controls/input/button.md`, `docs/concepts/styling.md`,
  `docs/architecture/project-structure.md`, `docs/architecture/showcase.md`,
  `docs/testing/showcase.md`, and
  `docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md`

**Interfaces:**

- Consumes:
  `Control.HasShadow`/`ShadowMode`/`ShadowOffset`/`ShadowGlyph`/`ShadowAttributes`
  (all pre-existing).
- Produces: no `Shadow` type, construction, page, or public-control
  documentation. Intrinsic shadows retain explicit styling and geometry.

The behavior port is one compile-green commit while `Shadow` still exists. The
type/page/spec deletion is a second atomic commit after the construction search
is silent outside the three deletion targets.

- [ ] **Step 1: Find every reference**

Run:

```bash
rg -n "new Shadow\b|ShouldBeOfType<Shadow>|Find<Shadow>|class Shadow\b|\bShadow (shadow|CreateArranged)\b" \
  src tests --glob '*.cs'
```

Expected on the current Foundation base: exactly five files — `Shadow.cs`,
`ShadowPane.cs`, `ShadowTests.cs`, `AmbiguousWidthControlTests.cs`, and
`ControlChromeTests.cs`. Re-run immediately before deletion so a newly added
call site cannot escape.

- [ ] **Step 2: Port the Shadow render contract to `IntrinsicShadowTests.cs`**

Read the deleted-target `tests/SharpVision.Tests/Controls/ShadowTests.cs` and
port its render cases (`Render_WhenModeIsBlockGlyph_DrawsTurboVisionFootprint`,
`Render_WhenModeIsComposite_PreservesUnderlyingGlyphs`,
`Render_WhenShadowTouchesWideGlyph_StylesCompleteOwner`,
`Render_WhenOffsetIsNegative_DrawsVisualOverflowWithoutHitTarget`,
`Render_WhenAncestorCanvasClipsShadow_DoesNotEscapeClip`, and
`Layout_WhenChildIsPresent_DoesNotReserveShadowOffset`) to assertions on a
chrome-rendering `LayoutProbe` with `HasShadow = true` and the relevant
intrinsic properties. Do not use `ProbeControl`: it overrides `OnRender` and
deliberately does not call `RenderChrome`. Preserve the removed wrapper defaults
explicitly in the shared fixture:

```csharp
private static LayoutProbe CreateArranged(ShadowMode mode, Point offset)
{
    var control = new LayoutProbe
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

Port the invalid-mode and invalid-glyph cases to the same public properties.
Keep clipping, wide-owner, negative-offset, hit-testing, and block-glyph cell
assertions unchanged.

Do **not** copy the old composite test's `shadow.Background = Color.Indexed(4)`
verbatim. `Shadow.OnRender` drew no body, while inherited `RenderChrome` treats
a resolved background as an opaque body fill. The intrinsic test must instead
prove that existing glyphs outside the body survive, `ShadowAttributes` are
applied, and the body is not shadow-styled. Add a separate assertion for
`ShadowBackground` under the ordinary `ControlChrome` fill contract. This
records the real intrinsic semantics instead of preserving a wrapper-only
accident.

- [ ] **Step 3: Migrate non-showcase `Shadow` usages while the wrapper
      survives**

At this base there is no shipped production construction outside the page that
will be deleted. In `AmbiguousWidthControlTests`, replace the wrapper with a
`LayoutProbe` configured with `HasShadow = true`, block-glyph mode, offset
`(1, 1)`, dim shadow attributes, and a `ProbeControl` child. Preserve its `#`
fallback assertion. In `ControlChromeTests`, replace only the `Shadow`
construction with the same intrinsic surface; leave its `Border` constructions
for Task 3.

If the Step 1 search finds a new construction, first decide whether the wrapper
was a distinct node. Move the properties onto the child only when the child
calls `RenderChrome`, has the same bounds, and did not rely on wrapper styling
or margins. Otherwise replace the wrapper with a chrome-rendering `Dock` and
explicitly preserve `HasShadow = true`, `ShadowOffset = new Point(2, 1)`, and
`ShadowAttributes = Attributes.Dim`.

- [ ] **Step 4: Verify and commit the compile-green behavior port**

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*IntrinsicShadowTests" "*ShadowTests" \
  "*AmbiguousWidthControlTests" "*ControlChromeTests" \
  --parallel none --timeout 180s
dotnet build SharpVision.slnx --configuration Release --no-incremental
git add -- \
  tests/SharpVision.Tests/Controls/IntrinsicShadowTests.cs \
  tests/SharpVision.Tests/Controls/AmbiguousWidthControlTests.cs \
  tests/SharpVision.Tests/Styling/ControlChromeTests.cs
git commit -m "test(controls): port shadow contract to intrinsic chrome"
```

- [ ] **Step 5: Delete the wrapper and its dedicated page atomically**

Delete `Shadow.cs`, `ShadowPane.cs`, and `ShadowTests.cs` with `apply_patch`.
Remove the `ShadowPane` catalog entry and the dedicated Shadow rendering test.
The current catalog has 21 pages, Text at zero-based index 17, and tmux sends 17
Down keys. After removing Shadow it has 20 pages, Text is index 16, and tmux
must derive its Down-key count from Text's resulting catalog index rather than
encode that descriptive number. `GalleryTests` already pins the complete ordered
inventory through `_controls`; do not duplicate numeric count or index guards.
In `TmuxSmokeTests`, instantiate `Gallery`, scan `Pages` for Text with a helper
that fails when the page is absent, and use the returned index for navigation.

Delete the Shadow control spec and update the control index, Button's shared
chrome link, styling and project-structure wrapper claims, showcase
architecture, showcase-testing docs, and the `ShadowMode` API summary in this
same retirement slice.

- [ ] **Step 6: Build and verify retirement**

Run:

```bash
rg -n "new Shadow\b|ShouldBeOfType<Shadow>|Find<Shadow>|class Shadow\b|\bShadow (shadow|CreateArranged)\b" \
  src tests --glob '*.cs'
dotnet build SharpVision.slnx --configuration Release --no-incremental
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*IntrinsicShadowTests" "*AmbiguousWidthControlTests" \
  "*ControlChromeTests" --parallel none --timeout 180s
dotnet test --project tests/SharpVision.Showcase.Tests --parallel none --timeout 300s
npx prettier --write \
  docs/controls/index.md \
  docs/controls/input/button.md \
  docs/concepts/styling.md \
  docs/architecture/project-structure.md \
  docs/architecture/showcase.md \
  docs/testing/showcase.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md
npx prettier --check \
  docs/controls/index.md \
  docs/controls/input/button.md \
  docs/concepts/styling.md \
  docs/architecture/project-structure.md \
  docs/architecture/showcase.md \
  docs/testing/showcase.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md
npm run lint:markdown
npm run lint:links
npm run test:docs
```

Expected: the search is silent; build, both test commands, Markdown lint, link
validation, and documentation tests pass.

- [ ] **Step 7: Commit the atomic retirement**

```bash
git add -- \
  src/SharpVision/Controls/Shadow.cs \
  src/SharpVision/Controls/ShadowMode.cs \
  src/SharpVision.Showcase/Gallery.cs \
  src/SharpVision.Showcase/Panes/ShadowPane.cs \
  tests/SharpVision.Tests/Controls/ShadowTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs \
  tests/SharpVision.Showcase.Tests/TmuxSmokeTests.cs \
  docs/controls/display/shadow.md \
  docs/controls/index.md \
  docs/controls/input/button.md \
  docs/concepts/styling.md \
  docs/architecture/project-structure.md \
  docs/architecture/showcase.md \
  docs/testing/showcase.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md
git commit -m "refactor: remove Shadow control; shadow is intrinsic via HasShadow"
```

---

## Task 3: Migrate border call sites, then retire `Border`

**Files:**

- Create: `tests/SharpVision.Tests/Controls/IntrinsicBorderTests.cs`
- Modify in the contract slice: `src/SharpVision/Controls/Border.cs`,
  `src/SharpVision/Controls/Control.StyleProperties.cs`,
  `tests/SharpVision.Tests/Controls/AmbiguousWidthControlTests.cs`,
  `tests/SharpVision.Tests/Styling/ControlChromeTests.cs`, and
  `docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md`
- Modify in the integration slice:
  `tests/SharpVision.Tests/Controls/PopupTests.cs`,
  `tests/SharpVision.Tests/Integration/DisplayPanelTests.cs`,
  `tests/SharpVision.Tests/Performance/DisplayPanelPerformanceTests.cs`, and
  `tests/SharpVision.Showcase.Tests/ThemeSwatchLiveThemeTests.cs`
- Modify in the showcase slice: `src/SharpVision.Showcase/Gallery.cs` and
  `src/SharpVision.Showcase/Panes/CanvasPane.cs`, `Doc.cs`, `DockPane.cs`,
  `GridPane.cs`, `MenuPane.cs`, `OverlayPane.cs`, `RadioButtonPane.cs`,
  `ShowcasePanel.cs`, `StackPane.cs`, `ThemingPane.cs`, and `WindowPane.cs`
- Modify in the showcase slice:
  `tests/SharpVision.Showcase.Tests/GalleryTests.cs` and
  `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Delete in the retirement slice: `src/SharpVision/Controls/Border.cs`,
  `src/SharpVision.Showcase/Panes/BorderPane.cs`,
  `tests/SharpVision.Tests/Controls/BorderTests.cs`, and
  `docs/controls/display/border.md`
- Modify in the retirement slice: `src/SharpVision.Showcase/Gallery.cs`,
  `scripts/capture-showcase.sh`,
  `tests/SharpVision.Showcase.Tests/GalleryTests.cs`,
  `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`,
  `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs`,
  `docs/controls/index.md`, `docs/concepts/layout.md`,
  `docs/architecture/project-structure.md`, `docs/architecture/showcase.md`,
  `docs/testing/showcase.md`, and
  `docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md`

**Interfaces:**

- Consumes: base border reservation (Task 1);
  `Control.BorderThickness`/`BorderGlyphs`; `ControlChrome.DrawPartialBorder`
  (already used by `RenderChrome`).
- Produces: no `Border` type, construction, page, or public-control
  documentation. `ColorRole.Border`, `ThemeColors.Border`, and
  `Control.BorderGlyphs` remain unchanged.

Task 1 must be committed first. Migrate call sites in compile-green slices while
the compatibility wrapper survives; delete the wrapper only after the type-use
search is silent outside the three retirement targets. A broad `Border` search
also matches the semantic color role, which must remain.

- [ ] **Step 1: Find every Border-control reference**

Run:

```bash
rg -n "new Border\b|ShouldBeOfType<Border>|Find<Border>|class Border\b|[,(][[:space:]]*Border[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*[,)=]|(public|internal|private|protected)([[:space:]]+static)?[[:space:]]+Border[[:space:]]+[A-Za-z_]" \
  src tests --glob '*.cs'
```

Expected on the current tree after Task 2: the `Border` implementation/tests/
page plus every source and test path enumerated above. Re-run before every slice
and immediately before deletion.

- [ ] **Step 2: Port the Border render/layout contract to
      `IntrinsicBorderTests.cs`**

Read the deleted-target `tests/SharpVision.Tests/Controls/BorderTests.cs` and
port its cases to an intrinsic-bordered control (a `LayoutProbe`/`Stack` with
`BorderThickness`/`BorderGlyphs` set):

- `Layout_WhenChildHasMarginPaddingAndBorder_ComputesExactBounds` → assert the
  exact `DesiredSize(8, 7)` and child `Bounds` inset by margin+border+padding
  (this is Task 1's contract at full generality).
- `Render_WhenBorderIsComplete_WritesCornersEdgesAndChild`,
  `Render_WhenEdgesArePartial_UsesOnlyActiveCustomGlyphsAndStyles`,
  `Render_WhenBoundsAreTiny_RemainsContained` → assert the per-side border
  glyphs/corners via `Frame`/`FrameOracle` on the intrinsic-bordered control
  (the default `OnRender` → `RenderChrome` → `ControlChrome.DrawPartialBorder`
  draws them). The partial-edge case must also prove inactive right and bottom
  cells retain no glyph while preserving the configured body background.
- `Glyphs_WhenPresetIsSelected_UsesExactRunes` and
  `BorderThickness_WhenAnEdgeExceedsOne_Throws` → move to
  `BorderGlyphs`/`BorderThickness` property-setter tests on `Control`.
- `Constructor_WhenGlyphIsNotPrintableNarrow_Throws` → retain as an honest
  `Glyphs` constructor-validation test; invalid constructor arguments fail
  before any property setter can run.

`BorderThickness` already validates before mutation. `default(Glyphs)` bypasses
the value type's validating constructor, so register the smallest
`BorderGlyphsProperty` validator that reconstructs the value through `Glyphs`,
rejects that default before mutation, and preserves the previous property value.
Document the public setter's `ArgumentException` contract in XML. The temporary
`Border.Glyphs` forwarding property has the same rejection contract and must
document the same exception until the wrapper is deleted.

Migrate the two remaining `Border` constructions in `ControlChromeTests` and the
border construction in `AmbiguousWidthControlTests` to `LayoutProbe` so their
default `OnRender` exercises intrinsic chrome. Keep `BorderTests` intact until
the final retirement slice, giving both paths overlapping proof.

- [ ] **Step 3: Verify and commit the intrinsic border contract**

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*IntrinsicBorderTests" "*BorderTests" \
  "*AmbiguousWidthControlTests" "*ControlChromeTests" \
  --parallel none --timeout 180s
dotnet build SharpVision.slnx --configuration Release --no-incremental
git add -- \
  src/SharpVision/Controls/Border.cs \
  src/SharpVision/Controls/Control.StyleProperties.cs \
  tests/SharpVision.Tests/Controls/IntrinsicBorderTests.cs \
  tests/SharpVision.Tests/Controls/AmbiguousWidthControlTests.cs \
  tests/SharpVision.Tests/Styling/ControlChromeTests.cs \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md
git commit -m "test(controls): port border contract to intrinsic chrome"
```

- [ ] **Step 4: Migrate integration and theme-swatch usages**

Use an ordinary `Dock` whenever the old wrapper supplied a distinct ownership or
rendering node:

```csharp
var frame = new Dock
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

Use `Dock` for the opaque popup cover, integration display-panel frame,
performance navigation cards, and theme swatch chip. In
`ThemeSwatchLiveThemeTests`, change the helper parameter type and comments from
`Border` to `Dock`/“intrinsic control surface.” Then verify and commit this
independent slice:

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*PopupTests" "*DisplayPanelTests" \
  "*DisplayPanelPerformanceTests" --parallel none --timeout 300s
dotnet test --project tests/SharpVision.Showcase.Tests \
  --filter-class "*ThemeSwatchLiveThemeTests" --parallel none --timeout 180s
dotnet build SharpVision.slnx --configuration Release --no-incremental
git add -- \
  tests/SharpVision.Tests/Controls/PopupTests.cs \
  tests/SharpVision.Tests/Integration/DisplayPanelTests.cs \
  tests/SharpVision.Tests/Performance/DisplayPanelPerformanceTests.cs \
  tests/SharpVision.Showcase.Tests/ThemeSwatchLiveThemeTests.cs
git commit -m "refactor(controls): migrate framed integration surfaces"
```

- [ ] **Step 5: Migrate showcase frames while `Border` survives**

Apply the `Dock` conversion to every listed pane-local `Card`/`Frame`: return
`Dock`, rename `Glyphs =` to `BorderGlyphs =`, and replace `Child = value` with
`Children = { value }`. Convert Gallery's sidebar and unbordered surface now,
but retain the `BorderPane` catalog entry until retirement. Expose
`public Dock Sidebar { get; }` and preserve its width, rounded glyphs, border,
content, and key handler.

In the same compile-green slice, update `GalleryTests` to assert the sidebar is
`Dock`. In `GalleryRenderingTests`, replace the Canvas specimen's `Find<Border>`
with `Find<Dock>`, update its child predicate to the Dock child collection, and
replace numeric Canvas selection with `IndexOf`. These are structural
consequences of the frame migration, not catalog-retirement work.

`ShowcasePanel` is the known custom-renderer audit failure: it sets
`BorderThickness` but its `OnRender` does not call `RenderChrome`. Call
`RenderChrome(canvas)` first, remove the whole-bounds clear that would erase the
frame, and retain its deliberate caption/body drawing. Add a Theming-page frame
assertion in `GalleryRenderingTests`. Audit every other `OnRender` override:

```bash
rg -n "protected override void OnRender|BorderThickness|HasShadow" src --glob '*.cs'
```

Each override that owns intrinsic chrome must either call `RenderChrome`, use a
documented specialized `ControlChrome` path (`Button`/`Window`), or keep the
properties at their zero/false defaults.

Verify and commit the showcase migration while the Border page still compiles:

```bash
dotnet test --project tests/SharpVision.Showcase.Tests --parallel none --timeout 300s
dotnet build SharpVision.slnx --configuration Release --no-incremental
git add -- \
  src/SharpVision.Showcase/Gallery.cs \
  src/SharpVision.Showcase/Panes/CanvasPane.cs \
  src/SharpVision.Showcase/Panes/Doc.cs \
  src/SharpVision.Showcase/Panes/DockPane.cs \
  src/SharpVision.Showcase/Panes/GridPane.cs \
  src/SharpVision.Showcase/Panes/MenuPane.cs \
  src/SharpVision.Showcase/Panes/OverlayPane.cs \
  src/SharpVision.Showcase/Panes/RadioButtonPane.cs \
  src/SharpVision.Showcase/Panes/ShowcasePanel.cs \
  src/SharpVision.Showcase/Panes/StackPane.cs \
  src/SharpVision.Showcase/Panes/ThemingPane.cs \
  src/SharpVision.Showcase/Panes/WindowPane.cs \
  tests/SharpVision.Showcase.Tests/GalleryTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs
git commit -m "refactor(showcase): compose frames with intrinsic border surfaces"
```

- [ ] **Step 6: Delete `Border` and its dedicated page atomically**

Delete `Border.cs`, `BorderPane.cs`, `BorderTests.cs`, and the Border control
spec with `apply_patch`. Remove the Border catalog entry. After Task 2 the
catalog has 20 pages and Text at index 16; after removing Border it has 19
pages, starts on Button, and Text is index 15. The smoke test's catalog-derived
helper automatically navigates to that final index; do not edit `TmuxSmokeTests`
in this slice.

In `GalleryTests`, remove `"Border"`, assert the initial page is `"Button"`, and
change the index-1 selection expectation to `"Canvas"`. In
`GalleryRenderingTests`, remove the Border-page assumptions and replace
`screen.Count("Border").ShouldBeGreaterThanOrEqualTo(2)` with an assertion for
unique Button-page content such as
`screen.Text.ShouldContain("Activation log: waiting")`. Update the control
index, layout and project-structure contracts, showcase architecture and testing
docs, and this execution plan in the same retirement slice.

Update `GalleryInteractionTests` to remove stale Border-era sidebar positions:
locate Button and Canvas entries by catalog name and live bounds, select Canvas
before proving a pointer click returns to Button, and expect Down/Enter on the
second entry to select Canvas.

Make `scripts/capture-showcase.sh` follow the Button-first inventory without
hardcoded sidebar rows: prove Down selects Canvas, return Up to Button before
the hover proof, derive the visible Button and Canvas rows, and use `send_sgr`
for both complete pointer clicks. Keep PNG regeneration deferred to Task 4 after
the final architecture settles.

- [ ] **Step 7: Build and verify retirement**

Run:

```bash
rg -n "new Border\b|ShouldBeOfType<Border>|Find<Border>|class Border\b|[,(][[:space:]]*Border[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*[,)=]|(public|internal|private|protected)([[:space:]]+static)?[[:space:]]+Border[[:space:]]+[A-Za-z_]" \
  src tests --glob '*.cs'
rg -n "\bBorder\b" src tests --glob '*.cs'
rg -n 'display/border|new Border|Border control|Border page|`Border` sidebar|typeof\(Border\)|nameof\(Border\)' \
  docs AGENTS.md src tests --glob '*.md' --glob '*.cs' --glob '!docs/superpowers/**'
bash -n scripts/capture-showcase.sh
dotnet build SharpVision.slnx --configuration Release --no-incremental
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*IntrinsicBorderTests" "*AmbiguousWidthControlTests" \
  "*ControlChromeTests" "*PopupTests" "*DisplayPanelTests" \
  "*DisplayPanelPerformanceTests" --parallel none --timeout 300s
dotnet test --project tests/SharpVision.Showcase.Tests --parallel none --timeout 300s
npx prettier --write \
  docs/controls/index.md \
  docs/concepts/layout.md \
  docs/architecture/project-structure.md \
  docs/architecture/showcase.md \
  docs/testing/showcase.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md
npx prettier --check \
  docs/controls/index.md \
  docs/concepts/layout.md \
  docs/architecture/project-structure.md \
  docs/architecture/showcase.md \
  docs/testing/showcase.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md
```

Expected: the wrapper-type and retired-name searches are silent; build and
focused suites pass. The broad audit may contain only permanent semantic uses
such as `ColorRole.Border`, `ThemeColors.Border`, theme-loader fallback prose,
and border-glyph/property documentation. It must contain no type declaration,
construction, generic type argument, parameter, return type, or local of the
removed wrapper.

- [ ] **Step 8: Commit the atomic retirement**

```bash
git add -- \
  src/SharpVision/Controls/Border.cs \
  src/SharpVision.Showcase/Gallery.cs \
  src/SharpVision.Showcase/Panes/BorderPane.cs \
  scripts/capture-showcase.sh \
  tests/SharpVision.Tests/Controls/BorderTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs \
  docs/controls/display/border.md \
  docs/controls/index.md \
  docs/concepts/layout.md \
  docs/architecture/project-structure.md \
  docs/architecture/showcase.md \
  docs/testing/showcase.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md
git commit -m "refactor: remove Border control; border is intrinsic via BorderThickness"
```

---

## Task 4: Reconcile remaining normative docs and run the quality gate

**Files:**

- Modify: `docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md`,
  `docs/concepts/styling.md`, `docs/concepts/custom-components.md`,
  `docs/controls/control.md`, `docs/controls/index.md`,
  `docs/architecture/rendering-pipeline.md`,
  `docs/architecture/memory-ownership.md`,
  `docs/architecture/project-structure.md`, and
  `docs/testing/controls-integration.md`, `docs/testing/showcase.md`
- Modify: `src/SharpVision/Controls/Control.StyleProperties.cs`,
  `src/SharpVision/Controls/Button.cs`, and `AGENTS.md`
- Modify: `docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md`
- Regenerate: `docs/images/showcase-dashboard.png`
- Verify the already-updated paths from earlier tasks:
  `docs/concepts/layout.md`, `docs/controls/input/button.md`, and
  `docs/architecture/showcase.md`

- [ ] **Step 1: Update docs**

- Correct the sibling design spec: `Button` removes its padding class default;
  `OnPressedChanged` remains intentional; AutoSize and saturating inset
  arithmetic participate; wrapper-default shadow offset/dim styling are
  preserved explicitly; a wrapper-to-child migration is not claimed equivalent
  when a distinct styling/layout node or custom renderer was involved.
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
- `custom-components.md` and `architecture/showcase.md`: replace concrete
  wrapper examples with intrinsic properties or ordinary `Dock` frame nodes.
- `AGENTS.md`: add the locked rule that border and shadow are intrinsic
  `Control` properties (`BorderThickness`, `BorderGlyphs`, `HasShadow`, and the
  related style properties), there are no `Border` or `Shadow` wrapper types, an
  ordinary container supplies a distinct chrome node when needed, and a custom
  `OnRender` calls `RenderChrome` when it opts into intrinsic chrome.
- Complete the public XML exception contract for validated attributes,
  `ShadowMode`, `ShadowGlyph`, and `Button.Glyphs`; focused API tests must
  retain validation-before-mutation behavior.
- After the final architecture and gallery behavior settle, run
  `scripts/capture-showcase.sh docs/images/showcase-dashboard.png` to regenerate
  the checked-in Release dashboard capture and visually verify the 19-page
  Button-first sidebar contains no retired Border or Shadow entry.

- [ ] **Step 2: Validate documentation and retired names**

```bash
rg -n "new Border\b|new Shadow\b|class Border\b|class Shadow\b|ShouldBeOfType<(Border|Shadow)>|Find<(Border|Shadow)>" \
  src tests --glob '*.cs'
rg -n "display/border|display/shadow|new Border|new Shadow|Border control|Shadow control" \
  docs AGENTS.md --glob '*.md' --glob '!docs/superpowers/**'
scripts/capture-showcase.sh docs/images/showcase-dashboard.png
file docs/images/showcase-dashboard.png
npx prettier --write \
  AGENTS.md \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md \
  docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md \
  docs/concepts/styling.md \
  docs/concepts/custom-components.md \
  docs/controls/control.md \
  docs/controls/index.md \
  docs/architecture/rendering-pipeline.md \
  docs/architecture/memory-ownership.md \
  docs/architecture/project-structure.md \
  docs/testing/controls-integration.md \
  docs/testing/showcase.md
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*IntrinsicBorderTests" "*IntrinsicShadowTests" \
  "*ControlChromeTests" "*StylePropertyTests" "*ButtonTests" \
  --parallel none --timeout 300s
dotnet test --project tests/SharpVision.Consumer.Tests \
  --filter-class "*ExternalContractTests" --parallel none --timeout 300s
npm run format:check
npm run lint:markdown
npm run lint:links
npm run test:docs
```

Expected: no retired API link/example remains and every documentation command
passes. Remaining “border” and “shadow” text describes intrinsic chrome, colors,
or historical migration context.

- [ ] **Step 3: Commit**

```bash
git add -- \
  AGENTS.md \
  src/SharpVision/Controls/Control.StyleProperties.cs \
  src/SharpVision/Controls/Button.cs \
  docs/superpowers/plans/2026-07-15-intrinsic-border-shadow.md \
  docs/superpowers/specs/2026-07-15-intrinsic-border-shadow-design.md \
  docs/concepts/styling.md \
  docs/concepts/custom-components.md \
  docs/controls/control.md \
  docs/controls/index.md \
  docs/architecture/rendering-pipeline.md \
  docs/architecture/memory-ownership.md \
  docs/architecture/project-structure.md \
  docs/testing/controls-integration.md \
  docs/testing/showcase.md \
  docs/images/showcase-dashboard.png
git commit -m "docs: reconcile intrinsic border and shadow contracts"
```

- [ ] **Step 4: Run the full plan gate**

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

**Spec coverage:** Decision 1 (delete Shadow) → Task 2; Decision 2 (delete
Border) → Task 3; Decision 3 (base reserves BorderThickness) → Task 1; Decision
4 (reserve exactly once) → Task 1's atomic base/Button/temporary-Border change,
including immediate and post-layout pressed-content parity; Decision 5 (idiom) →
the explicit `Dock` frame migration in Task 3; Decision 6 (showcase pages
removed) → Tasks 2 and 3. Normative docs and full gates are Task 4.

**Defects corrected:** the plan removes Button's redundant padding class
default, includes AutoSize and saturating inset arithmetic, preserves the Shadow
wrapper's offset and dim appearance explicitly without claiming arbitrary child
equivalence, keeps `OnPressedChanged`'s intentional immediate arrangement,
prevents a red double-reserving Border commit, covers `ControlChromeTests` and
the custom-rendering `ShowcasePanel`, enumerates current source/test/doc paths,
derives both showcase index shifts, and keeps every migration slice compile
green before atomic wrapper retirement.

**Placeholder scan:** No implementation step contains a conditional edit,
inferred commit path list, or unspecified migration. The pre-deletion searches
are drift guards, not substitutes for the enumerated call sites and exact
mechanical conversions.

**Type consistency:**
`BorderThickness`/`BorderGlyphs`/`HasShadow`/`ShadowMode`/`ShadowOffset`/`ShadowGlyph`/`ShadowAttributes`,
`Thickness.Horizontal`/`Vertical`/`Deflate`, `LayoutProbe`/`ProbeControl`, and
the saturated border-plus-padding inset are consistent across tasks and match
the current Foundation API.

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between
   tasks.
2. **Inline Execution** — execute tasks in this session with checkpoints.

Use the subagent-driven option in the current component-architecture worktree,
starting from the committed Foundation result after its complete gates pass. Do
not create a separate branch or worktree that would bypass that dependency.
Before each task, confirm its target paths have no uncommitted changes from
another active slice.
