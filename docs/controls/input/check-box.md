# CheckBox

## CheckBox contract

`CheckBox` is a focusable toggle with optional content and explicit two- or
three-state behavior.

## API

- `IsChecked` is nullable Boolean; null is valid only when `IsThreeState` is
  true. Disabling three-state while null moves to false before notifications.
- `IsThreeState` selects `false → true → null → false`; two-state cycles
  `false ↔ true`.
- `Content` uses managed parent ownership.
- `Checked`, `Unchecked`, `Indeterminate`, and `StateChanged` are control
  events.

State setters validate before mutation, update visual state, invalidate render,
then raise the specific event followed by `StateChanged`.

The shipped events carry immutable `CheckChangedEventArgs` with previous/current
state and Keyboard, Pointer, or Programmatic cause. `PerformToggle()` shares the
same transition pipeline and ignores unavailable controls. Disabling three-state
mode while null commits the mode and false value before `Unchecked` then
`StateChanged`.

`Marks` stores validated printable one-cell Runes for unchecked, checked, and
indeterminate states. Layout reserves one mark cell and, only when content is
present, one separator cell before the atomic capacity-one child. A true value
adds `State.Checked` to the inherited visual-state flags.
A mark that becomes wide under the inherited policy presents as the
state-equivalent ASCII `o`, `x`, or `-`; its configured Rune remains unchanged.


## Interaction

Space and pointer activation use the same transition. Press/capture behavior
matches [Button](button.md#interaction). Radio-group semantics do not apply.

## Example

```csharp
var option = new CheckBox
{
    Content = new Text("Include hidden files"),
    IsThreeState = false,
};
```

## Test obligations

Cover both cycles, programmatic changes, event order, invalid null assignment,
Space/pointer parity, capture cancellation, disabled state, focus, combined
styles, content ownership, resize/tiny bounds, and exact mark/content cells.
