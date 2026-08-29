# Kitty clipboard and OSC 52

## Overview

Primary source:
[Kitty clipboard protocol](https://sw.kovidgoyal.net/kitty/clipboard/), accessed
2026-08-29. Plain-text OSC 52 and Kitty OSC 5522 are separate typed protocols.

OSC 52 transfers Base64 text to and from named selections when the terminal
permits it. OSC 5522 uses `OSC 5522 ; metadata ; payload ST` for MIME-aware
read/write, aliases, status, permission errors, correlation identifiers, and
paste events. Metadata values that carry text use Base64 as specified.

OSC 5522 write data is split into at most 4096 raw bytes per independently
padded Base64 chunk. Chunks for one MIME type remain contiguous. An empty
`wdata` ends a write. IDs contain only `[A-Za-z0-9-_+.]` when echoed by a
terminal/multiplexer. Clipboard payloads, passwords, and permission tokens are
redacted from diagnostics.

An OSC 52 `Pc` field is empty or an ordered list made entirely from `c`, `p`,
`q`, `s`, and `0` through `7`. SharpVision resolves a valid list to its first
selection and rejects the whole reply if any list character is invalid; it never
skips malformed bytes to manufacture a successful correlation.

## Detection and state

`CSI ? 5522 $ p` queries support; DECRPM values 0 or 4 mean unsupported and 1,
2, or 3 mean supported. Private mode 5522 enables paste events. A host opts in
with `ConsoleRunOptions.ClipboardPasteEvents` or
`ConsoleApplicationBuilder.UseClipboardPasteEvents()`. The session acquires
`CSI ? 5522 h` only after authoritative Kitty support and an authorized route
are known, and restores it with `CSI ? 5522 l` during reverse cleanup.
Transactions enforce documented ordering, total-size, metadata-size, timeout,
and concurrency limits. Invalid Base64 or ordering aborts only that transaction
and preserves outer parsing.

`Kitty.Clipboard.KittyClipboardPacket` validates colon-separated metadata, all
documented statuses, Base64 MIME/password/name values, optional primary
location, and correlation IDs. Unknown metadata remains observable by key name
only. The packet publishes those names as an immutable snapshot.
`Kitty.Clipboard.KittyClipboardWriter` emits read/list/write/data/alias/end
packets, DECRQM, and paste-mode controls.
`Kitty.Clipboard.KittyClipboardTransaction` enforces `OK -> DATA* -> DONE`
reads, write `DONE`, one MIME type at a time, 4096-byte chunks, total-size
limits, cancellation, and fake-clock deadlines. Successful data transfers into
an owned `Kitty.Clipboard.KittyClipboardResult` whose immutable item collection
cannot be rewritten by consumers and whose disposal clears every transferred
data buffer.

SSH environment markers do not narrow Kitty clipboard support: they locate the
client process, not the terminal receiving the protocol. The standard mode-5522
DECRQM probe remains enabled over SSH, while an explicitly detected multiplexer
without authorized two-way routing still suppresses the protocol. Runtime
deadline callbacks recheck the transaction's UTC deadline; an early relative
timer callback reschedules the remaining interval instead of discarding the
active transaction.

Correlation is checked before validity. A transaction bound to an `id` ignores
any packet whose `id` does not match its own — malformed or well-formed,
including a malformed packet whose `id` could not be recovered before its
structural error. A packet fails the transaction only when it carries a matching
`id` and fails validation. The same rule applies to a transaction with no `id`:
it ignores any malformed packet that recovers an `id` before its structural
error, since a recovered `id` is necessarily foreign when there is nothing to
match. A malformed packet fails an id-less transaction only when no `id` could
be recovered before the error.

## Supported features

SharpVision supports OSC 52 text plus typed OSC 5522 MIME reads and writes,
aliases, permissions and errors, passwords and names, detection, and multiplexer
correlation. `Application.Terminal.Clipboard` performs Kitty-preferred
selection: `Write` and `Request` use Kitty OSC 5522 when it is authoritatively
proven, fall back to OSC 52 text when only that is proven, and stay byte-quiet
when neither is. Inbound OSC 52 and OSC 5522 replies route through the decoder
into `TerminalServices`, which owns the `KittyClipboardTransaction` lifecycle
(correlation, deadline, cancellation) and reports each completed, failed, or
timed-out operation through the single `IClipboard.KittyClipboardReplyReceived`
event — for every `Request`, whichever protocol served it, and for a `Write`
served by OSC 5522. An OSC 52 `Write` is fire-and-forget - the protocol defines
no acknowledgement for a write, so no transaction is opened and no event is
raised.

OSC 52 request state and its encoded query are posted as one dispatcher-owned
operation. Concurrent callers therefore serialize in the same order on the wire
and in the single pending-request slot; the last query written is always the
request whose reply or timeout can complete.

> [!WARNING]
>
> A superseded or shutdown-abandoned operation raises no event at all. Starting
> a new `Write` or `Request` while one is pending silently cancels and disposes
> the pending transaction, and disposal at shutdown abandons an outstanding
> request the same way. A caller that awaits `KittyClipboardReplyReceived` to
> observe a specific operation never resumes for one that was superseded, and
> the fetched selection is lost.

Mode 5522 also lets a terminal push clipboard changes without a request. When
the application owns that mode lease, an id-less `OK -> DATA* -> DONE` read
notification is assembled separately from application-issued transactions. Its
single `mime=.` payload is a whitespace-separated inventory of available MIME
types. `IClipboard.ClipboardPasteReceived` publishes an owned
`ClipboardPasteEventArgs` containing that inventory, the source selection, and
the one-time password supplied on every packet. A missing or inconsistent
password, malformed UTF-8 inventory, terminal error, ordering violation, or
deadline expiry discards only the notification and raises no partial event.
Applications can therefore distinguish a terminal paste offer from completion of
their own `Write` or `Request` through `KittyClipboardReplyReceived`.

An approved tmux clipboard route wraps every complete OSC 52 or OSC 5522 string
independently as `DCS tmux; ... ST`; the paste-mode lease crosses the same typed
route. Each Kitty data chunk is budgeted against the route separately, so a
valid multipart transfer need not fit the envelope limit as one aggregate
buffer. The input route accepts only one typed clipboard packet per returned
envelope. GNU screen is rejected for this OSC family, as are routes without
explicit clipboard authorization; those configurations report the service
unsupported and never lease paste events.

## Bounds and recovery

Chunking behavior is pinned at 0, 1, 4095, 4096, 4097, and 8192 bytes, along
with reads, list, aliases, primary selection, credentials, permissions, every
status, and terminators. OSC 52 selection-list validation and the packet parser
accept every split point and reject malformed metadata, Base64, or ordering
before recovering. Cancellation, limits, binary data, and
writer-to-parser-to-transaction integration are also covered.

## Sources

- [Kitty clipboard protocol](https://sw.kovidgoyal.net/kitty/clipboard/) defines
  OSC 52, OSC 5522, metadata, chunking, status, and private mode 5522.

Source accessed 2026-08-29. Paste notifications require Kitty 0.44.1 or later.

## Expected behavior

| Layer       | Required evidence                                                                              |
| ----------- | ---------------------------------------------------------------------------------------------- |
| Writer      | Exact boundary-size chunks, metadata, selection, MIME, terminator, and final empty write.      |
| Parser      | Every split, malformed Base64/order/status, limits, cancellation, and resynchronization.       |
| Transaction | Correlation, timeout, permissions, binary payload ownership, redaction, and typed integration. |
