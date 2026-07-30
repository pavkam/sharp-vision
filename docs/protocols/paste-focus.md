# Bracketed paste and focus reporting

## Paste and focus contract

xterm private mode 2004 wraps pasted data between `CSI 200 ~` and `CSI 201 ~`.
Private mode 1004 reports focus gained as `CSI I` and focus lost as `CSI O`.
Primary source:
[XTerm Control Sequences](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html),
accessed 2026-07-11.

Paste content is UTF-8 application data, not control input. While a paste is
active, bytes that resemble keys or escape sequences remain paste payload until
the exact end marker. Payload size is bounded; oversized input reports a
diagnostic and discards through the terminator without losing parser sync.

Focus reports are typed terminal events and are distinct from control focus
inside the UI tree. Terminal focus loss may clear hover/pressed state according
to UI policy but does not synthesize arbitrary key releases.

## Supported features

Manage both modes through lifecycle leases, decode fragmented begin/end markers,
emit immutable paste/focus events on the dispatcher, and restore modes at exit.

`Input.Decoder` recognizes CSI 200~/201~ and switches to raw paste mode after
the begin marker. A six-byte exact matcher holds only a possible end-marker
prefix; mismatches return the held bytes to payload, so embedded ESC and every
proper marker prefix remain data. Parser callbacks are bypassed until the exact
terminator, meaning paste content can never trigger keys, focus, mouse, OSC, or
CSI handling.

Payload retention is capped by `Input.Options.MaxPasteBytes`. Overflow clears
retained bytes, discards through the terminator, reports one structural
diagnostic, and resumes ordinary decoding at the following byte. Successful
payloads are normalized to valid UTF-8 with U+FFFD for malformed subsequences,
copied into an owned `Paste`, and remain stable when decoder storage is reused.
End-of-stream drops partial payload and reports truncation.

CSI I/O emit immutable gained/lost `Focus` values. They are terminal focus only;
application routing applies the separate
[UI focus policy](../concepts/input-routing.md#route-construction).

## Bounds and lifecycle

Supported input covers empty, multiline, Unicode, invalid UTF-8, embedded ESC,
every proper marker prefix, owned retention, megabyte overflow, truncation,
every byte split, adjacent focus/text events, and terminal focus transitions.
Lifecycle cleanup is proved by `Runtime.Session`, which enables only supported
modes and restores them in reverse even after startup, input, handler,
cancellation, or cleanup failure.

## Sources

- [XTerm Control Sequences, Patch #410](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  defines bracketed-paste mode 2004 and focus-reporting mode 1004.

Source accessed 2026-07-28.

## Expected behavior

| Layer     | Required evidence                                                                        |
| --------- | ---------------------------------------------------------------------------------------- |
| Paste     | Every marker split/prefix, embedded ESC, UTF-8, limit overflow, ownership, and recovery. |
| Focus     | Exact gain/loss events, ordering beside text, and UI-state policy.                       |
| Lifecycle | Capability-gated leases and reverse restoration after every exit/failure path.           |
