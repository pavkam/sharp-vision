# DCS and string commands

## DCS and string command contract

ECMA string families are DCS (`ESC P`), SOS (`ESC X`), OSC (`ESC ]`), PM
(`ESC ^`), and APC (`ESC _`), terminated by ST (`ESC \`). OSC has its own
[contract](osc.md#osc-contract); this file governs shared streaming behavior.

The parser holds a bounded payload or streams to a registered typed consumer.
Unregistered strings are skipped with bounded diagnostics. A split `ESC \`
terminator is recognized across reads. CAN and SUB abort where the compatibility
profile specifies; end-of-stream reports truncation without synthesizing ST.

## Supported features

DCS supports terminal queries and a bounded raw extension boundary used by
multiplexer passthrough. APC recognizes the Kitty graphics introducer
diagnostically. SOS and PM are observable but have no public high-level
behavior.

## Typed API and behavior

`Parser` recognizes DCS, SOS, OSC, PM, and APC across arbitrary read
fragmentation, including split `ESC \\` terminators. CAN/SUB cancel active
sequences. Configurable limits bound payload storage; overflow discards until a
valid terminator and then resumes ground-state parsing. `Complete` reports one
truncation diagnostic for unfinished input. `Writer` emits validated DCS, APC,
PM, and SOS commands with ST termination. The
[runtime router](runtime-routing.md#runtime-routing-contract) owns copied
observation after framing. Known query families are decoded through their
protocol-specific typed parsers; generic DCS consumer registration is not a
public API. Multiplexer framing is governed by the tmux and GNU screen
contracts.

## Security and tests

No string payload is logged by default. Limits apply before buffering or Base64
decoding. Tests cover every introducer, empty payloads, all split points,
embedded ESC, oversized input, cancellation, truncation, unknown consumers, and
recovery.

## Sources

- [ECMA-48, fifth edition, June 1991](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
  defines DCS, SOS, OSC, PM, APC, ST, and cancellation controls.
- [XTerm Control Sequences, Patch #410, 2026-04-19](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  records xterm string handling and compatibility behavior.

Sources accessed 2026-07-17.

## Expected behavior

| Layer         | Required evidence                                                                            |
| ------------- | -------------------------------------------------------------------------------------------- |
| Framing       | Every split, BEL/ST policy, CAN/SUB interruption, limit boundaries, and post-error recovery. |
| Typed payload | Exact DECRQSS/XTGETTCAP and supported extension parsing without raw lifetime leaks.          |
| Security      | Oversized and hostile strings remain bounded and diagnostics reveal no payload.              |
