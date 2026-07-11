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
Phase 5 controls map Tab/Shift+Tab to those calls and add control-specific arrow
navigation for menus, radio groups, and lists. Explicit and pointer-triggered
focus requests use the same `Focus(Control?)` validation path.

`MoveNext(reverse)` sorts eligible members by `TabIndex` and then stable tree
order, wraps at both ends, and uses the same cancellable transaction as an
explicit request.

Detach, hide/collapse, disable, disposal, or manager disposal releases invalid
focus deterministically. Modal focus containment and popup/menu restoration are
Phase 5 responsibilities built from these manager guarantees.

Terminal focus from
[mode 1004](../protocols/paste-focus.md#paste-and-focus-contract) is separate
from control focus and never invents a new focused control.

## Tests

Cover traversal order, cancellation, event order, disabled/hidden/detached
targets, nested/modal scopes, popup restoration, mutation during notification,
explicit/pointer navigation, radio/menu arrows, terminal focus loss, and resize.

Manager/root disposal tests require every focus flag and inherited manager
reference to be cleared before descendants are released.
