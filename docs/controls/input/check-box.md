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
- `Checked`, `Unchecked`, `Indeterminate`, and `StateChanged` are routed events.

State setters validate before mutation, update visual state, invalidate render,
then raise the specific event followed by `StateChanged`.

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
