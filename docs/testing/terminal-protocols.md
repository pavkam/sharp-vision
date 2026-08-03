# Terminal protocol testing

## Overview

Each typed encoder has exact-byte tests for its default, minimum, maximum,
combined, and rejected parameter values. Each streaming decoder runs a
representative input once whole and once for every possible split point, then
again with adjacent text and controls.

`ProtocolRouter` tests apply the same whole-versus-every-split rule to each
typed reply and to OSC, DCS, APC, PM, and SOS. They overwrite the source read
after dispatch to prove owned payloads retain no transport memory, and they
follow hostile bounded strings with known input to prove the outer parser
recovers. Multiplexer tests additionally require exactly one typed reply per
envelope, reject trailing input and concatenated replies at every split, and
verify raw diagnostic offsets across accepted nested framing, rejected
candidates, oversized discard, and post-recovery malformed input. Runtime
coverage proves an atomic outbound route failure publishes immediately with no
write, flush, pending correlation, optional mode, or deadline advancement.

## Backend and discovery architecture

Backend tests prove the exact VT-to-xterm-to-Kitty/iTerm2 inheritance graph,
inherited-before-local extension order, immutable composition, duplicate
rejection, and deterministic resolver specificity. They distinguish optional
sixel evidence from emulator identity and prove that tmux and GNU screen adapt
routes without becoming backends themselves. Capability-refinement tests retain
the exact backend reference for the complete application lifetime.

Discovery adapter tests prove owned immutable input, absence behavior,
source-specific validation, case-insensitive known-environment lookup, and
redaction of raw descriptions, environment values, and terminal payloads.
Pipeline tests supply strategies out of order and freeze the environment, query,
and override execution; they reject null, missing, duplicate, and undefined
phases. Description evidence is tested as the immutable baseline established
before those three strategy phases run.

`Negotiator` facade parity tests compare against direct
`ActiveQueryDiscoveryStrategy` behavior for exact query bytes, started/completed
status, the shared exclusive deadline, reply classification, diagnostics,
published query results, capabilities, and public validation.
`CapabilityDetector` facade tests likewise compare against the direct discovery
pipeline. Moving behavior behind a facade must not reduce fragmentation,
capacity, timeout, multiplexer, or allocation coverage.

