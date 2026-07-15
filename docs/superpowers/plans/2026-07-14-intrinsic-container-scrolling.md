# Intrinsic Container Scrolling Implementation Plan

<!-- markdownlint-disable MD013 -->

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make overflow scrolling and grow/shrink intrinsic, opt-in capabilities
of `Container` (WinForms/Delphi model), then delete `ScrollView` and refactor
`List`/`Table` onto the base mechanism.

**Architecture:** Hoist `ScrollView`'s proven engine (reservation probe, offset
translation, viewport clip, two owned `ScrollBar` chrome controls, input) into
the `Control`/`Container` boundary behind small internal virtual seams, so each
container's existing `MeasureOverride`/`ArrangeOverride`/`OnRender` is wrapped
transparently. Arming is per instance via `AutoScroll`; the
eligible/measured-unbounded axes are chosen by `ScrollBars` (default
`Vertical`). Keep the build green after every task.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly. Layout via
`Engine().Layout(root, size)`; rendering asserted with `Frame` + `FrameOracle`.

**Spec:**
`docs/superpowers/specs/2026-07-14-intrinsic-container-scrolling-design.md`.

**Base commit:** `2beda39`. Branch: `codex/runtime-protocol-router` (shared — a
concurrent showcase effort touches `SharpVision.Showcase`; coordinate on Phase
4).

## Global Constraints

- .NET 10 / C# 14; file-scoped namespaces; `var` for locals; `using` after
  `namespace`; shared imports in each project's `GlobalUsings.cs`.
- **One public/named type per file, named after the type (incl. enums,
  delegates, test helpers)** — enforced by `make lint`. No nested named types;
  no two types per file.
- No primary constructors / positional records. Declare every constructor
  explicitly and validate arguments before assigning state. XML docs on every
  public/internal type and member and every thrown exception.
- Validate every public argument before mutating observable state. Use
  `Debug.Assert` only for post-validation invariants.
- Property changes invalidate only the required phase via
  `Set(ref field, value, Invalidation.X)` /
  `NotifyChanged(name, Invalidation.X)`. `Invalidation` is
  `None|Render|Arrange|Measure|All`.
- All mutation is dispatcher-affine; setters/commands call `VerifyMutable()` (or
  `Set`, which verifies).
- Quality gate before every commit: `make format && make lint && make build`,
  plus the task's focused tests. `make test` must stay at/above its configured
  minimum discovered-test count.
- KNOWN pre-existing flaky test: `Integration/ScrollingTests` errors ~1 run in 3
  (unrelated to render logic). If a full run shows exactly one error there in an
  unchanged file, re-run once.
- Focused test command shape:
  `dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`.

## New internal seams on `Control` (used by every task)

These are added in Task 2 and Task 5 and consumed throughout. Signatures are
fixed here so tasks agree:

```csharp
// Control.cs — layout seams. Defaults preserve today's behavior exactly.
internal Size ContentExtent { get; private set; }                       // natural content size captured in Measure
internal virtual Constraint OnMeasuringContent(Constraint content) => content;   // Container nulls eligible axes
internal virtual Size OnMeasuredDesired(Size desired) => desired;                // Container adds Always-bar reserve + GrowOnly floor
internal virtual Rect ResolveContentSlot(Rect padded) => padded;                 // Container returns extent-sized, offset slot
internal virtual void ArrangeOverlays(Rect padded) { }                           // Container arranges bar chrome
internal virtual bool ShrinkWrapsWidth => false;                                 // Container returns AutoSize
internal virtual bool ShrinkWrapsHeight => false;                                // Container returns AutoSize
internal virtual void RenderContent(TerminalCanvas canvas) => RenderChildren(canvas); // child-iteration seam
```

---

## Phase 1 — Extent capture and grow/shrink (no bars)

### Task 1: `AutoSizeMode` enum

**Files:**

- Create: `src/SharpVision/Layout/AutoSizeMode.cs`
- Test: (covered by Task 4; enum alone needs no test)

**Interfaces:**

- Produces: `enum SharpVision.Layout.AutoSizeMode { GrowAndShrink, GrowOnly }`

- [ ] **Step 1: Create the enum**

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Selects whether an auto-sizing container may shrink below its explicit size.</summary>
public enum AutoSizeMode
{
    /// <summary>Fit the container's border box exactly to its content on each auto-sized axis.</summary>
    GrowAndShrink,

    /// <summary>Grow to content but never shrink below the explicit fixed-cell size on that axis.</summary>
    GrowOnly,
}
```

- [ ] **Step 2: Build**

Run: `make build` Expected: success, zero warnings.

- [ ] **Step 3: Commit**

```bash
git add src/SharpVision/Layout/AutoSizeMode.cs
git commit -m "feat(layout): add AutoSizeMode for container grow/shrink"
```

### Task 2: Capture the natural content extent

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs` (the `Measure` body near line
  485; add `ContentExtent` and the `OnMeasuringContent`/`OnMeasuredDesired`
  seams)
- Test: `tests/SharpVision.Tests/Controls/ControlExtentTests.cs`

**Interfaces:**

- Produces: `Control.ContentExtent` (internal Size);
  `Control.OnMeasuringContent(Constraint)`, `Control.OnMeasuredDesired(Size)`
  internal virtuals (defaults identity).

- [ ] **Step 1: Write the failing test**

`ContentExtent` must hold the unclamped `MeasureOverride` result even when
`DesiredSize` is clamped to a smaller constraint.

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the natural content extent captured during measure.</summary>
public sealed class ControlExtentTests
{
    /// <summary>Verifies the extent keeps the natural size when desired size is clamped smaller.</summary>
    [Fact]
    public void ContentExtent_WhenConstraintClampsDesired_KeepsNaturalSize()
    {
        ProbeControl probe = new(new Size(20, 40));

        probe.Measure(new Constraint(10, 12));

        probe.DesiredSize.ShouldBe(new Size(10, 12));
        probe.ExposedContentExtent.ShouldBe(new Size(20, 40));
    }
}
```

Add a test-only accessor on `ProbeControl` (it lives in
`SharpVision.Tests.Support`; the test assembly has `InternalsVisibleTo`, so
expose the internal via a helper):

```csharp
// ProbeControl.cs — add:
/// <summary>Gets the natural content extent captured by the base measure.</summary>
internal Size ExposedContentExtent => ContentExtent;
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ControlExtentTests" --timeout 120s`
Expected: FAIL — `ContentExtent`/`ExposedContentExtent` do not exist.

- [ ] **Step 3: Add the property and seams, and capture in `Measure`**

In `Control.cs`, add near `DesiredSize` (around line 300):

```csharp
/// <summary>Gets the natural content size from the last measure, before outer-constraint clamping.</summary>
/// <remarks>Equals <see cref="MeasureOverride"/>'s result. Scrollable containers compare it against the arranged viewport.</remarks>
internal Size ContentExtent { get; private set; }
```

