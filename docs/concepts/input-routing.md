# Input routing

## Input routing contract

Terminal bytes decode into immutable key, text, pointer, paste, focus, resize,
query, closure, or fault events before entering the UI project. Controls never
parse terminal bytes.

## Route construction

Keyboard targets the focused control. Pointer input targets capture when active,
otherwise hit testing over committed layout and clipping. The dispatcher
snapshots ancestry, previews root to target, then bubbles target to root.
`OriginalSource` never changes; controlled retargeting may change `Source`.

`Handled` suppresses remaining ordinary handlers and default control behavior.
Handlers explicitly registered for handled events still run. Tree mutation
during dispatch does not alter the current route; invalidation waits until
dispatch completes.

## Pointer capture and coordinates

Capture is exclusive per pointer source and supports press, drag, scrollbar,
selection, move/resize, and popup interactions. Detach, disable, close,
terminal-focus loss where configured, or explicit release ends capture and
raises cancellation when required.

Pointer events preserve screen cells, optional pixels, inferred cell position,
buttons, wheel delta, modifiers, action, and timestamp. Local coordinates are
derived from committed transforms at each route element.

## Tests

Use recording controls to assert route order, handled semantics, default action,
capture, focus, coordinates, clipping, z-order, disabled/hidden targets,
mutation/reparent during dispatch, nested scrolling, and final control/render
behavior.
