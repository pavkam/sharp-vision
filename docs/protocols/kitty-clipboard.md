# Kitty clipboard and OSC 52

## Kitty clipboard contract

Primary source:
[Kitty clipboard protocol](https://sw.kovidgoyal.net/kitty/clipboard/), accessed
2026-07-11. Plain-text OSC 52 and Kitty OSC 5522 are separate typed protocols.

OSC 52 transfers Base64 text to/from named selections subject to terminal
permission. OSC 5522 uses `OSC 5522 ; metadata ; payload ST` for MIME-aware
read/write, aliases, status, permission errors, correlation identifiers, and
paste events. Metadata values that carry text use Base64 as specified.

OSC 5522 write data is split into at most 4096 raw bytes per independently
padded Base64 chunk. Chunks for one MIME type remain contiguous. An empty
`wdata` ends a write. IDs contain only `[A-Za-z0-9-_+.]` when echoed by a
terminal/multiplexer. Clipboard payloads, passwords, and permission tokens are
redacted from diagnostics.

## Detection and state

`CSI ? 5522 $ p` queries support; DECRPM values 0 or 4 mean unsupported. Private
mode 5522 enables paste events. Transactions enforce documented ordering,
total-size, metadata-size, timeout, and concurrency limits. Invalid Base64 or
ordering aborts only that transaction and preserves outer parsing.

## First milestone contract

Implement OSC 52 text plus typed OSC 5522 MIME reads/writes, aliases,
permissions/errors, passwords/names, paste events, detection, and multiplexer
correlation. Unsupported terminals fall back to OSC 52 text when safe, then to
an unavailable result rather than an exception.

## Tests

Exact bytes cover 0, 1, 4095, 4096, and 4097 bytes; reads, list, aliases,
primary selection, permissions, every status, and terminators. Parser tests use
all split points, malformed metadata/Base64/order, recovery, cancellation,
limits, randomized binary data, and writer-to-parser integration.
