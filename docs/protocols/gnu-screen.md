# GNU screen compatibility

## GNU screen contract

Primary source:
[GNU screen 5.0.0 control sequences](https://www.gnu.org/software/screen/manual/html_node/Control-Sequences.html),
accessed 2026-07-20. Screen recognizes a VT/ANSI subset and can pass a DCS
payload to the host terminal without interpretation.

Screen may filter, reinterpret, or limit modern mouse, OSC, color, and graphics
features. A `TERM` value associated with screen selects a conservative profile;
outer-terminal behavior requires explicit override or verified passthrough.

## DCS framing limit

The documented Screen relay uses the first ST to end its outer DCS and defines
no ESC-doubling or other nested-ST escape. Consequently an inner OSC or DCS
sequence terminated by ST cannot be represented inside that relay: Screen
consumes the inner ST as its own terminator. A real Screen 4.00.03
`script`-owned pseudoterminal corroborates the grammar: `DCS CSI-DA ST` reaches
the host as the exact CSI query, while wrapped XTGETTCAP and DECRQSS reach the
host as unterminated DCS bodies. SharpVision does not invent an escaping form.

## Implemented passthrough behavior

[`GnuScreenWriter.WritePassthrough`](../../src/SharpVision.Terminal/Multiplexing/GnuScreenWriter.cs)
writes a typed DCS envelope around one already-validated outer-terminal
sequence. GNU screen forwards this payload directly, so the implementation
preserves embedded ESC bytes rather than applying tmux's doubling rule.
`GnuScreenWriter.TryUnwrap` accepts only a complete `DCS ... ST` envelope and
owns no borrowed input after returning. The low-level writer remains exact, but
the policy layer admits only a complete batch of CSI sequences.

The shared [tmux routing policy](tmux.md#routing-policy) supplies the explicit
outer profile, typed-operation approval, visibility gate, finite nesting, and
bounded reply handling. A mixed route permits one farthest Screen layer with
zero or more surrounding tmux layers. Screen-before-tmux and duplicate Screen
layers are unsafe and therefore rejected. `TERM=screen-*` detects only the
nearest inner layer and cannot prove outer-terminal support.

Startup routes Kitty keyboard status, DA1, DA2, DEC mode reports, and cell or
window metrics because those are CSI sequences. It neither writes nor registers
OSC 4/10/11, XTGETTCAP, or DECRQSS, so omitted families consume no query slot
and cannot hold publication until the deadline. Clipboard and graphics do not
opt in through Screen without a separately sourced typed encoding that avoids
the nested-ST limit.

Terminal replies normally return as ordinary raw input. The explicit reply seam
also accepts a complete Screen-wrapped CSI reply before correlation. A
fabricated wrapped OSC/DCS reply is retained through its inner ST, rejected at
the full outer boundary with one redacted diagnostic, and never leaks either ST
as a key, text, or raw sequence. Overflow follows the same bounded discard path;
its diagnostic counts the complete discarded raw envelope, and subsequent
diagnostic offsets include every discarded wrapper byte. If the bounded Screen
envelope cannot be encoded atomically, startup publishes conservative absent
evidence immediately and performs no transport write, flush, optional-mode
lease, cleanup sequence, or deadline wait.

## Supported features

Support the documented VT/ANSI subset, bounded DCS passthrough for approved CSI
queries, and safe omission of string-terminated or otherwise unsupported modern
extensions. Do not emit OSC 83 screen commands or other session-control
operations.

## Compatibility evidence

Pure tests cover exact CSI batches, omitted OSC/DCS registration, every split,
safe and rejected mixed topologies, complete and oversized recovery, explicit
outer profiles, correlation, and timeout behavior. A real `script`-owned
pseudoterminal freezes exact CSI relay and the missing XTGETTCAP/DECRQSS ST
bytes on installed GNU screen; a missing executable is an explicit platform
skip.

## Sources

- [GNU screen 5.0.0 control sequences](https://www.gnu.org/software/screen/manual/html_node/Control-Sequences.html)
  defines the supported DCS relay boundary.

Source accessed 2026-07-28.

## Expected behavior

| Layer          | Required evidence                                                               |
| -------------- | ------------------------------------------------------------------------------- |
| Writer         | Exact DCS relay bytes, size bounds, and rejection of unrepresentable nested ST. |
| Discovery      | Conservative filtering without an explicit approved outer profile.              |
| Pseudoterminal | Installed-screen relay behavior or an explicit platform skip.                   |
