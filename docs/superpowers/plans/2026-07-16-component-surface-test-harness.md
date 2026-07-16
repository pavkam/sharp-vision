# Component Surface Test Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable full-stack component test surface and prove Button
appearance and behavior in normal, hovered, pressed, focused, bordered, and
shadowed combinations.

**Architecture:** Internal test helpers mount a control beneath a real
`Application`, encode typed actions as terminal bytes, and apply renderer writes
to an independent semantic screen. Button tests combine exact text snapshots
with public visual-state and representative cell-style assertions.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, SharpVision terminal parser,
real `Application`/`Session`/renderer pipeline.

---

## File structure

- Create `tests/SharpVision.Tests/Support/SurfaceCell.cs` for one immutable
  modeled terminal cell.
- Create `tests/SharpVision.Tests/Support/ComponentScreen.cs` for independent
  ANSI output application and text snapshots.
- Create `tests/SharpVision.Tests/Support/ComponentTerminal.cs` for
  deterministic input, resize, writes, and consumption barriers.
- Create `tests/SharpVision.Tests/Support/ComponentSurface.cs` for mount,
  lifecycle, settling, cell, snapshot, and state assertions.
- Create `tests/SharpVision.Tests/Support/ComponentPointer.cs` for SGR mouse
  movement, primary press, release, and click.
- Create `tests/SharpVision.Tests/Support/ComponentKeyboard.cs` for the real Tab
  key sequence used by Button focus proof.
- Create `tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs` as the only
  initial consumer-control fixture.
- Modify `docs/testing/controls-integration.md` to define the mounted component
  proof pattern.
- Modify `docs/controls/input/button.md` to require Button surface coverage.

### Task 1: Mount and inspect a normal Button

**Files:**

- Create: `tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Support/SurfaceCell.cs`
- Create: `tests/SharpVision.Tests/Support/ComponentScreen.cs`
- Create: `tests/SharpVision.Tests/Support/ComponentTerminal.cs`
- Create: `tests/SharpVision.Tests/Support/ComponentSurface.cs`

- [ ] **Step 1: Write the failing normal-state test**

Add `Render_WhenButtonIsMounted_ShowsNormalFaceAndCompositeShadowAsync`. It
constructs an 8×3 left/top-aligned Button with `Save` content, mounts it in a
10×5 surface, and uses the wished-for API:

```csharp
await using var surface = await ComponentSurface.MountAsync(
    button,
    new Size(10, 5),
    TestContext.Current.CancellationToken);

surface.ShouldHaveState(button, State.Normal);
surface.ShouldRender("""
╭──────╮
│Save  │
╰──────╯


""");
surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(8));
surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ButtonSurfaceTests" --timeout 60s
```

Expected: compilation fails because `ComponentSurface` does not exist.

- [ ] **Step 3: Implement the independent cell and screen model**

`SurfaceCell` is an immutable `readonly record struct` with explicit
constructor, validated text/width/continuation ownership, and `Text`, `Style`,
`Width`, `IsContinuation`, and `LeadX` properties. `ComponentScreen` implements
`ISequenceSink`, applies CUP, cursor visibility, OSC 8, reset, standard
16-color, indexed-color, RGB-color, attribute, underline, and underline-color
sequences, and exposes validated `Cell(Point)` and right-padded `Text`.

- [ ] **Step 4: Implement the terminal and mounted surface lifecycle**

`ComponentTerminal` implements `ITransport` and `IResizeSource` with channels.
Each queued input carries a consumption completion, each write is copied and
applied to `ComponentScreen`, and each resize is immutable.

`ComponentSurface.MountAsync` validates the detached control and positive size,
adds it to a private `Overlay`, starts a real `Application`, and waits for the
first `FrameRendered` event. It exposes:

```csharp
internal static Task<ComponentSurface> MountAsync(
    Control control,
    Size size,
    CancellationToken cancellationToken);

internal SurfaceCell Cell(Point point);
internal void ShouldRender(string expected);
internal void ShouldHaveState(Control control, State expected);
public ValueTask DisposeAsync();
```

