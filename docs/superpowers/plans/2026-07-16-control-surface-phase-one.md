# Control Surface Phase One Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add intended-behavior mounted surface suites for Text, FigletText,
Separator, ProgressBar, CheckBox, and RadioButton, implementing and showcasing
the two missing display controls and fixing every defect the new tests expose.

**Architecture:** Existing controls mount beneath the real `Application` and are
driven by terminal bytes or dispatcher-affine public mutation. The harness adds
only update and keyboard actions demanded by these scenarios; exact screen rows
are paired with semantic state, event, style, and Unicode-cell assertions.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, SharpVision retained
controls, real terminal input/session/rendering pipeline, independent component
screen model.

---

## File structure

- Modify `tests/SharpVision.Tests/Support/ComponentSurface.cs` to support
  dispatcher-safe public mutations and descendant state assertions.
- Modify `tests/SharpVision.Tests/Support/ComponentKeyboard.cs` to encode
  complete Kitty press/release actions, arrows, and UTF-8 text.
- Create `tests/SharpVision.Tests/Controls/TextSurfaceTests.cs` for markup,
  Unicode, overflow, mutation, and resize-facing Text proof.
- Create `tests/SharpVision.Tests/Controls/FigletTextSurfaceTests.cs` for exact
  FIGlet art, style, mutation, and clipping proof.
- Create `src/SharpVision/Controls/Separator.cs` and
  `tests/SharpVision.Tests/Controls/SeparatorSurfaceTests.cs`.
- Create `src/SharpVision/Controls/ProgressBar.cs` and
  `tests/SharpVision.Tests/Controls/ProgressBarSurfaceTests.cs`.
- Create `tests/SharpVision.Tests/Controls/CheckBoxSurfaceTests.cs` and
  `tests/SharpVision.Tests/Controls/RadioButtonSurfaceTests.cs`.
- Create `src/SharpVision.Showcase/Panes/SeparatorPane.cs` and
  `src/SharpVision.Showcase/Panes/ProgressBarPane.cs`; modify
  `src/SharpVision.Showcase/Gallery.cs`.
- Create matching showcase screen tests under
  `tests/SharpVision.Showcase.Tests/` following `DisplayPaneTests.cs`.
- Modify the six control specifications and
  `docs/testing/controls-integration.md` to link the mounted proof.

### Task 1: Extend only the harness actions phase one requires

**Files:**

- Modify: `tests/SharpVision.Tests/Support/ComponentSurface.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentKeyboard.cs`
- Create: `tests/SharpVision.Tests/Support/ComponentSurfaceTests.cs`

- [x] **Step 1: Write failing helper-contract tests**

Add `UpdateAsync_WhenMountedControlChanges_SettlesTheRenderedMutationAsync`,
`ShouldHaveState_WhenOwnedDescendantIsPassed_ObservesItsStateAsync`, and
`Keyboard_WhenSpaceCompletes_EmitsPressAndReleaseAsync`. Mount a `Stack`
containing a `Text` and `CheckBox`; mutate the text through `UpdateAsync`, focus
the checkbox through Tab, then complete Space through Kitty CSI-u.

```csharp
await surface.UpdateAsync(() => text.Content = "After", "change Text content");
surface.ShouldRender("After");

await surface.Keyboard.PressAsync(Code.Tab);
await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

checkBox.IsChecked.ShouldBe(true);
surface.ShouldHaveState(checkBox, State.Focused);
```

