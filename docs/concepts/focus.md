# Focus

## Focus contract

One control within an active focus scope may hold keyboard focus. A focusable
control is attached, visible, enabled, and accepts focus according to its
contract. Windows, menus, and popups establish scopes where documented.

Focus changes are dispatcher-affine and transactional. Preview handlers may
cancel a requested change. A committed change updates the manager before lost
and gained notifications so handlers observe the new state consistently.

`FocusManager` owns one attached root. `Focus(Control?)` rejects foreign
controls, returns false for ineligible or cancelled targets, and commits
`Focused` plus both controls' `IsFocused` state before `Lost` and `Gained`. Tree
mutation during `Changing` is revalidated before commit. Cleanup caused by
detach, hide, disable, or disposal cannot be cancelled.

## Navigation

Tab and Shift+Tab traverse deterministic tab order; arrows use control-specific
spatial/group navigation for menus, radio groups, and lists. Explicit focus
requests and pointer focus use the same validation path.

`MoveNext(reverse)` sorts eligible members by `TabIndex` and then stable tree
order, wraps at both ends, and uses the same cancellable transaction as an
explicit request.

Modal scopes prevent focus escaping. When a popup/menu closes, focus returns to
the recorded valid owner or the nearest valid scope fallback. Detach, collapse,
disable, or window deactivation releases invalid focus deterministically.

Terminal focus from
[mode 1004](../protocols/paste-focus.md#paste-and-focus-contract) is separate
from control focus and never invents a new focused control.

## Tests

Cover traversal order, cancellation, event order, disabled/hidden/detached
targets, nested/modal scopes, popup restoration, mutation during notification,
explicit/pointer navigation, radio/menu arrows, terminal focus loss, and resize.

Manager/root disposal tests require every focus flag and inherited manager
reference to be cleared before descendants are released.
