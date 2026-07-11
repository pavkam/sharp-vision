# Terminal protocol testing

## Terminal protocol testing

Each typed encoder has exact-byte tests for default, minimum, maximum, combined,
and rejected parameter values. Each streaming decoder representative runs once
whole and once for every possible split point, then with adjacent text and
controls.

## Parser matrix

Cover empty input, byte-at-a-time input, multiple frames per read, split ESC/ST,
invalid UTF-8, missing/empty/default parameters, numeric overflow, excess
parameters/intermediates, unknown valid sequences, CAN/SUB, oversized strings,
cancellation, end-of-stream truncation, and recovery into a known next event.

Transaction protocols additionally cover correlation, duplicate/late replies,
invalid state order, timeouts with a fake clock, concurrency limits, permission
errors, cancellation, and payload redaction.

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

The warmed CSI parser path is measured at zero managed bytes per event. A
hostile 2 MiB oversized OSC case proves retained/allocation behavior stays
bounded and that the next valid sequence is still decoded.

## Integration

At least one test per implemented protocol family traverses typed command,
encoder, transport fake, streaming decoder, typed response/event, and terminal
lifecycle cleanup. Clipboard tests include arbitrary binary MIME data and final
encoded bytes, not merely command construction.
