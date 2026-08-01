# Pressable base API

## Overview

`Pressable : ContentControl` is the public base for a focusable, single-face
control that completes a semantic activation through keyboard or pointer
input. It inherits the atomic, publicly replaceable `Content` edge from
[`ContentControl`](content-control.md#overview); it is not a multi-child panel
and exposes no `Children` collection or capacity constructor.

Concrete controls implement `Activate(ActivationCause)`. `Button`, `CheckBox`,
`RadioButton`, `MenuItem`, and each internal `ListItem` use this role. A
control whose face is derived from data rather than replaceable content, such
as [`ComboBox`](input/combo-box.md#overview), derives from `Control` instead
of pretending to be a `Pressable`.

## API

| Member                                          | Purpose                                                                                            |
| ----------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Inherited `Content`                             | Owns the replaceable control face.                                                                 |
| `Activate(ActivationCause)`                     | Commits the concrete semantic action after keyboard, pointer, or programmatic activation succeeds. |
| `IsCheckedState` and related visual-state seams | Let a derived toggle add semantic state without replacing the shared press or capture behavior.    |

`Pressable` makes the control focusable and supplies the shared input state
machine; concrete classes publish their own events and their own programmatic
activation method.

## Interaction

When its `Content` is a `Text`, a `Pressable` treats that text as its caption.
The owner's `UseMnemonic` value controls both marker rendering and automatic
[access-key activation](../concepts/access-keys.md#focus-and-semantic-actions).
An accepted access key focuses the semantic owner and calls `Activate` with
`ActivationCause.Keyboard`.

Space commits the pressed state on its first key press and ignores key
repeats. The matching release activates the control when it is still focused,
or when it is detached and therefore has no focus owner at all. Enter
activates immediately on press. A primary pointer press inside the arranged
box requests focus and capture for the `Pressable` itself, even when `Content`
was the original hit target. Pointer motion updates the pressed state by
containment: releasing inside the bounds activates once, and releasing outside
cancels.

Focus loss, disable, hide, collapse, detach, disposal, terminal-focus loss,
and capture cancellation all clear any held state without activating. The
protected capture-cancellation hook runs after manager ownership and pressed
state are already clear. Keyboard and pointer completions carry the `Keyboard`
and `Pointer` `ActivationCause` values; a concrete programmatic API uses
`Programmatic`.

The input state machine is one internal composed behavior, also composed by
`ComboBox`, `DateInput`, `DateTimeInput`, `Expander`, and `Window`. It owns no
control-tree state and operates only through the
protected focus and capture boundaries. That keeps the public inheritance role
about replaceable single content, rather than using inheritance merely to
reuse event handling.

## Visual state and extension

`Pressable` enables `Focusable` and `TabStop` by default, so `CanFocus` is
effectively true, and supplies the normal, hovered,
focused, pressed, and disabled states from `Control`. A derived semantic
toggle adds checked, indeterminate, or selected flags through its visual-state
override. Its CLR setter uses `SetVisualStateProperty`, which validates
dispatcher and lifetime access before checking equivalence, commits the field,
clears resolved style caches, requests the strongest phase declared by the
active state styles, and then publishes one property notification.

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

## Expected behavior

A `Pressable` inherits from `ContentControl` and exposes no `Children`; its
content edge stays replaceable and follows the documented ownership rules.
Space and Enter follow the transitions above, pointer presses that originate
on the content still route focus and capture to the owner, and an inside
release activates exactly once while an outside release cancels. Every
availability change cancels a held press without activating, visual-state
changes request their combined impact, callbacks arrive in the documented
order, and Unicode content and tiny bounds render safely. Tests cover each of
these paths.
