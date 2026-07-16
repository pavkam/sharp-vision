# CheckBox

## CheckBox contract

`CheckBox` is a sealed [`Pressable`](../pressable.md#pressable-contract) toggle
with optional inherited `Content` and explicit two- or three-state behavior.

## API

- `IsChecked` is nullable Boolean; null is valid only when `IsThreeState` is
  true. Disabling three-state while null moves to false before notifications.
- `IsThreeState` selects `false → true → null → false`; two-state cycles
  `false ↔ true`.
- `Content` uses managed parent ownership.
- `Checked`, `Unchecked`, `Indeterminate`, and `StateChanged` are control
  events.

State setters validate before mutation, clear resolved style caches, and request
the strongest impact declared by the active visual-state styles. They then raise
the specific event followed by `StateChanged`. Disabling three-state mode from
null stages `IsThreeState = false` and `IsChecked = false` before either
property notification, so no callback can observe an invalid false/null pair.

The shipped events carry immutable `CheckChangedEventArgs` with previous/current
state and Keyboard, Pointer, or Programmatic cause. `PerformToggle()` shares the
same transition pipeline and ignores unavailable controls. Disabling three-state
mode while null commits the mode and false value before `Unchecked` then
`StateChanged`.

`MarkStyle` selects built-in square, fixed-width bracket, or Unicode tick marks.
`Marks` stores validated printable one-cell Runes for unchecked, checked, and
indeterminate square states. Bracket marks reserve three cells and every other
style reserves one; content receives one further separator cell when present, so
state changes never move its label. A mark that becomes wide under the inherited
policy presents as the state-equivalent ASCII `o`, `x`, or `-`; its configured
Rune remains unchanged. A true value adds `State.Checked` to the inherited
visual-state flags. Disabled foreground always resolves to the muted role,
including checked and indeterminate retained values.

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
`CheckBoxSurfaceTests` mounts the control beneath a real application and proves
normal, hovered, held, focused, checked, indeterminate, and disabled appearance;
complete-Space and pointer causes; Unicode ownership; and tiny clipping.
