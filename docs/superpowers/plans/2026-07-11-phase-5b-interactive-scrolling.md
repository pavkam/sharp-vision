# Phase 5B Interactive Controls and Scrolling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:subagent-driven-development` (recommended) or
> `superpowers:executing-plans` to implement this plan task-by-task. Execute
> directly on `main`; the user explicitly forbids additional worktrees. Steps
> use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Button, CheckBox, RadioButton, TextInput, ScrollBar, ScrollView,
and List as traditional mutable controls with complete keyboard, pointer, focus,
styling, scrolling, Unicode, and integration proofs.

**Architecture:** Add one internal press interaction base over the existing
routed input, focus, and capture managers; concrete controls remain ordinary
objects with properties, CLR events, and owned children. Scrolling uses integer
cell geometry with a pure range/thumb allocator, while TextInput uses a pure
grapheme-boundary edit model before its visual control. No virtual tree,
reconciliation, hooks, raw ANSI, or claimed list virtualization is introduced.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly,
System.Windows.Input.ICommand, SharpVision Unicode geometry, routed input,
semantic frames, Markdown

---

## Normative inputs

- `docs/superpowers/specs/2026-07-11-sharpvision-foundation-design.md`
- `docs/controls/input/{button,check-box,radio-button,text-input}.md`
- `docs/controls/layout/{scroll-bar,scroll-view}.md`
- `docs/controls/collections/list.md`
- `docs/concepts/{input-routing,focus,scrolling,styling,unicode-cell-geometry}.md`
- `docs/testing/{controls-integration,correctness-model,performance}.md`

RichText, Menu, MenuItem, Popup, Window, and Showcase pages remain in Phase 5C
and Phase 6. Phase 5B must expose the public behavior those consumers need but
must not add partial placeholder versions of later controls.

## Task 1: Add shared activation contracts

**Files:**

- Create: `src/SharpVision/Input/ActivationCause.cs`
- Create: `src/SharpVision/Input/ActivationEventArgs.cs`
- Create: `src/SharpVision/Controls/Pressable.cs`
- Create: `tests/SharpVision.Tests/Controls/PressableTests.cs`
- Modify: `docs/concepts/input-routing.md`

- [x] **Step 1: Write failing interaction tests**

  Define a test-only control deriving from `Pressable` in its own exactly named
  file. Prove Space press/release, Enter direct activation, primary pointer
  press/capture/move/release, release outside, capture cancellation, focus loss,
  disable, hide, detach, and non-primary buttons. Assert one activation at most,
  exact `ActivationCause`, handled state, focus acquisition, and
  normal/hovered/pressed/focused/disabled state transitions.

