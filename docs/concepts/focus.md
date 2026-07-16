# Focus

## Focus contract

One control within a `FocusManager` root may hold keyboard focus. A focusable
control is attached, visible, enabled, and has `CanFocus` set. Phase 5 windows,
menus, and popups will compose manager roots into modal scopes.

Focus changes are dispatcher-affine and transactional. Preview handlers may
cancel a requested change. A committed change updates the manager before lost
and gained notifications so handlers observe the new state consistently.

`FocusManager` owns one attached root. `Focus(Control?)` rejects foreign
controls, returns false for ineligible or cancelled targets, and commits
`Focused` plus both controls' `IsFocused` state before `Lost` and `Gained`. Tree
mutation during `Changing` is revalidated before commit. Cleanup caused by
detach, hide, disable, or disposal cannot be cancelled.

## Navigation

`MoveNext()` and `MoveNext(reverse: true)` traverse deterministic tab order.
After an unhandled key route, the shared control default maps a pressed Tab to
`MoveNext()` and Shift+Tab to `MoveNext(reverse: true)`. A control-specific
behavior may handle the key first; for example, `TextInput.AcceptsTab` inserts a
tab instead of moving focus. Other modifiers remain available to explicit
control behavior. Explicit and pointer-triggered focus requests use the same
`Focus(Control?)` validation path.

`MoveNext(reverse)` sorts eligible members by `TabIndex` and then stable tree
order, wraps at both ends, and uses the same cancellable transaction as an
explicit request. Stable tree order descends navigation-participating ownership
slots on every `Control`, in slot-registration then item order; private content
and framework parts therefore participate when their slot opts in without
pretending their owner is a public multi-child `Container`. Controls with a
semantic visual order, such as `Stack.Reverse`, may override only that local
navigation order while retaining the same registry membership and eligibility.

A primary pointer press focuses the nearest eligible `CanFocus` member from the
hit target toward the owned root before routed pointer behavior runs. Clicking
content inside a focusable composite therefore focuses the composite; clicking a
focusable leaf focuses that leaf. The committed focus state drives the control's
`Focused` visual-state overlay.

Detach, hide/collapse, disable, disposal, or manager disposal releases invalid
focus deterministically.

## Navigation scopes

`Control.TabNavigation` governs how Tab traversal treats one control's subtree.
Three modes are available:

```
TabNavigation.Continue    (default — no boundary, global flat traversal)
TabNavigation.Cycle       (Tab wraps within this control's children)
TabNavigation.Contained   (Tab is trapped; focus cannot exit via Tab/Shift+Tab)
```

`FocusManager.MoveNext` resolves the nearest ancestor with a non-`Continue`
`TabNavigation` mode and collects eligible tab stops only within that scope. The
scope root itself is excluded from its own candidate list; nested scopes define
independent boundaries so the innermost scope always wins. Controls that do not
set `TabNavigation` behave identically to prior versions — their tab stops are
part of the enclosing scope (or the tree root for the default global traversal).

The scope resolution walk, the scope-bounded collection, and the per-mode
wrapping behavior form the complete scope algorithm:

```
                        ┌────────────────────────────────────────┐
   Tab pressed          │  FocusManager.MoveNext                 │
  ─────────────────►    │                                        │
                        │  1. FindScope(Focused)                 │
                        │     walk Focused → Parent → … → Root   │
                        │     return first TabNavigation≠Continue │
                        │     or Root if none                    │
                        │                                        │
                        │  2. Collect tab stops within scope     │
                        │     • skip scope root itself           │
                        │     • skip nested scope subtrees       │
                        │     • sort (TabIndex, TreeOrder)       │
                        │                                        │
                        │  3. Navigate                           │
                        │     Cycle / Contained: wrap in scope   │
                        │     Continue (root):   wrap globally   │
                        └────────────────────────────────────────┘
```

### Standard scope assignments

**ComboBox popup** sets `TabNavigation.Contained` on the owned `Popup` and
`IsTabStop = false` on the inner List. When the drop-down opens, the popup
becomes a closed navigation scope: Tab cycles through the realized `ListItem`
controls and cannot escape to controls outside the popup.

```
ComboBox
├─ [Popup]  TabNavigation = Contained
│  └─ List  IsTabStop = false
│     ├─ ListItem  "A"
│     ├─ ListItem  "B"     ◄── Tab cycles A → B → C → A
│     └─ ListItem  "C"
└─ (field)
```

**Menu** sets `TabNavigation.Cycle`. Tab wraps through the menu's `MenuItem`
children, skipping separators. `MenuItem.OnFocusChanged` synchronizes
`Menu.SelectedIndex` whenever a child receives focus externally (for example
through Tab), so subsequent arrow-key navigation starts from the correct
position.

```
Menu  TabNavigation = Cycle
├─ MenuItem  "File"
├─ MenuItem  "Edit"        ◄── Tab cycles File → Edit → Help → File
├─ MenuSeparator           (skipped: not focusable)
└─ MenuItem  "Help"
```

Setting `CanFocus` to false on the focused control commits the new eligibility
before synchronously clearing `FocusManager.Focused` and `IsFocused`. This
cleanup bypasses the cancellable `Changing` event, and `Lost` observes the
committed false/null state before the `CanFocus` property-change notification.
If eligibility changes from a `Changing`, `Lost`, or `Gained` callback, cleanup
waits only for the active transaction guard to unwind and completes before the
enclosing `Focus` request returns. Its `CanFocus` property-change notification
is deferred behind that cleanup, preserving the same observable ordering. Focus
eligibility is local to the control and independent of pointer capture, so this
transition neither releases capture nor evicts a focused descendant.

Terminal focus from
[mode 1004](../protocols/paste-focus.md#paste-and-focus-contract) is separate
from control focus and never invents a new focused control.

## Tests

Cover traversal order, cancellation, event order, disabled/hidden/detached
targets, nested/modal scopes, popup restoration, mutation during notification,
explicit/pointer navigation, radio/menu arrows, terminal focus loss, and resize.

Manager/root disposal tests require every focus flag and inherited manager
reference to be cleared before descendants are released.
