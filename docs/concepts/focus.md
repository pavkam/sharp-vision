# Focus

## Overview

At most one control within a `FocusManager` root holds keyboard focus at a time.
A control is focusable when it is attached, visible, enabled, and has `CanFocus`
set. When a [modal plane](modality.md#modal-focus) is active, it narrows which
controls are eligible for focus, but it does not create a second focus manager.

Focus changes happen on the dispatcher thread and are transactional. Preview
handlers may cancel a requested change. Once a change commits, the manager
updates its state before the lost and gained notifications fire, so handlers
always observe the new state consistently.

Each `FocusManager` owns exactly one attached root. `Focus(ControlBase?)`
rejects controls that belong to another root, and returns false when the target
is ineligible, the change is cancelled, or a focus callback invalidated the
just-committed target — a `Gained`, `Lost`, or `FocusEntered` handler that
re-points focus, detaches, hides, disables, or disposes the target makes the
request report false even though the change committed and every notification
fired. A committed change updates `Focused` and both controls' `IsFocused` state
before raising `Lost` and `Gained`. If the tree is mutated during the `Changing`
event, the target is revalidated before the change commits. A request made by
one `Changing` subscriber supersedes the interrupted proposal's remaining
subscriber delivery immediately. The outer transaction still finishes under its
normal cancellation and eligibility rules, then the queued newer request runs
next. Cleanup triggered by detach, hide, disable, or disposal cannot be
cancelled.

Every synchronous focus callback is an invalidation boundary. The manager
revalidates the committed target after each control and manager notification; an
obsolete, detached, hidden, disabled, or disposed target cannot receive a later
notification. Input handlers that request focus apply the same rule before
continuing their pointer, keyboard, access-key, selection, or edit action.
`IsFocused` property observers cannot skip a control's mandatory focus-change
work: after property publication, a non-virtual notifier cancels any active
framework text-selection gesture and releases its capture before the component
`OnFocusChanged` hook runs. The hook may omit its base call or throw; all three
steps still run, and the first failure is rethrown after focus-state cleanup has
completed.

Tab, Shift+Tab, and access-key focus register a latest-wins reveal intent rather
than reading arranged bounds inside the focus transaction. The application
consumes that intent only after `IsFocused`, `FocusEntered`, `GotFocus`, and
`FocusManager.Gained` callbacks have completed and pending measure/arrange work
has settled. A reveal that changes nested scroll offsets receives a bounded
follow-up arrange before rendering. A newer focus commit replaces the intent;
focus loss, detach or reattach, disposal, ineligibility, or modal-plane
replacement cancels it. Pointer and ordinary programmatic focus never register
automatic reveal work.

```mermaid
sequenceDiagram
    participant Caller
    participant FocusManager
    participant PrevAncestors as Previous's divergent ancestors
    participant Previous as Previous control
    participant New as New control
    participant NewAncestors as New's divergent ancestors

    Caller->>FocusManager: Focus(control)
    FocusManager->>FocusManager: Changing subscribers in registration order

    alt cancellable && preview.Cancel, or target now ineligible/disallowed, or manager disposed
        FocusManager-->>Caller: false (no commit)
    else change proceeds
        FocusManager->>FocusManager: Focused = control<br/>Previous.IsFocused = false; New.IsFocused = true
        FocusManager->>Previous: LostFocus
        FocusManager->>Previous: FocusLeft (self)
        FocusManager->>PrevAncestors: FocusLeft (deepest first, up to but excluding the common ancestor)
        FocusManager->>FocusManager: Lost?.Invoke(changed)
        FocusManager->>NewAncestors: FocusEntered (from just below the common ancestor, downward)
        FocusManager->>New: FocusEntered (self)
        FocusManager->>New: GotFocus
        FocusManager->>FocusManager: Gained?.Invoke(changed)
        FocusManager->>FocusManager: retain latest keyboard reveal intent
        FocusManager-->>Caller: true while the target stays valid after callbacks
    end
```

The "common ancestor" is the deepest control shared by the previous and new
focus paths - it never receives `FocusLeft`/`FocusEntered` since it stays
focus-within throughout the transition. When there is no previous control or no
new control (a release or the initial focus), the corresponding lifelines are
simply omitted.

## Navigation

`MoveNext()` and `MoveNext(reverse: true)` walk a deterministic tab order, and
the anchored overload `MoveNext(ControlBase? anchor, bool reverse = false)`
walks the same order from an explicit starting control — the application's
post-route Tab command uses it against a stable route anchor. After a key routes
unhandled, the shared control default maps a pressed Tab to `MoveNext()` and
Shift+Tab to `MoveNext(reverse: true)`. A control-specific behavior may handle
the key first; for example, `TextInput.AcceptsTab` inserts a tab character
instead of moving focus. Other modifiers are left for explicit control behavior
to interpret. Explicit and pointer-triggered focus requests go through the same
`Focus(ControlBase?)` validation path. While modality is active, that path
rejects targets outside the active plane, and unhandled Tab and Shift+Tab follow
the [plane-wide traversal contract](modality.md#keyboard-text-and-paste).

`MoveNext(reverse)` sorts the eligible members by `TabIndex` and then by stable
tree order, wraps around at both ends, and uses the same cancellable transaction
as an explicit request. When the currently focused control is not itself an
eligible member - for example a `IsTabStop=false` leaf that was focused by a
pointer press, or a descendant excluded by an ancestor's `TabNavigation.None` -
traversal falls back to that same tree order instead of wrapping: forward moves
to the nearest following member, and backward moves to the nearest preceding
one.

Stable tree order descends the navigation-participating ownership slots on every
`ControlBase`, visiting slots in registration order and items within each slot
in item order. Private content and framework parts therefore take part in
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
background cannot climb past the Window to focus an application-shell ancestor.
Window activation is independent of this search, so such a press still updates
`Application.ActiveWindow` even when focus stays where it was.

Every committed focus transition updates `Application.ActiveWindow` from the
nearest Window ancestor of the new focus target. Programmatic focus, pointer
focus, Tab traversal, access keys, modal entry, and focus release all use this
same activation rule. When the committed target lies outside every Window,
including a null target, activation is cleared. Focus flags and a Window's
`IsActive` remain separate pieces of state.

Detach, hide/collapse, disable, disposal, or disposal of the manager itself
releases invalid focus deterministically.

A `Changing`, `Lost`, or `Gained` handler, or a control focus-state callback,
may dispose the `FocusManager` while a focus request is still in flight. That
disposal runs synchronously, in this order:

- The manager becomes unavailable immediately, so no later step in the current
  transition can restore focus.
- Physical focus and ownership cleanup finish before the enclosing focus request
  returns.
- Requests that were queued behind the in-flight one complete as rejected, in
  their original order; each completion observes only the failure attached to
  its own request.
- The enclosing rethrow preserves an earlier deferred failure over later focus
  callbacks or disposal cleanup.
- If disposal cancels a modal restoration in progress, that restoration treats
  the disposed manager as a terminal no-focus state rather than attempting a new
  fallback request.

## Hierarchical Tab navigation

`ControlBase.TabNavigation` governs how a control contributes to its owning
navigation tree:

| Mode       | Forward contribution                                                       | Reverse contribution                                                  | Wrap boundary                                                                                                       |
| ---------- | -------------------------------------------------------------------------- | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| `Continue` | The eligible owner, then its descendants in tree order                     | Same flat list walked backward — descendants visited before the owner | No — wraps at the nearest enclosing `Cycle` ancestor, or the root                                                   |
| `Once`     | One entry: the eligible owner, otherwise its first eligible descendant     | Same single entry regardless of direction                             | No — not a scope boundary                                                                                           |
| `Cycle`    | Same as `Continue`: the eligible owner, then its descendants in tree order | Same flat list walked backward — descendants before the owner         | Yes — traversal wraps within this control's own contributed candidates instead of continuing into an ancestor scope |
| `None`     | One entry: the eligible owner only (descendants never enter traversal)     | Same single entry                                                     | No — excludes descendants, but is not itself a scope boundary                                                       |

Each owner sorts only its direct navigation participants, by `TabIndex` and then
insertion order. A grandchild therefore never competes directly with a
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
control and independent of pointer capture, so this transition neither releases
capture nor evicts a focused descendant.

Terminal focus reported through
[mode 1004](../protocols/paste-focus.md#overview) is separate from control focus
and never invents a new focused control.

[Access keys](access-keys.md#focus-and-semantic-actions) reuse this focus
manager: focusable captions target themselves, captioned scopes target their
first eligible descendant in hierarchical tab order, and label-like leaves
advance from their stable tree anchor. Modal eligibility remains authoritative
throughout.

## Expected behavior

The guarantees above are verified across traversal order, cancellation, event
order, disabled, hidden, and detached targets,
[nested modal restoration](modality.md#modal-focus), popup restoration, tree
mutation during notifications, explicit and pointer-driven navigation, radio and
menu arrow keys, terminal focus loss, and resize.

When the manager or its root is disposed, every focus flag and inherited manager
reference is cleared before the descendants themselves are released.
