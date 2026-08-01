# Terminal protocol testing

## Overview

Each typed encoder has exact-byte tests for default, minimum, maximum, combined,
and rejected parameter values. Each streaming decoder representative runs once
whole and once for every possible split point, then with adjacent text and
controls.

`ProtocolRouter` tests apply the same whole-versus-every-split rule to each
typed reply and to OSC, DCS, APC, PM, and SOS. They overwrite the source read
after dispatch to prove owned payloads do not retain transport memory, and they
follow hostile bounded strings with known input to prove outer-parser recovery.
Multiplexer tests additionally require exactly one typed reply per envelope,
reject trailing input and concatenated replies at every split, and verify raw
diagnostic offsets across accepted nested framing, rejected candidates,
oversized discard, and post-recovery malformed input. Runtime coverage proves
atomic outbound route failure publishes immediately with no write, flush,
pending correlation, optional mode, or deadline advancement.

## Backend and discovery architecture

Backend tests prove the exact VT to xterm to Kitty/iTerm2 inheritance graph,
inherited-before-local extension order, immutable composition, duplicate
rejection, and deterministic resolver specificity. They distinguish optional
sixel evidence from emulator identity and prove that tmux and GNU screen adapt
routes without becoming backends. Capability-refinement tests retain the exact
backend reference for the complete application lifetime.

Discovery adapter tests prove owned immutable input, absence behavior,
source-specific validation, case-insensitive known environment lookup, and
redaction of raw descriptions, environment values, and terminal payloads.
Pipeline tests supply strategies out of order and freeze environment, query, and
override execution; they reject null, missing, duplicate, and undefined phases.
Description evidence is tested as the immutable baseline established before
those three strategy phases.

`Negotiator` facade parity tests compare direct `ActiveQueryDiscoveryStrategy`
behavior for exact query bytes, started/completed status, shared exclusive
deadline, reply classification, diagnostics, published query results,
capabilities, and public validation. `Detector` facade tests likewise compare
the direct discovery pipeline. Moving behavior behind a facade MUST NOT reduce
fragmentation, capacity, timeout, multiplexer, or allocation coverage.

