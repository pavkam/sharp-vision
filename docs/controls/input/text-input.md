# TextInput

## TextInput contract

`TextInput` is a focusable single- or multiline editor whose caret and selection
indices are valid grapheme boundaries.

## API

| Member group                                                   | Default              | Purpose                                                                 |
| -------------------------------------------------------------- | -------------------- | ----------------------------------------------------------------------- |
| `Text`, `Placeholder`                                          | Empty, `null`        | Store committed text and optional empty and unfocused hint text.        |
| `IsReadOnly`, `AcceptsReturn`, `AcceptsTab`                    | `false`              | Control mutation, multiline Enter, and local Tab insertion.             |
| `PasswordCharacter`                                            | `null`               | Masks one display Rune per source grapheme and suppresses copy or cut.  |
| `MaxLength`                                                    | `0`                  | Limits grapheme count; zero is unlimited.                               |
| `CaretIndex`, `SelectionStart`, `SelectionLength`              | `0`                  | Address only valid Unicode grapheme boundaries.                         |
| `CursorShape`                                                  | `Block`              | Requests a protocol-neutral block, underline, or bar cursor.            |
| `ScrollBars`, `ShowScrollBars`                                 | `Both`, `WhenNeeded` | Configure owned horizontal and vertical overflow rails.                 |
| `UndoLimit`, `CanUndo`, `CanRedo`                              | `100`, read-only     | Bound and inspect immutable edit history.                               |
| `TextChanging`, `TextChanged`, `SelectionChanged`, `Submitted` | No subscribers       | Cancel a proposal or observe committed text, selection, and submission. |

## Default field chrome

