# Pressable base API

## Overview

`PressInteractionBase : ControlBase` is the public base for any focusable
control that completes a semantic activation through keyboard or pointer input,
independent of what content the concrete control owns. It builds the shared
press-interaction state machine, makes the control focusable, and forwards
focus-loss, capture-loss, and availability-loss notifications into that state
machine; a concrete control implements `Activate(ActivationCause)` and, when its
own event handling needs to intercept or short-circuit routed events first (a
drop-down's own Escape/arrow-key handling, for example), overrides `OnEvent` and
forwards to the protected `Interaction` member itself at the point that fits its
own routing.

`PressableBase : PressInteractionBase` layers the single-text-caption authoring
role on top: no `Content` and no `Children` collection, with the only caption
surface being the string `Text` property, backed by a lazily materialized owned
caption child that never allocates until a control's text is first assigned.
`Pressable<TStyle>` further adds the standard primary `Style`/`ActualStyle` slot
on top of `PressableBase` for a pressable with an immutable complete typed
style. A control that needs arbitrary owned content instead of a plain caption
does not derive from `PressableBase` — it derives from `PressInteractionBase`
directly instead, the way [`ComboBox`](input/combo-box.md#overview),
`DateInput`, and `DateTimeInput` do for their popup-backed fields. `ListItem`
composes the same shared press behavior directly (not through
`PressInteractionBase`) while keeping
[`ContentControl`](content-control.md#overview)'s replaceable `Content` edge for
realized template output, since it already derives from `ContentControl` for
that reason.

Concrete controls implement `Activate(ActivationCause)`. `Button`, `CheckBox`,
and `RadioButton` use `Pressable<TStyle>`; `HyperlinkButton`, `MenuItem`, and
`NavigationViewItem` use `PressableBase` directly; each internal `TabHeader`
also does, with its own field-backed `Text` override that never materializes a
caption child. A control whose face is derived from data rather than a caption,
such as [`ComboBox`](input/combo-box.md#overview), derives from
`PressInteractionBase` instead of pretending to be a `PressableBase`.

## API

| Member                                        | Purpose                                                                                                                                |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `Text`                                        | The non-null caption string; virtual, so `NavigationViewItem` and `TabHeader` back it with their own field instead of a caption child. |
| `Command`, `CommandParameter`                 | Optional `ICommand` a concrete control invokes on activation, and its borrowed parameter.                                              |
| `Activate(ActivationCause)`                   | Commits the concrete semantic action after keyboard, pointer, or programmatic activation succeeds.                                     |
| `CheckedState` and related visual-state seams | Let a derived toggle add semantic state without replacing the shared press or capture behavior.                                        |

`PressableBase` makes the control focusable, supplies the shared input state
machine, and owns `Command`/`CommandParameter` lifetime — including
`CanExecuteChanged` subscription and dispatcher-marshaled invalidation. Concrete
classes still publish their own events and their own programmatic activation
method (`Button.Click`/`PerformClick`, `MenuItem.Invoked`/ `PerformInvoke`, and
so on); only the command lifetime itself is shared. A concrete `Activate`
override decides whether and how `Command` factors into its own semantics —
`Button` and `HyperlinkButton` check `CanExecute` and call `Execute` after their
`Click` event; toggles and menu items that have not opted into command support
simply leave the inherited property unused.

## Interaction

The owner's `UseMnemonic` value controls both marker rendering and automatic
[access-key activation](../concepts/access-keys.md#focus-and-semantic-actions)
against `Text`. An accepted access key focuses the semantic owner and calls
`Activate` with `ActivationCause.Keyboard`.

Space commits the pressed state on its first key press and ignores key repeats.
The matching release activates the control when it is still focused, or when it
is detached and therefore has no focus owner at all. Enter activates immediately
on press. A primary pointer press inside the arranged box requests focus and
capture for the `PressableBase` itself, even when the owned caption child was
the original hit target. Pointer motion updates the pressed state by
containment: releasing inside the bounds activates once, and releasing outside
cancels.

The rectangle used for that containment test is `InteractionBounds`, a
`protected virtual` seam that defaults to `Bounds`. A derived control that
paints its pressed face translated away from `Bounds` - such as `Button`'s
whole-cell shadow translation while `Pressed` is true - overrides
`InteractionBounds` to return the same rectangle it actually paints, so press,
drag, and release always agree with the committed painted geometry instead of
the untranslated layout footprint (see
[`docs/controls/input/button.md`](input/button.md#interaction)). `HitTest` stays
on `Bounds` regardless: it is the stable layout footprint used before a press
begins, and capture governs hit testing once a press is under way.

Focus loss, disable, hide, collapse, detach, disposal, terminal-focus loss, and
capture cancellation all clear any held state without activating. The protected
capture-cancellation hook runs after manager ownership and pressed state are
already clear. Keyboard and pointer completions carry the `Keyboard` and
`Pointer` `ActivationCause` values; a concrete programmatic API uses
`Programmatic`.

The input state machine is one internal composed behavior, built once by
`PressInteractionBase` and inherited by every `PressableBase` descendant as well
as `ComboBox`, `DateInput`, and `DateTimeInput`. `Expander`, `ListItem`, and
`Window` compose the same behavior directly instead of through
`PressInteractionBase`, since each already derives from a different public base
(`HeaderedContentControl`, `ContentControl`, and
`ChromeAuthoringFloatingSurface` respectively) for its own content role. In
every case the behavior owns no control-tree state and operates only through the
protected focus and capture boundaries, keeping the public inheritance role
about replaceable content, rather than using inheritance merely to reuse event
handling.

## Visual state and extension

`PressableBase` enables `Focusable` and `TabStop` by default, so `CanFocus` is
effectively true, and supplies the normal, hovered, focused, pressed, and
disabled states from `ControlBase`. A derived semantic toggle adds checked,
indeterminate, or selected flags through its visual-state override. Its CLR
setter uses `SetVisualStateProperty`, which validates dispatcher and lifetime
access before checking equivalence, commits the field, clears resolved style
caches, requests the strongest phase declared by the active state styles, and
then publishes one property notification.

```csharp
public sealed class ToggleChip : PressableBase
{
    private bool _isChecked;

    public bool IsChecked => _isChecked;

    protected override void Activate(ActivationCause cause) =>
        _ = SetVisualStateProperty(ref _isChecked, !_isChecked, nameof(IsChecked));

    protected override bool CheckedState => IsChecked;
}
```

## Example

```csharp
public sealed class ActionChip : PressableBase
{
    public event EventHandler<ActivationEventArgs>? Activated;

    protected override void Activate(ActivationCause cause) =>
        Activated?.Invoke(this, new ActivationEventArgs(cause));
}
```

## Expected behavior

A `PressableBase` derives from `PressInteractionBase`, not `ContentControl`, and
exposes no `Content` or `Children`; `Text` is the sole caption surface, and
assigning it notifies exactly once and is silent on same-value assignment. Space
and Enter follow the transitions above, pointer presses that originate on the
owned caption child still route focus and capture to the owner, and an inside
release activates exactly once while an outside release cancels. Every
availability change cancels a held press without activating, visual-state
changes request their combined impact, callbacks arrive in the documented order,
and Unicode captions and tiny bounds render safely. Tests cover each of these
paths.
