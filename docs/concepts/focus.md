# Focus

## Focus contract

One control within an active focus scope may hold keyboard focus. A focusable
control is attached, visible, enabled, and accepts focus according to its
contract. Windows, menus, and popups establish scopes where documented.

Focus changes are dispatcher-affine and transactional. Preview handlers may
cancel a requested change. A committed change updates the manager before lost
and gained notifications so handlers observe the new state consistently.

## Navigation

Tab and Shift+Tab traverse deterministic tab order; arrows use control-specific
spatial/group navigation for menus, radio groups, and lists. Explicit focus
requests and pointer focus use the same validation path.

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
