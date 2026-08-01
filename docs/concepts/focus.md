# Focus

## Overview

At most one control within a `FocusManager` root holds keyboard focus at a
time. A control is focusable when it is attached, visible, enabled, and has
`CanFocus` set. When a [modal plane](modality.md#modal-focus) is active, it
narrows which controls are eligible for focus, but it does not create a second
focus manager.

Focus changes happen on the dispatcher thread and are transactional. Preview
handlers may cancel a requested change. Once a change commits, the manager
updates its state before the lost and gained notifications fire, so handlers
always observe the new state consistently.

Each `FocusManager` owns exactly one attached root. `Focus(Control?)` rejects
controls that belong to another root, and returns false when the target is
ineligible or the change is cancelled. A committed change updates `Focused` and
both controls' `IsFocused` state before raising `Lost` and `Gained`. If the
tree is mutated during the `Changing` event, the target is revalidated before
the change commits. Cleanup triggered by detach, hide, disable, or disposal
cannot be cancelled.

## Navigation

`MoveNext()` and `MoveNext(reverse: true)` walk a deterministic tab order.
After a key routes unhandled, the shared control default maps a pressed Tab to
`MoveNext()` and Shift+Tab to `MoveNext(reverse: true)`. A control-specific
behavior may handle the key first; for example, `TextInput.AcceptsTab` inserts
a tab character instead of moving focus. Other modifiers are left for explicit
control behavior to interpret. Explicit and pointer-triggered focus requests go
through the same `Focus(Control?)` validation path. While modality is active,
that path rejects targets outside the active plane, and unhandled Tab and
Shift+Tab follow the
[plane-wide traversal contract](modality.md#keyboard-text-and-paste).

`MoveNext(reverse)` sorts the eligible members by `TabIndex` and then by stable
tree order, wraps around at both ends, and uses the same cancellable
transaction as an explicit request. When the currently focused control is not
itself an eligible member - for example a `TabStop=false` leaf that was focused
by a pointer press, or a descendant excluded by an ancestor's
`TabNavigation.None` - traversal falls back to that same tree order instead of
wrapping: forward moves to the nearest following member, and backward moves to
the nearest preceding one.

Stable tree order descends the navigation-participating ownership slots on
every `Control`, visiting slots in registration order and items within each
slot in item order. Private content and framework parts therefore take part in
navigation when their slot opts in, without pretending that their owner is a
public multi-child `Container`. A control with a semantic visual order, such as
`Stack.Reverse`, may override only that local navigation order; its registry
membership and eligibility are unchanged.

A primary pointer press focuses the nearest eligible `CanFocus` member found by
walking from the hit target toward the owned root, before routed pointer
behavior runs. Clicking content inside a focusable composite therefore focuses
the composite, while clicking a focusable leaf focuses that leaf. The committed
focus state drives the control's `Focused` visual-state overlay. When modality
is active, the [active-plane filter](modality.md#modal-pointer-and-capture)
applies to the pointer target before this focus request.

When the modal-eligible hit target belongs to a
[`Window`](../controls/windows/window.md#chrome-and-interaction), the nearest
Window also bounds the pointer-focus search. Eligible controls along the hit
ancestry inside that Window may receive focus, but a press on chrome or
background cannot climb past the Window to focus an application-shell
ancestor. Window activation is independent of this search, so such a press
still updates `Application.ActiveWindow` even when focus stays where it was.

Every committed focus transition updates `Application.ActiveWindow` from the
nearest Window ancestor of the new focus target. Programmatic focus, pointer
focus, Tab traversal, access keys, modal entry, and focus release all use this
same activation rule. When the committed target lies outside every Window,
including a null target, activation is cleared. Focus flags and a Window's
`IsActive` remain separate pieces of state.

Detach, hide/collapse, disable, disposal, or disposal of the manager itself
releases invalid focus deterministically.

Disposing the `FocusManager` from inside `Changing`, a control focus-state
callback, `Lost`, or `Gained` makes the manager unavailable immediately, stops
the in-flight transition, and finishes physical focus and ownership cleanup
before the enclosing request returns. Requests that were queued behind it
complete as rejected, in their original order, and each completion observes
only the failure attached to its own request. The enclosing rethrow preserves
an earlier deferred failure over later focus callbacks or disposal cleanup. If
a modal restoration is cancelled by this cleanup, it treats the disposed
manager as a terminal no-focus state rather than attempting a new fallback
request.

## Hierarchical Tab navigation

`Control.TabNavigation` governs how a control contributes to its owning
navigation tree. The modes are:

- Continue: contribute an eligible control, then its descendants in direct
  sibling order. Reverse traversal visits descendants before the control.
- Once: contribute one entry: the eligible owner, otherwise the first eligible
  descendant.
- Cycle: use Continue order while focus is inside the scope and wrap only at
  that scope's boundaries.
- None: an eligible owner contributes itself but its descendants do not enter
  sequential traversal.

Each owner sorts only its direct navigation participants, by `TabIndex` and
then insertion order. A grandchild therefore never competes directly with a
grandparent's siblings. Generated framework parts do not participate.

Lists, Menu, NavigationView, and ComboBox use None: the widget owns the single
Tab stop, and arrow keys move a current item without moving focus onto private
item faces. TabControl uses Continue: its header owner is a tab stop, and the
selected page contributes its ordinary descendant controls. A standalone
ScrollBar can be a tab stop; generated scrollbar parts cannot.

Setting `CanFocus` to false on the focused control commits the new eligibility
first, then synchronously clears `FocusManager.Focused` and `IsFocused`. This
cleanup bypasses the cancellable `Changing` event, and `Lost` observes the
committed false/null state before the `CanFocus` property-change notification
fires. If eligibility changes from inside a `Changing`, `Lost`, or `Gained`
callback, cleanup waits only for the active transaction guard to unwind and
completes before the enclosing `Focus` request returns. The `CanFocus`
property-change notification is deferred until after that cleanup, which
preserves the same observable ordering. Focus eligibility is local to the
control and independent of pointer capture, so this transition neither
releases capture nor evicts a focused descendant.

Terminal focus reported through
[mode 1004](../protocols/paste-focus.md#overview) is separate from control
focus and never invents a new focused control.

[Access keys](access-keys.md#focus-and-semantic-actions) reuse this focus
manager: focusable captions target themselves, captioned scopes target their
first eligible descendant in hierarchical tab order, and label-like leaves
advance from their stable tree anchor. Modal eligibility remains authoritative
throughout.

## Expected behavior

The guarantees above are verified across traversal order, cancellation, event
order, disabled, hidden, and detached targets,
[nested modal restoration](modality.md#modal-focus), popup restoration, tree
mutation during notifications, explicit and pointer-driven navigation, radio
and menu arrow keys, terminal focus loss, and resize.

When the manager or its root is disposed, every focus flag and inherited
manager reference is cleared before the descendants themselves are released.
