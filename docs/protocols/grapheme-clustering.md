# Grapheme clustering

## Overview

Grapheme clustering uses DEC private mode 2027, implemented by kitty, foot,
WezTerm, Contour, and Ghostty: `CSI ? 2027 h` tells the terminal to measure
extended grapheme clusters (emoji with variation selectors, ZWJ sequences,
flags, and similar) the same way this library's own Unicode 17 tables do, and
`CSI ? 2027 l` reverts to the terminal's legacy per-scalar width behavior. It
is a terminal extension rather than an ECMA-48 guarantee and must be
capability-gated.

This library never changes its own width algorithm to match a terminal.
`Width.Measure` and the shared grapheme-segmentation pipeline documented in
[Unicode cell geometry](../concepts/unicode-cell-geometry.md#overview) are
capability-unaware by design: they always compute cells from the pinned
Unicode 17 tables. Mode 2027 exists so an authoritative terminal can be told to
agree with those tables instead, which keeps the same cell widths this library
already assumes for layout, wrapping, and cursor geometry, rather than letting
a terminal's own legacy wcwidth heuristic silently disagree and shift the rest
of the row. When the terminal does not prove support, behavior is unchanged
from before this mode existed: widths still come from this library's own
tables, and nothing is enabled.

## Supported features

The renderer negotiates mode 2027 with the same bounded DECRQM probe strategy
used for synchronized output (mode 2026), and enables it only from
authoritative evidence — a validated terminal description, a bounded query
reply, or an explicit caller override.

Unlike synchronized output, this mode is not a per-frame atomicity wrapper: it
does not need to bracket each individual render, because it does not defer
presentation the way mode 2026 does. It only needs to be asserted once so the
terminal's width interpretation matches this library's tables for the rest of
the session. The renderer therefore enables it at most once — the first time
an authoritative profile proves support, even on a frame with no other cell
damage — and disables it again during `ShutdownAsync` if it was ever enabled,
the same restoration this library applies to other global terminal-emulator
state (such as DECSCUSR cursor shape) that has no session-lease owner of its
own.

## Failure recovery

An unanswered DECRQM probe, a deadline, or a terminal that answers negatively
all leave grapheme clustering unauthoritative, so the renderer emits nothing
and behavior is identical to a terminal that predates mode 2027 entirely: this
library's own Unicode 17 tables keep deciding every cell's width. A render
that fails before its enable byte reaches the terminal is retried on the next
frame, exactly like any other unflushed output.

## Sources

- [Contour synchronized output specification](https://contour-terminal.org/vt-extensions/synchronized-output/)
  documents the sibling mode 2026 DECRQM/DECSET/DECRST convention this mode
  follows.
- [kitty terminal graphemes documentation](https://sw.kovidgoyal.net/kitty/text-sizing-protocol/)
  and the Contour, foot, WezTerm, and Ghostty release notes for DEC private
  mode 2027 document the enable/disable sequences and grapheme-cluster width
  behavior this mode negotiates.
- [ECMA-48, fifth edition, June 1991](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
  defines the underlying CSI grammar; mode 2027 itself is an extension.

Sources accessed 2026-09-07.

## Expected behavior

| Layer      | Required evidence                                                                |
| ---------- | ---------------------------------------------------------------------------------- |
| Capability | DECRQM support states, explicit override, and unsupported fallback.                |
| Renderer   | Exact mode 2027 enable bytes sent at most once; exact disable bytes at shutdown.    |
| Failure    | An unanswered probe, deadline, or negative reply never enables the mode.            |