Add the seams beside `MeasureOverride` (around line 888):

```csharp
/// <summary>Adjusts the content constraint before content measurement. Default returns it unchanged.</summary>
/// <param name="content">The padding-deflated content constraint.</param>
/// <returns>The constraint passed to <see cref="MeasureOverride"/>.</returns>
internal virtual Constraint OnMeasuringContent(Constraint content) => content;

/// <summary>Adjusts the resolved desired size after content measurement. Default returns it unchanged.</summary>
/// <param name="desired">The border-box desired size.</param>
/// <returns>The committed desired size.</returns>
internal virtual Size OnMeasuredDesired(Size desired) => desired;
```

In `Measure` (lines 485-487) replace:

```csharp
Constraint contentConstraint = CreateContentConstraint(constraint);
Size content = MeasureOverride(contentConstraint);
Size desired = ResolveDesiredSize(constraint, content);
```

with:

```csharp
Constraint contentConstraint = OnMeasuringContent(CreateContentConstraint(constraint));
Size content = MeasureOverride(contentConstraint);
ContentExtent = content;
Size desired = OnMeasuredDesired(ResolveDesiredSize(constraint, content));
```

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ControlExtentTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 5: Regression — full layout suite still green**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Layout" --timeout 180s`
Expected: PASS (seams default to identity, so nothing changes).

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision/Controls/Control.cs tests/SharpVision.Tests/Support/ProbeControl.cs tests/SharpVision.Tests/Controls/ControlExtentTests.cs
git commit -m "feat(layout): capture natural content extent and add measure seams"
```

### Task 3: Grow/shrink arrange seam (`ShrinkWraps*`)

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs` (`Arrange`, lines 552-571; add
  `ShrinkWrapsWidth`/`ShrinkWrapsHeight`)
- Test: `tests/SharpVision.Tests/Controls/ControlShrinkWrapTests.cs`

**Interfaces:**

- Produces: `Control.ShrinkWrapsWidth`, `Control.ShrinkWrapsHeight` internal
  virtuals (default `false`).
- Consumes: nothing new.

- [ ] **Step 1: Write the failing test**

A control that shrink-wraps width must size to its content width even when it is
`Stretch`-aligned in a larger slot.

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the shrink-wrap arrange seam overrides stretch.</summary>
public sealed class ControlShrinkWrapTests
{
    /// <summary>Verifies a shrink-wrapping control ignores stretch and sizes to content.</summary>
    [Fact]
    public void Arrange_WhenShrinkWrapsWidth_SizesToContentDespiteStretch()
    {
        ShrinkProbe probe = new(new Size(6, 2)) { HorizontalAlignment = HorizontalAlignment.Stretch };

        probe.Measure(new Constraint(20, 20));
        probe.Arrange(new Rect(0, 0, 20, 20));

        probe.Bounds.Width.ShouldBe(6);
    }
}
```

Add the test-only subclass `tests/SharpVision.Tests/Support/ShrinkProbe.cs`:

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>A probe that shrink-wraps its width for arrange-seam tests.</summary>
internal sealed class ShrinkProbe: Control
{
    private readonly Size _intrinsic;

    /// <summary>Initializes the probe with one intrinsic size.</summary>
    /// <param name="intrinsic">The non-negative intrinsic content size.</param>
    internal ShrinkProbe(Size intrinsic) => _intrinsic = intrinsic;

    /// <inheritdoc/>
    internal override bool ShrinkWrapsWidth => true;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => _intrinsic;
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ControlShrinkWrapTests" --timeout 120s`
Expected: FAIL — width is 20 (stretched).

- [ ] **Step 3: Add the seams and consult them in `Arrange`**

Add beside the other seams in `Control.cs`:

```csharp
/// <summary>Gets whether this control sizes its width to content, overriding stretch. Default false.</summary>
internal virtual bool ShrinkWrapsWidth => false;

/// <summary>Gets whether this control sizes its height to content, overriding stretch. Default false.</summary>
internal virtual bool ShrinkWrapsHeight => false;
```

In `Arrange` (lines 552-571), change the two `stretch` arguments so shrink-wrap
forces non-stretch:

```csharp
int width = widthResolved
    ? available.Width
    : ResolveArrangeAxis(
        Width,
        HorizontalAlignment == HorizontalAlignment.Stretch && !ShrinkWrapsWidth,
        slot.Width,
        available.Width,
        DesiredSize.Width,
        MinWidth,
        MaxWidth);
int height = heightResolved
    ? available.Height
    : ResolveArrangeAxis(
        Height,
        VerticalAlignment == VerticalAlignment.Stretch && !ShrinkWrapsHeight,
        slot.Height,
        available.Height,
        DesiredSize.Height,
        MinHeight,
        MaxHeight);
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ControlShrinkWrapTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Controls/Control.cs tests/SharpVision.Tests/Support/ShrinkProbe.cs tests/SharpVision.Tests/Controls/ControlShrinkWrapTests.cs
git commit -m "feat(layout): add shrink-wrap arrange seam for auto-size"
```

### Task 4: `AutoSize` / `AutoSizeMode` on `Container`

**Files:**

- Modify: `src/SharpVision/Controls/Container.cs`
- Test: `tests/SharpVision.Tests/Controls/ContainerAutoSizeTests.cs`

**Interfaces:**

- Consumes: `ShrinkWrapsWidth`/`ShrinkWrapsHeight` (Task 3), `OnMeasuredDesired`
  (Task 2).
- Produces: `Container.AutoSize` (bool, default false), `Container.AutoSizeMode`
  (default `GrowAndShrink`).

- [ ] **Step 1: Write the failing tests**

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Tests.Support;

/// <summary>Verifies AutoSize grow/shrink on a container.</summary>
public sealed class ContainerAutoSizeTests
{
    /// <summary>Verifies AutoSize shrink-wraps a stretched container to its content.</summary>
    [Fact]
    public void AutoSize_WhenStretchedSlot_SizesToContent()
    {
        ProbeContainer container = new() { AutoSize = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        container.Children.Add(new ProbeControl(new Size(5, 3)) { HorizontalAlignment = HorizontalAlignment.Left });

        new Engine().Layout(container, new Size(40, 40));

        container.Bounds.Width.ShouldBe(5);
        container.Bounds.Height.ShouldBe(3);
    }

    /// <summary>Verifies GrowOnly keeps the explicit fixed width as a floor.</summary>
    [Fact]
    public void AutoSizeGrowOnly_WhenContentSmallerThanFixedWidth_KeepsFixedWidth()
    {
        ProbeContainer container = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Width = Length.Cells(10),
        };
        container.Children.Add(new ProbeControl(new Size(4, 2)));

        new Engine().Layout(container, new Size(40, 40));

        container.Bounds.Width.ShouldBe(10);
    }
}
```

`ProbeContainer` needs an `AutoSize` passthrough — it already derives from
`Container`, so once the properties exist the test compiles. `ProbeContainer`
measures children with a default `MeasureOverride`? It does not override it, so
add a minimal children-union `MeasureOverride` to `ProbeContainer`:

```csharp
// ProbeContainer.cs — add:
/// <inheritdoc/>
protected override Size MeasureOverride(Constraint constraint)
{
    int width = 0;
    int height = 0;

    foreach (Control child in Children)
    {
        child.Measure(constraint);
        width = Math.Max(width, child.DesiredSize.Width);
        height = Math.Max(height, child.DesiredSize.Height);
    }

    return new Size(width, height);
}

