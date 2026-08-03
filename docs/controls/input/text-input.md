# TextInput

## Overview

`TextInput` is a focusable single- or multiline text editor whose caret and
selection indices always fall on valid grapheme boundaries.

## API

| Member group                                                   | Default                | Purpose                                                                   |
| -------------------------------------------------------------- | ---------------------- | ------------------------------------------------------------------------- |
| `Text`, `Placeholder`                                          | Empty, `null`          | The committed text and the optional hint shown while empty and unfocused. |
| `IsReadOnly`, `AcceptsReturn`, `AcceptsTab`                    | `false`                | Control mutation, multiline Enter, and local Tab insertion.               |
| `PasswordCharacter`                                            | `null`                 | Masks one display Rune per source grapheme and suppresses copy or cut.    |
| `MaxLength`                                                    | `0`                    | Limits the grapheme count; zero means unlimited.                          |
| `CaretIndex`, `SelectionStart`, `SelectionLength`              | `0`                    | Address only valid Unicode grapheme boundaries.                           |
| `CursorShape`                                                  | `Block`                | Requests a protocol-neutral block, underline, or bar cursor.              |
| `ScrollBars`, `ShowScrollBars`                                 | `Both`, `WhenNeeded`   | Configure the owned horizontal and vertical overflow rails.               |
| inherited `ContextMenu`                                        | `TextInputContextMenu` | Provides Undo, Redo, Cut, Copy, Paste, and Select All.                    |
| `UndoLimit`, `CanUndo`, `CanRedo`                              | `100`, read-only       | Bound and inspect the immutable edit history.                             |
| `TextChanging`, `TextChanged`, `SelectionChanged`, `Submitted` | No subscribers         | Cancel a proposal or observe committed text, selection, and submission.   |

## Default field chrome