- [x] **Step 2: Run the helper fixture and verify RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ComponentSurfaceTests" --timeout 60s
```

Expected: compilation fails because `UpdateAsync`, descendant state,
`CompleteCharacterAsync`, and descendant state observation are absent.

- [x] **Step 3: Add dispatcher-safe mutation and state composition**

Implement `UpdateAsync(Action, string)` by validating both arguments, invoking
the action on `_application.Dispatcher`, and waiting for the same idle/frame
settling barrier as input. Change `ShouldHaveState` to accept any `IsOwned`
descendant. Keep the helper limited to the inherited publicly observable normal,
hovered, focused, pressed, and disabled flags. Toggle suites assert `IsChecked`
directly rather than adding a production test hook for protected visual-state
composition.

```csharp
internal async Task UpdateAsync(Action update, string description)
{
    ArgumentNullException.ThrowIfNull(update);
    ArgumentException.ThrowIfNullOrEmpty(description);
    await SettleAsync(
        _application.Dispatcher.InvokeAsync(update, _cancellationToken),
        description);
}
```

- [x] **Step 4: Add complete keyboard actions through Kitty CSI-u**

Encode `Code.Enter`, `Code.Up`, `Code.Down`, `Code.Left`, and `Code.Right` with
their Kitty native values. Encode printable characters as
`CSI codepoint;1:event u`, where event 1 is press and event 3 is release.
`CompleteCharacterAsync` sends both actions and therefore exercises Pressable's
real held-state transition.

```csharp
internal async Task CompleteCharacterAsync(Rune value)
{
    await _surface.SendAsync(Encode(value.Value, eventType: 1), $"press {value}");
    await _surface.SendAsync(Encode(value.Value, eventType: 3), $"release {value}");
}
```

- [x] **Step 5: Run helper and Button surface fixtures and verify GREEN**

Run the Task 1 command, then the same command with filter class
`*ButtonSurfaceTests`. Expected: all discovered tests pass without warnings.

- [x] **Step 6: Commit the harness increment**

Stage only the three Task 1 files and commit
`test: extend component surface actions`.

### Task 2: Add Text and FigletText surface proof

**Files:**

- Create: `tests/SharpVision.Tests/Controls/TextSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Controls/FigletTextSurfaceTests.cs`
- Modify: `docs/controls/display/text.md`
- Modify: `docs/controls/display/figlet-text.md`

- [x] **Step 1: Write the failing Text scenarios**

Add separately named facts for styled markup with a combining sequence and wide
glyph, ellipsis/alignment in fixed width, and content mutation that clears stale
cells. Use a surface-owned opaque `Dock` behind transparent Text so background
composition is observable.

```csharp
var text = new Text("<b>A\u0301</b><fg=14>界</fg> tail")
{
    Width = Length.Cells(6),
    Overflow = Overflow.Ellipsis,
};
await using var surface = await ComponentSurface.MountAsync(
    text,
    new Size(6, 2),
    TestContext.Current.CancellationToken);

