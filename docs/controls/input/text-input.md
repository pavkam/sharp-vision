# TextInput

## TextInput contract

`TextInput` is a focusable single- or multiline editor whose caret and selection
indices are valid grapheme boundaries.

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
- `Backspace` and `Delete` remove the selected range or exactly one neighboring
  cluster. `Replace` validates the complete proposal before allocation, enforces
  return/tab policy, and truncates maximum-length input only at a grapheme
  boundary. `MaxLength` zero means unlimited.
- `ProjectPassword` validates a printable one-cell mask and returns exactly one
  mask `Rune` per source grapheme. Invalid UTF-16 source units count as their
  own conservative replacement clusters but are never normalized or copied into
  the projection.
- `EditResult` owns the resulting immutable `Text`, directional `Selection`, and
  a `Changed` flag. Callers own undo/redo history by retaining these snapshots;
  the pure model retains no hidden mutable history.

## API

- `Text` is non-null; invalid assignment throws before mutation.
- `IsReadOnly`, `AcceptsReturn`, `AcceptsTab`, `PasswordCharacter`, and
  `MaxLength` define editing policy. Max length counts grapheme clusters.
- `CaretIndex`, `SelectionStart`, and `SelectionLength` clamp only through
  explicit selection methods; invalid direct values throw.
- `TextChanging` is cancellable; `TextChanged`, `SelectionChanged`, and
  `Submitted` occur after commit.

Password mode masks display and excludes secret text from diagnostics,
snapshots, and default clipboard copy. The model still stores caller-provided
text; it is not a secure-memory primitive.

## Interaction

Typed text, navigation, selection, Backspace/Delete, Home/End, word movement,
undo/redo policy, paste, copy/cut, mouse placement/drag, and scrolling operate
on grapheme boundaries. IME composition is represented separately from committed
text when the terminal protocol supplies it.

## Example

```csharp
var name = new TextInput
{
    MaxLength = 80,
    Width = Length.Percent(100),
};
```

## Test obligations

Cover empty/Unicode editing, combining/emoji movement and deletion, selection,
read-only/password/max length, events/cancellation, paste limits, clipboard
fallback, mouse/pixel hit testing, horizontal/vertical scrolling, resize,
focus/disabled state, and final cursor/cells.

The pure model additionally replays 10,000 operations with seed `0x00ED175A`
over ASCII, CJK, combining sequences, emoji ZWJ sequences, flags, and invalid
UTF-16. Every step independently validates both endpoints, maximum grapheme
count, deterministic replay, and absence of split clusters.
