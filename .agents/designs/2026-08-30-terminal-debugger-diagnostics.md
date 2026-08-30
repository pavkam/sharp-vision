# Terminal Debugger Diagnostics Design

## Purpose

Turn `examples/TerminalDebugger` from a capability summary into a complete,
bounded diagnostic tool for the terminal behavior SharpVision actually owns. The
tool must distinguish implementation support, detected terminal support, runtime
activation, multiplexer routing, and live verification instead of collapsing
those separate facts into one green or red label.

## Diagnostic boundary

SharpVision.Terminal will publish one immutable `TerminalDiagnostics` snapshot.
It contains only typed, redacted facts already selected by the runtime:

- canonical backend family, display name, protocol-extension composition, and
  ordered backend-evidence sources;
- negotiation state and final normalized `TerminalQueryDiagnostics`, when
  negotiation ran, retaining XTGETTCAP names but never values;
- detected multiplexer layers, authorization policy, and effective query,
  clipboard, graphics, and string-terminated-query routing decisions;
- configured, evidence-authorized, and successfully activated input/output
  modes;
- the currently selected graphics backend family.

Raw environment values, query bytes, clipboard content, terminal replies, and
unbounded diagnostic history are never retained. Backend identity remains fixed
for the session lifetime while capability and query evidence may refine.

`Session.Diagnostics` exposes the terminal-layer snapshot to direct runtime
consumers. `ISink.Diagnostics` publishes ordered refinements using a default
no-op implementation so existing sinks remain source-compatible.
`Application.TerminalDiagnostics` exposes the latest snapshot and folds in the
renderer-owned graphics selection. `TerminalDiagnosticsChanged` tells retained
UI surfaces to refresh without polling.

## Protocol inventory

The debugger owns a curated exhaustive catalog of every protocol family
SharpVision implements, not only the optional values in `TerminalProtocol`. It
covers description databases, ECMA-48/ANSI/VT framing, CSI, OSC, DCS, DEC modes,
SGR, Xterm extensions and queries, keyboard protocols, mouse, focus, paste,
synchronized output, clipboard protocols, notification, graphics protocols,
tmux, and GNU screen.

Each row reports four independent dimensions:

1. SharpVision implementation and direction.
2. Detected support and evidence origin when the family has a capability.
3. Configured/effective runtime state or route decision where applicable.
4. Passive or explicit live verification.

The catalog validates itself against `TerminalCapabilities.Features`, so a new
optional capability cannot disappear from the tool silently.

## User interface

The retained screen has six views:

- **Overview**: connection description, canonical backend, fixed identity
  evidence, dimensions, color/Unicode policy, service support, and concise
  health summary.
- **Protocols**: the complete catalog with implementation, terminal evidence,
  active/blocked state, verification, and a detailed explanation.
- **Discovery**: every bounded query result, final negotiation state, and the
  evidence that refined the active capability profile.
- **Routing**: multiplexer layers, passthrough policy, visibility, approvals,
  bounds, outer profile, and effective per-operation routing decisions.
- **Input events**: the existing bounded decoded event timeline and details.
- **Probes**: explicit bell, title, notification, clipboard, rendition, Unicode,
  keyboard/mouse/focus/paste, and graphics checks.

Runtime diagnostics emitted through `Application.Diagnostic` and graphics
fallback diagnostics are copied into the same bounded event history. Sensitive
payloads remain redacted.

## tmux behavior

Environment detection alone remains conservative and never invents an outer
terminal. The debugger clearly shows that a detected tmux layer can be present
while passthrough is inactive. It diagnoses the inner tmux terminal by default.
Explicit outer routing remains a host policy and is displayed when configured;
the example does not silently authorize passthrough.

Manual verification runs the built debugger in a dedicated tmux session,
captures the rendered screen, drives tab/list navigation, observes decoded
input, and exits through Ctrl+Q. The proof must show non-empty capability,
backend, discovery, and route information from inside tmux.

## Tests and documentation

Terminal-layer value validation, immutability, identity preservation,
negotiation publication, route classification, mode classification, and
graphics-backend reporting are covered in `SharpVision.Terminal.Tests` and
`SharpVision.Tests`. Public API snapshots are reviewed and accepted for both
changed assemblies. Normative discovery, integration, protocol coverage, and
example documentation are updated with the diagnostic contract.

Completion requires focused red/green cycles, the compatibility gate,
`make format`, `make lint`, `make build`, `make test`, and the tmux exercise.
