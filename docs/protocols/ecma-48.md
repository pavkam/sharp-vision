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

## First milestone contract

Phase 2 provides typed encoders for renderer-required functions and typed events
for input/query responses. Unknown valid functions remain observable.
Unsupported functions do not corrupt parser synchronization or throw in normal
mode.

## Test obligations

Every representative sequence is tested whole, at every read split, adjacent to
text and other controls, malformed, truncated, cancelled, and followed by a
known sequence that proves recovery.
