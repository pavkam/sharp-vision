# ECMA-48 control functions

## Overview

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
oversized strings emit diagnostics and recover at a recognized terminator;
malformed or oversized CSI and escape sequences additionally recover at a new
control introducer. A string's recognized terminators are `ESC \` (ST), CAN,
SUB, an 8-bit ST when `ParserLimits.AcceptEightBitControls` is set, and BEL for
OSC when `ParserLimits.AcceptBellTerminatedOsc` is set; no other byte, including
a following `ESC [`, ends string recovery. This is standards-conformant: ECMA-48
leaves recovery from an unterminated control string unspecified, so SharpVision
deliberately uses terminator-only recovery.

Default limits bound parameter count, parameter magnitude, intermediate bytes,
and string payload length. Options may lower or raise limits but cannot disable
boundedness.

The typed API is `SharpVision.Terminal.Protocols.ProtocolParser`. It reports
borrowed spans synchronously through `ISequenceSink`; a sink must copy any value
retained after its callback. `Complete` reports one truncated sequence and
returns to ground, `Reset` discards partial state, and disposal returns cleared
pooled storage. The warmed CSI path has a regression test requiring zero managed
bytes per event.

`ParserLimits.Default` currently allows 256 parameter bytes, 16 intermediate
bytes, and 1 MiB per terminal string. `TransferLimits.Default` allows 16 MiB per
clipboard transaction and 8 KiB of Kitty metadata. `QueryLimits.Default` allows
32 concurrent queries and a 750 ms query deadline. OSC accepts BEL by default.
Eight-bit C1 controls are opt-in so UTF-8 continuation bytes are text unless the
caller explicitly selects an eight-bit control stream.

## Supported features

The implementation provides raw bounded framing, typed renderer-required
CSI/SGR/OSC encoders, typed DA/DSR/DECRPM and OSC color responses, and raw
observation of unknown valid functions. Unsupported functions do not corrupt
parser synchronization or throw in normal mode.

## Sources

- [ECMA-48, fifth edition, June 1991](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
  defines the control-function architecture and byte classes used here.

Source accessed 2026-07-28.

## Expected behavior

Readers can rely on the same typed result regardless of where transport reads
split a sequence. Text and adjacent controls keep their order. Malformed,
truncated, cancelled, and oversized sequences produce bounded diagnostics, then
recover at the documented boundary so the next valid sequence is decoded.
