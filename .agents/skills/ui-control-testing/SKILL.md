---
name: ui-control-testing
description:
  Use when writing, reviewing, or debugging SharpVision control tests — unit
  layout tests with Engine/ProbeControl, surface tests with ComponentSurface for
  rendering and interaction, showcase pane verification by running the app in
  tmux and checking visual output, or when a control's showcase page has
  alignment, clipping, or text issues that need an iterative fix-rebuild-check
  loop.
---

# UI Control Testing

## Overview

SharpVision controls are tested at three levels: **unit** (layout math),
**surface** (mounted rendering and interaction), and **visual** (showcase app in
a real terminal). Each level catches different classes of bugs.

## Level 1 — Unit layout tests

Synchronous tests using `Engine().Layout()` with `ProbeControl`.

### Pattern

```csharp
[Fact]
public void Layout_WhenChildHasPercentWidth_ResolvesAgainstSlot()
{
    var panel = new Dock();
    var child = new ProbeControl(new Size(3, 1)) { Width = Length.Percent(50) };
    panel.Children.Add(child);

    new Engine().Layout(panel, new Size(10, 1));

    child.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
}
```

### What to assert

| Property                   | Proves                                  |
| -------------------------- | --------------------------------------- |
| `control.DesiredSize`      | Measure produced correct intrinsic size |
| `control.Bounds`           | Arrange placed the control correctly    |
| `probe.MeasureConstraints` | Parent passed correct constraint        |
| `probe.ArrangeBounds`      | Parent passed correct arrange rect      |

### ProbeControl constructor

`new ProbeControl(new Size(w, h))` — the size is returned from
`MeasureOverride`, making layout deterministic. Default is `Size(0, 0)`.

## Level 2 — Surface tests

Async tests mounting a real control in an in-memory terminal via
`ComponentSurface`.

### Pattern

```csharp
[Fact]
public async Task Render_WhenMounted_DrawsExpectedLayoutAsync()
{
    var control = new MyControl { Value = 42 };
    await using var surface = await ComponentSurface.MountAsync(
        control, new Size(20, 5), TestContext.Current.CancellationToken);

    control.Bounds.Width.ShouldBeGreaterThan(0);
    surface.ShouldRender("""
                         expected output here
                         """);
}
```

### Key APIs

**Mounting:**

- `ComponentSurface.MountAsync(control, size, ct)` — mount a single control
- `ComponentSurface.MountScreenAsync(screen, size, ct)` — mount a Screen

**Rendering assertions:**

- `surface.ShouldRender("expected")` — exact text match (trailing blanks padded)
- `surface.Cell(point)` — get one cell (`.Text`, `.Style`)

**State assertions:**

- `surface.ShouldHaveState(control, VisualState.Focused | VisualState.PointerOver)`
- `surface.ShouldHaveFocus(control)`

**Mutations:**

- `surface.UpdateAsync(() => { control.Value = 99; }, "update value")`
- `surface.ResizeAsync(new Size(30, 10))`

### Keyboard interaction

```csharp
await surface.Keyboard.PressAsync(Code.Tab);        // focus
await surface.Keyboard.PressAsync(Code.Right);       // arrow key
await surface.Keyboard.PressAsync(Code.Up);          // arrow key
await surface.Keyboard.TypeAsync("42");              // printable text
await surface.Keyboard.PasteAsync("pasted text");   // bracketed paste
```

### Pointer interaction

```csharp
await surface.Pointer.MoveToAsync(control);                    // hover
await surface.Pointer.ClickAsync(control);                     // click center
await surface.Pointer.ClickAsync(control, new Point(2, 0));    // click offset
await surface.Pointer.DragAsync(control, start, end);          // drag
await surface.Pointer.LeaveAsync();                            // pointer exit
```

### Focusing a control inside a container

When the control is nested, use `UpdateAsync` with direct focus:

```csharp
await surface.UpdateAsync(
    () => surface.Application.Focus.Focus(input).ShouldBeTrue(),
    "focus the nested control");
```

### ComponentBehaviorEvidence

Every public control type needs coverage. Add the attribute to your test method:

```csharp
[ComponentBehaviorEvidence(typeof(MyControl),
    ComponentBehavior.Mounted | ComponentBehavior.Hover | ComponentBehavior.Focus)]
[Fact]
public async Task Render_WhenMounted_DrawsCorrectlyAsync() { ... }
```

The coverage system enforces that every control has exactly one of each
exclusive pair (Hover/HoverExcluded, Focus/FocusExcluded, Tab/TabExcluded,
Directional/DirectionalExcluded, PressRelease/PressReleaseExcluded).

## Level 3 — Visual showcase verification

Run the real showcase app, navigate to the pane, and verify the visual output.

### Launch in tmux

