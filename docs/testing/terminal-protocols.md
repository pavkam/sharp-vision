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

## Integration

At least one test per implemented protocol family traverses typed command,
encoder, transport fake, streaming decoder, typed response/event, and terminal
lifecycle cleanup. Clipboard tests include arbitrary binary MIME data and final
encoded bytes, not merely command construction.
