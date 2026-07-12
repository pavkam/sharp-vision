# Terminal capabilities

## Capability contract

`Capabilities` is an immutable value published after bounded detection. It
states supported protocol features, color/style fidelity, Unicode-width policy,
cell/pixel metrics, and multiplexer constraints.

The Phase 2 profile represents each optional protocol as a `Feature` containing
`Support` and `Origin`. `Support` is unknown, unsupported, tentative, or
supported; only `Feature.IsSupported` authorizes active use. Consequently an
environment-derived tentative value is observable evidence but never silently
enables a feature. `Origin` records default, environment, query, or override
evidence. `ColorDepth` separately records monochrome, 16-color, indexed-256, or
true-color fidelity and its origin.

The profile reports `UnicodeVersion` as the library's pinned Unicode 17.0.0 data
and carries an explicit `AmbiguousWidth` policy. It defaults to narrow, never
changes from locale or terminal-name hints, and may be set to wide only by
caller `Settings` applied at the final precedence step.

## Precedence

1. Conservative built-in defaults establish safe behavior.
2. Environment hints (`TERM`, `COLORTERM`, terminal-specific variables, SSH,
   tmux, and GNU screen) narrow or tentatively identify features.
3. Safe query responses refine tentative values before the startup deadline.
4. Explicit caller overrides win and record their origin.

Environment names never prove every extension associated with a terminal.
Missing, late, malformed, duplicate, and contradictory query responses leave a
conservative value and emit structured diagnostics.

`Detector.Detect` reads only its caller-supplied dictionary, so tests and hosts
do not depend on process-global state. Kitty, xterm, and iTerm names contribute
tentative hints. tmux, GNU screen, and SSH presence may only narrow risky
features. Nullable `Queries` replace hints with query evidence, and nullable
`Settings` are applied last as explicit caller policy.

## Queries and publication

Queries use typed transactions defined by the
[device-attribute contract](../protocols/device-attributes.md#device-attribute-contract).
Each has a fake-clock-testable timeout. Publication creates a new immutable
profile; late replies may inform diagnostics or a later explicit refresh but do
not mutate values being used by a frame.

`QueryTracker` enforces `Limits.MaxConcurrentQueries`, permits one active
uncorrelated query per response family, and uses a sanitized identifier for
concurrent Kitty clipboard queries. It retains a bounded grace record after
completion, cancellation, or timeout so duplicate and late replies are
classified without reopening a transaction.

### Runtime negotiator

`Negotiator` snapshots caller-supplied environment values and emits one bounded
startup batch. DA1 is highest priority. Kitty keyboard status precedes DA1 only
when two query slots are available. Remaining slots query private modes 2026,
1004, 2004, 1006, and 1016 in that order.

All emitted queries share `Limits.QueryTimeout`. Validated responses may
complete negotiation early. At the deadline, absent replies remain absent query
evidence; they do not become unsupported values and therefore cannot erase
environment hints or explicit overrides.

The runtime publishes exactly one immutable startup profile before forwarding
the first resize. Explicit overrides are applied last. Matched, duplicate, late,
and unsolicited replies remain observable through runtime response events.

`Application` initializes its active `Capabilities` from static session options,
then applies a negotiated profile on the UI dispatcher before attaching the root
or processing the retained first resize. It raises `CapabilitiesChanged` after
the immutable reference changes and before invalidation. An ambiguous-width
change invalidates measure; every other profile change invalidates rendering.
Each frame captures one profile, so a refresh arriving during terminal output is
used only by the next frame and cannot alter an in-flight encoding.

The effective Unicode cell policy is an immutable derivative of the capability
profile. Root attachment gives that same policy reference to every control;
children added later inherit it before measurement. A geometry-affecting profile
change replaces the tree policy on the dispatcher and invalidates root measure
once. A nullable control-local ambiguous-width override, where an API offers
one, wins only for that control and remains explicit.

## Safe degradation

Feature fallback is deterministic: omit an unsupported visual attribute, reduce
color fidelity through the selected palette, use legacy input when enhanced
keyboard/mouse modes are absent, and return an unavailable result for operations
such as clipboard reads that lack a safe alternative. Strict mode promotes
selected diagnostics without changing valid encodings.