```bash
# Start the showcase in a named tmux session
tmux new-session -d -s showcase -x 120 -y 40 \
  'dotnet run --project src/SharpVision.Showcase'

# Wait for the app to render
sleep 3

# Capture the terminal output
tmux capture-pane -t showcase -p > /tmp/showcase-output.txt
```

### Navigate to a specific pane

Use tmux `send-keys` to navigate the Gallery sidebar:

```bash
# Focus the filter, type the pane name, press Enter
tmux send-keys -t showcase 'Tab' && sleep 0.3
tmux send-keys -t showcase 'Button' && sleep 0.5
tmux send-keys -t showcase 'Enter' && sleep 1

# Capture the rendered pane
tmux capture-pane -t showcase -p > /tmp/pane-output.txt
```

Or use arrow keys to navigate the NavigationView:

```bash
tmux send-keys -t showcase Down && sleep 0.3
tmux send-keys -t showcase Down && sleep 0.3
tmux send-keys -t showcase Enter && sleep 1
```

### Capture and inspect

```bash
# Capture with escape sequences for color inspection
tmux capture-pane -t showcase -p -e > /tmp/pane-styled.txt

# Read the plain-text output
cat /tmp/pane-output.txt
```

### Iterative fix loop

When a showcase pane has visual issues (misalignment, clipping, cut-off text),
iterate:

1. **Capture** — get the current output via tmux
2. **Diagnose** — read the output, identify what's wrong (element positions,
   clipping, text overflow)
3. **Fix** — edit the showcase pane code (widths, heights, alignment, text)
4. **Rebuild** — the app reloads on rebuild:

   ```bash
   # Kill and restart (or use dotnet watch)
   tmux send-keys -t showcase C-c && sleep 1
   tmux send-keys -t showcase 'dotnet run --project src/SharpVision.Showcase' Enter
   sleep 3
   ```

5. **Re-navigate** — go back to the pane and capture again
6. **Verify** — compare with expected layout; repeat from step 3 if not correct

### Cleanup

```bash
tmux kill-session -t showcase
```

## Naming conventions

`{Category}_When{Condition}_{Outcome}Async`

| Prefix      | Use                                                 |
| ----------- | --------------------------------------------------- |
| `Render_`   | Initial appearance, layout-driven visual output     |
| `Pointer_`  | Mouse/pointer interaction                           |
| `Keyboard_` | Keyboard interaction                                |
| `Input_`    | Combined pointer + keyboard                         |
| `Focus_`    | Focus-specific behavior                             |
| `Layout_`   | Synchronous layout engine tests (no Async suffix)   |
| `Measure_`  | Measurement-specific layout tests (no Async suffix) |
| `Surface_`  | General surface behavior                            |

## Testing event bubbling

Verify that a focused child handles keys first, and unhandled keys bubble to the
parent (e.g., an AutoScroll container):

```csharp
[Fact]
public async Task Keyboard_WhenInsideAutoScrollContainer_KeysReachChildFirstAsync()
{
    var input = new TimeInput { Value = new TimeOnly(10, 30) };
    var filler = new ControlText(string.Join('\n',
        Enumerable.Range(0, 20).Select(i => $"Line {i}")));
    var body = new Stack
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Vertical,
        Children = { input, filler }
    };
    await using var surface = await ComponentSurface.MountAsync(
        body, new Size(20, 6), TestContext.Current.CancellationToken);

    await surface.UpdateAsync(
        () => surface.Application.Focus.Focus(input).ShouldBeTrue(),
        "focus child");

    // Child handles Right (moves segment) — does NOT scroll parent
    await surface.Keyboard.PressAsync(Code.Right);
    await surface.Keyboard.PressAsync(Code.Up);
    input.Value!.Value.Minute.ShouldBe(31);  // child handled it
}
```

## Showcase pane structure

Panes follow a standard hierarchy:

```text
DocPage(title, overview,
    DocSection(icon, heading, description,
        DocExample(heading, description, specimen, source?)))
```

- `DocColumn(children...)` — vertical stack, spacing 1
- `DocRow(children...)` — horizontal stack, spacing 2
- `DocExample` wraps the specimen in a `GroupBox` labeled "Example"
- `DocPage` body is a `Stack { AutoScroll = true }` — percentage/star widths
  resolve against this scrollable area

## Common mistakes

- Forgetting `Overflow = Overflow.Clip` on `ControlText` in surface tests — text
  wraps unexpectedly without it.
- Not setting `HorizontalAlignment = Stretch` on showcase wrappers for
  Percent/Star samples — the wrapper shrinks to content, double-resolving the
  percentage.
- Using `_ = ScrollBy(...)` instead of checking the return value — swallows the
  key event even when no scrolling occurred.
- Testing only with `ComponentSurface` and never running the real showcase —
  surface tests don't catch container hierarchy issues (event interception by
  AutoScroll parents, modal boundaries, etc.).
