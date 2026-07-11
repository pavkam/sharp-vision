# DCS and string commands

## DCS and string command contract

ECMA string families are DCS (`ESC P`), SOS (`ESC X`), OSC (`ESC ]`), PM
(`ESC ^`), and APC (`ESC _`), terminated by ST (`ESC \`). OSC has its own
[contract](osc.md#osc-contract); this file governs shared streaming behavior.

The parser holds a bounded payload or streams to a registered typed consumer.
Unregistered strings are skipped with bounded diagnostics. A split `ESC \`
terminator is recognized across reads. CAN and SUB abort where the compatibility
profile specifies; end-of-stream reports truncation without synthesizing ST.

## First milestone contract

DCS supports terminal queries and a bounded raw extension boundary used by
multiplexer passthrough and future graphics. APC recognizes the Kitty graphics
introducer diagnostically. SOS and PM are observable but have no public
high-level behavior.

## Security and tests

No string payload is logged by default. Limits apply before buffering or Base64
decoding. Tests cover every introducer, empty payloads, all split points,
embedded ESC, oversized input, cancellation, truncation, unknown consumers, and
recovery.