`TextInput` uses the global `ThemeRole.Input` profile. Bundled themes paint its
normal face with `ThemeColor.Surface` and provide a one-cell border on every
edge. Hover, direct focus, and disabled state apply the theme's partial input
overlays, while a caller-assigned complete face or border remains authoritative.
The intrinsic [shared chrome](../../concepts/styling.md#shared-chrome) reserves
those cells before the editor viewport, so text, selection, caret, pointer
mapping, and the owned scrollbars all use the inset content box. Callers may
choose another glyph family through a complete `Border`, or opt out by assigning
a complete border whose `Sides` is `BorderSide.None`.

All mutation first runs through the pure
[`Edit`](../../../src/SharpVision/Text/Edit.cs) transaction model. It stores
immutable strings and a directional
[`Selection`](../../../src/SharpVision/Text/Selection.cs) with an `Anchor` and
an active `Caret` endpoint. `Start`, `Length`, and `End` are normalized views;
keeping both endpoints means repeated selection extension never loses its
direction.

## Edit model API

- `Edit.Validate` rejects endpoints beyond the source or inside a surrogate
  pair, combining sequence, emoji sequence, flag, Indic conjunct, or any other
  Unicode 17 extended grapheme cluster.
- `MovePrevious` and `MoveNext` step by complete grapheme. Without extension, an
  existing selection collapses toward the requested direction; with extension,
  the original anchor stays fixed.
- `MoveHome` and `MoveEnd` target logical line boundaries and treat CRLF as one
  grapheme-safe separator. `MovePreviousWord` and `MoveNextWord` classify the
  first `Rune` of each cluster and keep marks attached to their base.
- `SelectWord` returns the complete letter, digit, or underscore run containing
  a grapheme boundary. A non-word position returns its single complete grapheme,
  and the end of the source returns an empty selection.
- `Backspace` and `Delete` remove the selected range or exactly one neighboring
  cluster. `Replace` validates the complete proposal before allocating anything.
  It enforces the control-character policy — CR and LF only with
  `AcceptsReturn`, tab only with `AcceptsTab`, and every other control character
  (ESC, DEL, NEL, LS/PS, ...) always rejected, because such a character would be
  stored with no paint width and freeze the caret at that index — and truncates
  over-length input only at a grapheme boundary. A `MaxLength` of zero means
  unlimited.
- `ProjectPassword` validates a printable one-cell mask under the default narrow
  policy and returns exactly one mask `Rune` per source grapheme. Invalid UTF-16
  source units count as their own conservative replacement clusters but are
  never normalized or copied into the projection.
- `EditResult` owns the resulting immutable `Text`, the directional `Selection`,
  and a `Changed` flag. Callers own undo/redo history by retaining these
  snapshots; the pure model keeps no hidden mutable history.

## Behavior

- `Text` is never null. Direct assignment validates the control-character
  policy, `MaxLength`, and complete Unicode boundaries before mutating, moves
  the caret to the new end, and participates in cancellable events and undo
  history.
- `Placeholder` is an optional nullable string drawn with dim attributes while
  `Text` is empty and the control is unfocused. Only its first line renders,
  clipped to the content width. Setting it invalidates the render pass.
- `IsReadOnly`, `AcceptsReturn`, `AcceptsTab`, `PasswordCharacter`, and
  `MaxLength` define the editing policy. The maximum length counts grapheme
  clusters.
- `CaretIndex`, `SelectionStart`, and `SelectionLength` never clamp direct
  assignments. `Select(start, length)` validates overflow, containment, and both
  grapheme boundaries before committing a forward range.
- `SelectedText` returns a caller-owned copy. `HorizontalOffset` and
  `VerticalOffset` expose the automatic caret scrolling in cells and logical
  lines.
- `CursorShape` accepts only `Block`, `Underline`, or `Bar`. Changing it is
  dispatcher-affine and invalidates rendering only. The focused editor commits
  the value to the semantic frame; a terminal without a complete described
  cursor-shape pair keeps the cursor's position and visibility and emits no
  shape bytes.
- `ScrollBars`, `ShowScrollBars`, and the nullable `ScrollBarStyle` follow the
  common overflow policy. When the rails reserve cells, the editor's Unicode
  text, caret, selection, pointer mapping, and wheel offsets use the remaining
  viewport. `ActualScrollBarStyle` exposes the complete Theme or local result,
  and the owned canonical `ScrollBar` controls keep their normal keyboard,
  track, thumb-drag, and focus behavior.
- `CopySelection()` returns the selected text as an owned string, and
  `CutSelection()` also deletes it when the editor is mutable. Password mode
  returns empty text and never cuts; read-only mode permits copying but never
  deletes.
- Construction installs one `TextInputContextMenu`, a public specialized
  `ContextMenu` reached through the inherited `ContextMenu` property. It orders
  Undo, Redo, a separator, Cut, Copy, Paste, another separator, and Select All.
  Opening it recomputes enablement from the selection, the password and
  read-only policy, the application clipboard content, the text length, and
  undo/redo availability. Callers may replace or clear it through the ordinary
  context-menu ownership contract.
- `UndoLimit` defaults to 100, and zero disables retained undo. `CanUndo`,
  `CanRedo`, `Undo()`, and `Redo()` operate on immutable text-and-selection
  snapshots and never keep more than the configured number per stack.
- `TextChanging` receives the complete proposed `EditResult` and may cancel it
  before any field changes. After the text, selection, and scroll commit
  atomically, `TextChanged` precedes `SelectionChanged` when both apply.
  `Submitted` carries the committed single-line text.

Password mode masks the display and keeps secret text out of diagnostics,
snapshots, and the default clipboard copy. The model still stores the
caller-provided text; it is not a secure-memory primitive.

Rendering never builds a display string that contains the source text: it emits
one validated mask `Rune` directly for each source cluster. An ambiguous mask
uses the inherited policy for measurement, scrolling, pointer mapping, caret
placement, and rendering. A selected wide cluster receives reverse rendition on
both its lead and continuation cells. The terminal cursor is visible only while
the editor is focused, and its position and requested shape are committed
through the semantic frame — the control never emits terminal bytes itself.

`TextInput` clears its complete committed content box with its resolved style
before drawing graphemes. A configured background therefore paints the full
editable rectangle — including empty trailing cells, multiline slack, selection,
and caret space — rather than only the cells occupied by text. Themes provide
the actual colors through the normal, hovered, focused, and disabled style
overlays.

## Interaction

Typed text, navigation, selection, Backspace/Delete, Home/End, word movement,
undo/redo, paste, copy/cut, mouse placement and drag, and scrolling all operate
on grapheme boundaries. An unhandled Tab moves focus through the owning
manager's tab order, while `AcceptsTab` handles Tab locally and inserts it.
Shift+Tab moves backward when the editor does not accept tabs. IME composition
is represented separately from committed text when the terminal protocol
supplies it. Keys outside the editor command set remain available to inherited
routed input.

Space-independent text events insert decoded `Rune` values. Bracketed paste
decodes its owned UTF-8 payload once and applies one atomic proposal; a policy
rejection drops the complete proposal, while subscriber exceptions propagate
after any committed notification state. Shift extends the selection from the
retained anchor, Control switches to word movement, and Up/Down map the rendered
caret column to the nearest grapheme boundary on the adjacent line. Control+A,
Control+Z, and Control+Y select all, undo, and redo. Enter inserts LF only when
`AcceptsReturn` is set; otherwise it submits. Tab inserts only when `AcceptsTab`
is set.

Inside a running `Application`, Control+C and Control+X publish a non-empty,
non-password selection to the application-owned text clipboard and mirror it to
the capability-gated terminal clipboard writer. Control+V inserts that owned
text through the same policy, events, Unicode validation, and undo history as a
terminal paste. An empty or password-suppressed copy or cut preserves the
existing application buffer. External terminal paste remains the bracketed
`Paste` event path; the in-process shortcut never claims an unavailable
synchronous host clipboard read. While a modal plane is active, these shortcuts
follow the
[modal keyboard contract](../../concepts/modality.md#keyboard-text-and-paste) on
its stable
[modal route boundary](../../concepts/modality.md#modal-route-boundaries), so
editor callbacks cannot redirect the in-progress handled key to newly exposed
ancestry.

A primary pointer press focuses the editor and captures the pointer. Cell
coordinates — including those inferred from pixel protocols — map through the
same grapheme widths used for rendering, so wide and combining clusters can
never yield interior indices. Drag release, focus or capture cancellation,
disabling, hiding, detachment, and disposal release transient ownership without
changing the text.

A primary double-click selects the complete Unicode-safe word or non-word
grapheme beneath the pointer. The routed input manager owns the deterministic
500-millisecond same-target, same-cell, same-button gesture count; the editor
owns only the resulting grapheme-aligned selection.

Wheel deltas scroll the editor's existing horizontal and vertical content
offsets in cell and logical-line units. The editor handles a wheel event only
when at least one enabled offset changes. At either endpoint it leaves an
otherwise unmoved event unhandled, so normal bubble routing can offer it to an
enclosing [scrollable `Container`](../../concepts/scrolling.md) instead of
swallowing nested scrolling.

## Example

![The TextInput control rendered in the live showcase](../../images/controls/text-input.png)

![The TextInput control focused for editing in the live showcase](../../images/controls/text-input-focused.png)

```csharp
var name = new TextInput
{
    MaxLength = 80,
    Placeholder = "Enter your name",
    Width = Length.Percent(100),
};
```

## Expected behavior

Editing behaves correctly for empty and Unicode text, including movement and
deletion across combining and emoji clusters. Selection, read-only, password,
and maximum-length policies hold; events fire and cancel as documented; paste
respects the same limits; and the clipboard fallback works. Mouse and pixel hit
testing, horizontal and vertical scrolling, resize, focus and disabled state,
the cursor-shape fallback, and the final cursor and cells all behave as
described above.

Mounted cross-layer coverage in
[`TextInputSurfaceTests`](../../../tests/SharpVision.Tests/Controls/Input/TextInputSurfaceTests.cs)
drives terminal bytes through the live application and demonstrates placeholder
rendering, focus, Unicode typing and deletion, atomic paste, wide-cell
selection, the submit policy, password masking, read-only and disabled refusal,
automatic offsets, resize repair, and the committed terminal cursor. The
component harness does not consider input consumed until the terminal session
requests its next read, so an earlier dispatcher idle cannot expose a partially
routed action.

The pure model additionally replays 10,000 operations with seed `0x00ED175A`
over ASCII, CJK, combining sequences, emoji ZWJ sequences, flags, and invalid
UTF-16. Every step independently validates both endpoints, the maximum grapheme
count, deterministic replay, and the absence of split clusters.