`ShouldHaveState` derives Normal, Hovered, Focused, Pressed, and Disabled flags
from public control state and rejects state bits it cannot observe.
`ShouldRender` normalizes line endings, removes only the raw-string boundary
newline, right-pads each row, and reports row-delimited expected/actual output.

- [ ] **Step 5: Run the focused test and verify GREEN**

Run the Task 1 command. Expected: one discovered test passes with no warnings.

- [ ] **Step 6: Commit the normal mount slice**

```bash
git add tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs \
  tests/SharpVision.Tests/Support/SurfaceCell.cs \
  tests/SharpVision.Tests/Support/ComponentScreen.cs \
  tests/SharpVision.Tests/Support/ComponentTerminal.cs \
  tests/SharpVision.Tests/Support/ComponentSurface.cs
git commit -m "test: mount controls in a terminal surface"
```

### Task 2: Drive hover and pressed Button states through terminal mouse input

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Support/ComponentPointer.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurface.cs`

- [ ] **Step 1: Write the failing hover test**

Add `Pointer_WhenMovedOverButton_ShowsHoveredAppearanceAsync`. Call
`await surface.Pointer.MoveToAsync(button)`, then assert `State.Hovered`, normal
geometry, accent border/foreground `Color.Indexed(14)`, and a dim detached
shadow. Do not call `SetHovered` or `Router.Route`.

- [ ] **Step 2: Run the focused fixture and verify RED**

Run the Task 1 command. Expected: compilation fails because `Pointer` and
`ComponentPointer` do not exist.

- [ ] **Step 3: Implement pointer move and deterministic settling**

`ComponentPointer.MoveToAsync(Control)` resolves the target's center cell on the
application dispatcher and emits `ESC [ < 35 ; column ; row M`. The point
overload validates surface bounds. `ComponentSurface.SendAsync` waits for input
consumption, then an application idle/frame completion barrier, with the test
cancellation token and a two-second diagnostic timeout.

- [ ] **Step 4: Run the hover test and verify GREEN**

Run the Task 1 command. Expected: normal and hover tests pass.

- [ ] **Step 5: Write the failing pressed test**

Add `Pointer_WhenPrimaryButtonIsHeld_ShowsPressedTranslatedFaceAsync`. Call
`MoveToAsync(button)` then `PressAsync()`. Assert Hovered, Focused, and Pressed,
the face translated by `(1,1)`, the normal origin cleared, content translated,
and the detached shadow retains normal styling.

- [ ] **Step 6: Implement press, release, and click**

`PressAsync` emits SGR button `0` with final `M` at the last point;
`ReleaseAsync` emits button `0` with final `m`; `ClickAsync(Control)` performs
move, press, and release through three independently settled inputs. A missing
last point fails before queuing bytes.

- [ ] **Step 7: Run the pressed test and verify GREEN**

Run the Task 1 command. Expected: all mouse-state tests pass and disposal clears
capture.

- [ ] **Step 8: Commit the pointer slice**

```bash
git add tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs \
  tests/SharpVision.Tests/Support/ComponentPointer.cs \
  tests/SharpVision.Tests/Support/ComponentSurface.cs
git commit -m "test: drive mounted controls with pointer input"
```

### Task 3: Focus Button through real keyboard decoding

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Support/ComponentKeyboard.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurface.cs`

- [ ] **Step 1: Write the failing focus test**

Add `Keyboard_WhenTabIsPressed_ShowsFocusedAppearanceAsync`. Call
`await surface.Keyboard.PressAsync(Code.Tab)`, assert exactly `State.Focused`,
`button.IsFocused`, unchanged geometry, and accent foreground/border cells.

- [ ] **Step 2: Run the fixture and verify RED**

Run the Task 1 command. Expected: compilation fails because `Keyboard` and
`ComponentKeyboard` do not exist.

- [ ] **Step 3: Implement the minimal keyboard driver**

`ComponentKeyboard.PressAsync(Code)` accepts only `Code.Tab` for this slice and
emits the real tab byte (`0x09`) through `ComponentSurface.SendAsync`. Undefined
enum values and unsupported defined codes throw `ArgumentOutOfRangeException` or
`NotSupportedException` before input is queued.

- [ ] **Step 4: Run the focus test and verify GREEN**

Run the Task 1 command. Expected: all state tests pass, proving Tab decoding,
input routing, focus navigation, invalidation, rendering, and final cells.

- [ ] **Step 5: Commit the keyboard slice**

```bash
git add tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs \
  tests/SharpVision.Tests/Support/ComponentKeyboard.cs \
  tests/SharpVision.Tests/Support/ComponentSurface.cs
git commit -m "test: drive mounted controls with keyboard input"
```

### Task 4: Cover every Button border and shadow combination

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs`

- [ ] **Step 1: Add the failing chrome matrix**

Add a theory named
`Pointer_WhenChromeCombinationIsPressed_RendersExpectedGeometryAsync` with four
member-data cases: border/shadow both present, border only, shadow only, and
neither. Use `ShadowMode.BlockGlyph` for shadow-present matrix cases so text and
style independently expose the footprint. Each case supplies exact released and
pressed surface text.

- [ ] **Step 2: Run the matrix and verify RED where Button is broken**

Run the Task 1 command. Expected: every mismatch reports bounded rows and cell
coordinates. If all existing behavior is correct, first deliberately invert one
expected row to prove the oracle fails, then restore the specified expectation.

- [ ] **Step 3: Fix only demonstrated Button/core defects**

For each genuine failing case, write a separately named regression test before
changing production behavior. Modify only the smallest responsible Button,
layout, hit-test, chrome, or rendering code and update its normative contract.
Do not weaken snapshots or bypass the real input path to accommodate a defect.

- [ ] **Step 4: Run the complete Button surface fixture**

Run the Task 1 command. Expected: normal, hover, pressed, focus, and all four
chrome combinations pass.

- [ ] **Step 5: Commit the chrome proof and any verified fix**

Stage only the Button surface files and any production/spec files changed by a
demonstrated regression. Commit with a message naming that behavior.

### Task 5: Document the reusable testing contract

**Files:**

- Modify: `docs/testing/controls-integration.md`
- Modify: `docs/controls/input/button.md`

- [ ] **Step 1: Add the component-surface testing contract**

Specify real application mounting, terminal-byte actions, deterministic
settling, independent virtual-screen output, semantic state/style assertions,
inline text right-padding, action diagnostics, and the rule that snapshots
supplement rather than replace semantic assertions.

- [ ] **Step 2: Link Button to its mounted proof**

In the Button test obligations, require normal, hovered, pressed, and focused
surface states plus the complete border/shadow matrix. Link directly to the new
testing-contract section.

- [ ] **Step 3: Validate the changed Markdown**

```bash
npx prettier --write docs/testing/controls-integration.md \
  docs/controls/input/button.md
npx markdownlint-cli2 docs/testing/controls-integration.md \
  docs/controls/input/button.md
npm run lint:links
```

Expected: formatting, Markdown, and links pass.

- [ ] **Step 4: Commit the normative docs**

```bash
git add docs/testing/controls-integration.md docs/controls/input/button.md
git commit -m "docs: specify mounted component testing"
```

### Task 6: Verify the completed goal

**Files:**

- Verify all intentional files from Tasks 1-5.

- [ ] **Step 1: Run the focused fixture**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ButtonSurfaceTests" --timeout 60s
```

Expected: every Button surface test passes.

- [ ] **Step 2: Run the project suite**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --timeout 60s
```

Expected: all discovered SharpVision tests pass.

- [ ] **Step 3: Run repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings, zero errors, test discovery at or above configured
minimums, and no Markdown or link failures.

- [ ] **Step 4: Audit the diff and requirements**

Confirm only Button consumes the new harness; actions traverse raw bytes;
normal, hovered, pressed, focused, and four chrome combinations have state,
text, and style evidence; docs link to the implementation; and unrelated dirty
files remain unstaged and unmodified by this work.
