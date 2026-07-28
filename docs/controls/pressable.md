# Pressable base API

## Pressable contract

`Pressable : ContentControl` is the public authoring role for one focusable,
single-face control that completes semantic activation through keyboard or
pointer input. It inherits the atomic, publicly replaceable `Content` edge from
[`ContentControl`](content-control.md#contentcontrol-contract); it is not a
multi-child panel and exposes no `Children` collection or capacity constructor.

Concrete controls implement `Activate(ActivationCause)`. `Button`, `CheckBox`,
`RadioButton`, `MenuItem`, and each internal `ListItem` use this role. A control
whose face is derived from data rather than replaceable content, such as
[`ComboBox`](input/combo-box.md#combobox-contract), derives from `Control`
instead of pretending to be a `Pressable`.

## API

| Member                                          | Purpose                                                                                            |
| ----------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Inherited `Content`                             | Owns the replaceable control face.                                                                 |
| `Activate(ActivationCause)`                     | Commits the concrete semantic action after keyboard, pointer, or programmatic activation succeeds. |
| `IsCheckedState` and related visual-state seams | Let a derived toggle add semantic state without replacing the shared press or capture behavior.    |

`Pressable` makes the control focusable and supplies the shared input state
machine; concrete classes publish their own events and programmatic activation
method.

## Interaction

When its `Content` is `Text`, a Pressable treats that text as its caption. The
owner's `UseMnemonic` value controls both marker rendering and automatic
[access-key activation](../concepts/access-keys.md#focus-and-semantic-actions).
An accepted key focuses the semantic owner and calls `Activate` with
`ActivationCause.Keyboard`.

Space commits pressed state on its first key press, ignores repeats, and
activates on the matching release while detached or still focused. Enter
activates immediately on press. A primary pointer press inside the arranged box
requests focus and capture for the `Pressable` itself even when `Content` was
the original hit target. Motion updates pressed state by containment; release
inside activates once, while release outside cancels.

Focus loss, disable, hide, collapse, detach, disposal, terminal-focus loss, and
capture cancellation clear every held state without activation. The protected
capture-cancellation hook runs after manager ownership and pressed state are
clear. Keyboard and pointer completions carry `Keyboard` and `Pointer`
`ActivationCause` values; a concrete programmatic API uses `Programmatic`.

The input state machine is one internal composed behavior shared with
`ComboBox`. It owns no control-tree state and operates only through the
protected focus/capture boundaries. This keeps the public inheritance role about
replaceable single content rather than using inheritance merely to reuse event
handling.

## Visual state and extension

`Pressable` sets `CanFocus` by default and supplies the normal, hovered,
focused, pressed, and disabled states from `Control`. A derived semantic toggle
adds checked, indeterminate, or selected flags through its visual-state
override. Its CLR setter uses `SetVisualStateProperty`, which validates
dispatcher and lifetime access before equivalence, commits the field, clears
resolved style caches, requests the strongest phase declared by active state
styles, and then publishes one property notification.

```csharp
public sealed class ToggleChip : Pressable
{
    private bool _isChecked;

    public bool IsChecked => _isChecked;

    protected override void Activate(ActivationCause cause) =>
        _ = SetVisualStateProperty(ref _isChecked, !_isChecked, nameof(IsChecked));

    protected override bool IsCheckedState => IsChecked;
}
```

## Example

```csharp
public sealed class ActionChip : Pressable
{
    public event EventHandler<ActivationEventArgs>? Activated;

    protected override void Activate(ActivationCause cause) =>
        Activated?.Invoke(this, new ActivationEventArgs(cause));
}
```

## Test obligations

Tests cover inheritance and absence of `Children`, replaceable content
ownership, Space/Enter transitions, content-originated pointer routes, focus and
capture, inside/outside release, every availability cancellation path, combined
visual-state impacts, callback ordering, Unicode content, and tiny bounds.