`TextInput` selects the global `ThemeRole.Input` profile. Bundled themes paint
its normal face with `ThemeColor.Surface` and provide a one-cell border on every
edge. Hover, direct focus, and disabled state apply the theme's partial input
overlays, while a caller-assigned complete face or border remains authoritative.
The intrinsic [shared chrome](../../concepts/styling.md#shared-chrome) reserves
those cells before the editor viewport, so text, selection, caret, pointer
mapping, and owned scrollbars all use the inset content box. Callers may choose
another glyph family through a complete `Border` or opt out by assigning a
complete border whose `Sides` is `BorderSide.None`.

All mutation first runs through the pure
[`Edit`](../../../src/SharpVision/Text/Edit.cs) transaction model. It stores
immutable strings and a directional
[`Selection`](../../../src/SharpVision/Text/Selection.cs) with `Anchor` and
active `Caret` endpoints. `Start`, `Length`, and `End` are normalized views;
retaining both endpoints prevents repeated selection extension from losing its
direction.

## Edit model API

- `Edit.Validate` rejects endpoints beyond the source or inside a surrogate
  pair, combining sequence, emoji sequence, flag, Indic conjunct, or any other
  Unicode 17 extended grapheme cluster.
- `MovePrevious` and `MoveNext` step by complete grapheme. Without extension an
  existing selection collapses toward the requested direction; with extension
  the original anchor remains fixed.
- `MoveHome` and `MoveEnd` target logical line boundaries, treating CRLF as one
  grapheme-safe separator. `MovePreviousWord` and `MoveNextWord` classify the
  first `Rune` of each cluster and keep marks attached to their base.
- `SelectWord` returns the complete letter/digit/underscore run containing one
  grapheme boundary. A non-word position returns its one complete grapheme and
  the source end returns an empty selection.
- `Backspace` and `Delete` remove the selected range or exactly one neighboring
  cluster. `Replace` validates the complete proposal before allocation, enforces
  return/tab policy, and truncates maximum-length input only at a grapheme
  boundary. `MaxLength` zero means unlimited.
- `ProjectPassword` validates a printable one-cell mask under the default narrow
  policy and returns exactly one mask `Rune` per source grapheme. Invalid UTF-16
  source units count as their own conservative replacement clusters but are
  never normalized or copied into the projection.
- `EditResult` owns the resulting immutable `Text`, directional `Selection`, and
  a `Changed` flag. Callers own undo/redo history by retaining these snapshots;
  the pure model retains no hidden mutable history.

## Behavior

- `Text` is non-null. Direct assignment validates return/tab policy,
  `MaxLength`, and complete Unicode boundaries before mutation, moves the caret
  to the new end, and participates in cancellable events and undo history.
- `Placeholder` is an optional nullable string drawn with dim attributes when
  `Text` is empty and the control is not focused. Only the first line of the
  placeholder is rendered, clipped to the content width. Setting it invalidates
  the render pass.
- `IsReadOnly`, `AcceptsReturn`, `AcceptsTab`, `PasswordCharacter`, and
  `MaxLength` define editing policy. Max length counts grapheme clusters.
- `CaretIndex`, `SelectionStart`, and `SelectionLength` never clamp direct
  assignments. `Select(start, length)` validates overflow, containment, and both
  grapheme boundaries before committing a forward range.
- `SelectedText` returns a caller-owned copy. `HorizontalOffset` and
  `VerticalOffset` expose automatic caret scrolling in cells and logical lines.
- `CursorShape` accepts only `Block`, `Underline`, or `Bar`. Changing it is
  dispatcher-affine and invalidates rendering only. The focused editor commits
  the value to the semantic frame; a terminal without a complete described
  cursor-shape pair preserves position and visibility and emits no shape bytes.
- `ScrollBars`, `ShowScrollBars`, and nullable `ScrollBarStyle` use the common
  overflow policy. When rails reserve cells, the editor's Unicode text, caret,
  selection, pointer mapping, and wheel offsets use the remaining viewport while
  `ActualScrollBarStyle` exposes the complete Theme/local result and the owned
  canonical `ScrollBar` controls retain their normal keyboard, track,
  thumb-drag, and focus behavior.
- `CopySelection()` returns owned selected text and `CutSelection()` deletes it
  when mutable. Password mode returns empty and performs no cut, while read-only
  mode permits copying but never deletes.
- `UndoLimit` defaults to 100; zero disables retained undo. `CanUndo`,
  `CanRedo`, `Undo()`, and `Redo()` operate on immutable text-and-selection
  snapshots and never retain more than the configured number per stack.
- `TextChanging` receives the complete proposed `EditResult` and may cancel
  before any field changes. After atomic text, selection, and scroll commit,
  `TextChanged` precedes `SelectionChanged` when both apply. `Submitted` carries
  the committed single-line text.

Password mode masks display and excludes secret text from diagnostics,
snapshots, and default clipboard copy. The model still stores caller-provided
text; it is not a secure-memory primitive.

Rendering never builds a source-containing password display string: it emits one
validated mask `Rune` directly for each source cluster. An ambiguous mask uses
the inherited policy for measurement, scrolling, pointer mapping, caret
placement, and rendering. A selected wide cluster receives reverse rendition on
both its lead and continuation cells. The terminal cursor is visible only while
focused and its position and requested shape are committed through the semantic
frame, never by emitting terminal bytes from the control.

`TextInput` clears its complete committed content box with its resolved style
before drawing graphemes. Consequently, a configured background paints the full
editable rectangle—including empty trailing cells, multiline slack, selection,
and caret space—rather than only the cells occupied by text. Themes provide the
actual colors through normal, hovered, focused, and disabled style overlays.

## Interaction

Typed text, navigation, selection, Backspace/Delete, Home/End, word movement,
undo/redo policy, paste, copy/cut, mouse placement/drag, and scrolling operate
on grapheme boundaries. An unhandled Tab moves focus through the owning
manager's tab order, while `AcceptsTab` handles Tab locally and inserts it.
Shift+Tab moves backward when the editor does not accept tabs. IME composition
is represented separately from committed text when the terminal protocol
supplies it.

Space-independent text events insert decoded `Rune` values. Bracketed paste
decodes its owned UTF-8 payload once and applies one atomic proposal; policy
rejection drops the complete proposal, while subscriber exceptions propagate
after any committed notification state. Shift extends from the retained anchor,
Control modifies word movement, Up/Down map the rendered caret column to the
nearest grapheme boundary on an adjacent line, and Control+A/Z/Y select all,
undo, and redo. Enter inserts LF only when `AcceptsReturn`; otherwise it
submits. Tab inserts only when `AcceptsTab`.

Inside a running `Application`, Control+C and Control+X publish a non-empty,
non-password selection to the application-owned text clipboard and mirror it to
the capability-gated terminal clipboard writer. Control+V inserts that owned
text through the same policy, events, Unicode validation, and undo history as a
terminal paste. Empty or password-suppressed copy/cut preserves the existing
application buffer. External terminal paste remains the bracketed `Paste` event
path; the in-process shortcut never claims an unavailable synchronous host
clipboard read. While a modal plane is active, these shortcuts follow the
[modal keyboard contract](../../concepts/modality.md#keyboard-text-and-paste) on
its stable
[modal route boundary](../../concepts/modality.md#modal-route-boundaries), so
editor callbacks cannot redirect the in-progress handled key to newly exposed
ancestry.

Primary pointer press focuses and captures. Cell coordinates—including those
inferred from pixel protocols—map through the same grapheme widths used for
rendering, so wide and combining clusters cannot yield interior indices. Drag
release, focus/capture cancellation, disable, hide, detach, and disposal release
transient ownership without changing text.

A primary double-click selects the complete Unicode-safe word or non-word
grapheme beneath the pointer. The routed input manager owns the deterministic
500-millisecond same-target/cell/button gesture count, while the editor owns
only the resulting grapheme-aligned selection.

Wheel deltas scroll the editor's existing horizontal and vertical content
offsets in cell and logical-line units. The editor handles a wheel event only
when at least one enabled offset changes. At either endpoint it leaves an
otherwise unmoved event unhandled, so normal bubble routing can offer it to an
enclosing [scrollable `Container`](../../concepts/scrolling.md) instead of
swallowing nested scrolling.

## Example

```csharp
var name = new TextInput
{
    MaxLength = 80,
    Placeholder = "Enter your name",
    Width = Length.Percent(100),
};
```

## Test obligations

Cover empty/Unicode editing, combining/emoji movement and deletion, selection,
read-only/password/max length, events/cancellation, paste limits, clipboard
fallback, mouse/pixel hit testing, horizontal/vertical scrolling, resize,
focus/disabled state, cursor-shape fallback, and final cursor/cells.

Mounted cross-layer coverage in
[`TextInputSurfaceTests`](../../../tests/SharpVision.Tests/Controls/Input/TextInputSurfaceTests.cs)
drives terminal bytes through the live application and proves placeholder,
focus, Unicode typing and deletion, atomic paste, wide-cell selection, submit
policy, password masking, read-only and disabled refusal, automatic offsets,
resize repair, and the committed terminal cursor. The component harness does not
consider input consumed until the terminal session requests its next read, so an
earlier dispatcher idle cannot expose a partially routed action.

The pure model additionally replays 10,000 operations with seed `0x00ED175A`
over ASCII, CJK, combining sequences, emoji ZWJ sequences, flags, and invalid
UTF-16. Every step independently validates both endpoints, maximum grapheme
count, deterministic replay, and absence of split clusters.
