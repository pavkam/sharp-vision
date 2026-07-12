# Hover Interaction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans`
> to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for
> tracking.

**Goal:** Make passive terminal pointer motion visibly and correctly hover every
interactive SharpVision control, including composites whose visible content is a
child control.

**Architecture:** The Showcase explicitly enables xterm any-event SGR mouse
tracking while the library retains conservative defaults. `CaptureManager`
separates physical hover hit testing from routed pointer capture, resolving a
child hit to the nearest pressable semantic owner. The Showcase supplies
explicit visual-state overlays and proves them through virtual frames and tmux.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, SGR mouse input, tmux.

---

## Task 1: Opt the Showcase into passive pointer motion

**Files:**

- Modify: `tests/SharpVision.Showcase.Tests/StartupOptionsTests.cs`
- Modify: `src/SharpVision.Showcase/StartupOptions.cs`
- Modify: `docs/architecture/showcase.md`
- Modify: `docs/testing/showcase.md`

- [ ] **Step 1: Write the failing mode test**

```csharp
[Fact]
public async Task Create_WhenShowcaseStarts_EnablesSgrAnyEventMouseAsync()
{
    options.Tracking.ShouldBe(MouseTracking.Any);
    await application.StartAsync(TestContext.Current.CancellationToken);

    var output = Encoding.ASCII.GetString([.. terminal.Writes.SelectMany(static value => value)]);
    output.ShouldContain("\u001b[?1003h\u001b[?1006h");
}
```

- [ ] **Step 2: Verify the test is red**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --no-restore --filter-method "*StartupOptionsTests.Create_WhenShowcaseStarts_EnablesSgrAnyEventMouseAsync"
```

Expected: failure because the current executable policy is `Drag` and emits
`1002`.

- [ ] **Step 3: Change only the executable opt-in policy**

```csharp
return new RuntimeOptions
{
    Capabilities = capabilities,
    Tracking = MouseTracking.Any,
    Coordinates = MouseCoordinates.Sgr,
};
```

Update XML and the two docs to name passive `1003` plus SGR `1006`. Do not alter
the terminal library default policy.

- [ ] **Step 4: Verify green and commit**

Run the Step 2 command. Expected: one passing test and exact `1003`/`1006`
bytes.

```bash
git add src/SharpVision.Showcase/StartupOptions.cs tests/SharpVision.Showcase.Tests/StartupOptionsTests.cs docs/architecture/showcase.md docs/testing/showcase.md
git commit -m "feat(showcase): request passive mouse motion"
```

## Task 2: Prove semantic hover ownership before implementation

**Files:**

- Modify: `tests/SharpVision.Tests/Input/CaptureManagerTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ButtonTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ScrollBarTests.cs`

- [ ] **Step 1: Add failing public-behavior tests**

```csharp
[Fact]
public void Dispatch_WhenPointerMovesOverButtonContent_HoversButtonInsteadOfText()
{
    manager.Dispatch(Pointer.Move(point));

    button.IsHovered.ShouldBeTrue();
    button.Content!.IsHovered.ShouldBeFalse();
    manager.Hovered.ShouldBeSameAs(button);
}

[Fact]
public void Dispatch_WhenButtonCapturesAndPointerMovesOverAnotherControl_RoutesToCaptureButHoversPhysicalTarget()
{
    manager.Dispatch(Pointer.Move(secondPoint));

    manager.Captured.ShouldBeSameAs(first);
    manager.Hovered.ShouldBeSameAs(second);
    first.IsHovered.ShouldBeFalse();
    second.IsHovered.ShouldBeTrue();
}
```

Add a direct ScrollBar leaf test and leave/disable cleanup assertions.

- [ ] **Step 2: Verify red**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --no-restore --filter-class "*CaptureManagerTests" --filter-class "*ButtonTests" --filter-class "*ScrollBarTests"
```

Expected: the composite test finds the `Text` child hovered, and capture
incorrectly remains the hover target.

## Task 3: Resolve physical hover to semantic controls

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/Pressable.cs`
- Modify: `src/SharpVision/Input/CaptureManager.cs`
- Modify: `docs/concepts/input-routing.md`
- Modify: `docs/controls/control.md`

- [ ] **Step 1: Add the internal hover-owner hook**

```csharp
/// <summary>Gets whether this control owns hover for hit-tested descendants.</summary>
internal virtual bool OwnsHover => false;
```

- [ ] **Step 2: Mark pressable composites as owners**

```csharp
/// <inheritdoc/>
internal override bool OwnsHover => true;
```

- [ ] **Step 3: Resolve hover independently from capture**

```csharp
var physical = pointer.Action == PointerAction.Leave ? null : Root.HitTest(pointer.Cells);
var target = IsEligible(Captured) ? Captured : physical;
SetHovered(ResolveHover(physical));

