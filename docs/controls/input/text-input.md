# TextInput

## TextInput contract

`TextInput` is a focusable single- or multiline editor whose caret and selection
indices are valid grapheme boundaries.

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
