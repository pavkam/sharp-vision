# Kitty clipboard and OSC 52

## Overview

Primary source:
[Kitty clipboard protocol](https://sw.kovidgoyal.net/kitty/clipboard/), accessed
2026-07-11. Plain-text OSC 52 and Kitty OSC 5522 are separate typed protocols.

OSC 52 transfers Base64 text to and from named selections when the terminal
permits it. OSC 5522 uses `OSC 5522 ; metadata ; payload ST` for MIME-aware
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

`Kitty.Clipboard.Packet` validates colon-separated metadata, all documented
statuses, Base64 MIME/password/name values, optional primary location, and
correlation IDs. Unknown metadata remains observable by key name only. The
packet publishes those names as an immutable snapshot.
`Kitty.Clipboard.KittyClipboardWriter` emits read/list/write/data/alias/end
packets, DECRQM, and paste-mode controls. `Kitty.Clipboard.Transaction` enforces
`OK -> DATA* -> DONE` reads, write `DONE`, one MIME type at a time, 4096-byte
chunks, total-size limits, cancellation, and fake-clock deadlines. Successful
data transfers into an owned `Kitty.Clipboard.KittyClipboardResult` whose
immutable item collection cannot be rewritten by consumers and whose disposal
clears every transferred data buffer.

Correlation is checked before validity. A transaction bound to an `id` ignores
any packet whose `id` does not match its own — malformed or well-formed,
including a malformed packet whose `id` could not be recovered before its
structural error. A packet fails the transaction only when it carries a matching
`id` and fails validation. A transaction with no `id` cannot tell unrelated
traffic apart, so any malformed packet fails it.

## Supported features

SharpVision supports OSC 52 text plus typed OSC 5522 MIME reads and writes,
aliases, permissions and errors, passwords and names, detection, and multiplexer
correlation. `Application.Terminal.Clipboard` performs Kitty-preferred
selection: `Write` and `Request` use Kitty OSC 5522 when it is authoritatively
proven, fall back to OSC 52 text when only that is proven, and stay byte-quiet
when neither is. Inbound OSC 52 and OSC 5522 replies route through the decoder
into `TerminalServices`, which owns the `Transaction` lifecycle (correlation,
deadline, cancellation) and reports every outcome through the single
`IClipboard.KittyClipboardReplyReceived` event, whichever protocol actually
served the request.

Unsolicited/proactive Kitty paste events (mode 5522 also lets a terminal push
clipboard changes without a request) remain intentionally out of scope: no
runtime path currently reports a mode-5522 paste notification that did not
originate from an explicit `Write` or `Request`.

## Bounds and recovery

Chunking behavior is pinned at 0, 1, 4095, 4096, 4097, and 8192 bytes, along
with reads, list, aliases, primary selection, credentials, permissions, every
status, and terminators. The parser accepts every split point and rejects
malformed metadata, Base64, or ordering before recovering. Cancellation, limits,
binary data, and writer-to-parser-to-transaction integration are also covered.

## Sources

- [Kitty clipboard protocol](https://sw.kovidgoyal.net/kitty/clipboard/) defines
  OSC 52, OSC 5522, metadata, chunking, status, and private mode 5522.

Source accessed 2026-07-28.

## Expected behavior

| Layer       | Required evidence                                                                              |
| ----------- | ---------------------------------------------------------------------------------------------- |
| Writer      | Exact boundary-size chunks, metadata, selection, MIME, terminator, and final empty write.      |
| Parser      | Every split, malformed Base64/order/status, limits, cancellation, and resynchronization.       |
| Transaction | Correlation, timeout, permissions, binary payload ownership, redaction, and typed integration. |