private static Control? ResolveHover(Control? physical)
{
    for (var current = physical; current is not null; current = current.Parent)
    {
        if (current.OwnsHover)
        {
            return current;
        }
    }

    return physical;
}
```

Keep route construction, capture acquisition, pressed transitions, and
activation unchanged. Existing leave, unavailable, disposal, and focus-loss
cleanup clears the resolved owner.

- [ ] **Step 4: Update contracts, verify green, and commit**

Document that `IsHovered` belongs to the nearest semantic owner and capture
affects routed delivery but not physical hover.

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --no-restore --filter-class "*CaptureManagerTests" --filter-class "*ButtonTests" --filter-class "*ScrollBarTests"
git add src/SharpVision/Controls/Control.cs src/SharpVision/Controls/Pressable.cs src/SharpVision/Input/CaptureManager.cs docs/concepts/input-routing.md docs/controls/control.md tests/SharpVision.Tests/Input/CaptureManagerTests.cs tests/SharpVision.Tests/Controls/ButtonTests.cs tests/SharpVision.Tests/Controls/ScrollBarTests.cs
git commit -m "feat(input): resolve hover to semantic controls"
```

Expected: all focused tests pass.

## Task 4: Make Showcase state contrast observable

**Files:**

- Modify: `src/SharpVision.Showcase/Palette.cs`
- Modify: `src/SharpVision.Showcase/Examples.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs`
- Modify: `scripts/capture-showcase.sh`
- Modify: `docs/testing/showcase.md`

- [ ] **Step 1: Add failing passive-motion tests**

```csharp
[Fact]
public async Task Input_WhenPointerMovesOverButtonContent_HighlightsTheButtonAsync()
{
    terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b[<35;{x};{y}M"));

    await WaitUntilAsync(() => button.IsHovered, application, "button hover");
    button.Appearance.Background.ShouldBe(Palette.Hover);
}
```

Add the same no-button SGR proof for one sidebar entry, exact virtual-frame
change, and leave cleanup.

- [ ] **Step 2: Verify red, then add state overlays**

```csharp
style.Set(State.Normal, new Appearance(Palette.Text, Palette.Surface));
style.Set(State.Hovered, new Appearance(Palette.Text, Palette.Hover, Attributes.Bold));
style.Set(State.Focused, new Appearance(Palette.Accent, Palette.Surface, Attributes.Bold));
style.Set(State.Pressed, new Appearance(Palette.Text, Palette.Pressed, Attributes.Bold));
style.Set(State.Disabled, new Appearance(Palette.Muted, Palette.Panel, Attributes.Dim));
```

Use a dedicated Palette factory for interactive samples. Disabled specimens stay
actually disabled. Preserve the specialized sidebar palette.

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --no-restore --filter-class "*GalleryInteractionTests" --filter-class "*StartupOptionsTests"
```

Expected before implementation: no-button motion is absent or invisible.
Expected after implementation: tests pass.

- [ ] **Step 3: Extend tmux proof and commit**

Inject a no-button SGR move over a sidebar entry, wait for hover indication,
send a leave report, then continue click, Figlet, and ScrollBar drag checks.

```bash
scripts/capture-showcase.sh /tmp/sharpvision-hover.png
git add src/SharpVision.Showcase/Palette.cs src/SharpVision.Showcase/Examples.cs tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs scripts/capture-showcase.sh docs/testing/showcase.md
git commit -m "feat(showcase): show interactive hover states"
```

Expected: tmux reports `Captured live SharpVision pane` after passive hover and
leave cleanup.

## Task 5: Run repository-wide verification

**Files:**

- Modify: `docs/images/showcase-dashboard.png`

- [ ] **Step 1: Run all gates**

```bash
make format
make lint
make build
make test
```

Expected: zero formatting, lint, link, build, warning, or test failures.

- [ ] **Step 2: Refresh visual proof and commit**

```bash
scripts/capture-showcase.sh docs/images/showcase-dashboard.png
git diff --check
git add docs/images/showcase-dashboard.png
git commit -m "docs(showcase): refresh interactive dashboard capture"
```

Expected: capture completes after passive-hover, navigation, Figlet, and
ScrollBar checks; whitespace validation is clean.

## Plan self-review

The plan covers the hover specification end-to-end: `1003` policy in Task 1,
semantic composite ownership and capture separation in Tasks 2–3, visible
Showcase state contrast and tmux proof in Task 4, and repository gates in
Task 5. It intentionally excludes preserve-background drawing, popup/combo-box
work, and the broader recipe-page overhaul; those independent changes follow
this verified hover foundation.