One cross-layer initialization test drives a usable terminal description, caller
environment, correlated query replies, resolved terminal backend, capability
publication, authorized optional-mode startup, router delivery, first renderer
output, and reverse cleanup. It proves one `TerminalContext` lineage, no
repeated identity resolution, exact query and lifecycle bytes, and single
disposal of the transport, resize source, graphics backend, and platform lease.
The
[terminal backend contract](../architecture/terminal-backends.md#expected-behavior)
and
[discovery contract](../architecture/discovery-pipeline.md#expected-behavior)
own the architecture-specific assertions.

## Parser matrix

The matrix covers empty input, byte-at-a-time input, multiple frames per read,
split ESC/ST, invalid UTF-8, missing, empty, and default parameters, numeric
overflow, excess parameters and intermediates, unknown valid sequences, CAN/SUB,
oversized strings, cancellation, end-of-stream truncation, and recovery into a
known next event.

Transaction protocols additionally cover correlation, duplicate and late
replies, invalid state order, timeouts driven by a fake clock, concurrency
limits, permission errors, cancellation, and payload redaction.

Kitty graphics tests freeze the explicit three-zero official query, validated
RGBA and PNG uploads, 4,096-byte Base64 chunk boundaries, continuation metadata,
source and destination placement geometry, stable IDs, soft and hard deletion,
and cursor restoration. Checked pre-encoded input proves exact canonical decode
and re-encode, action and payload-shape validation, rejection atomicity, and the
absence of a public validation bypass. RGB transmission and zlib are rejected.

Successful and printable-error APC replies, malformed duplicate-field recovery,
and enabled 8-bit framing each run at every transport split. Canonical unsigned
IDs reject leading zeroes, signs, zero, and overflow without consuming an active
query; configured bounds, one-character errors, duplicate correlation, and
redaction remain covered. Real backend-to-renderer tests prove byte-quiet
prepare and commit, bounded uncertain-ID recovery, exact cleanup before any ID
reuse, an allocation-free cleanup commit, and immediate shutdown after partial
image or placement output. Multi-chunk tmux routing wraps every APC and every
shutdown delete independently while CUP stays pane-local; Screen is rejected.
Explicit renderer shutdown is also driven through success, idempotency,
concurrency, cancellation, write failure, flush failure, and no-backend silence.

Application-level graphics tests additionally freeze lazy selection after the
profile/resize barrier, exact live metrics, no-leak routing for the unauthorized
layer, Kitty delete and flush before borrowed transport disposal, and
failure-safe cleanup. The consumer path uses the public Image control and
asserts that ordinary fallback bytes precede the selected graphics bytes.

A paused-flush Kitty case publishes profile revocation after the prepared batch
has been written but before the flush completes. It requires the in-flight
transaction to commit, the immediately following frame to remove the retained
graphics, and a normal shutdown. Public PNG coverage requires fallback bytes
before the explicit iTerm2 3.5 OSC 1337 multipart bytes. A three-layer public
control case makes the upper placement ineffective with later cell paint and
requires backward, transitive blocking to suppress the overlapping lower Kitty
placement.

Sixel tests freeze the transparent-background DCS parameters, raster attributes,
sorted dense palette definitions, six-row bit order, repeat compression,
graphics carriage return and newline, source clipping, scaling, and the
canonical ST. The indexed raster reports exactly one sample per output pixel; a
large 216-color plane bound and checked overflow prove finite preflight without
a timing oracle. PNG and Screen are rejected, destination exceptions are
preserved, and an output-policy failure is atomic. DA1 parameter 4 runs through
the real router at every split, with negative evidence and explicit true and
false override precedence. Real renderer tests prove exact and uneven metric
changes, missing-metric fallback, movement, removal, unsupported replacement,
intersecting cell damage, complete stale-cell repair before repaint, pane-local
cursor restoration, independently routed tmux DCS, byte-quiet cleanup, and
failure invalidation followed by full reconstruction.

iTerm2 tests freeze the 3.5 multipart metadata order, exact PNG size, cell
dimensions, the preserve-aspect flag, the inline-only policy, the omitted name,
Base64 parts, FileEnd, and the canonical ST. Every complete direct and
nested-tmux OSC is bounded using exact framing and ESC-doubling math; a payload
crossing the default routed boundary reconstructs byte for byte while every
outer envelope stays within policy. RGBA, cover, clipped source, Screen, BEL
output, legacy `File`, and destination or policy partial writes are rejected.
The generic router accepts OSC 1337 with ST and BEL at every split and recovers
the following text after overflow. Shared non-retained backend tests prove the
original mixed RGBA/PNG paint order, stale-cell repair, intersecting-only
repaint, cursor restoration, allocation-free synchronous phases, byte-quiet
cleanup, and a full retry after transport failure. Selection tests freeze Kitty
priority, query and override sixel, override-only iTerm2 3.5+, tentative and
database rejection, route authorization, and PNG viability without sixel
metrics.

The implemented terminfo parameter-program suite uses raw-byte fixtures taken
from current xterm, xterm-direct, screen, tmux, and Kitty-family descriptions,
with exact current source values for xterm, xterm-direct, screen, tmux, Kitty,
and Kitty direct-color inheritance. It covers every supported directive and
`printf` form, nested and chained else-if conditionals, dynamic and static
variable lifetime and ownership, malformed stack use, padding rejection, divide
and modulo by zero, configured defaults and hard ceilings, and destination
atomicity. Static string assignment tests cover immutable snapshot aliasing,
repeated assignment, caller mutation, and failed evaluation. Warmed numeric
`cup` and `setaf` expansion must allocate zero managed bytes per operation and
stay within the relative CSI-write budget.

Capability-negotiation tests assert the complete startup batch byte for byte at
every supported query capacity. They begin with database and override evidence,
prove DA2 follows DA1, and prove complete local `TIOCGWINSZ` geometry suppresses
the 14/16/18 queries. They deliver DA, mode, OSC 4/10/11, and window and cell
responses out of order and across every split, advance an injected clock to the
shared exclusive deadline, and test every family one tick before, exactly at,
and one tick after it. Registration tests move the clock between families to
prove the absolute deadline cannot skew, and they prove unanswered queries
preserve tentative hints. OSC 4 tests deliver an unsolicited nonzero index
before the requested zero index and at the deadline to prove exact correlation
without transaction consumption. Oversized decimal indices run across every
split and must recover the following typed reply. Runtime tests deliver input
and resize before publication, then prove the profile, optional-mode, resize,
layout, typed-response-event, and first-frame ordering through the real session
and application path. Late queried metrics never rewrite an already delivered
pointer value.

DECRQSS tests freeze every approved selector and representative `1$r`/`0$r`
replies, repeat a status reply at every split, retain an unknown valid result as
a typed value plus a diagnostic, reject selector spoofs and malformed returned
CSI, and recover adjacent text at every split. Public construction tests reject
contradictory status combinations. XTGETTCAP tests freeze the uppercase request
hex, repeat multi-item and `0+r` replies at every split, and reject unknown
names, duplicates, trailing separators, odd or non-hex input,
exact-limit-plus-one items, and exact-limit-plus-one value bytes. Query tracking
proves exact identity, wrong-name, identity-less failure, matched, duplicate,
late, and grace-expiration behavior for both DCS families. Multiplexer tests
freeze single and safely mixed nested tmux and screen envelopes, rejected
Screen-before-tmux and duplicate-Screen topologies, repeated tmux ESC doubling,
disabled/visible/all behavior, explicit outer-profile separation, depth and byte
overflow, malformed recovery, and every fragmentation of a routed reply. Screen
batches freeze the exact CSI-only output and prove OSC 4/10/11, XTGETTCAP, and
DECRQSS are not registered, consume no capacity, and cannot delay completion
once all CSI replies arrive. Complete and oversized fabricated Screen DCS
replies produce one redacted diagnostic and no stray key, text, typed response,
or raw sequence at every split. Runtime oracles send routed DA1 through real
sessions and prove unwrapping precedes originating-query correlation and profile
publication for tmux and Screen. A fake clock proves unanswered routed queries
retain conservative evidence at the original exclusive deadline. Installed tmux
and GNU screen executables run under a real `script`-owned pseudoterminal;
Screen evidence freezes the exact CSI relay and the missing XTGETTCAP/DECRQSS ST
bytes. Their absence is an explicit platform skip. Enhanced-key tests cover
exact query, set, and restore bytes, legacy `CSI 27;modifier;key~`, compatible
CSI-u, malformed recovery, Kitty precedence, and session reverse cleanup.

Color-precedence tests begin with the default, `TERM=*-256color`, and
`COLORTERM` heuristics, then prove a validated `RGB=24` publishes true color
with query origin. Negative and non-RGB values preserve the heuristics, and
database, prior-query, and override origins remain unchanged. Capacity tests
prove an explicit `Settings.ColorDepth` emits no RGB request, registers no
pending RGB family, and does not consume the next bounded query slot.

Terminal-description provider tests use process-isolated fixture terminfo
directories and termcap files rather than mutating the test-runner environment.
The implemented probe proves `TERMINFO`, ordered `TERMINFO_DIRS`, and inline
`TERMCAP`, a `TERMCAP` file, and ordered `TERMPATH` when the native build
supports them, all through the one `setupterm` path. The remaining
host-selection tests must prove the `$HOME/.terminfo`, configured directory,
`TERMCAP`, `TERMPATH`, `/etc/termcap`, `/usr/share/misc/termcap`, and
`$HOME/.termcap` source order through the single
[lookup and fallback contract](../protocols/terminfo.md#lookup-and-fallback).
They cover every retained `DescriptionLimits` and `ProgramLimits` default and
hard ceiling, provider failures, generic and hardcopy entries,
required-capability omissions, and one-sided requested cursor or
alternate-screen pairs before a frame writes bytes. Native lookup stays outside
the adversarial parser guarantee described by the
[provider trust boundary](../protocols/terminfo.md#native-provider-trust-boundary):
tests prove retained snapshot bounds and the explicit-`TerminalProfile`
native-discovery bypass, not an impossible native deadline.

Fixtures assert the exact
[terminfo identifier allowlist](../protocols/terminfo.md#finite-capability-boundary),
that all other names are ignored, and that database programs — not environment
names — determine legacy encoded commands. They cover numeric `U8`, the one
Boolean `XF`, and every accepted `RGB` form plus malformed, overflow, zero,
extra-field, and `colors`-inconsistent precision rejection. Termcap fixtures
prove the required raw-to-canonical mappings, no guessed optional aliases,
inline 1023-byte rejection, provider failure, and that absent extended values
never become support claims.

Windows VT profile tests use Microsoft's current Console VT sequence tables as
the independent oracle. They assert every retained command's exact compiled
source bytes, the matched cursor/keypad and restoration pair, the fixed input
map, guaranteed 16-color fidelity, and the absence of unsupported rendition and
optional-protocol claims. Exact accepted-snapshot boundary tests reject the
fixed profile one byte below its deterministic size and accept that exact size.
Loader tests prove explicit-profile bypass, Unix and Windows provider selection,
established-VT gating, contradictory platform-fact rejection, diagnostic
retention, provider failure, and that missing-or-generic or
accepted-but-unsuitable results are never replaced by the opt-in ANSI fallback.
Only Unix provider unavailability may use that explicit fallback.

Hosting preflight tests inject only the interactive, open, and plain-message
host boundaries. They prove every non-usable suitability and ordinary
description absence constructs no `Application` or `Session`, writes no mode,
query, or renderer bytes, disposes resize, transport, and platform resources
once in lifecycle order, and maps only the typed preflight rejection to
`ConsoleRunStatus.UnsupportedTerminal`. Direct public `Application` constructor
tests prove every non-usable profile is rejected before root mutation,
dispatcher creation, or resource ownership. Session tests independently retain
the same guard, prove `Options.Minimal` is usable and byte-quiet, and prove
negotiation starts from `Profile.Capabilities`. Description-lifecycle tests use
noncanonical exact programs for `smcup`/`rmcup`, `civis`/`cnorm`, and
`smkx`/`rmkx`. They prove complete-pair gating, zero-parameter and output
bounds, SS3 cursor versus SS3 F1–F4 keypad selection, session-scoped static
variables with pair rollback, partial writes, failed flushes, cancellation,
reverse cleanup, continued cleanup failure, and original-exception preservation.
The authorization matrix proves database, bounded-query, and explicit-override
origins can emit typed optional modes while default and environment origins
cannot, for both built-in and explicit profiles. The built-in ANSI compatibility
path separately freezes its typed intrinsic bytes. The cross-layer suite is
compiled in `SharpVision.Tests`, which receives test-only access to Terminal
description internals and already owns the higher-layer hosting integration. It
injects `IDescriptionProvider` results through `DescriptionLoader` and
`ConsoleConnection.ResolveDescription`, then drives the real builder. Separate
TERM=dumb generic, ordinary missing, and provider-failure cases prove
`Build`/`RunAsync`, ordered diagnostic retention, zero terminal writes, no
attachment, and exact ownership cleanup. Resize, transport, and platform-restore
failures are injected independently to prove rejection remains primary while
every resource receives one ordered disposal attempt. Public consumer tests
separately compile the parameterless terminal-options overload, an original
`IProtocolSink` implementation with no typed color or metrics callbacks, public
response construction and empty-sentinel rejection, and every explicit response
and query enum ordinal. The original sink must receive routed OSC 10/11 at every
split as legacy red, green, and blue values, indexed OSC 4 as an index plus
normalized RGB, and every metrics family as width then height. Status and color
tests freeze their enum values and override provenance.

## Independent oracles

Use primary-standard byte examples, a small parser state reference, encode and
decode round trips where the form is canonical, and invariants such as "whole
input equals every fragmentation." Never generate expected bytes with the
production encoder.

Randomized parser invariants use fixed seeds so failures reproduce exactly.
Every generated valid sequence must produce the same events at every
fragmentation, while hostile generated input must recover to a known trailing
CSI event. Failure messages include the seed, case, and input bytes.

The typed input decoder has a separate fixed-seed hostile suite. Random bytes
arrive in random 1–8 byte fragments under small paste and parser limits,
followed by explicit paste termination or cancellation and a known text key.
Every case must terminate, retain no oversized paste, and recover the known key;
failures print the seed, case, and hexadecimal input.

Description-key tests enumerate `kf1` through `kf63`, all six keypad-position
names, and all seventy allowlisted extended modified-key names. They prove exact
seven-bit and eight-bit CSI and SS3 signature conflicts, and terminals whose
database strings deliberately differ from ANSI grammar. Representative Control,
Escape, CSI, and SS3 signatures in both accepted widths, plus overlapping
invalid-UTF-8 trie entries, run at every split. A fixed-seed suite randomizes
the active description bindings and 1–8-byte fragmentation around a typed query
reply and known trailing text, printing the seed, case, and bytes on failure.
Runtime proof passes the profile map through `Session` and `ProtocolRouter` to
the final typed stroke.

With both eight-bit CSI and SS3 maps active, valid UTF-8 scalars whose
continuation byte is `0x8F` or `0x9B` run at every split and remain exact
Unicode text, with no key and no diagnostic. Separate cases prove standalone
described C1 keys, unmatched recovery, and caller-enabled parser-wide C1
behavior.

Fallback-key tests prove a pending UTF-8 scalar wins over a new
continuation-leading binding while an established matcher prefix retains
ownership. Overlapping shorter and longer bindings rematch a described suffix at
every split. Fixed-seed cases randomize continuation bindings, suffixes,
adjacent key counts, and fragmentation, then compare the exact later diagnostic
offset. Warmed signature and fallback-rematch paths allocate zero managed bytes.
Lifecycle cases dispose both a retained prefix and a decoder after suffix
rematching, then prove matcher bindings, trie arrays, pending bytes, and the
replay workspace are released, repeated disposal is harmless, and decoder
operations continue to reject post-disposal use.

Provider fixtures admit parameter and intermediate signatures at the exact
active limit, reject limit-plus-one, empty, and incomplete introducers with
`InvalidKey`, and retain another valid optional key in the same usable profile.

The warmed CSI parser path is measured at zero managed bytes per event. A
hostile 2 MiB oversized-OSC case proves retention and allocation stay bounded
and that the next valid sequence is still decoded.

## Integration

At least one test per implemented protocol family traverses the typed command,
encoder, transport fake, streaming decoder, typed response or event, and
terminal lifecycle cleanup. Clipboard tests include arbitrary binary MIME data
and the final encoded bytes, not merely command construction.

## Required evidence

| Layer             | Required observation                                                                 |
| ----------------- | ------------------------------------------------------------------------------------ |
| Encoder           | Exact bytes for every typed value, boundary, and supported form.                     |
| Streaming decoder | Every split, malformed/unknown/oversized recovery, offsets, and bounded state.       |
| Router            | Typed owner, reply correlation, redaction, ordering, and ordinary input coexistence. |
| Lifecycle         | Mode pairs, multiplexer framing, transport failure, timeout, and reverse cleanup.    |

At least one implemented-family scenario traverses a typed command to its final
bytes and a typed response back to its runtime consumer.