surface.ShouldRender("A\u0301界 …");
surface.Cell(default).Style.Attributes.ShouldBe(Attributes.Bold);
surface.Cell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
```

The exact expected row must be calculated from the Text contract and confirmed
against the independent cell-width oracle before implementation changes.

- [x] **Step 2: Run `*TextSurfaceTests` and verify RED**

Expected: at least the mutation/clearing or intended overflow assertion fails
for a behavioral reason. If all behavior already conforms, temporarily invert
one semantic assertion to prove the new fixture detects a mismatch, restore it,
and record that no production fix was required.

- [x] **Step 3: Fix only demonstrated Text/core defects and verify GREEN**

For each failure, retain the focused test, change the smallest responsible
Text/layout/rendering method, and run both `*TextSurfaceTests` and `*TextTests`.

- [x] **Step 4: Write the failing FigletText scenarios**

Load one deterministic embedded font through `FigletCatalog.Default`, mount a
short source in clipped bounds, assert exact rows and styles, then change
`Content` through `UpdateAsync` and assert stale art is removed.

```csharp
var title = new FigletText(FigletCatalog.Default.Load("Small"))
{
    Content = "A",
    Width = Length.Cells(8),
};
```

Use the audited embedded `Small` font; do not embed a second parser fixture in
the surface suite.

- [x] **Step 5: Verify RED, apply minimal fixes, and verify GREEN**

Run `*FigletTextSurfaceTests`, then `*FigletTextTests`. Any clipping, cache, or
cell-width correction must retain the failing surface regression.

- [x] **Step 6: Link both specs and commit the display proof**

Add one mounted-surface paragraph to each control's test obligations and commit
only the two fixtures, verified production fixes, and two specs.

### Task 3: Specify and implement Separator test-first

**Files:**

- Modify: `docs/controls/display/separator.md`
- Create: `src/SharpVision/Controls/Separator.cs`
- Create: `tests/SharpVision.Tests/Controls/SeparatorTests.cs`
- Create: `tests/SharpVision.Tests/Controls/SeparatorSurfaceTests.cs`

- [x] **Step 1: Clarify the normative Separator contract**

Specify horizontal `─` and vertical `│` defaults through explicit
`HorizontalGlyph` and `VerticalGlyph` properties, printable one-cell validation,
default horizontal orientation, one-cell intrinsic size, parent-controlled
stretch along the line axis, resolved-style drawing, no focus, and no hit-test
participation.

- [x] **Step 2: Write unit tests for defaults and validation, then verify RED**

```csharp
var separator = new Separator();
separator.Orientation.ShouldBe(Orientation.Horizontal);
separator.HorizontalGlyph.ShouldBe(new Rune('─'));
separator.VerticalGlyph.ShouldBe(new Rune('│'));
separator.CanFocus.ShouldBeFalse();
separator.IsHitTestVisible.ShouldBeFalse();
```

Run `*SeparatorTests`. Expected: compilation fails because `Separator` is
missing.

- [x] **Step 3: Write horizontal, vertical, mutation, and tiny surface tests**

Assert a 5×1 horizontal row, a 1×4 vertical column, inherited foreground,
orientation mutation through `UpdateAsync`, and zero effective content after a
parent clips it to no cells.

- [x] **Step 4: Implement the smallest complete Separator**

Create one sealed `Control` with validated `Orientation`, `HorizontalGlyph`, and
`VerticalGlyph`, one-cell intrinsic measure, and clipped cell drawing in
`OnRender`.

```csharp
protected override Size MeasureOverride(Constraint constraint)
{
    _ = constraint;
    return new Size(1, 1);
}
```

Use the shared orientation validator and terminal Unicode width API. Do not add
animation, labels, children, or an independent styling system.

- [x] **Step 5: Run both Separator fixtures and verify GREEN**

Expected: defaults, invalid glyphs, both orientations, style, mutation, and tiny
bounds pass.

- [x] **Step 6: Commit the Separator control slice**

Commit the spec, production type, and both fixtures as
`feat: add separator control`.

### Task 4: Specify and implement ProgressBar test-first

**Files:**

- Modify: `docs/controls/display/progress-bar.md`
- Create: `src/SharpVision/Controls/ProgressBar.cs`
- Create: `tests/SharpVision.Tests/Controls/ProgressBarTests.cs`
- Create: `tests/SharpVision.Tests/Controls/ProgressBarSurfaceTests.cs`

- [x] **Step 1: Clarify the normative ProgressBar contract**

Specify finite `double` values, `Minimum < Maximum`, clamp-on-assignment and
range mutation, horizontal left-to-right and vertical bottom-to-top fill,
floor-based complete-cell fill, `█` fill and `░` track glyphs, deterministic
indeterminate `▒`, transparent semantic drawing, and no focus or hit testing.

- [x] **Step 2: Write range and validation tests, then verify RED**

```csharp
var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 125 };
bar.Value.ShouldBe(100);
Should.Throw<ArgumentOutOfRangeException>(() => bar.Minimum = double.NaN);
Should.Throw<ArgumentException>(() => bar.Maximum = bar.Minimum);
```

Run `*ProgressBarTests`. Expected: compilation fails because `ProgressBar` is
missing.

- [x] **Step 3: Write determinate and indeterminate surface tests**

Assert exact 0%, 40%, and 100% horizontal rows, bottom-up vertical cells,
deterministic indeterminate glyphs, range mutation through `UpdateAsync`, style,
resize, and one-cell bounds.

```csharp
surface.ShouldRender("████░░░░░░");
surface.Cell(new Point(3, 0)).Text.ShouldBe("█");
surface.Cell(new Point(4, 0)).Text.ShouldBe("░");
```

- [x] **Step 4: Implement minimal validated properties and rendering**

Use `SetProperty` with render impact for value/range/indeterminate/glyph
changes, measure impact for orientation, and calculate fill from normalized
range in `double` before a checked cell-count conversion. Draw only within
`ContentBounds` through `TerminalCanvas`.

- [x] **Step 5: Verify both ProgressBar fixtures GREEN**

Run `*ProgressBarTests` and `*ProgressBarSurfaceTests`. Expected: all cases pass
without warnings.

- [x] **Step 6: Commit the ProgressBar control slice**

Commit the spec, production type, and both fixtures as
`feat: add progress bar control`.

### Task 5: Add CheckBox surface state and input parity

**Files:**

- Create: `tests/SharpVision.Tests/Controls/CheckBoxSurfaceTests.cs`
- Modify: `docs/controls/input/check-box.md`

- [x] **Step 1: Write intended-behavior scenarios and verify RED**

Use isolated surfaces for unchecked, hover, held pointer, focus, checked,
indeterminate, and disabled combinations. Add a parity fact that records
`ActivationCause` for `CompleteCharacterAsync(' ')` and `Pointer.ClickAsync`.
Assert exact bracket mark and Unicode label cells, style flags, and capture
release.

```csharp
await surface.Keyboard.PressAsync(Code.Tab);
await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

