# Control Surface Phase Two Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add intended-behavior mounted surface suites for TextInput and
ScrollBar, extending the harness with real text, paste, navigation, cursor,
relative pointer, wheel, and drag actions only as demanded by those tests.

**Architecture:** Both controls remain mounted beneath the real `Application`.
Keyboard and pointer helpers encode terminal bytes, resize continues through
`IResizeSource`, public mutations remain dispatcher-affine, and assertions
combine public editor/range state with exact terminal cells and cursor state.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, Kitty CSI-u, legacy CSI
navigation, SGR mouse/wheel reports, bracketed paste, semantic terminal frames,
and the independent component screen model.

---

## File structure

- Modify `tests/SharpVision.Tests/Support/ComponentKeyboard.cs` for UTF-8 text,
  bracketed paste, navigation keys, and Shift-modified movement.
- Modify `tests/SharpVision.Tests/Support/ComponentScreen.cs` and
  `ComponentSurface.cs` for final terminal cursor visibility and position.
- Modify `tests/SharpVision.Tests/Support/ComponentPointer.cs` for
  target-relative cells, wheel reports, and held-primary drag.
- Extend `tests/SharpVision.Tests/Support/ComponentSurfaceTests.cs` with one
  cross-layer proof for each new helper family.
- Create `tests/SharpVision.Tests/Controls/TextInputSurfaceTests.cs`.
- Create `tests/SharpVision.Tests/Controls/ScrollBarSurfaceTests.cs`.
- Modify `docs/controls/input/text-input.md`,
  `docs/controls/layout/scroll-bar.md`, and
  `docs/testing/controls-integration.md`.

### Task 1: Add text, navigation, paste, and cursor notation

**Files:**

- Modify: `tests/SharpVision.Tests/Support/ComponentKeyboard.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentScreen.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurface.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurfaceTests.cs`

- [x] **Step 1: Write failing helper-contract tests**

Mount a single-line TextInput, focus it with Tab, type `Á界`, press Left and
Backspace, then send one bracketed paste. Assert public text and final cursor
state through wished-for APIs:

```csharp
await surface.Keyboard.TypeAsync("A\u0301界");
await surface.Keyboard.PressAsync(Code.Left);
await surface.Keyboard.PressAsync(Code.Backspace);
await surface.Keyboard.PasteAsync("e\u0301");

input.Text.ShouldBe("e\u0301界");
surface.ShouldHaveCursor(new Point(1, 0), visible: true);
```

Add a separate Shift+Left assertion through
`PressAsync(Code.Left, Modifiers.Shift)` so modifier encoding is not inferred
from the TextInput suite alone.

- [x] **Step 2: Run `*ComponentSurfaceTests` and verify RED**

Expected: compilation fails because typing, paste, navigation overloads, and
cursor assertions do not exist.

- [x] **Step 3: Encode only the required keyboard actions**

`TypeAsync` validates non-empty text without terminal controls and sends its
owned UTF-8 bytes once. `PasteAsync` validates non-null text and emits one owned
`ESC[200~ payload ESC[201~` transaction. Add exact legacy encodings for Left,
Right, Up, Down, Home, End, Backspace, Delete, PageUp, PageDown, and Kitty
Enter. Use CSI modifier parameters for Shift movement; unsupported modifier/code
pairs throw before bytes are queued.

```csharp
internal Task TypeAsync(string value) =>
    _surface.SendAsync(Encoding.UTF8.GetBytes(value), "type text");

internal Task PasteAsync(string value) =>
    _surface.SendAsync(
        "\u001b[200~"u8.ToArray()
            .Concat(Encoding.UTF8.GetBytes(value))
            .Concat("\u001b[201~"u8.ToArray())
            .ToArray(),
        "paste text");
```

Use a single allocation-oriented buffer construction in the implementation; the
concatenation above documents bytes, not a hot-path requirement.

- [x] **Step 4: Model final cursor semantics**

Teach `ComponentScreen.Csi` to apply DEC private `?25h` and `?25l`, expose its
locked cursor position and visibility, and add
`ComponentSurface.ShouldHaveCursor(Point, bool)`. Validate the point against the
current resized screen before comparison.

- [x] **Step 5: Verify helper and Button surface fixtures GREEN**

Run `*ComponentSurfaceTests` and `*ButtonSurfaceTests`. Expected: all tests pass
and existing screen parsing remains unchanged.

- [x] **Step 6: Commit the keyboard/cursor harness slice**

Commit only these helper files as
`test: drive text input on component surfaces`.

### Task 2: Add TextInput intended-behavior surface coverage

**Files:**

- Create: `tests/SharpVision.Tests/Controls/TextInputSurfaceTests.cs`
- Modify: `docs/controls/input/text-input.md`

- [x] **Step 1: Write placeholder, focus, typing, and grapheme tests**

Prove unfocused placeholder cells are dim with hidden cursor; Tab focuses and
reveals the cursor; raw UTF-8 inserts combining and wide graphemes; Left/Right,
Backspace, and Delete never split a cluster; and exact continuation cells match
the caret index.

```csharp
await surface.Keyboard.PressAsync(Code.Tab);
await surface.Keyboard.TypeAsync("A\u0301界");

input.Text.ShouldBe("A\u0301界");
input.CaretIndex.ShouldBe(3);
surface.Cell(new Point(1, 0)).Text.ShouldBe("界");
surface.Cell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
surface.ShouldHaveCursor(new Point(3, 0), visible: true);
```

- [x] **Step 2: Verify RED and fix only demonstrated editor/cursor defects**

