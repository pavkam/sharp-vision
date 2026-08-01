# Runtime protocol routing

## Overview

[`Parser`](ecma-48.md#streaming-grammar) owns bounded ECMA-48 framing.
`ProtocolRouter` owns the decision between typed input, typed terminal
responses, and bounded raw extension strings. Parser callback spans are
borrowed; every `ProtocolSequence` copies its header and payload before the
callback returns.

The active terminal profile's key map is copied into immutable input options
when `Session` constructs its router. CSI, SS3, Escape, and control callbacks
first run registered reply, paste, mouse, focus, and Kitty consumers, then exact
described-key signatures, then the explicit ANSI compatibility grammar when that
built-in profile selected it. Non-signature description strings use a bounded
longest-match trie before ordinary text decoding. An unmatched retained prefix
is replayed once through the ordinary decoder, preserving byte order and
recovery.

Seven-bit `ESC O final` and eight-bit `SS3 final` compile to the same structural
signature. When the active map contains an eight-bit CSI or SS3 spelling, the
decoder recognizes only that standalone ground-state introducer; it does not
enable parser-wide C1 handling. A pending UTF-8 scalar owns `0x8F` or `0x9B` as
its continuation, while an explicit caller-supplied `AcceptEightBitControls`
policy remains independently authoritative for other C1 bytes. Escape signatures
retain their intermediate bytes; an exact described signature is checked before
an otherwise unsupported Escape intermediate sequence becomes a diagnostic.

`IProtocolSink.Response` overloads receive recognized numeric DA/DSR/DECRPM and
Kitty keyboard replies, immutable `PaletteResponse` values for OSC 4/10/11, and
immutable `MetricsResponse` values for window/cell geometry. Validated DECRQSS
and XTGETTCAP replies use owned `StatusResponse` and `CapabilityResponse`
overloads. The palette and metrics overloads adapt through the original
numeric-response callback when a sink does not override them. A legacy protocol
sink which does not override either DCS overload receives one synthetic
`DiagnosticCode.Unsupported` DCS diagnostic through its inherited input
callback. That diagnostic has zero offset and discarded bytes because the typed
compatibility callback has no raw payload. OSC 10/11 preserve normalized red,
green, blue; OSC 4 supplies index, red, green, blue; and metrics supply width,
height. Kitty graphics APC payloads beginning with `G` use the strict owned
`Kitty.Graphics.Response` callback; its default compatibility path reports a
redacted unsupported APC diagnostic. Built-in sinks override every typed
overload they consume, so discovery still receives the typed value rather than
the compatibility diagnostic. `IProtocolSink.Sequence` receives completed OSC,
DCS, APC, PM, and SOS values without a registered typed consumer. A recognized
response is never emitted again as input or a raw sequence.

Validated queried metrics refine pixel-to-cell inference only after their
ordered response callback. A complete local resize grid has precedence. Earlier
pointer values are immutable and are never revisited when later geometry
arrives.

`Session` owns a `ProtocolRouter` and delivers the complete sink contract in
transport order. `Application` queues typed replies as immutable records and
raises `ResponseReceived`, `PaletteResponseReceived`, `MetricsResponseReceived`,
`StatusResponseReceived`, or `CapabilityResponseReceived` only on its
dispatcher. These records share one ordered input queue. An unregistered raw
sequence becomes a `DiagnosticCode.Unsupported` record containing its family and
byte count, never its payload.

The fixed terminal backend contributes only immutable extension-family metadata.
Existing `ProtocolRouter` consumers remain the wire implementation; they are not
recreated inside backend classes. Capability evidence still gates optional
consumers and output. Backend composition and identity are specified by the
[terminal backend contract](../architecture/terminal-backends.md#extensions-and-authorization),
while query planning, response classification, and immutable publication are
specified by the
[discovery pipeline](../architecture/discovery-pipeline.md#active-query-strategy).

`ProtocolRouter` owns decoding and ordered metric refinement; the
[capability contract](../architecture/capabilities.md#queries-and-publication)
owns query selection, transaction deadlines, and profile publication.

## Ordering and ownership

Callbacks are synchronous and remain in transport order. `Session` invokes one
sink callback at a time. `Response` owns its numeric values; palette and metrics
responses and Kitty graphics responses are self-contained owned values; and
`ProtocolSequence` owns copied parameters, intermediates, and payload bytes. No
value retains parser storage or the transport read buffer.

`PaletteResponse` and `MetricsResponse` expose validated public constructors.
Their all-zero `default` value is an explicit empty sentinel rather than a
decoded terminal reply. `StatusResponse` likewise has an empty default sentinel;
`CapabilityResponse` is a non-null owned reference. Their event payloads reject
empty or null values before changing observable state. `Queries` rejects an
empty response and a response assigned to the wrong family.

The application may enqueue immutable numeric responses for dispatcher-affine
services and events. It also enqueues the owned bounded status and capability
responses without retaining parser or transport buffers. It does not enqueue
unregistered raw strings. Those strings become redacted diagnostics containing
only their family and discarded byte count. Clipboard, graphics, and
notification services register typed consumers below that fallback boundary.

## Recovery and fallback

Malformed, interrupted, truncated, and oversized sequences retain the
[parser recovery contract](ecma-48.md#streaming-grammar). A legacy `Decoder`
sink that does not implement `IProtocolSink` receives
`DiagnosticCode.Unsupported` for a valid reply or string instead of silently
losing it. A legacy `IProtocolSink` implementation which predates the typed DCS
overloads receives the synthetic no-payload unsupported diagnostic described
above instead of silently dropping DECRQSS or XTGETTCAP.

Unknown valid strings are observable through `IProtocolSink.Sequence`; their
presence does not enable a capability. Strict mode may promote the redacted
diagnostic but cannot reinterpret the payload as user input.

## Security and bounds

The parser's parameter, intermediate, and string limits apply before routing.
Owned values cannot exceed those limits. Application diagnostics never include
raw payload bytes, clipboard data, paths, credentials, or terminal-provided
metadata.

## Inbound consumption surface

`Application` exposes seven events for consuming inbound protocol activity,
unchanged in ordering by hosting and discovery. `ResponseReceived` raises one
typed numeric `Response` (DA, DSR, DECRPM, or Kitty keyboard reply) per record.
`PaletteResponseReceived` raises OSC 4/10/11 colors, `MetricsResponseReceived`
raises window/cell geometry, `StatusResponseReceived` raises each validated
DECRQSS result, and `CapabilityResponseReceived` raises each validated XTGETTCAP
result. All five response events run on the dispatcher in mutual transport
order. Matched, wrong-identity or otherwise unsolicited, duplicate, and late DCS
values remain observable; query classification changes capability evidence, not
event delivery. `CapabilitiesChanged` raises once whenever a new immutable
`Capabilities` profile becomes active — including the startup negotiation result
— before any resulting invalidation. `Diagnostic` raises one redacted
`Diagnostic` for every malformed, unsupported, or otherwise unrouted protocol
occurrence, containing only its family and discarded byte count, never raw
payload bytes. These seven events are the complete way an application consumes
protocol replies and capability changes; there is no lower-level polling
surface.

## Sources

- [ECMA-48](ecma-48.md#overview) owns framing and recovery.
- [Device attributes](device-attributes.md#overview) owns startup-response
  correlation.
- [Terminal integration](../architecture/terminal-integration.md#protocol-routing)
  owns the application-facing end-to-end path.

## Expected behavior

Test each recognized reply and each string family whole and at every read split.
Mutate the source buffer after routing to prove ownership. Test adjacent input
and responses to prove ordering. Follow malformed, cancelled, truncated, and
oversized strings with known input to prove recovery. Drive at least one reply
through `ProtocolRouter`, `Session`, and the application dispatcher. Exercise
matched, wrong-identity, duplicate, and late typed DCS delivery in transport
order, retain the event values after the source buffer is overwritten, and run
both DCS families through a legacy sink at every read split to prove the
observable default fallback.
