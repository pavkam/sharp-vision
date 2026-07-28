# ECMA-48 control functions

## ECMA-48 contract

Primary source:
[ECMA-48, fifth edition, June 1991](https://ecma-international.org/publications-and-standards/standards/ecma-48/),
accessed 2026-07-11. ECMA-48 is open-ended and explicitly expects a device to
implement only an appropriate subset.

SharpVision accepts the 7-bit forms emitted by contemporary UTF-8 terminals. The
parser recognizes C0 controls, ESC intermediates/finals, CSI, OSC, DCS, APC, PM,
SOS, and ST. Eight-bit C1 bytes are observable only when the configured input
encoding permits them; UTF-8 continuation bytes must never be mistaken for C1
controls.

## Streaming grammar

A parser state consumes one byte at a time and retains only bounded state. A
control sequence can span any number of reads. CAN and SUB abort an active
escape sequence according to the selected compatibility profile. Malformed or
oversized strings emit diagnostics and recover at a recognized terminator or new
control introducer.

Default limits bound parameter count, parameter magnitude, intermediate bytes,
and string payload length. Options may lower or raise limits but may not disable
boundedness.

The Phase 2 API is `SharpVision.Terminal.Protocols.Parser`. It reports borrowed
spans synchronously through `ISequenceSink`; a sink must copy any value retained
after its callback. `Complete` reports one truncated sequence and returns to
ground, `Reset` discards partial state, and disposal returns cleared pooled
storage. The warmed CSI path has a regression test requiring zero managed bytes
per event.

`Limits.Default` currently allows 256 parameter bytes, 16 intermediate bytes, 1
MiB per terminal string, 16 MiB per clipboard transaction, 8 KiB of Kitty
metadata, 32 concurrent queries, and a 750 ms query deadline. OSC accepts BEL by
default. Eight-bit C1 controls are opt-in so UTF-8 continuation bytes are text
unless the caller explicitly selects an eight-bit control stream.

## First milestone contract

Phase 2 implements raw bounded framing, typed renderer-required CSI/SGR/OSC
encoders, typed DA/DSR/DECRPM and OSC color responses, and raw observation of
unknown valid functions. Unsupported functions do not corrupt parser
synchronization or throw in normal mode.

## Sources

- [ECMA-48, fifth edition, June 1991](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
  defines the control-function architecture and byte classes used here.

Source accessed 2026-07-28.

## Test obligations

Every representative sequence is tested whole, at every read split, adjacent to
text and other controls, malformed, truncated, cancelled, and followed by a
known sequence that proves recovery.
