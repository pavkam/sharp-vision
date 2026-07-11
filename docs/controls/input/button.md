# Button

## Button contract

`Button` is a focusable command control with one optional content child. One
completed activation raises `Click` and invokes its command once.

## API

- `Content` uses managed parent ownership.
- `Command` and `CommandParameter` provide optional command activation.
- `IsDefault` and `IsCancel` participate in window-level Enter/Escape handling
  only when no focused control consumes the key.
- `Click` is a control event raised after pressed state is released and before
  command execution; command failure follows runtime exception policy.

The shipped control exposes `Click` as a conventional CLR event carrying
`ActivationEventArgs`; it uses the same committed activation pipeline as routed
keyboard and pointer input. `PerformClick()` uses Programmatic cause and rejects
disabled or hidden controls. A non-null `ICommand` is queried with the exact
borrowed `CommandParameter`; false suppresses both Click and execution. Command
replacement observes `CanExecuteChanged` and raises standard property change
notification without retaining disposed Buttons.

`Content` is the atomic capacity-one child. Measure and arrange include its
margin inside the Button's shared padding, and rendering remains semantic
through the child's inherited active style. `IsDefault` and `IsCancel` are
stored for Window fallback routing in Phase 5C.

## Interaction

Space presses on key down and activates on matching key up while focused. Enter
activates directly. Pointer press captures and sets pressed only while the
pointer remains inside; release inside activates once. Disable, detach, focus
loss policy, or capture cancellation clears pressed without activation.

## Example

```csharp
var save = new Button { Content = new Text("Save") };
save.Click += (_, _) => Save();
```

## Test obligations

Cover Space/Enter/pointer parity, capture movement, cancellation, default/cancel
routing, command ordering/failure, disabled/hidden state, focus, content
ownership, combined visual states, Unicode/tiny layout, and final cells/events.
