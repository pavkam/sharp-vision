# Kitty graphics protocol

## Kitty graphics contract

Primary source:
[Kitty terminal graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/),
accessed 2026-07-11. Commands use APC `ESC _ G`, comma-separated control data, a
semicolon, encoded payload, and ST.

The protocol supports direct, file, temporary-file, and shared-memory
transmission; RGB/RGBA/PNG data; zlib compression; images/placements; queries;
updates; animation; and Unicode placeholders. Several transports carry file or
shared-memory names and therefore require strict trust and ownership rules.

## First milestone contract

Provide sourced grammar, capability detection, bounded response decoding, and a
raw extension boundary. Full image upload, placement, animation, rasterization,
file access, and shared-memory ownership are unsupported in the first milestone.
The UI falls back to documented text/cell representations.

## Security and tests

Never open terminal-supplied paths. Extension payloads and responses are bounded
and redacted. Tests prove detection, unsupported fallback, APC framing, oversize
recovery, and that unknown graphics replies do not corrupt input.