Run `*TextInputSurfaceTests`. Every failure must identify decoded input,
grapheme transaction, focus, committed cursor, or final-cell disagreement.
Retain one regression per production correction.

- [x] **Step 3: Add selection, paste, submit, and policy scenarios**

Prove Shift selection, atomic combining-sequence paste, single-line Enter
submission, multiline Enter insertion, read-only refusal, disabled refusal,
password masking without source text in snapshots, and selection styling across
a wide lead/continuation pair.

- [x] **Step 4: Add resize and automatic offset repair**

Type content wider and taller than a fixed editor, assert horizontal/vertical
offsets and cursor, resize the same surface, and prove offsets clamp while stale
cells disappear. Use public `HorizontalOffset`/`VerticalOffset` as the semantic
oracle and final screen/cursor as cross-layer proof.

- [x] **Step 5: Run TextInput surface and unit fixtures GREEN**

Run `*TextInputSurfaceTests`, `*TextInputTests`, and `*EditTests`. Expected: all
editor, pure model, Unicode, and mounted behavior passes.

- [x] **Step 6: Link the TextInput proof and commit**

Update the control test obligations and commit the fixture plus only verified
production fixes as `test: cover text input on mounted surfaces`.

### Task 3: Add relative pointer, wheel, and captured drag notation

**Files:**

- Modify: `tests/SharpVision.Tests/Support/ComponentPointer.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurface.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurfaceTests.cs`

- [x] **Step 1: Write failing pointer helper tests**

Mount a horizontal ScrollBar and demand target-relative click, wheel, and drag
APIs. The test clicks the increment button, wheels toward the range, then drags
the thumb to the opposite endpoint while asserting values and released state.

```csharp
await surface.Pointer.ClickAsync(bar, new Point(11, 0));
await surface.Pointer.WheelAsync(bar, new Point(6, 0), wheelY: -1);
await surface.Pointer.DragAsync(
    bar,
    new Point(1, 0),
    new Point(10, 0));
```

- [x] **Step 2: Run helper fixture and verify RED**

Expected: compilation fails because relative points, wheel, and drag do not
exist.

- [x] **Step 3: Resolve validated relative points**

Add a dispatcher-side resolver that verifies ownership, non-empty bounds, and
relative containment before converting to an absolute surface cell. Foreign,
negative, and right/bottom-edge offsets fail before input is queued.

- [x] **Step 4: Encode wheel and held-primary motion**

Wheel up/down uses SGR buttons 64/65. Drag emits a normal move to the start,
primary press, button-32 motion to the end, and primary release at the end. Each
record settles separately so capture, value, pressed state, and final frame are
observable at every checkpoint.

- [x] **Step 5: Verify helper and existing pointer fixtures GREEN**

Run `*ComponentSurfaceTests`, `*ButtonSurfaceTests`, and
`*InteractiveControlTests`.

- [x] **Step 6: Commit the pointer harness slice**

Commit only helper/test files as
`test: drive wheel and drag on component surfaces`.

### Task 4: Add ScrollBar intended-behavior surface coverage

**Files:**

- Create: `tests/SharpVision.Tests/Controls/ScrollBarSurfaceTests.cs`
- Modify: `docs/controls/layout/scroll-bar.md`

- [ ] **Step 1: Write exact horizontal and vertical appearance scenarios**

Mount full-chrome bars with deterministic ASCII glyphs. Assert exact buttons,
track, thumb length/position, focused/hovered/pressed styles,
one-/two-/three-cell fallbacks, and resize recomputation.

- [ ] **Step 2: Write keyboard, pointer, and wheel cause scenarios**

Prove arrows, Page, Home/End, buttons, track presses, and wheel use their public
changes and exact `Cause`. At an endpoint, assert an unchanged wheel produces no
event or value change.

- [ ] **Step 3: Write captured thumb-drag and cancellation scenarios**

Drag the thumb by cells to the endpoint, assert held pressed state during the
sequence through explicit press/move/release steps, and prove release clears it.
In a separate mounted instance, disable during a held drag and assert capture
cancellation causes no spurious value event.

- [ ] **Step 4: Verify RED and apply minimal production fixes**

Run `*ScrollBarSurfaceTests`. Fix only demonstrated range, geometry, routing,
capture, style, or rendering defects and retain every failing scenario.

- [ ] **Step 5: Verify ScrollBar and scrolling regressions GREEN**

Run `*ScrollBarSurfaceTests`, `*ScrollBarTests`, `*ScrollingTests`, and
`*AmbiguousWidthControlTests`.

- [ ] **Step 6: Link the ScrollBar proof and commit**

Update its test obligations and commit as
`test: cover scroll bars on mounted surfaces`.

### Task 5: Close phase two

**Files:**

- Modify: `docs/testing/controls-integration.md`
- Modify: `docs/superpowers/plans/2026-07-16-control-surface-phase-two.md`

- [ ] **Step 1: Audit scenario coverage**

Map every TextInput and ScrollBar scenario in the umbrella design to a named
surface or unit test. Add missing cursor, Unicode, tiny, disabled, resize,
endpoint, or cleanup evidence before proceeding.

- [ ] **Step 2: Update mounted-action documentation**

Document exact typing, paste, navigation, cursor, relative-cell, wheel, and drag
notation plus validation/settling rules. Keep raw terminal bytes as the only
input path.

- [ ] **Step 3: Run repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings/errors, docs and links pass, all discovered tests pass,
and isolated package consumption succeeds.

- [ ] **Step 4: Commit phase documentation**

Commit only intentional phase-two files as
`docs: complete second control surface coverage phase`.
