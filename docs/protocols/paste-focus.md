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

## First milestone contract

Manage both modes through lifecycle leases, decode fragmented begin/end markers,
emit immutable paste/focus events on the dispatcher, and restore modes at exit.

## Tests

Cover empty, multiline, Unicode, embedded ESC, marker-prefix payloads,
oversized/truncated paste, all split points, adjacent focus/mouse events,
terminal focus transitions, and cleanup failures.
