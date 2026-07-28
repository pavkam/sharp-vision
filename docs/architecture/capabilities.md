# Terminal capabilities

## Capability contract

`Capabilities` is an immutable value published after bounded detection. It
states supported protocol features, color/style fidelity, Unicode-width policy,
cell/pixel metrics, and multiplexer constraints.

Capabilities authorize optional runtime behavior; they do not identify the
terminal emulator. The
[terminal backend contract](terminal-backends.md#terminal-backend-contract) owns
fixed VT/xterm/Kitty/iTerm2 identity, while the
[discovery pipeline](discovery-pipeline.md#discovery-pipeline-contract) owns
source precedence, adapters, resolution, and immutable publication.

The Phase 2 profile represents each optional protocol as a `Feature` containing
`Support` and `Origin`. `Support` is unknown, unsupported, tentative, or
supported. Active protocol output requires both `Feature.IsSupported` and an
authoritative database, bounded-query, or explicit-override origin. Default and
environment origins never authorize output, even if a caller constructs the
otherwise inconsistent `Supported` state. Environment evidence remains
observable but cannot silently enable a feature. `ColorDepth` separately records
monochrome, 16-color, indexed-256, or true-color fidelity and its origin.

The profile reports `UnicodeVersion` as the library's pinned Unicode 17.0.0 data
and carries an explicit `AmbiguousWidth` policy. It defaults to narrow, never
changes from locale or terminal-name hints, and may be set to wide only by
caller `Settings` applied at the final precedence step.

## Current terminal-description boundary

The terminal assembly has an internal ncurses provider which loads one requested
Unix terminfo/termcap description and a deterministic Windows VT provider which
builds the
[documented built-in profile](../protocols/ansi-vt.md#windows-vt-built-in-description).
`DescriptionLoader` gives an explicit profile precedence, selects the injected
Unix provider or established Windows VT provider, preserves typed absence and
failure, and performs only the explicitly permitted unavailable-Unix ANSI
fallback. Platform console connections retain immutable platform, output
descriptor, and Windows-VT facts. `ConsoleConnection.ResolveDescription`
publishes the immutable `DescriptionResult`: `DescriptionLoadStatus`, an
optional owned profile, and ordered redacted `DescriptionDiagnostic` values.
`ResolveProfile` remains a convenience projection that intentionally discards
status and diagnostics. Hosting consumes the complete result before application
or session construction and rejects every suitability other than `Usable` before
terminal/query/render bytes. Runtime options and capability negotiation carry
that complete profile and use its semantic capabilities as the baseline.
`Runtime.Options.Profile` is always non-null; its compatibility `Capabilities`
initializer wraps the exact semantic value in a built-in ANSI profile, with the
last initializer winning when low-level code supplies both. `Options.Minimal`
uses that usable profile but enables no mode or negotiation and remains
byte-quiet. `Runtime.Session` consumes matched lifecycle programs and routes the
profile key map into its input decoder. The renderer consumes description
programs. The [coverage matrix](../protocols/coverage-matrix.md#coverage) owns
that boundary.

## Terminal-description profile

Immutable `TerminalProfile` now owns `Description`, semantic `Capabilities`,
opaque compiled-program values, and the key map as immutable snapshots. The
ncurses provider loads a database and compiles programs. Session lifecycle
consumes the matched base and keypad pairs, and renderer routing consumes exact
cursor, erase, rendition, color, default, and cursor-shape programs. Input
routing consumes the same profile's described key map. Built-in ANSI programs
are intrinsic markers for operations already owned by the existing ANSI encoder,
not placeholder output bytecode.

`TerminalProfile.CreateAnsi` is a deliberate trusted compatibility boundary: it
retains the exact caller `Capabilities` instance and value, including explicit
database-origin features, because its required programs are library-owned
intrinsics rather than transplanted database claims. `WithCapabilities` keeps
that exact behavior only for profiles derived from this private ANSI singleton.
All general and database-loaded profile construction continues to normalize a
database support claim to unknown when its exact backing program is absent.

`KeyBinding` copies one non-empty terminal byte sequence without interpreting it
as text; invalid UTF-8 is retained exactly. `KeyMap` snapshots binding order,
compiles C0/DEL, Escape, CSI, and SS3 strings to typed structural signatures,
coalesces an identical repeated binding, and rejects exact-byte or equivalent-
signature conflicts. Seven-bit and eight-bit CSI/SS3 aliases share one
signature. Non-signature strings compile into a bounded longest-match trie owned
by the input decoder. Its lifecycle query recognizes exact seven- or eight-bit
SS3 cursor/Home/End spellings as application-mode requirements while excluding
SS3 F1–F4 normal spellings.

Key signature construction uses the parser limits captured for the active
profile. Native provider validation diagnoses one malformed optional key locally
and publishes every other valid binding; it does not turn an otherwise usable
terminal description into provider failure.

Eight-bit key signatures do not mutate the caller's parser limits. The decoder
tracks CSI and SS3 authorization separately and recognizes a described C1
introducer only from ground state with no pending UTF-8 scalar or legacy mouse
or SS3 payload. Explicit parser-wide C1 policy remains a separate caller choice.

A usable profile requires non-empty `cup` and `sgr0` programs plus either
non-empty `clear` or both `el` and `ed`; an otherwise usable description reports
`Suitability.Incomplete` when that requirement is not met. Database evidence
records `Description.Origin` as `DescriptionOrigin.Database`. A semantic feature
records supported `Origin.Database` evidence only when its exact non-empty
backing program is retained. Projection fills only the exact conservative
`Feature.Unknown` value; it preserves every other state or origin so later
environment, query, and explicit evidence keeps precedence. A supported database
claim transplanted without its exact backing program is normalized to
`Feature.Unknown` at the profile boundary.

Database focus support requires the complete `fe`, `fd`, `kxIN`, and `kxOUT`
set. Bracketed paste requires `BE`, `BD`, `PS`, and `PE`. Cell mouse requires
`XM` plus at least one compatible `kmous` or `xm` program; `kmous` alone never
authorizes active mouse mode. Provider projection and transplanted-claim
normalization apply these same exact sets.

The public two-argument `TerminalProfile(Description, Capabilities)` constructor
owns semantic values only and supplies no compiled programs or key bindings. It
therefore publishes an otherwise usable description as `Suitability.Incomplete`;
`CreateAnsi` supplies the intrinsic required programs and remains usable.

Final profile construction will apply built-in safety defaults, accepted
database evidence, environment narrowing and hints, bounded query results, then
explicit semantic settings or an explicit replacement `TerminalProfile`. The
[terminfo lookup contract](../protocols/terminfo.md#lookup-and-fallback) and the
loaded ncurses build own database selection and compatibility fallback.
Environment names MUST NOT replace database command programs. `Settings` may
override semantic features only; it MUST NOT carry raw command strings. A
complete explicit `TerminalProfile` replacement is the only explicit
command-program override.

An unsuitable database description is rejected before output rather than
weakened by an environment hint. A bounded query may refine a semantic feature
but MUST NOT rewrite a compiled database program.

Environment names never prove every extension associated with a terminal.
Missing, late, malformed, duplicate, and contradictory query responses leave a
conservative value and emit structured diagnostics.

`Detector.Detect` reads only its caller-supplied dictionary, so tests and hosts
do not depend on process-global state. It delegates the immutable baseline and
owned evidence snapshot through the ordered environment, query, and override
strategies specified by the
[discovery pipeline](discovery-pipeline.md#immutable-input-and-adapters). Kitty,
xterm, and iTerm names contribute tentative hints. tmux, GNU screen, and SSH
presence may only narrow risky features. Nullable `Queries` replace hints with
query evidence, and nullable `Settings` are applied last as explicit caller
policy.

### Multiplexer boundary

A detected `TMUX`, `TERM=tmux-*`, or `TERM=screen-*` value identifies only the
nearest inner multiplexer. It never identifies the outer terminal, never becomes
the terminal backend identity, and never enables passthrough.
`Multiplexing.Policy` keeps the active inner profile and an explicit outer
profile separate, owns a nearest-to-farthest route with finite depth and bytes,
and approves only typed capability-query, clipboard, or graphics families. The
current runtime connects capability queries, clipboard, and graphics through
their typed implementations. Graphics selection receives the complete detected
route even when passthrough is unauthorized, preventing a direct-output fallback
around multiplexer policy.

tmux may carry the complete approved query set. A route containing GNU screen
permits one farthest Screen layer and surrounding tmux layers, and carries CSI
queries only. OSC palette/default-color queries plus XTGETTCAP and DECRQSS are
omitted before registration because Screen's first ST ends its DCS relay. Unsafe
topologies and batches are rejected atomically rather than partially written.

An active query route uses the outer profile only as negotiation's semantic
baseline. It does not replace description programs or key strings used to drive
the inner multiplexer terminal, and inner environment names do not narrow or
augment that explicit outer evidence. Replies are unwrapped through every
configured layer before ordinary typed parsing and exact `QueryTracker`
correlation. Unwrapping admits exactly one recognized query-response value;
text, input controls, unrecognized strings, trailing bytes, and concatenated
responses reject the complete envelope. A structurally valid wrong-identity
reply remains observable for correlation diagnostics. Screen-wrapped CSI is
accepted; string-terminated Screen envelopes recover through the full outer
boundary without leaking control bytes. Raw diagnostic offsets include accepted
wrapper overhead and every byte of rejected or oversized envelopes. An atomic
outbound encoding failure retires the registered batch and publishes absent
evidence immediately, without bytes, a flush, active modes, or a deadline.
Disabled or visibility-ineligible passthrough, missing explicit outer evidence,
malformed or oversized envelopes, and the shared exclusive timeout leave the
inner profile conservatively narrowed.

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
classified without reopening a transaction. DECRQSS and XTGETTCAP registration
retains an exact typed selector or name in active and history keys. A wrong or
identity-less response cannot retire another request. Their typed responses
preserve only approved bounded data; unknown valid status replies are
diagnostic, and returned capability bytes never become executable description
programs.

### Runtime negotiator

`Negotiator` is the compatibility facade over `ActiveQueryDiscoveryStrategy`.
The strategy snapshots caller-supplied environment values and emits one bounded
startup batch. DA1 is highest priority. Kitty keyboard status precedes DA1 only
when it remains unknown and two query slots are available. DA2 follows DA1 when
capacity permits. Remaining slots refine only unknown or tentative private modes
2026, 1004, 2004, 1006, and 1016, then query missing geometry and OSC
palette/default colors. Definitive database evidence and explicit overrides
suppress redundant feature probes. The
[active-query architecture](discovery-pipeline.md#active-query-strategy) owns
the facade, deadline, correlation, classification, and publication boundaries;
this section owns the capability-specific query order.

When `TERM` is an xterm hint but not a Kitty hint, remaining slots append the
finite XTGETTCAP `RGB` refinement followed by DECRQSS modifyOtherKeys status.
The status may publish query-origin `XtermKeyboard` support. RGB can refine only
default or environment-only color evidence; database, prior query, and override
origins remain authoritative. Explicit `Settings.ColorDepth` prevents the RGB
query from registering or writing, preserving its capacity slot. `Session`
prefers proven Kitty keyboard support; otherwise it leases the configured xterm
level and restores xterm's initial resource value during reverse cleanup.

Synchronous host dimensions are the highest-confidence geometry evidence.
`TIOCGWINSZ` cells and pixels suppress the corresponding window queries before
any batch bytes are emitted. A validated terminal metrics reply remains
observable and may refine only pointer bytes decoded after that reply; it never
reinterprets an already delivered value or overrides complete local geometry.
OSC 10/11 defaults remain owned query evidence for diagnostics or explicit
application theme adaptation; they never mutate semantic theme colors.

The active strategy's
[deadline and response lifecycle](discovery-pipeline.md#active-query-strategy)
owns timing, correlation, classification, and completion. The
[initialization sequence](discovery-pipeline.md#initialization-sequence) owns
startup publication relative to resize and optional modes, while
[runtime routing](../protocols/runtime-routing.md#inbound-consumption-surface)
owns typed response-event delivery. Absent replies remain absent query evidence;
they do not become unsupported values and therefore cannot erase environment
hints or explicit overrides.

`Application` publishes its active `TerminalProfile` and semantic `Capabilities`
from static session options, then applies negotiated capabilities by creating a
new profile which retains the description, programs, and key map. It does so on
the UI dispatcher before attaching the root or processing the retained first
resize. It raises `CapabilitiesChanged` after the immutable reference changes
and before invalidation. An ambiguous-width change invalidates measure; every
other profile change invalidates rendering. Every refresh invalidates the
renderer completely. Each frame captures one profile, so a refresh arriving
during terminal output is used only by the next frame and cannot alter an
in-flight encoding.

The effective Unicode cell policy is an immutable derivative of the capability
profile. Root attachment gives that same policy reference to every control;
children added later inherit it before measurement. A geometry-affecting profile
change replaces the tree policy on the dispatcher and invalidates root measure
once. A nullable control-local ambiguous-width override, where an API offers
one, wins only for that control and remains explicit.

Exact `Dimensions.CellMetrics` is a separate dispatcher-owned inherited context.
Application updates it before each resize layout, and a child added later
inherits the current value before measurement. Uniform and exact uneven grids
provide a bounded pixel-to-cell inverse that counts a partial final cell. The
same immutable metrics snapshot is passed to graphics rendering for that frame;
missing metrics conservatively preserve cell fallback for protocols that require
pixel geometry.

### Output projection

`Renderer` passes the exact immutable profile captured for a frame to `Encoder`.
Resolved semantic frame colors remain RGB. The encoder projects only the emitted
presentation: true color preserves RGB, indexed 256 privately selects the
nearest xterm-compatible reference position, basic 16 uses typed ANSI/aixterm
SGR, and monochrome emits no color selection. Database true color requires
complete `setrgbf`/`setrgbb` and indexed color requires complete
`setaf`/`setab`; an incomplete directional pair lowers to the next complete
tier. A capability change forces the existing full-redraw path, so one frame
cannot mix color tiers.

The reference palette is a deterministic degradation policy, not a claim about
physical terminal colors. Terminals may configure their first sixteen entries.
Equal-distance projection selects the lower palette index, and projected style
equality suppresses redundant transitions.

Styled underlines, independent underline color, and overline are separate
optional features. Terminal-name hints may mark them tentative for diagnostics,
but only query evidence or an explicit nullable `Settings` override marks them
supported. A supported styled-underline feature permits `4:1` through `4:5`;
otherwise every typed variant degrades to legacy SGR 4. Unsupported underline
color and overline are omitted. For database profiles every decoration requires
its exact retained program; an absent program omits the decoration. The encoder
compares these projected decoration fields alongside projected colors before
deciding whether a transition is redundant.

## Safe degradation

Feature fallback is deterministic: omit an unsupported visual attribute, reduce
color fidelity through the selected palette, use legacy input when enhanced
keyboard/mouse modes are absent, and return an unavailable result for operations
such as clipboard reads that lack a safe alternative. Strict mode promotes
selected diagnostics without changing valid encodings.

## Test obligations

| Layer       | Required evidence                                                                                            |
| ----------- | ------------------------------------------------------------------------------------------------------------ |
| Unit        | Defaults, feature/origin mapping, description projection, overrides, validation, and deterministic fallback. |
| Integration | Description, environment, query, multiplexer, publication, encoder projection, and invalidation.             |
| End to end  | Unsupported features remain byte-quiet while proven features emit exact authorized bytes.                    |