/// <inheritdoc/>
protected override void ArrangeOverride(Rect bounds)
{
    foreach (Control child in Children)
    {
        child.Arrange(bounds);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerAutoSizeTests" --timeout 120s`
Expected: FAIL — `AutoSize`/`AutoSizeMode` do not exist.

- [ ] **Step 3: Implement `AutoSize`/`AutoSizeMode` on `Container`**

Add to `Container.cs` (in a `#region Grow and shrink`):

```csharp
/// <summary>Gets or sets whether this container sizes its border box to its content, overriding stretch and star sizing.</summary>
/// <remarks>Honors <see cref="Control.MinWidth"/>/<see cref="Control.MaxWidth"/> and the height equivalents. See <see cref="AutoSizeMode"/>.</remarks>
/// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
/// <exception cref="ObjectDisposedException">The container is disposed.</exception>
public bool AutoSize
{
    get;
    set => _ = Set(ref field, value, Invalidation.Measure);
}

/// <summary>Gets or sets whether an auto-sizing axis may shrink below its explicit fixed-cell size.</summary>
/// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
/// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
/// <exception cref="ObjectDisposedException">The container is disposed.</exception>
public AutoSizeMode AutoSizeMode
{
    get;
    set
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The auto-size mode is unknown.");
        }

        _ = Set(ref field, value, Invalidation.Measure);
    }
} = AutoSizeMode.GrowAndShrink;

/// <inheritdoc/>
internal override bool ShrinkWrapsWidth => AutoSize;

/// <inheritdoc/>
internal override bool ShrinkWrapsHeight => AutoSize;

/// <inheritdoc/>
internal override Size OnMeasuredDesired(Size desired)
{
    if (!AutoSize || AutoSizeMode != AutoSizeMode.GrowOnly)
    {
        return desired;
    }

    // GrowOnly never shrinks below an explicit fixed-cell size on that axis.
    int width = Width.Kind == Kind.Cells ? Math.Max(desired.Width, (int) Width.Value) : desired.Width;
    int height = Height.Kind == Kind.Cells ? Math.Max(desired.Height, (int) Height.Value) : desired.Height;
    return new Size(width, height);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerAutoSizeTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 5: Regression**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Layout" --timeout 180s`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision/Controls/Container.cs tests/SharpVision.Tests/Support/ProbeContainer.cs tests/SharpVision.Tests/Controls/ContainerAutoSizeTests.cs
git commit -m "feat(controls): add AutoSize/AutoSizeMode grow-shrink to Container"
```

---

## Phase 2 — Arm scrolling: offsets, probe, translate, clip, bars

### Task 5: Scroll configuration properties + offsets + `ContentSlot` translation (bars hidden)

**Files:**

- Modify: `src/SharpVision/Controls/Container.cs`
- Test: `tests/SharpVision.Tests/Controls/ContainerScrollTests.cs`

This task lands the scroll state and offset translation while keeping
`ShowScrollBars = Never` semantics (no bar chrome yet), so translation is tested
in isolation. It lifts the geometry helpers, `Resolve`, `Apply`, `MaximumX/Y`,
and `ResolveContentSlot` from `ScrollView`.

**Interfaces:**

- Consumes: `ContentExtent` (Task 2), `ResolveContentSlot` seam (Task 2).
- Produces on `Container`: `AutoScroll` (bool, default false), `ScrollBars`
  (default `Vertical`), `ShowScrollBars` (default `WhenNeeded`),
  `HorizontalBarVisibility`/`VerticalBarVisibility` (`ScrollBarVisibility`),
  `HorizontalOffset`/`VerticalOffset` (int), `Extent`/`Viewport` (`Size`),
  `LineSize` (int, default 1), `PageOverlap` (int, default 0),
  `ScrollBy(int x, int y, Cause cause = Cause.Programmatic) → bool`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Scrolling;
using SharpVision.Tests.Support;

/// <summary>Verifies intrinsic Container scrolling geometry, offsets, clipping, and chrome.</summary>
public sealed class ContainerScrollTests
{
    /// <summary>Verifies an unarmed container reports an inert scroll state and clips overflow.</summary>
    [Fact]
    public void ScrollState_WhenNotArmed_IsInert()
    {
        ProbeContainer container = new();
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new Engine().Layout(container, new Size(4, 10));

        container.AutoScroll.ShouldBeFalse();
        container.Extent.ShouldBe(container.Viewport);
        container.VerticalOffset.ShouldBe(0);
        container.ScrollBy(0, 5).ShouldBeFalse();
    }

    /// <summary>Verifies an armed vertical container discovers the natural extent and clamps offsets.</summary>
    [Fact]
    public void Extent_WhenArmedVertical_IsNaturalContentHeight()
    {
        ProbeContainer container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new Engine().Layout(container, new Size(4, 10));

        container.Extent.Height.ShouldBe(40);
        container.Viewport.Height.ShouldBe(10);
        container.ScrollBy(0, 1000).ShouldBeTrue();
        container.VerticalOffset.ShouldBe(30);
    }

    /// <summary>Verifies the child is translated by the vertical offset during arrange.</summary>
    [Fact]
    public void Arrange_WhenScrolled_TranslatesChildByOffset()
    {
        ProbeControl child = new(new Size(4, 40));
        ProbeContainer container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(child);
        new Engine().Layout(container, new Size(4, 10));

        _ = container.ScrollBy(0, 6);
        new Engine().Layout(container, new Size(4, 10));

        child.Bounds.Y.ShouldBe(-6);
    }
}
```

The `ProbeContainer.MeasureOverride` from Task 4 measures children with the
incoming (possibly height-null) constraint and unions their desired sizes; with
the eligible height nulled by `OnMeasuringContent`, the child reports its
natural height 40. Confirm `ProbeContainer.MeasureOverride` passes `constraint`
straight through to the child (it does).

- [ ] **Step 2: Run tests to verify they fail**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: FAIL — `AutoScroll`, `Extent`, `Viewport`, `ScrollBy` do not exist.

- [ ] **Step 3: Add configuration + state + geometry**

Add to `Container.cs` a `#region Scrolling` with the properties. Copy the
enum-validating setters and `Add`/`Difference`/`MultiplyNegative` helpers from
`ScrollView.cs` (lines 60-104, 636-724). Key members (defaults per spec):

```csharp
public bool AutoScroll { get; set => _ = Set(ref field, value, Invalidation.Measure); }

public ScrollBars ScrollBars
{
    get;
    set
    {
        if ((value & ~ScrollBars.Both) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The scrollbar axes contain unknown flags.");
        }

        _ = Set(ref field, value, Invalidation.Measure);
    }
} = ScrollBars.Vertical;

public ShowScrollBars ShowScrollBars { get; set { /* validate; map to per-axis visibility exactly as ScrollView.cs:110-132 */ } } = ShowScrollBars.WhenNeeded;
public ScrollBarVisibility HorizontalBarVisibility { get; set { Validate(value); _ = Set(ref field, value, Invalidation.Measure); } } = ScrollBarVisibility.Auto;
public ScrollBarVisibility VerticalBarVisibility { get; set { Validate(value); _ = Set(ref field, value, Invalidation.Measure); } } = ScrollBarVisibility.Auto;
public int LineSize { get; set { ArgumentOutOfRangeException.ThrowIfNegative(value); _ = Set(ref field, value, Invalidation.None); } } = 1;
public int PageOverlap { get; set { ArgumentOutOfRangeException.ThrowIfNegative(value); _ = Set(ref field, value, Invalidation.None); } }

public Size Extent => _extent;
public Size Viewport => _viewport;
public int HorizontalOffset { get => _horizontalOffset; set { ValidateOffset(value, MaximumX(), nameof(value)); _ = Apply(value, VerticalOffset, Cause.Programmatic); } }
public int VerticalOffset { get => _verticalOffset; set { ValidateOffset(value, MaximumY(), nameof(value)); _ = Apply(HorizontalOffset, value, Cause.Programmatic); } }

public bool ScrollBy(int x, int y, Cause cause = Cause.Programmatic)
{
    Validate(cause);
    VerifyMutable();
    return Apply(Add(HorizontalOffset, x), Add(VerticalOffset, y), cause);
}
```

Backing fields: `_extent`, `_viewport` (`Size`), `_horizontalOffset`,
`_verticalOffset` (int), `_viewportBounds` (`Rect`).

Add `MaximumX`/`MaximumY` gated by `AutoScroll` (return 0 when `!AutoScroll`),
lifted from `ScrollView.cs:636-642`:

```csharp
private int MaximumX() => AutoScroll && (ScrollBars & ScrollBars.Horizontal) != 0
    ? Math.Max(0, Extent.Width - Viewport.Width) : 0;
private int MaximumY() => AutoScroll && (ScrollBars & ScrollBars.Vertical) != 0
    ? Math.Max(0, Extent.Height - Viewport.Height) : 0;
```

Add `Apply` lifted from `ScrollView.cs:460-478` but **without** the bar
`Synchronize` and `ScrollChanged` call yet (those arrive in Tasks 7 and 10). For
now:

```csharp
private bool Apply(int x, int y, Cause cause)
{
    x = Math.Clamp(x, 0, MaximumX());
    y = Math.Clamp(y, 0, MaximumY());
    bool changedX = Set(ref _horizontalOffset, x, Invalidation.Arrange, nameof(HorizontalOffset));
    bool changedY = Set(ref _verticalOffset, y, Invalidation.Arrange, nameof(VerticalOffset));
    return changedX || changedY;
}
```

- [ ] **Step 4: Null eligible axes in measure; run the probe and translate in
      arrange**

Override the layout seams (`Resolve` lifted verbatim from
`ScrollView.cs:655-695`, but reading `AutoScroll`, `ScrollBars`, and the
per-axis visibility from `this`):

```csharp
/// <inheritdoc/>
internal override Constraint OnMeasuringContent(Constraint content)
{
    if (!AutoScroll)
    {
        return content;
    }

    // Eligible axes measure unbounded so children report their natural extent
    // (SharpVision clamps DesiredSize to the constraint, which would hide overflow).
    int? width = (ScrollBars & ScrollBars.Horizontal) != 0 ? null : content.Width;
    int? height = (ScrollBars & ScrollBars.Vertical) != 0 ? null : content.Height;
    return new Constraint(width, height);
}

/// <inheritdoc/>
internal override Rect ResolveContentSlot(Rect padded)
{
    if (!AutoScroll)
    {
        return padded;
    }

    Resolve(new Size(padded.Width, padded.Height), ContentExtent, out bool horizontal, out bool vertical, out Size viewport);
    _viewportBounds = new Rect(padded.X, padded.Y, viewport.Width, viewport.Height);
    _ = Set(ref _extent, ContentExtent, Invalidation.None, nameof(Extent));
    _ = Set(ref _viewport, viewport, Invalidation.None, nameof(Viewport));
    _reserveHorizontal = horizontal;
    _reserveVertical = vertical;
    _ = Apply(Math.Min(HorizontalOffset, MaximumX()), Math.Min(VerticalOffset, MaximumY()), Cause.Resize);

    return new Rect(
        Difference(padded.X, HorizontalOffset),
        Difference(padded.Y, VerticalOffset),
        Math.Max(Extent.Width, viewport.Width),
        Math.Max(Extent.Height, viewport.Height));
}
```

Add fields `_reserveHorizontal`/`_reserveVertical` (bool). `Resolve` uses
`HorizontalBarVisibility`/`VerticalBarVisibility` exactly as `ScrollView`. When
`ShowScrollBars.Never`, both visibilities are `Hidden`, so `Resolve` reserves
nothing and `viewport == padded` — the translation test uses this path.

- [ ] **Step 5: Run tests to verify they pass**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 6: Regression**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Layout" --timeout 180s`
Expected: PASS (unarmed containers unchanged).

- [ ] **Step 7: Commit**

```bash
git add src/SharpVision/Controls/Container.cs tests/SharpVision.Tests/Controls/ContainerScrollTests.cs
git commit -m "feat(controls): add AutoScroll state, extent probe, and offset translation to Container"
```

### Task 6: Bar chrome ownership, reservation, arrange, render, hit-test

**Files:**

- Modify: `src/SharpVision/Controls/Container.cs`
- Modify: `src/SharpVision/Controls/Stack.cs` (route child rendering through
  `RenderContent`)
- Test: `tests/SharpVision.Tests/Controls/ContainerScrollTests.cs` (extend)

This is the largest lift: the two owned `ScrollBar` controls and the
render/arrange/hit-test integration, moved from `ScrollView.cs`.

**Interfaces:**

- Consumes: `_reserveHorizontal`/`_reserveVertical`, `_viewportBounds`,
  `Extent`/`Viewport`, `MaximumX/Y`, `Apply` (Task 5); `RenderContent` seam
  (defined here on `Control`).
- Produces on `Container`: `ScrollBarChrome`, `ScrollBarFill` properties; owned
  bars; overrides of `RenderChildren`, `ArrangeOverlays`, `HitTest`,
  `VisitChildren`, `NavigationCount`/`NavigationAt`, `DisposeChildren`.

- [ ] **Step 1: Write the failing test**

Reuse the exact chrome-glyph expectations from `ScrollViewTests` (`▲ ▓ ▼`
vertical, `◀ ▓ ▶` horizontal):

```csharp
// ContainerScrollTests.cs — add:
/// <summary>Verifies an armed container renders the automatic vertical bar chrome.</summary>
[Fact]
public void Render_WhenVerticalBarIsAutomatic_UsesScrollBarGlyphs()
{
    ProbeContainer container = new()
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Vertical,
        VerticalBarVisibility = ScrollBarVisibility.Auto,
    };
    container.Children.Add(new ProbeControl(new Size(1, 4)));
    Size size = new(3, 3);
    new Engine().Layout(container, size);
    using Frame frame = new(size);

    container.Render(frame.Canvas);

    FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("▲");
    FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("▓");
    FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▼");
}

/// <summary>Verifies one automatic bar can induce the other, converging with both.</summary>
[Fact]
public void Layout_WhenAutomaticBarInducesOther_ConvergesWithBothBars()
{
    ProbeContainer container = new()
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Both,
        HorizontalBarVisibility = ScrollBarVisibility.Auto,
        VerticalBarVisibility = ScrollBarVisibility.Auto,
    };
    container.Children.Add(new ProbeControl(new Size(5, 4)));

    new Engine().Layout(container, new Size(5, 3));

    container.Extent.ShouldBe(new Size(5, 4));
    container.Viewport.ShouldBe(new Size(4, 2));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: FAIL — no chrome renders; `ScrollBars.Both` induction not wired.

- [ ] **Step 3: Add owned bars, chrome props, `Synchronize`/`Configure`**

Add to `Container.cs`: fields `_bars` (`Children`, capacity 2, created lazily),
`_horizontal`/`_vertical` (`ScrollBar?`), `_syncing` (bool). Add
`ScrollBarChrome`/`ScrollBarFill` properties (lift from `ScrollView.cs:138-170`,
updating the two bars when set). Add a private `EnsureBars()` that lazily
constructs the two bars exactly as `ScrollView`'s constructor (lines 29-44),
subscribing `OnHorizontalChanged`/`OnVerticalChanged`. Lift
`Synchronize`/`Configure`/`OnHorizontalChanged`/`OnVerticalChanged` verbatim
from `ScrollView.cs:480-531`. Call `EnsureBars()` from `ResolveContentSlot` when
`AutoScroll` and either visibility can show a bar.

- [ ] **Step 4: Introduce the `RenderContent` seam and override render/arrange**

On `Control.cs`, add:

```csharp
/// <summary>Renders owned child content into the (already clipped) canvas. Default delegates to RenderChildren.</summary>
/// <param name="canvas">The child canvas.</param>
internal virtual void RenderContent(TerminalCanvas canvas) => RenderChildren(canvas);
```

On `Container.cs`, override `RenderChildren` to clip to the viewport and draw
bars when armed (adapting `ScrollView.cs:340-349`); when not armed, keep today's
behavior:

```csharp
internal override void RenderChildren(TerminalCanvas canvas)
{
    if (!AutoScroll)
    {
        base.RenderChildren(canvas);
        return;
    }

    RenderContent(canvas.Clip(_viewportBounds));
    _horizontal?.Render(canvas);
    _vertical?.Render(canvas);

    if (Parent is null)
    {
        RenderOwnedPopupLayer(canvas);
    }
}
```

Move the child loop from today's `Container.RenderChildren` into an override of
`RenderContent` (default `RenderChildren` still runs the loop for unarmed
containers — so factor the loop into a private `RenderChildLoop(canvas)` called
by both). Override `ArrangeOverlays` to arrange the two bars in the gutters
(adapt `ScrollView.cs:415-424`), and
`HitTest`/`VisitChildren`/`NavigationCount`/`NavigationAt`/`DisposeChildren` to
include the owned bars **only when armed** (adapt `ScrollView.cs:287-337`).

- [ ] **Step 5: Update `Stack` to render through `RenderContent`**

`Stack` overrides `RenderChildren` for `Reverse`. Move that logic to override
`RenderContent` instead, so it composes with the base clip+bars:

```csharp
// Stack.cs — replace the RenderChildren override with:
internal override void RenderContent(TerminalCanvas canvas)
{
    if (!Reverse)
    {
        base.RenderContent(canvas);
        return;
    }

    for (int index = Children.Count - 1; index >= 0; index--)
    {
        Children[index].Render(canvas);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 7: Regression — Stack, Grid, Canvas, and existing ScrollView still
      pass**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Controls" --timeout 240s`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/SharpVision/Controls/Container.cs src/SharpVision/Controls/Control.cs src/SharpVision/Controls/Stack.cs tests/SharpVision.Tests/Controls/ContainerScrollTests.cs
git commit -m "feat(controls): own scrollbar chrome and clip content in Container"
```

---

## Phase 3 — Input

### Task 7: Keyboard scrolling

**Files:**

- Modify: `src/SharpVision/Controls/Container.cs` (`OnEvent`)
- Test: `tests/SharpVision.Tests/Controls/ContainerScrollTests.cs`

**Interfaces:**

- Consumes: `ScrollBy`, `Apply`, `Viewport`, `MaximumY`, `PageOverlap`,
  `LineSize`.
- Produces: keyboard handling inside `Container.OnEvent`.

- [ ] **Step 1: Write the failing test**

```csharp
// ContainerScrollTests.cs — add:
/// <summary>Verifies the Down key advances the vertical offset by LineSize.</summary>
[Fact]
public void OnEvent_WhenDownKey_ScrollsByLineSize()
{
    ProbeContainer container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never, LineSize = 2 };
    container.Children.Add(new ProbeControl(new Size(4, 40)));
    new Engine().Layout(container, new Size(4, 10));

    container.RaiseKey(Code.Down);

    container.VerticalOffset.ShouldBe(2);
}
```

Add a `RaiseKey` test helper to `ProbeContainer` that builds a press
`KeyEventArgs` and calls `InvokeDefault` (mirror how `ScrollViewTests` drives
keys — see that file's `KeyAction` alias and helper usage).

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: FAIL — no key handling.

- [ ] **Step 3: Implement keyboard handling**

Override `OnEvent` in `Container` and lift `Handle(KeyEventArgs)` verbatim from
`ScrollView.cs:430-581`, guarded by
`if (!AutoScroll) { base.OnEvent(eventArgs); return; }`. Keep the exact key set
(`Left/Right/Up/Down/PageUp/PageDown/Home/End`) and `PageOverlap` math.

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Controls/Container.cs tests/SharpVision.Tests/Support/ProbeContainer.cs tests/SharpVision.Tests/Controls/ContainerScrollTests.cs
git commit -m "feat(controls): keyboard scrolling on armed Container"
```

### Task 8: Wheel scrolling with nested propagation

**Files:**

- Modify: `src/SharpVision/Controls/Container.cs` (`OnEvent`, `Ancestor`)
- Test: `tests/SharpVision.Tests/Controls/ContainerScrollTests.cs`

**Interfaces:**

- Consumes: `ScrollBy`, `HorizontalOffset`/`VerticalOffset`.
- Produces: wheel handling; `Ancestor` walks to the nearest armed `Container`.

- [ ] **Step 1: Write the failing test**

```csharp
// ContainerScrollTests.cs — add:
/// <summary>Verifies unused wheel delta propagates to the nearest armed ancestor.</summary>
[Fact]
public void Wheel_WhenLeafAtEnd_PropagatesToArmedAncestor()
{
    ProbeContainer outer = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
    ProbeContainer inner = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
    inner.Children.Add(new ProbeControl(new Size(4, 4)));   // inner cannot scroll (fits)
    outer.Children.Add(inner);
    // outer content taller than viewport via a second tall child
    outer.Children.Add(new ProbeControl(new Size(4, 40)));
    new Engine().Layout(outer, new Size(4, 10));

    inner.RaiseWheel(0, 3);   // wheel over inner, which has no room

    outer.VerticalOffset.ShouldBeGreaterThan(0);
}
```

Add a `RaiseWheel(int wheelX, int wheelY)` helper to `ProbeContainer` building a
wheel `PointerEventArgs` (mirror `ScrollViewTests`' pointer/wheel drive).

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: FAIL.

- [ ] **Step 3: Implement wheel handling + ancestor walk**

In `Container.OnEvent`, dispatch `PointerEventArgs` to
`Handle(PointerEventArgs)` lifted from `ScrollView.cs:583-608`. Replace the
`ScrollView`-typed `Ancestor` (`ScrollView.cs:610-621`) with:

```csharp
private static Container? Ancestor(Control control)
{
    for (Container? current = control.Parent; current is not null; current = current.Parent)
    {
        if (current.AutoScroll)
        {
            return current;
        }
    }

    return null;
}
```

and change the propagation loop's type from `ScrollView?` to `Container?`.

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Controls/Container.cs tests/SharpVision.Tests/Support/ProbeContainer.cs tests/SharpVision.Tests/Controls/ContainerScrollTests.cs
git commit -m "feat(controls): wheel scrolling with nested propagation on Container"
```

### Task 9: `BringIntoView`, `ScrollChanged`, and `Apply` completion

**Files:**

- Modify: `src/SharpVision/Controls/Container.cs`
- Test: `tests/SharpVision.Tests/Controls/ContainerScrollTests.cs`

**Interfaces:**

- Produces on `Container`:
  `event EventHandler<ScrollChangedEventArgs>? ScrollChanged`;
  `bool BringIntoView(Control descendant)`. Completes `Apply` to raise
  `ScrollChanged` and call `Synchronize`.

- [ ] **Step 1: Write the failing tests**

```csharp
// ContainerScrollTests.cs — add:
/// <summary>Verifies BringIntoView scrolls minimally to expose a descendant.</summary>
[Fact]
public void BringIntoView_WhenDescendantBelowViewport_ScrollsToReveal()
{
    ProbeControl target = new(new Size(4, 1)) { VerticalAlignment = VerticalAlignment.Top };
    ProbeContainer container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
    // stack two children so target lands at y = 20
    container.Children.Add(new ProbeControl(new Size(4, 20)));
    container.Children.Add(target);
    // ProbeContainer arranges each child at the full slot; use a vertical Stack instead:
    // (replaced below)
    new Engine().Layout(container, new Size(4, 10));

    bool changed = container.BringIntoView(target);

    changed.ShouldBeTrue();
    container.VerticalOffset.ShouldBeGreaterThan(0);
}

/// <summary>Verifies a committed offset change raises ScrollChanged with the cause.</summary>
[Fact]
public void ScrollBy_WhenOffsetChanges_RaisesScrollChanged()
{
    ProbeContainer container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
    container.Children.Add(new ProbeControl(new Size(4, 40)));
    new Engine().Layout(container, new Size(4, 10));
    ScrollChangedEventArgs? captured = null;
    container.ScrollChanged += (_, e) => captured = e;

    _ = container.ScrollBy(0, 3, Cause.Keyboard);

    captured.ShouldNotBeNull();
    captured!.Offset.ShouldBe(new Point(0, 3));
    captured.Cause.ShouldBe(Cause.Keyboard);
}
```

For `BringIntoView`, use a real vertical `Stack` as the container under test (it
stacks children, so the target lands below the viewport). Rewrite that test to
arm a `Stack`:

```csharp
Stack container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
container.Children.Add(new ProbeControl(new Size(4, 20)));
ProbeControl target = new(new Size(4, 1));
container.Children.Add(target);
new Engine().Layout(container, new Size(4, 10));
container.BringIntoView(target).ShouldBeTrue();
container.VerticalOffset.ShouldBeGreaterThan(0);
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: FAIL.

- [ ] **Step 3: Complete `Apply`; add `ScrollChanged`, `BringIntoView`,
      `Reveal`, `IsContentDescendant`**

Extend `Apply` (from Task 5) to raise `ScrollChanged` and call `Synchronize()`,
matching `ScrollView.cs:460-478`:

```csharp
private bool Apply(int x, int y, Cause cause)
{
    x = Math.Clamp(x, 0, MaximumX());
    y = Math.Clamp(y, 0, MaximumY());
    Point previous = new(HorizontalOffset, VerticalOffset);
    bool changedX = Set(ref _horizontalOffset, x, Invalidation.Arrange, nameof(HorizontalOffset));
    bool changedY = Set(ref _verticalOffset, y, Invalidation.Arrange, nameof(VerticalOffset));

    if (!changedX && !changedY)
    {
        return false;
    }

    Synchronize();
    ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(previous, new Point(x, y), Extent, Viewport, cause));
    return true;
}
```

Add `public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;` and
clear it in `OnUnavailable(ReleaseReason.Disposed)`. Lift `BringIntoView` (adapt
so "content descendant" means any descendant of `this`, using
`_viewportBounds`), `Reveal`, and `IsContentDescendant` from
`ScrollView.cs:269-284, 623-653`.

- [ ] **Step 4: Run tests to verify they pass**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ContainerScrollTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 5: Full controls + integration regression**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Controls" --timeout 240s`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision/Controls/Container.cs tests/SharpVision.Tests/Controls/ContainerScrollTests.cs
git commit -m "feat(controls): BringIntoView and ScrollChanged on Container"
```

---

## Phase 4 — Remove ScrollView; refactor List/Table; migrate showcase

### Task 10: Refactor `List` onto intrinsic scrolling

**Files:**

- Modify: `src/SharpVision/Controls/List.cs`
- Test: `tests/SharpVision.Tests/Controls/ListTests.cs` (adjust references)

**Interfaces:**

- Consumes: `Container.AutoScroll`, `ScrollBars`, `ShowScrollBars`,
  `ScrollBarChrome`, `ScrollBarFill`, `VerticalOffset`, `Viewport`,
  `BringIntoView` (inherited).
- Produces: `List` with no internal `ScrollView`; its item `Stack` becomes the
  single child with `AutoScroll = true`.

- [ ] **Step 1: Run the existing List tests to capture the baseline**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ListTests" --timeout 180s`
Expected: PASS (record which tests reference `_scroll`/`ScrollView` internals).

- [ ] **Step 2: Rework `List` to arm the item stack**

In `List.cs`: delete the `_scroll` field and its construction. Keep `_stack` as
the content. Set the item stack to scroll and make `List` delegate through it.
The simplest faithful change: `List` keeps a single-child chrome slot holding
`_stack`, and arms `_stack`:

```csharp
public List() : base(capacity: 0)
{
    _itemsView = _items.AsReadOnly();
    _selectedView = _selectedItems.AsReadOnly();
    _stack = new Stack
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Vertical,
        ShowScrollBars = ShowScrollBars.WhenNeeded,
    };
    _chrome = new Children(this, capacity: 1) { _stack };
    _ = AddHandler(Events.Key, OnKeyRouted);
    CanFocus = true;
}
```

Replace every `_scroll.X` delegation with `_stack.X`: `ScrollBars`,
`ShowScrollBars`, `ScrollBarChrome`, `ScrollBarFill`, `VerticalOffset`,
`Viewport`, `BringIntoView`,
`HitTest`/`HitTestPopup`/`RenderChildren`/`RenderPopupLayer`/`MeasureOverride`/`ArrangeOverride`
(they now target `_stack`). `ResolveNavigation`'s `_scroll.Viewport.Height` and
`_scroll.BringIntoView(target)` become `_stack.Viewport.Height` and
`_stack.BringIntoView(target)`.

- [ ] **Step 3: Update tests that referenced `ScrollView`**

Where `ListTests` asserted `list ... .Parent` is a `ScrollView` or reached
`_scroll`, retarget to the `Stack`. Keep the observable behavior assertions
(selection, `SelectedIndex=2` → "Gamma", background colors) unchanged.

- [ ] **Step 4: Run List tests**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ListTests" --timeout 180s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Controls/List.cs tests/SharpVision.Tests/Controls/ListTests.cs
git commit -m "refactor(controls): List uses intrinsic Container scrolling"
```

### Task 11: Enable intrinsic scrolling on `Table`

**Files:**

- Modify: `src/SharpVision/Controls/Table.cs`
- Test: `tests/SharpVision.Tests/Controls/TableTests.cs` (add overflow scroll
  test)

**Interfaces:**

- Consumes: inherited scroll surface.
- Produces: `Table` with `AutoScroll = true`, `ScrollBars = ScrollBars.Both`,
  and a `MeasureOverride` that reports natural (unclamped) column/row extents.

- [ ] **Step 1: Write the failing test**

```csharp
// TableTests.cs — add:
/// <summary>Verifies a Table taller than its viewport scrolls vertically.</summary>
[Fact]
public void Extent_WhenRowsExceedViewport_ExposesVerticalScroll()
{
    Table table = BuildTableWithRows(rowCount: 40);   // existing helper or inline builder

    new Engine().Layout(table, new Size(30, 10));

    table.Extent.Height.ShouldBeGreaterThan(table.Viewport.Height);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*TableTests" --timeout 180s`
Expected: FAIL — `Extent`/`Viewport` inert because `AutoScroll` is off, or
`MeasureCells` clamps.

- [ ] **Step 3: Arm the table and expose natural extents**

In `Table.cs` constructor set
`AutoScroll = true; ScrollBars = ScrollBars.Both;`. In
`MeasureCells`/`MeasureOverride`, when the eligible axis constraint is null (the
base nulled it), compute row heights and column widths from cell desired sizes
without clamping to the (absent) bound, so the returned size is the natural
extent. Confirm the returned `Size` sums natural `_rowHeights`/`_columnWidths`
plus gaps.

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*TableTests" --timeout 180s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Controls/Table.cs tests/SharpVision.Tests/Controls/TableTests.cs
git commit -m "feat(controls): Table scrolls via intrinsic Container mechanism"
```

### Task 12: Delete `ScrollView`; migrate Gallery and remaining usages

**Files:**

- Delete: `src/SharpVision/Controls/ScrollView.cs`
- Delete: `src/SharpVision.Showcase/Panes/ScrollViewShowcasePane.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`,
  `src/SharpVision.Showcase/GlobalUsings.cs`,
  `src/SharpVision.Showcase/Panes/ListShowcasePane.cs`,
  `src/SharpVision.Showcase/Panes/ComboBoxShowcasePane.cs`
- Delete/rename tests: `tests/SharpVision.Tests/Controls/ScrollViewTests.cs`,
  `tests/SharpVision.Tests/Controls/RandomizedScrollViewTests.cs` (migrated in
  Task 13)

**Interfaces:**

- Consumes: intrinsic scroll surface on `Container`.
- Produces: no `ScrollView` type anywhere.

- [ ] **Step 1: Find every reference**

Run: `grep -rln "ScrollView" src tests` Expected: the files listed above plus
the two test files.

- [ ] **Step 2: Migrate each usage**

Replace `new ScrollView { Content = c, ScrollBars = ScrollBars.X, ... }` with
the content container armed: set `c.AutoScroll = true` and copy
`ScrollBars`/`ShowScrollBars`/chrome onto `c` (or wrap in
`new Stack { AutoScroll = true, Children = { c } }` if `c` must stay unchanged).
In `Gallery.cs`, the scrolling content host (its `Content.Parent` is currently a
`ScrollView`) becomes an armed `Stack`/`Dock`; update the `GalleryTests`
assertion that `Content.Parent` is a `ScrollView` to expect the new armed
container type.

- [ ] **Step 3: Delete `ScrollView.cs` and the showcase pane**

```bash
git rm src/SharpVision/Controls/ScrollView.cs src/SharpVision.Showcase/Panes/ScrollViewShowcasePane.cs
```

- [ ] **Step 4: Build**

Run: `make build` Expected: success. Fix any remaining references the compiler
flags.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: remove ScrollView in favor of intrinsic Container scrolling"
```

### Task 13: Update the showcase inventory

**Files:**

- Modify: `src/SharpVision.Showcase/Gallery.cs` (page list/sidebar), and the
  inventory assertions in `tests/SharpVision.Showcase.Tests/GalleryTests.cs`,
  `GalleryRenderingTests.cs`, `GalleryInteractionTests.cs`.

**Interfaces:**

- Produces: sidebar inventory with `ScrollView` removed; `ScrollBar` retained.

- [ ] **Step 1: Run showcase tests to see the failing inventory**

Run: `dotnet test --project tests/SharpVision.Showcase.Tests --timeout 240s`
Expected: FAIL — inventory still lists `ScrollView`.

- [ ] **Step 2: Remove `ScrollView` from the page inventory and the test
      expectations**

Delete the `ScrollView` entry from `Gallery`'s page list and from every
`_controls`/inventory array in the showcase tests. Keep `ScrollBar`, `Border`,
`Shadow` (Border/Shadow are the sibling spec's concern, not this one). Verify
order is otherwise unchanged.

- [ ] **Step 3: Run showcase tests**

Run: `dotnet test --project tests/SharpVision.Showcase.Tests --timeout 240s`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "test(showcase): drop ScrollView from the control inventory"
```

---

## Phase 5 — Test migration and docs

### Task 14: Migrate the scrolling test contract to `Container`

**Files:**

- Create: `tests/SharpVision.Tests/Controls/ContainerScrollGeometryTests.cs`
  (port `RandomizedScrollViewTests`)
- Modify/rename: fold `ScrollViewTests` behaviors into `ContainerScrollTests`
- Delete: `tests/SharpVision.Tests/Controls/ScrollViewTests.cs`,
  `RandomizedScrollViewTests.cs`

**Interfaces:**

- Consumes: the full `Container` scroll surface.

- [ ] **Step 1: Port the randomized geometry suite**

Recreate `RandomizedScrollViewTests` against an armed `ProbeContainer`/`Stack`,
keeping the seed `0x005C7011` and the invariants (containment, repeatability,
monotonic endpoint position, exact invertible endpoints, one-step round-trip
error). The bar math lives in `ScrollBar`/`Thumb` (unchanged), so only the
driver type changes.

- [ ] **Step 2: Port the remaining `ScrollViewTests` cases**

For each `ScrollViewTests` case not already covered by `ContainerScrollTests`
(visibility policies, exact fit, zero/tiny viewport, resize appearance/removal,
content changes, offset clamping, capture, focus, disabled state, Unicode
clipping, final frames), add the equivalent armed-`Container` test. Delete the
two old files.

- [ ] **Step 3: Add the intrinsic-model tests from the spec's Testing section**

Add: fill-first (Star/Percent/Stretch on the cross axis do not scroll),
wrap-vs-horizontal (`ScrollBars = Vertical` wraps; `ScrollBars = Both` +
incompressible child grows a horizontal bar), natural-extent reporting for
`Grid`/`Table`, and grow/shrink + `AutoSize` + `MaxHeight` + `AutoScroll`
caps-then-scrolls.

- [ ] **Step 4: Run the full test project**

Run: `dotnet test --project tests/SharpVision.Tests --timeout 600s` Expected:
PASS, discovered-test count at/above the configured minimum. (Re-run once if
`Integration/ScrollingTests` shows the known single flake.)

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test(controls): migrate the scrolling contract to intrinsic Container scrolling"
```

### Task 15: Documentation, `AGENTS.md`, and the full quality gate

**Files:**

- Modify: `docs/concepts/scrolling.md`, `docs/concepts/layout.md`,
  `docs/controls/index.md` (+ remove the `ScrollView` control doc if one
  exists), `docs/architecture/showcase.md`, `AGENTS.md`

**Interfaces:** none (docs).

- [ ] **Step 1: Rewrite `docs/concepts/scrolling.md`**

Recast around `AutoScroll` on `Container`; describe the VCL/WinForms lineage,
the natural-extent (`DisplayRectangle`) model, the per-axis `ScrollBars`
unbounded-measure rule (default `Vertical`), the reservation probe, and nested
wheel propagation. Keep the "Automatic scrollbar algorithm", "Thumb geometry",
and "Test contract" sections, updating type names from `ScrollView` to
`Container`.

- [ ] **Step 2: Update `docs/concepts/layout.md`**

Add a grow/shrink section (`AutoSize`/`AutoSizeMode`) and the rule that a
determinate axis scrolls while an auto-sized axis grows (capping at `Max` then
scrolling).

- [ ] **Step 3: Update control docs and `AGENTS.md`**

Remove the `ScrollView` control spec; document the scroll surface on
`Container`. In `AGENTS.md`, add that scrolling and grow/shrink are intrinsic
`Container` properties (`AutoScroll`, `AutoSize`, `AutoSizeMode`) and there is
no dedicated scroll container.

- [ ] **Step 4: Full quality gate**

Run: `make format && make lint && make build && make test` Expected: zero
warnings, zero errors, docs/link checks pass, discovered tests at/above the
minimum.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: intrinsic Container scrolling and grow/shrink"
```

---

## Self-Review

**Spec coverage:** §1 base capability → Tasks 2/5/6; §2 public surface → Tasks
4/5/6/9; §3 grow/shrink → Tasks 1/3/4; §4 mechanism (per-axis unbounded measure,
probe, translate, clip, natural extent) → Tasks 5/6/11; §5 defaults/arming →
Tasks 5 (default false), 10/11 (List/Table on); §6 removal/migration → Tasks
10-13; Testing → Task 14; Docs → Task 15; Risks (bespoke containers) → Tasks
6/10/11 (Stack `RenderContent`, List/Table); (natural extent) → Task 11.

**Placeholders:** the `BuildTableWithRows` helper in Task 11 must be a real
inline builder or an existing `TableTests` helper — the implementer inlines row
construction from the existing `TableTests` patterns. `ShowScrollBars` setter
body in Task 5 is specified by reference to `ScrollView.cs:110-132` (verbatim
lift), not left blank.

**Type consistency:** seam names (`OnMeasuringContent`, `OnMeasuredDesired`,
`ResolveContentSlot`, `ArrangeOverlays`, `ShrinkWrapsWidth/Height`,
`RenderContent`, `ContentExtent`), `Apply(int,int,Cause)`,
`ScrollBy(int,int,Cause)`, `Ancestor(Control)→Container?`, and property defaults
(`ScrollBars=Vertical`, `AutoScroll=false`, `AutoSizeMode=GrowAndShrink`) are
consistent across tasks and match the spec.

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — a fresh subagent per task, review between
   tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session with checkpoints.

Which approach?
