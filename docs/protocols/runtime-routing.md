# Runtime protocol routing

## Runtime routing contract

[`Parser`](ecma-48.md#streaming-grammar) owns bounded ECMA-48 framing. `Router`
owns the decision between typed input, typed terminal responses, and bounded raw
extension strings. Parser callback spans are borrowed; every `ProtocolSequence`
copies its header and payload before the callback returns.

`IProtocolSink.Response` receives recognized DA, DSR, DECRPM, Kitty keyboard,
and OSC color replies. `IProtocolSink.Sequence` receives completed OSC, DCS,
APC, PM, and SOS values without a registered typed consumer. A recognized
response is never emitted again as input or a raw sequence.

The first implementation routes already-decoded response families. It does not
send capability queries or change the active capability profile. The
[capability contract](../architecture/capabilities.md#queries-and-publication)
owns those later transaction and publication rules.

## Ordering and ownership

Callbacks are synchronous and remain in transport order. `Session` invokes one
sink callback at a time. `Response` owns its numeric values, and
`ProtocolSequence` owns copied parameters, intermediates, and payload bytes.
Neither value retains parser storage or the transport read buffer.

The application may enqueue immutable numeric responses for dispatcher-affine
services and events. It does not enqueue unregistered raw strings. Those strings
become redacted diagnostics containing only their family and discarded byte
count. Clipboard, graphics, and notification services register typed consumers
below that fallback boundary.

## Recovery and fallback

Malformed, interrupted, truncated, and oversized sequences retain the
[parser recovery contract](ecma-48.md#streaming-grammar). A legacy `Decoder`
sink that does not implement `IProtocolSink` receives
`DiagnosticCode.Unsupported` for a valid reply or string instead of silently
losing it.

Unknown valid strings are observable through `IProtocolSink.Sequence`; their
presence does not enable a capability. Strict mode may promote the redacted
diagnostic but cannot reinterpret the payload as user input.

## Security and bounds

The parser's parameter, intermediate, and string limits apply before routing.
Owned values cannot exceed those limits. Application diagnostics never include
raw payload bytes, clipboard data, paths, credentials, or terminal-provided
metadata.

## Test obligations

Test each recognized reply and each string family whole and at every read split.
Mutate the source buffer after routing to prove ownership. Test adjacent input
and responses to prove ordering. Follow malformed, cancelled, truncated, and
oversized strings with known input to prove recovery. Drive at least one reply
through `Router`, `Session`, and the application dispatcher.
