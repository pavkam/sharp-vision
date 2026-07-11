# xterm compatibility

## xterm contract

Primary source:
[XTerm Control Sequences, patch 410](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html),
accessed 2026-07-11. The source combines ECMA, DEC, and xterm extensions; each
SharpVision API retains that origin.

xterm is the modern compatibility baseline for alternate screen, bracketed
paste, focus reporting, SGR mouse including pixel mode, OSC colors/titles,
hyperlinks, device attributes, and selected mode queries. A `TERM` value
containing `xterm` is a hint, not proof of every extension.

## First milestone contract

Implement only sequences used by the renderer, input decoder, and capability
detector. Obsolete X10/UTF-8/urxvt mouse encodings are decoded where safe but
not preferred for output. Highlight tracking is unsupported because it can block
a non-cooperating terminal.

## Quirks and tests

Tests distinguish DEC originals from xterm private modes, verify exact query
responses, and exercise conservative behavior through tmux, GNU screen, SSH, and
misleading `TERM` values.
