# Terminal capabilities

## Capability contract

`Capabilities` is an immutable value published after bounded detection. It
states supported protocol features, color/style fidelity, Unicode-width policy,
cell/pixel metrics, and multiplexer constraints.

## Precedence

1. Conservative built-in defaults establish safe behavior.
2. Environment hints (`TERM`, `COLORTERM`, terminal-specific variables, SSH,
   tmux, and GNU screen) narrow or tentatively identify features.
3. Safe query responses refine tentative values before the startup deadline.
4. Explicit caller overrides win and record their origin.

Environment names never prove every extension associated with a terminal.
Missing, late, malformed, duplicate, and contradictory query responses leave a
conservative value and emit structured diagnostics.

## Queries and publication

Queries use typed transactions defined by the
[device-attribute contract](../protocols/device-attributes.md#device-attribute-contract).
Each has a fake-clock-testable timeout. Publication creates a new immutable
profile; late replies may inform diagnostics or a later explicit refresh but do
not mutate values being used by a frame.

## Safe degradation

Feature fallback is deterministic: omit an unsupported visual attribute, reduce
color fidelity through the selected palette, use legacy input when enhanced
keyboard/mouse modes are absent, and return an unavailable result for operations
such as clipboard reads that lack a safe alternative. Strict mode promotes
selected diagnostics without changing valid encodings.