- [x] **Step 2: Verify RED**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release --filter-class "*PressableTests" --minimum-expected-tests 1 --timeout 60s
  ```

  Expected: compile failure because the activation contracts and Pressable do
  not exist.

- [x] **Step 3: Implement the shared interaction state machine**

  ```csharp
  public enum ActivationCause { Keyboard, Pointer, Programmatic }

  public sealed class ActivationEventArgs(ActivationCause cause) : EventArgs
  {
      public ActivationCause Cause { get; } = cause;
  }

  public abstract class Pressable : Container
  {
      protected Pressable(int capacity);
      protected abstract void Activate(ActivationCause cause);
  }
  ```

  `Pressable` defaults `CanFocus = true`, owns the Space-down token, captures
  primary pointer presses through `CaptureOwner`, and activates only a matching
  eligible release inside its bounds. Enter activates on press without a held
  state. It clears held state and releases capture on every unavailable path.
  Subscribe/unsubscribe capture cancellation through attachment ownership
  without retaining detached controls. Validate every routed payload before
  mutation and use `Debug.Assert` for impossible held/capture combinations.

- [x] **Step 4: Verify GREEN and commit**

  Run `*PressableTests`, then commit as `feat: add shared activation behavior`.

## Task 2: Implement Button

**Files:**

- Create: `src/SharpVision/Controls/Button.cs`
- Create: `tests/SharpVision.Tests/Controls/ButtonTests.cs`
- Modify: `docs/controls/input/button.md`

- [x] **Step 1: Write failing Button tests**

  Cover null/one child, atomic replacement, capacity enforcement, default
  focusability, programmatic `PerformClick`, Space/Enter/pointer parity, `Click`
  before command, `CanExecute`, command parameter identity, `CanExecuteChanged`,
  command exceptions, disabled/hidden behavior, `IsDefault`/`IsCancel` storage,
  Unicode content, padding, tiny bounds, inherited states, and exact semantic
  cells.

- [x] **Step 2: Verify RED**

  Run `*ButtonTests`; expect compile failure for Button.

- [x] **Step 3: Implement Button**

  ```csharp
  public sealed class Button : Pressable
  {
      public Control? Content { get; set; }
      public ICommand? Command { get; set; }
      public object? CommandParameter { get; set; }
      public bool IsDefault { get; set; }
      public bool IsCancel { get; set; }
      public event EventHandler<ActivationEventArgs>? Click;
      public void PerformClick();
  }
  ```

  Use capacity-one `Children.SetOnly`. Measure/arrange the margin-inclusive
  child through the shared box model. `Activate` raises `Click` after released
  state, then calls `Execute` only when `CanExecute` is true. Command
  replacement validates dispatcher affinity, unsubscribes the previous command,
  subscribes the next, and invalidates render when executability changes.
  Default/cancel routing is stored now and consumed by Window in Phase 5C.

- [x] **Step 4: Verify GREEN and commit**

  Run `*ButtonTests` and routing tests; commit as `feat: add button control`.

## Task 3: Implement CheckBox

**Files:**

- Create: `src/SharpVision/Controls/CheckBox.cs`
- Create: `src/SharpVision/Controls/Marks.cs`
- Create: `src/SharpVision/Input/CheckChangedEventArgs.cs`
- Create: `tests/SharpVision.Tests/Controls/CheckBoxTests.cs`
- Modify: `docs/controls/input/check-box.md`

- [x] **Step 1: Write failing CheckBox tests**

  Cover false/true and false/true/null cycles, invalid null in two-state mode,
  disabling three-state while null, programmatic/user causes, event order
  (`Checked`/`Unchecked`/`Indeterminate` then `StateChanged`), reentrancy,
  Space/pointer parity, cancellation, disabled state, content ownership,
  combined checked/focused/pressed styling, custom narrow marks, invalid marks,
  Unicode content, resize, tiny bounds, and exact cells.

- [x] **Step 2: Verify RED**

  Run `*CheckBoxTests`; expect missing CheckBox/Marks/event arguments.

- [x] **Step 3: Implement CheckBox**

  ```csharp
  public sealed class CheckBox : Pressable
  {
      public bool? IsChecked { get; set; }
      public bool IsThreeState { get; set; }
      public Control? Content { get; set; }
      public Marks Marks { get; set; }
      public event EventHandler<CheckChangedEventArgs>? Checked;
      public event EventHandler<CheckChangedEventArgs>? Unchecked;
      public event EventHandler<CheckChangedEventArgs>? Indeterminate;
      public event EventHandler<CheckChangedEventArgs>? StateChanged;
  }
  ```

  `Marks` is an immutable set of unchecked/checked/indeterminate printable
  narrow Runes validated through the shared width engine. Reserve one mark cell
  plus one separator only when content exists. Commit the new nullable value,
  update `State.Checked`, invalidate render, then raise events from the
  committed state. Handler reentrancy starts a new complete transition, never a
  half transition.

- [x] **Step 4: Verify GREEN and commit**

  Run `*CheckBoxTests`; commit as `feat: add check box control`.

## Task 4: Implement RadioButton group semantics

**Files:**

- Create: `src/SharpVision/Controls/RadioButton.cs`
- Create: `src/SharpVision/Controls/RadioGroup.cs`
- Create: `src/SharpVision/Input/SelectionChangedEventArgs.cs`
- Create: `tests/SharpVision.Tests/Controls/RadioButtonTests.cs`
- Modify: `docs/controls/input/radio-button.md`

- [x] **Step 1: Write failing group tests**

  Cover no initial selection, user/programmatic selection, activation of an
  already selected member, named and nearest-container groups, exclusivity
  including disabled members, regrouping, detach/reparent/disposal, atomic old
  and new notifications, reentrant handlers, arrow navigation in both directions
  with wrapping, skipped unavailable members, Space/pointer parity, focus,
  styling, Unicode content, and final cells.

- [x] **Step 2: Verify RED**

  Run `*RadioButtonTests`; expect missing radio types.

- [x] **Step 3: Implement transactional groups**

  ```csharp
  public sealed class RadioButton : Pressable
  {
      public bool IsChecked { get; set; }
      public string? GroupName { get; set; }
      public Control? Content { get; set; }
      public event EventHandler<SelectionChangedEventArgs>? Checked;
      public event EventHandler<SelectionChangedEventArgs>? Unchecked;
      public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
  }
  ```

  `RadioGroup` is an internal weak membership coordinator keyed by attached root
  plus explicit name, or nearest owning container for null names. A transaction
  identifies old/new members, validates both remain eligible, commits flags,
  then notifies old followed by new. Group changes and tree changes unregister
  before registering; no static strong reference may retain a detached tree.
  Arrow behavior uses stable sibling/tree order and `FocusOwner.Focus`.

- [x] **Step 4: Verify GREEN and commit**

  Run radio, tree, focus, and randomized tree tests; commit as
  `feat: add radio button control`.

## Task 5: Add pure scroll range and thumb geometry

**Files:**

- Create: `src/SharpVision/Scrolling/Cause.cs`
- Create: `src/SharpVision/Scrolling/Range.cs`
- Create: `src/SharpVision/Scrolling/Thumb.cs`
- Create: `src/SharpVision/Layout/ScrollBarVisibility.cs`
- Create: `tests/SharpVision.Tests/Scrolling/RangeTests.cs`
- Create: `tests/SharpVision.Tests/Scrolling/RandomizedRangeTests.cs`
- Modify: `docs/concepts/scrolling.md`

- [x] **Step 1: Write failing pure geometry tests**

  Cover minimum/maximum/value validation, zero/full range, viewport larger than
  range, one-cell minimum thumb, horizontal/vertical track lengths, cumulative
  rounding, value-to-position and drag-position-to-value, int boundary inputs,
  zero/tiny tracks, and resize. Add at least 20,000 seed `0x5C7011` cases
  proving containment, monotonicity, endpoint exactness, and round-trip error
  bounded by one representable track step.

- [x] **Step 2: Verify RED**

  Run the two scrolling geometry classes; expect missing types.

- [x] **Step 3: Implement allocation-free geometry**

  ```csharp
  public readonly record struct Range(int Minimum, int Maximum, int Value, int Viewport);
  public readonly record struct Thumb(int Start, int Length);
  ```

  Constructors validate all inputs before assignment. Static span-free methods
  clamp command changes, compute `viewport / (range + viewport)` with checked
  `long` intermediates, use cumulative integer edges, and never return geometry
  outside the non-negative track. Visibility enum values are Hidden, Auto, and
  Always.

- [x] **Step 4: Verify GREEN and commit**

  Run deterministic/randomized range tests; commit as
  `feat: add scroll geometry`.

## Task 6: Implement ScrollBar

**Files:**

- Create: `src/SharpVision/Controls/ScrollBar.cs`
- Create: `src/SharpVision/Input/ScrollEventArgs.cs`
- Create: `tests/SharpVision.Tests/Controls/ScrollBarTests.cs`
- Modify: `docs/controls/layout/scroll-bar.md`

- [x] **Step 1: Write failing ScrollBar tests**

  Cover every range property and pre-mutation failure, programmatic throw versus
  command clamp, event old/new/cause order, both orientations, arrows,
  Page/Home/End, decrement/increment cells, track click, cell and pixel thumb
  drag, capture cancellation, detach/disable, zero/tiny tracks, focus and visual
  states, custom narrow glyph validation, huge ranges, exact thumb cells, and
  resize rounding.

- [x] **Step 2: Verify RED**

  Run `*ScrollBarTests`; expect missing ScrollBar API.

- [x] **Step 3: Implement ScrollBar**

  ```csharp
  public sealed class ScrollBar : Control
  {
      public int Minimum { get; set; }
      public int Maximum { get; set; }
      public int ViewportSize { get; set; }
      public int Value { get; set; }
      public int SmallChange { get; set; }
      public int LargeChange { get; set; }
      public Orientation Orientation { get; set; }
      public event EventHandler<ScrollEventArgs>? ValueChanged;
      public bool ScrollBy(int delta, Cause cause = Cause.Programmatic);
  }
  ```

  Use the pure `Range`/`Thumb` functions for all inputs. Pointer drag stores the
  press thumb/value/pixel baseline and uses capture; no geometry is recomputed
  from mutated intermediate values. Render only semantic button, track, and
  thumb cells with resolved normal/hovered/pressed/focused/disabled styles.

- [x] **Step 4: Verify GREEN and commit**

  Run ScrollBar, pointer, capture, and rendering tests; commit as
  `feat: add scroll bar control`.

## Task 7: Implement ScrollView layout and commands

**Files:**

- Create: `src/SharpVision/Controls/ScrollView.cs`
- Create: `src/SharpVision/Input/ScrollChangedEventArgs.cs`
- Create: `tests/SharpVision.Tests/Controls/ScrollViewTests.cs`
- Create: `tests/SharpVision.Tests/Controls/RandomizedScrollViewTests.cs`
- Modify: `docs/controls/layout/scroll-view.md`
- Modify: `docs/concepts/scrolling.md`

- [x] **Step 1: Write failing ScrollView tests**

  Cover no content, atomic replacement, Hidden/Auto/Always policies per axis,
  exact fit, one bar inducing the other, zero/tiny viewport, extent/viewport and
  offset commits, invalid direct offsets, command clamping, bar synchronization,
  resize/content shrink clamping before events, wheel and pixel accumulation,
  arrows/pages/home/end, nested unused-delta propagation, minimal
  `BringIntoView`, focus, viewport clipping/hit testing, wide Unicode horizontal
  clipping, and exact cells. Seed `0x5C701E` with at least 10,000
  content/policy/ resize cases and assert stable visibility, containment, and
  valid offsets.

- [x] **Step 2: Verify RED**

  Run both ScrollView classes; expect missing API.

- [x] **Step 3: Implement the convergent two-axis algorithm**

  ```csharp
  public sealed class ScrollView : Container
  {
      public Control? Content { get; set; }
      public ScrollBarVisibility HorizontalBarVisibility { get; set; }
      public ScrollBarVisibility VerticalBarVisibility { get; set; }
      public int HorizontalOffset { get; set; }
      public int VerticalOffset { get; set; }
      public Size Extent { get; }
      public Size Viewport { get; }
      public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;
      public bool ScrollBy(int x, int y, Cause cause = Cause.Programmatic);
      public bool BringIntoView(Control descendant);
  }
  ```

  Capacity remains one public content child. Two internal bars live in a second
  private capacity-two `Children` owner; ScrollView overrides child visitation,
  disposal, render, hit testing, and navigation so dispatcher/focus/capture
  lifecycle remains identical without exposing bar mutation publicly. Probe
  starts with Always bars, adds Auto bars monotonically, repeats after each
  consumed axis, and stops in at most two additions. Arrange translates content
  by clamped offsets, keeps the viewport as the child clip, and synchronizes
  bars without recursive events. Bubble an immutable remaining delta only after
  local consumption.

- [x] **Step 4: Verify GREEN and commit**

  Run ScrollView, ScrollBar, randomized layout, pointer, and Unicode rendering
  tests; commit as `feat: add scroll view control`.

## Task 8: Add the pure grapheme edit model

**Files:**

- Create: `src/SharpVision/Text/Edit.cs`
- Create: `src/SharpVision/Text/Selection.cs`
- Create: `src/SharpVision/Text/EditResult.cs`
- Create: `tests/SharpVision.Tests/Text/EditTests.cs`
- Create: `tests/SharpVision.Tests/Text/RandomizedEditTests.cs`
- Modify: `docs/controls/input/text-input.md`

- [x] **Step 1: Write failing edit-model tests**

  Cover valid/invalid UTF-16, caret and selection boundary validation, movement,
  extend selection, Backspace/Delete, Home/End, word movement, replacement,
  paste, max grapheme length, single/multiline/tab policy, password projection,
  and undo/redo snapshots. Seed `0xED175A` over at least 10,000 mixed Unicode
  edit sequences and assert every committed index is a grapheme boundary,
  deterministic replay, no split surrogate/cluster, and max-length compliance.

- [x] **Step 2: Verify RED**

  Run edit tests; expect missing model types.

- [x] **Step 3: Implement immutable edit transactions**

  ```csharp
  public readonly record struct Selection(int Anchor, int Caret);
  public readonly record struct EditResult(string Text, Selection Selection, bool Changed);
  public static class Edit { /* validated boundary operations */ }
  ```

  Enumerate only with `Graphemes`; use `Rune` classification for word movement.
  Validate complete input and policies before allocating replacement text. Max
  length counts graphemes, newlines and tabs obey explicit policy, and invalid
  UTF-16 is retained as replacement-width source units without creating invalid
  indices. Caller-owned history remains outside the pure functions.

- [x] **Step 4: Verify GREEN and commit**

  Run deterministic/random edit tests; commit as
  `feat: add grapheme edit model`.

## Task 9: Implement TextInput

**Files:**

- Create: `src/SharpVision/Controls/TextInput.cs`
- Create: `src/SharpVision/Input/TextChangingEventArgs.cs`
- Create: `src/SharpVision/Input/TextChangedEventArgs.cs`
- Create: `src/SharpVision/Input/InputSelectionChangedEventArgs.cs`
- Create: `src/SharpVision/Input/SubmittedEventArgs.cs`
- Create: `tests/SharpVision.Tests/Controls/TextInputTests.cs`
- Modify: `docs/controls/input/text-input.md`

- [x] **Step 1: Write failing TextInput tests**

  Cover every property default/validation, cancellable TextChanging, committed
  event order, typed text, paste, navigation/selection/deletion, read-only,
  AcceptsReturn/Tab, password mask and snapshot secrecy, max length, undo/redo,
  submit, cell and pixel pointer placement/drag, horizontal/vertical scrolling,
  caret visibility/cursor state, resize, focus/disabled styling, Unicode output,
  and dispatcher/exception recovery.

- [x] **Step 2: Verify RED**

  Run `*TextInputTests`; expect missing TextInput and event types.

- [x] **Step 3: Implement TextInput over Edit and Text layout**

  ```csharp
  public sealed class TextInput : Control
  {
      public string Text { get; set; }
      public bool IsReadOnly { get; set; }
      public bool AcceptsReturn { get; set; }
      public bool AcceptsTab { get; set; }
      public Rune? PasswordCharacter { get; set; }
      public int MaxLength { get; set; }
      public int CaretIndex { get; set; }
      public int SelectionStart { get; set; }
      public int SelectionLength { get; set; }
  }
  ```

  All inputs create one proposed `EditResult`, raise cancellable TextChanging,
  then atomically commit text/selection/scroll before notifications. Maintain a
  bounded configurable undo ring of owned strings. Password rendering builds a
  reusable mask by grapheme count and never includes source text in event
  formatting, diagnostics, or clipboard defaults. Render selection and caret
  through semantic styles and set the frame cursor only while focused.

- [x] **Step 4: Verify GREEN and commit**

  Run TextInput, edit, input integration, Unicode, and allocation tests; commit
  as `feat: add text input control`.

## Task 10: Implement List selection without virtualization claims

**Files:**

- Create: `src/SharpVision/Controls/List.cs`
- Create: `src/SharpVision/Controls/SelectionMode.cs`
- Create: `src/SharpVision/Controls/ItemTemplate.cs`
- Create: `src/SharpVision/Input/ListSelectionChangingEventArgs.cs`
- Create: `src/SharpVision/Input/ListSelectionChangedEventArgs.cs`
- Create: `src/SharpVision/Input/ItemInvokedEventArgs.cs`
- Create: `tests/SharpVision.Tests/Controls/ListTests.cs`
- Modify: `docs/controls/collections/list.md`

- [x] **Step 1: Write failing List tests**

  Cover empty and replacement Items, null/template failures before mutation, all
  selection modes, invalid indexes, cancellable selection, event added/ removed
  order, selected views, keyboard active item, Space/Enter, pointer and
  modifiers, Home/End/Page, disabled item skipping, bring-into-view, Unicode and
  variable-height items, template rebuild cleanup, focus/styles, resize, and
  exact cells. Assert every realized control has exactly one parent and no
  recycled state leakage; do not assert virtualization.

- [x] **Step 2: Verify RED**

  Run `*ListTests`; expect missing List APIs.

- [x] **Step 3: Implement realized items over ScrollView**

  ```csharp
  public delegate Control ItemTemplate(object item);
  public enum SelectionMode { None, Single, Multiple }

  public sealed class List : Container
  {
      public IReadOnlyList<object?> Items { get; set; }
      public ItemTemplate ItemTemplate { get; set; }
      public SelectionMode SelectionMode { get; set; }
      public int SelectedIndex { get; set; }
      public object? SelectedItem { get; }
      public IReadOnlyList<object?> SelectedItems { get; }
  }
  ```

  Realize every item into an internal Stack inside ScrollView. Build the entire
  candidate realization and validate controls are detached before replacing the
  committed tree. Store selection by stable indexes, normalize after Items
  changes before notifications, and use one active index for focus/navigation.
  Selected views are owner-backed read-only collections without per-get arrays.

- [x] **Step 4: Verify GREEN and commit**

  Run List, ScrollView, focus, tree, and integration tests; commit as
  `feat: add list control`.

## Task 11: Prove Phase 5B interactions end to end

**Files:**

- Create: `tests/SharpVision.Tests/Integration/InteractiveControlTests.cs`
- Create: `tests/SharpVision.Tests/Integration/ScrollingTests.cs`
- Create: `tests/SharpVision.Tests/Performance/InteractivePerformanceTests.cs`
- Modify: `docs/testing/{controls-integration,performance,unicode-rendering}.md`

- [ ] **Step 1: Drive real terminal input**

  Compose every Phase 5B control under a real Application/FakeTerminal. Send
  Kitty/legacy keys, UTF-8 text, bracketed paste, SGR cell and pixel pointer,
  wheel, focus loss, and resizes. Assert event order, selection/caret/offset
  state, capture/focus cleanup, exact semantic frames, incremental bytes, and no
  stale cells after item/content removal.

- [ ] **Step 2: Prove nested scrolling**

  Compose horizontal and vertical nested ScrollViews containing wide Unicode and
  focusable controls. Assert local delta consumption, bubbled remainder,
  automatic bars inducing each other, focus bring-into-view, pixel accumulation,
  resizing, and exact final offsets/thumb cells.

- [ ] **Step 3: Add allocation and retained-memory gates**

  Warm unchanged 80×24 and 200×60 interactive trees, repeated TextInput edits,
  ScrollBar drags, 1,000 list items, and nested scroll commands. Require
  allocation-free unchanged layout/render windows, bounded per-edit allocations,
  no retained detached item controls after forced collection through weak
  references, and diagnostic elapsed-time output without noisy thresholds.

- [ ] **Step 4: Verify and commit**

  Run all Phase 5B unit, randomized, integration, and performance classes;
  commit as `test: prove interactive controls and scrolling`.

## Task 12: Publish and verify Phase 5B

**Files:**

- Modify:
  `docs/architecture/{project-structure,memory-ownership,rendering-pipeline}.md`
- Modify:
  `docs/concepts/{focus,input-routing,scrolling,styling,unicode-cell-geometry}.md`
- Modify: `docs/controls/index.md`
- Modify: `docs/superpowers/plans/2026-07-11-phase-5b-interactive-scrolling.md`

- [ ] **Step 1: Audit public API and one-type-per-file compliance**

  Compare every documented default, exception, invalidation, event order,
  ownership rule, keyboard/pointer/focus behavior, visual state, Unicode rule,
  and example against source and tests. Every added or touched named type must
  be alone in its exactly named non-generated file.

- [ ] **Step 2: Run all quality gates**

  ```bash
  make format
  npm run check:unicode
  make lint
  make build
  make test
  git diff --check
  rg -n "TODO|TBD|NotImplementedException" src tests scripts docs --glob '!docs/superpowers/plans/**'
  ```

  Expected: six projects build with zero warnings/errors; all test assemblies
  pass with non-zero discovery; Unicode generation, formatting, analyzers,
  Markdown links, randomized corpora, allocation gates, and placeholder scan are
  clean.

- [ ] **Step 3: Commit the verified slice**

  Commit documentation as `chore: complete interactive controls and scrolling`.

## Plan self-review

- Every Phase 5B control named by the milestone has a test-first implementation,
  integration, performance, documentation, and publication task.
- Button default/cancel storage is explicitly deferred to the owning Window;
  RichText, Menu, Popup, Window, and Showcase remain in later plans.
- Types and signatures are consistent across tasks: integer scrolling uses
  `Scrolling.Cause`, all content ownership uses capacity-one Children, and all
  text mutation uses the pure `Edit` model.
- The plan contains no implementation placeholders or virtualization claim.