checkBox.IsChecked.ShouldBe(true);
cause.ShouldBe(ActivationCause.Keyboard);
surface.ShouldRender("[x] 界");
```

Expected RED: any mismatch must concern routed state, final cells, or event
cause—not an unsupported helper.

- [x] **Step 2: Fix demonstrated behavior and verify focused suites**

Retain a separate regression per defect. Run `*CheckBoxSurfaceTests`,
`*CheckBoxTests`, and `*PressableTests` after every production correction.

- [x] **Step 3: Link the mounted proof and commit**

Update the test-obligations paragraph and commit the surface fixture plus only
verified fixes/spec changes.

### Task 6: Add RadioButton group surface behavior

**Files:**

- Create: `tests/SharpVision.Tests/Controls/RadioButtonSurfaceTests.cs`
- Modify: `docs/controls/input/radio-button.md`

- [x] **Step 1: Write group selection and appearance scenarios**

Mount a vertical `Stack` containing three named-group radios, with the middle
member disabled. Prove no initial selection, Space and pointer selection,
exclusivity, Right/Down wrapping that skips the disabled member, focus,
keyboard/pointer causes, and exact selected/unselected Unicode rows.

```csharp
await surface.Keyboard.PressAsync(Code.Tab);
await surface.Keyboard.PressAsync(Code.Down);

third.IsChecked.ShouldBeTrue();
first.IsChecked.ShouldBeFalse();
third.IsFocused.ShouldBeTrue();
surface.ShouldRender("○ One\n○ Skip\n◉ 界");
```

The exact default mark spacing comes from the RadioButton contract and existing
semantic-cell tests; do not copy a current malformed snapshot.

- [x] **Step 2: Run `*RadioButtonSurfaceTests` and verify RED**

Expected: each failing assertion identifies a routing, grouping, focus, style,
or rendering disagreement.

- [x] **Step 3: Apply minimal fixes and verify focused suites GREEN**

Run `*RadioButtonSurfaceTests`, `*RadioButtonTests`, `*FocusTests`, and
`*PressableTests` after corrections.

- [x] **Step 4: Link the mounted proof and commit**

Commit the fixture, spec, and any demonstrated fixes as one radio-group slice.

### Task 7: Add showcase pages and close phase one

**Files:**

- Create: `src/SharpVision.Showcase/Panes/SeparatorPane.cs`
- Create: `src/SharpVision.Showcase/Panes/ProgressBarPane.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Create: `tests/SharpVision.Showcase.Tests/SeparatorPaneTests.cs`
- Create: `tests/SharpVision.Showcase.Tests/ProgressBarPaneTests.cs`
- Modify: `docs/testing/controls-integration.md`

- [ ] **Step 1: Write failing showcase page and screen tests**

Prove the Gallery catalog exposes both exact titles and each fresh page contains
interactive or state-complete horizontal/vertical specimens. Render each page at
the standard showcase test size and assert representative semantic cells.

- [ ] **Step 2: Run showcase tests and verify RED**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*SeparatorPaneTests|*ProgressBarPaneTests" --timeout 60s
```

Expected: compilation or catalog assertions fail because the panes are absent.

- [ ] **Step 3: Implement both focused showcase panes**

Follow `CheckBoxPane`/`ScrollBarPane` retained composition. Separator shows both
orientations and custom glyph/style. ProgressBar shows empty, partial, full,
vertical, indeterminate, and a Button-driven value mutation without timers.

- [ ] **Step 4: Verify showcase and all phase-one fixtures**

Run all six `*SurfaceTests` fixtures, existing matching unit fixtures, and all
showcase tests. Expected: every discovered test passes without warnings.

- [ ] **Step 5: Update the testing contract and self-audit phase coverage**

Link the six fixtures from mounted-component documentation. Compare every phase
one scenario card in the design against a named test and add any missing proof.
Search the plan and changed docs for placeholders or unsupported claims.

- [ ] **Step 6: Run repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings, zero errors, documentation/link checks pass, test
discovery meets the configured minimum, and package-consumer proof passes.

- [ ] **Step 7: Commit the showcase and phase documentation**

Stage only intentional phase-one files and commit
`docs: complete first control surface coverage phase`.
