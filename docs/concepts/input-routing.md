# Input routing

## Input routing contract

Terminal bytes decode into immutable key, text, pointer, paste, focus, resize,
query, closure, or fault events before entering the UI project. Controls never
parse terminal bytes.

## Terminal input values

`SharpVision.Terminal.Input.Decoder` incrementally consumes borrowed byte spans
and synchronously calls `IInputSink`. The sink receives immutable `Stroke`,
`Text`, `Pointer`, and `Focus` values, an owned `Paste`, or a redacted protocol
`Diagnostic`; no parser callback span crosses that boundary.

`Stroke` preserves a logical `Code`, an optional Unicode `Rune` for character
keys, a non-negative native numeric code, composable `Modifiers`, and a
press/repeat/release `Action`. `Text` contains exactly one valid Rune. Printable
input emits a stroke/text pair so keyboard commands and text composition remain
distinct. Legacy Escape-prefixed printable text sets Alt on the stroke while
preserving the same text Rune.

The decoder retains at most three incomplete UTF-8 bytes and replaces malformed
subsequences minimally with U+FFFD. It maps Enter, Tab, Backspace, cursor keys,
Home/End, Insert/Delete, Page Up/Down, F1-F12, Shift-Tab, xterm CSI modifiers,
and SS3 forms. Valid unknown keys retain `Code.Unknown` plus their native code;
malformed and unsupported forms produce one structural diagnostic and leave the
next input decodable.

Enhanced Kitty events add optional shifted/base-layout Runes, all defined
modifier bits, native F13-F35/lock identities, and press/repeat/release without
changing the same `Stroke`/`Text` boundary. Pointer input preserves both cell
and optional pixel coordinates, while resize-derived metrics mark inferred cell
coordinates explicitly. These additions degrade to the legacy values above when
support is not proven.

A raw Escape remains ambiguous until another byte arrives. `ExpireEscape` emits
it only after `Options.EscapeTimeout` on the injected `TimeProvider`, and
`Complete` resolves it immediately at end-of-stream. The decoder accounts for
bytes handled outside the protocol parser so later diagnostic offsets remain
absolute. Bracketed paste, focus, and mouse decoding build on these values in
the [paste/focus](../protocols/paste-focus.md#paste-and-focus-contract) and
[mouse](../protocols/mouse.md#mouse-reporting-contract) milestones.

## Route construction

Keyboard targets the focused control. Pointer input targets capture when active,
otherwise hit testing over committed layout and clipping. The dispatcher
snapshots ancestry, previews root to target, then bubbles target to root.
`OriginalSource` never changes; controlled retargeting may change `Source`.

`Handled` suppresses remaining ordinary handlers and default control behavior.
Handlers explicitly registered for handled events still run. Tree mutation
during dispatch does not alter the current route; invalidation waits until
dispatch completes.

## Routed-event API

`Event<TArgs>` is an immutable typed identifier with a diagnostic name and a
`TunnelBubble`, `Bubble`, or `Direct` strategy. The standard `Events` catalog
provides key, text, pointer, paste, and terminal-focus identifiers paired with
sealed argument classes over the immutable terminal input values.

`Control.AddHandler` rejects null or duplicate event/delegate pairs and returns
an idempotent registration. Attached registration and removal are
dispatcher-affine. Setting `Handled` skips later ordinary handlers and target
default behavior; `handledEventsToo: true` opts into observing handled routes.

`Router.Route` snapshots both ancestry and the registration-order cutoff before
preview begins. Reparenting and newly added handlers therefore affect the next
route, never the current bubble. Disposed registrations stop immediately. Both
ancestry and per-control handler snapshots use cleared pooled storage so they do
not retain controls or delegates.

`OriginalSource` remains the initiating target. `Source` begins at that target
and can be changed through `Retarget` only while dispatch is active. The current
route control is the handler's `sender`; `Phase` reports preview or bubble.
After an unhandled bubble, only the target's protected default behavior runs.
Exceptions propagate after route state and pooled storage are cleaned.

## Pointer capture and coordinates

Capture is exclusive per pointer source and supports press, drag, scrollbar,
selection, move/resize, and popup interactions. Detach, disable, close,
terminal-focus loss where configured, or explicit release ends capture and
raises cancellation when required.

Pointer events preserve screen cells, optional pixels, inferred cell position,
buttons, wheel delta, modifiers, action, and timestamp. Local coordinates are
derived from committed transforms at each route element.

`Control.HitTest(Point)` requires effective visibility and enabled state, clips
at each parent, and searches `Container.Children` from last to first so the
highest z-order wins. A pointer handler receives `LocalCells` relative to its
current sender's committed bounds.

`CaptureManager.Dispatch` targets exclusive capture when present and otherwise
uses root hit testing. It updates `IsHovered` and `IsPressed` before routing so
handlers observe committed visual state. Release clears press after routing.
Explicit `Release` is quiet; detach, disable, hide, disposal, and terminal-focus
loss emit one `Cancelled` callback with a precise `ReleaseReason`, then clear
capture, hover, and press references synchronously.

## Tests

Use recording controls to assert route order, handled semantics, default action,
capture, focus, coordinates, clipping, z-order, disabled/hidden targets,
mutation/reparent during dispatch, nested scrolling, and final control/render
behavior.

Routing tests additionally force collection after registration disposal and tree
detachment, proving pooled snapshots and registrations retain neither handler
targets nor controls.

Terminal-layer tests repeat representative UTF-8, CSI, and SS3 inputs at every
byte split, cover malformed recovery and completion, and require warmed ASCII
and non-ASCII Rune decoding to allocate zero managed bytes per event. The
fixed-seed hostile-byte suite caps paste/parser retention, injects an explicit
recovery boundary, and requires a known trailing Rune to survive every case.
`Pressable` is the shared traditional-control activation state machine. Space
holds pressed state until a matching release; Enter activates directly. Primary
pointer press focuses and captures, movement updates inside/outside pressed
state, and release inside activates once. Focus loss, capture cancellation,
disable, hide, detach, and disposal clear all held state without activation.
Completed activations carry a validated `ActivationCause` of Keyboard, Pointer,
or Programmatic.