One cross-layer initialization test drives a usable terminal description, caller
environment, correlated query replies, resolved terminal backend, capability
publication, authorized optional-mode startup, router delivery, first renderer
output, and reverse cleanup. It proves one `TerminalContext` lineage, no
repeated identity resolution, exact query and lifecycle bytes, and single
disposal of transport, resize source, graphics backend, and platform lease. The
[terminal backend contract](../architecture/terminal-backends.md#expected-behavior)
and
[discovery contract](../architecture/discovery-pipeline.md#expected-behavior)
own the architecture-specific assertions.

## Parser matrix

Cover empty input, byte-at-a-time input, multiple frames per read, split ESC/ST,
invalid UTF-8, missing/empty/default parameters, numeric overflow, excess
parameters/intermediates, unknown valid sequences, CAN/SUB, oversized strings,
cancellation, end-of-stream truncation, and recovery into a known next event.

Transaction protocols additionally cover correlation, duplicate/late replies,
invalid state order, timeouts with a fake clock, concurrency limits, permission
errors, cancellation, and payload redaction.

Kitty graphics tests freeze the explicit three-zero official query, validated
RGBA and PNG uploads, 4,096-byte Base64 chunk boundaries, continuation metadata,
source/destination placement geometry, stable IDs, soft and hard deletion, and
cursor restoration. Checked pre-encoded input proves exact canonical
decode/re-encode, action and payload-shape validation, rejection atomicity, and
the absence of a public validation bypass. RGB transmission and zlib are
rejected.

Successful and printable-error APC replies, malformed duplicate-field recovery,
and enabled 8-bit framing each run at every transport split. Canonical unsigned
IDs reject leading zeroes, signs, zero, and overflow without consuming an active
query; configured bounds, one-character errors, duplicate correlation, and
redaction remain covered. Real backend-to-renderer tests prove byte-quiet
prepare/commit, bounded uncertain-ID recovery, exact cleanup before ID reuse,
allocation-free cleanup commit, and immediate shutdown after partial image or
placement output. Multi-chunk tmux routing wraps every APC and every shutdown
delete independently while CUP remains pane-local; Screen is rejected. Explicit
renderer shutdown is also driven through success, idempotency, concurrency,
cancellation, write failure, flush failure, and no-backend silence.

Application-level graphics tests additionally freeze lazy selection after the
profile/resize barrier, exact live metrics, unauthorized-layer no-leak routing,
Kitty delete/flush before borrowed transport disposal, and failure-safe cleanup.
The consumer path uses the public Image control and asserts ordinary fallback
bytes precede selected graphics bytes.

A paused-flush Kitty case publishes profile revocation after the prepared batch
is written but before flush completes. It requires the in-flight transaction to
commit, the immediately following frame to remove retained graphics, and normal
shutdown. Public PNG coverage requires fallback bytes before explicit iTerm2 3.5
OSC 1337 multipart bytes. A three-layer public control case makes the upper
placement ineffective with later cell paint and requires backward/transitive
blocking to suppress the overlapping lower Kitty placement.

Sixel tests freeze transparent-background DCS parameters, raster attributes,
sorted dense palette definitions, six-row bit order, repeat compression,
graphics carriage return/newline, source clipping, scaling, and canonical ST.
The indexed raster reports exactly one sample per output pixel; a large
216-color plane bound and checked overflow prove finite preflight without a
timing oracle. PNG and Screen are rejected, destination exceptions are
preserved, and output-policy failure is atomic. DA1 parameter 4 runs through the
real router at every split, with negative evidence and explicit true/false
override precedence. Real renderer tests prove exact and uneven metric changes,
missing-metric fallback, movement, removal, unsupported replacement,
intersecting cell damage, complete stale-cell repair before repaint, pane-local
cursor restoration, independently routed tmux DCS, byte-quiet cleanup, and
failure invalidation followed by full reconstruction.

iTerm2 tests freeze the 3.5 multipart metadata order, exact PNG size, cell
dimensions, preserve-aspect flag, inline-only policy, omitted name, Base64
parts, FileEnd, and canonical ST. Every complete direct and nested-tmux OSC is
bounded using exact framing and ESC-doubling math; a payload crossing the
default routed boundary reconstructs byte-for-byte while every outer envelope
remains within policy. RGBA, cover, clipped source, Screen, BEL output, legacy
`File`, and destination/policy partial writes are rejected. The generic router
accepts OSC 1337 ST and BEL at every split and recovers following text after
overflow. Shared non-retained backend tests prove original mixed RGBA/PNG paint
order, stale-cell repair, intersecting-only repaint, cursor restoration,
allocation-free synchronous phases, byte-quiet cleanup, and full retry after
transport failure. Selection tests freeze Kitty priority, query/override sixel,
override-only iTerm2 3.5+, tentative/database rejection, route authorization,
and PNG viability without sixel metrics.

The implemented terminfo parameter-program suite uses raw-byte fixtures from
current xterm, xterm-direct, screen, tmux, and Kitty-family descriptions. It
uses exact current source values for xterm, xterm-direct, screen, tmux, Kitty,
and Kitty direct-color inheritance. It covers every supported directive and
`printf` form, nested and chained else-if conditionals, dynamic/static variable
lifetime and ownership, malformed stack use, padding rejection, divide/modulo by
zero, configured defaults and hard ceilings, and destination atomicity. Static
string assignment tests cover immutable snapshot aliasing, repeated assignment,
caller mutation, and failed evaluation. Warmed numeric `cup` and `setaf`
expansion must allocate zero managed bytes per operation and remain within the
relative CSI-write budget.

Capability-negotiation tests assert the complete startup batch byte for byte at
every supported query capacity. They begin with database and override evidence,
prove DA2 follows DA1, and prove complete local `TIOCGWINSZ` geometry suppresses
14/16/18 queries. They deliver DA, mode, OSC 4/10/11, and window/cell responses
out of order and across every split, advance an injected clock to the shared
exclusive deadline, and test every family one tick before, exactly at, and one
tick after it. Registration tests move the clock between families to prove the
absolute deadline cannot skew. They prove that unanswered queries preserve
tentative hints. OSC 4 tests deliver an unsolicited nonzero index before the
requested zero index and at the deadline to prove exact correlation without
transaction consumption. Oversized decimal indices run across every split and
must recover the following typed reply. Runtime tests deliver input and resize
before publication, then prove profile, optional-mode, resize, layout,
typed-response-event, and first-frame ordering through the real session and
application path. Late queried metrics never rewrite an already delivered
pointer value.

DECRQSS tests freeze every approved selector and representative `1$r`/`0$r`
reply, repeat a status reply at every split, retain an unknown valid result as a
typed value plus diagnostic, reject selector spoofs and malformed returned CSI,
and recover adjacent text at every split. Public construction tests reject
contradictory status combinations. XTGETTCAP tests freeze uppercase request hex,
repeat multi-item and `0+r` replies at every split, and reject unknown names,
duplicates, trailing separators, odd/non-hex input, exact-limit-plus-one items,
and exact-limit-plus-one value bytes. Query tracking proves exact identity,
wrong-name, identity-less failure, matched, duplicate, late, and
grace-expiration behavior for both DCS families. Multiplexer tests freeze single
and safely mixed nested tmux/screen envelopes, rejected Screen-before-tmux and
duplicate-Screen topologies, repeated tmux ESC doubling, disabled/visible/all
behavior, explicit outer-profile separation, depth and byte overflow, malformed
recovery, and every fragmentation of a routed reply. Screen batches freeze the
exact CSI-only output and prove OSC 4/10/11, XTGETTCAP, and DECRQSS are not
registered, consume no capacity, and cannot delay completion after all CSI
replies arrive. Complete and oversized fabricated Screen DCS replies produce one
redacted diagnostic and no stray key, text, typed response, or raw sequence at
every split. Runtime oracles send routed DA1 through real sessions and prove
unwrapping precedes originating-query correlation and profile publication for
tmux and Screen. A fake clock proves unanswered routed queries retain
conservative evidence at the original exclusive deadline. Installed tmux and GNU
screen executables run under a real `script`-owned pseudoterminal; Screen
evidence freezes exact CSI relay and missing XTGETTCAP/DECRQSS ST bytes. Their
absence is an explicit platform skip. Enhanced-key tests cover exact
query/set/restore bytes, legacy `CSI 27;modifier;key~`, compatible CSI-u,
malformed recovery, Kitty precedence, and session reverse cleanup.

Color-precedence tests begin with default, `TERM=*-256color`, and `COLORTERM`
heuristics, then prove validated `RGB=24` publishes true color with query
origin. Negative and non-RGB values preserve heuristics; database, prior-query,
and override origins remain unchanged. Capacity tests prove explicit
`Settings.ColorDepth` emits no RGB request, registers no pending RGB family, and
does not consume the next bounded query slot.

Terminal-description provider tests use process-isolated fixture terminfo
directories and termcap files rather than mutating the test runner environment.
The implemented probe proves `TERMINFO`, ordered `TERMINFO_DIRS`, and inline
`TERMCAP`, a `TERMCAP` file, and ordered `TERMPATH` when supported by the native
build through the one `setupterm` path. Remaining host-selection tests MUST
prove `$HOME/.terminfo`, configured directory, `TERMCAP`, `TERMPATH`,
`/etc/termcap`, `/usr/share/misc/termcap`, and `$HOME/.termcap` source order
through the single
[lookup and fallback contract](../protocols/terminfo.md#lookup-and-fallback).
They cover every retained `DescriptionLimits` and `ProgramLimits` default and
hard ceiling, provider failures, generic/hardcopy entries, required-capability
omissions, and one-sided requested cursor or alternate-screen pairs before a
frame writes bytes. Native lookup remains outside the adversarial parser
guarantee described by the
[provider trust boundary](../protocols/terminfo.md#native-provider-trust-boundary):
tests prove retained snapshot bounds and explicit-`TerminalProfile` native-
discovery bypass, not an impossible native deadline.

Fixtures assert the exact
[terminfo identifier allowlist](../protocols/terminfo.md#finite-capability-boundary),
that all other names are ignored, and that database programs rather than
environment names determine legacy encoded commands. They cover numeric `U8`,
one Boolean `XF`, and every accepted `RGB` form plus malformed, overflow, zero,
extra-field, and `colors`-inconsistent precision rejection. Termcap fixtures
prove the required raw-to-canonical mappings, no guessed optional aliases,
inline 1023-byte rejection, provider failure, and that absent extended values
never become support claims.

Windows VT profile tests use Microsoft's current Console VT sequence tables as
the independent oracle. They assert every retained command's exact compiled
source bytes, the matched cursor/keypad and restoration pair, the fixed input
map, 16-color guaranteed fidelity, and absence of unsupported rendition and
optional-protocol claims. Exact accepted-snapshot boundary tests reject the
fixed profile one byte below its deterministic size and accept that exact size.
Loader tests prove explicit-profile bypass, Unix and Windows provider selection,
established-VT gating, contradictory platform-fact rejection, diagnostic
retention, provider failure, and that missing-or-generic or accepted unsuitable
results are never replaced by opt-in ANSI fallback. Only Unix provider
unavailability may use that explicit fallback.

Hosting preflight tests inject only the interactive/open/plain-message host
boundaries. They prove every non-usable suitability and ordinary description
absence constructs no `Application` or `Session`, writes no mode, query, or
renderer bytes, disposes resize/transport/platform resources once in lifecycle
order, and maps only the typed preflight rejection to
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
description internals and already owns higher-layer hosting integration. It
injects `IDescriptionProvider` results through `DescriptionLoader` and
`ConsoleConnection.ResolveDescription`, then drives the real builder. Separate
TERM=dumb generic, ordinary missing, and provider-failure cases prove
`Build`/`RunAsync`, ordered diagnostic retention, zero terminal writes, no
attachment, and exact ownership cleanup. Resize, transport, and platform-restore
failures are injected independently to prove rejection remains primary while
every resource receives one ordered attempt. Public consumer tests separately
compile the parameterless terminal-options overload, an original `IProtocolSink`
implementation with no typed color or metrics callbacks, public response
construction and empty-sentinel rejection, and every explicit response/query
enum ordinal. The original sink must receive routed OSC 10/11 at every split as
legacy red, green, blue values, indexed OSC 4 as index plus normalized RGB, and
every metrics family as width then height. Status/color tests freeze their enum
values and override provenance.

## Independent oracles

Use primary-standard byte examples, a small parser state reference,
encode/decode round trips where canonical, and invariants such as “whole input
equals every fragmentation.” Do not generate expected bytes with the production
encoder.

Randomized parser invariants use fixed seeds so failures reproduce exactly.
Every generated valid sequence must produce the same events at every
fragmentation, while hostile generated input must recover to a known trailing
CSI event. Failure messages include seed, case, and input bytes.

The typed input decoder has a separate fixed-seed hostile suite. Random bytes
arrive in random 1–8 byte fragments under small paste/parser limits, followed by
explicit paste termination/cancellation and a known text key. Every case must
terminate, retain no oversized paste, and recover the known key; failures print
seed, case, and hexadecimal input.

Description-key tests enumerate `kf1` through `kf63`, all six keypad-position
names, and all seventy allowlisted extended modified-key names. They prove exact
seven-bit/eight-bit CSI and SS3 signature conflicts and terminals whose database
strings deliberately differ from ANSI grammar. Representative Control, Escape,
CSI, and SS3 signatures in both accepted widths, plus overlapping invalid-UTF-8
trie entries, run at every split. A fixed-seed suite randomizes active
description bindings and 1–8-byte fragmentation around a typed query reply and
known trailing text, printing seed, case, and bytes on failure. Runtime proof
passes the profile map through `Session` and `ProtocolRouter` to the final typed
stroke.

With both eight-bit CSI and SS3 maps active, valid UTF-8 scalars whose
continuation is `0x8F` or `0x9B` run at every split and remain exact Unicode
text without a key or diagnostic. Separate cases prove standalone described C1
keys, unmatched recovery, and caller-enabled parser-wide C1 behavior.

Fallback-key tests prove that a pending UTF-8 scalar wins over a new
continuation-leading binding while an established matcher prefix retains
ownership. Overlapping shorter/longer bindings rematch a described suffix at
every split. Fixed-seed cases randomize continuation bindings, suffixes,
adjacent key counts, and fragmentation, then compare the exact later diagnostic
offset. Warmed signature and fallback-rematch paths allocate zero managed bytes.
Lifecycle cases dispose both a retained prefix and a decoder after suffix
rematching, then prove matcher bindings, trie arrays, pending bytes, and replay
workspace are released, repeated disposal is harmless, and decoder operations
continue to reject post-disposal use.

Provider fixtures admit parameter and intermediate signatures at the exact
active limit, reject limit-plus-one, empty, and incomplete introducers with
`InvalidKey`, and retain another valid optional key in the same usable profile.

The warmed CSI parser path is measured at zero managed bytes per event. A
hostile 2 MiB oversized OSC case proves retained/allocation behavior stays
bounded and that the next valid sequence is still decoded.

## Integration

At least one test per implemented protocol family traverses typed command,
encoder, transport fake, streaming decoder, typed response/event, and terminal
lifecycle cleanup. Clipboard tests include arbitrary binary MIME data and final
encoded bytes, not merely command construction.

## Required evidence

| Layer             | Required observation                                                                 |
| ----------------- | ------------------------------------------------------------------------------------ |
| Encoder           | Exact bytes for every typed value, boundary, and supported form.                     |
| Streaming decoder | Every split, malformed/unknown/oversized recovery, offsets, and bounded state.       |
| Router            | Typed owner, reply correlation, redaction, ordering, and ordinary input coexistence. |
| Lifecycle         | Mode pairs, multiplexer framing, transport failure, timeout, and reverse cleanup.    |

At least one implemented-family scenario traverses typed command to final bytes
and typed response back to its runtime consumer.
