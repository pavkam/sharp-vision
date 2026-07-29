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

`KittyPacket` validates colon-separated metadata, all documented statuses,
Base64 MIME/password/name values, optional primary location, and correlation
IDs. Unknown metadata remains observable by key name only. `KittyWriter` emits
read/list/write/data/alias/end packets, DECRQM, and paste-mode controls.
`KittyTransaction` enforces `OK -> DATA* -> DONE` reads, write `DONE`, one MIME
type at a time, 4096-byte chunks, total-size limits, cancellation, and
fake-clock deadlines. Successful data transfers into an owned `KittyResult`
whose disposal clears every data buffer.

Correlation is checked before validity. An ID-bound transaction ignores any
packet — malformed or well-formed — whose `id` does not match its own, including
a malformed packet whose `id` could not be recovered before its structural
error. Only a packet that both fails validation and carries a matching `id`
fails the transaction; an unbound transaction (no `id` supplied) fails on any
malformed packet, since it has no correlation basis to discriminate unrelated
traffic.

## First milestone contract

Implement OSC 52 text plus typed OSC 5522 MIME reads/writes, aliases,
permissions/errors, passwords/names, paste events, detection, and multiplexer
correlation. Unsupported terminals fall back to OSC 52 text when safe, then to
an unavailable result rather than an exception.

## Tests

Exact bytes cover 0, 1, 4095, 4096, 4097, and 8192 bytes; reads, list, aliases,
primary selection, credentials, permissions, every status, and terminators.
Parser tests use all split points, malformed metadata/Base64/order, recovery,
cancellation, limits, binary data, and writer-to-parser-to-transaction
integration.

## Sources

- [Kitty clipboard protocol](https://sw.kovidgoyal.net/kitty/clipboard/) defines
  OSC 52, OSC 5522, metadata, chunking, status, and private mode 5522.

Source accessed 2026-07-28.

## Test obligations

| Layer       | Required evidence                                                                            |
| ----------- | -------------------------------------------------------------------------------------------- |
| Writer      | Exact boundary-size chunks, metadata, selection, MIME, terminator, and final empty write.    |
| Parser      | Every split, malformed Base64/order/status, limits, cancellation, and resynchronization.     |
| Transaction | Correlation, timeout, permissions, binary payload ownership, redaction, and runtime routing. |
