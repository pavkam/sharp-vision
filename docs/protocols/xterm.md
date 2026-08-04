# xterm compatibility

## Overview

Primary source:
[XTerm Control Sequences, patch 410](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html),
updated 2026-04-19 and accessed 2026-07-20. The source combines ECMA, DEC, and
xterm extensions; each SharpVision API retains that origin.

xterm is the modern compatibility baseline for alternate screen, bracketed
paste, focus reporting, SGR mouse including pixel mode, OSC colors/titles,
hyperlinks, device attributes, and selected mode queries. A `TERM` value
containing `xterm` is a hint, not proof of every extension.

## Supported features

SharpVision implements only the sequences used by the renderer, input decoder,
and capability detector. Obsolete X10/UTF-8/urxvt mouse encodings are decoded
where safe but not preferred for output. Highlight tracking is unsupported
because it can block a non-cooperating terminal.

## Status and capability queries

`XtermDecrqss.Query` emits `DCS $ q Pt ST` only for SGR (`m`), cursor style
(`SP q`), vertical and horizontal margins (`r` and `s`), and the selected xterm
`>4m`/`>4f` keyboard status names. The router promotes structurally valid
`DCS 1 $ r Pt ST` and `DCS 0 $ r ST` replies to owned `StatusResponse` values.
An otherwise valid but unrecognized returned CSI body remains observable as an
unknown typed response plus a redacted unsupported diagnostic; malformed input
does not consume the next parser event.

Recognized replies use exact returned CSI grammar. modifyOtherKeys is only
`>4;Pv m` with `Pv` from zero through three; formatOtherKeys is only `>4;Pv f`
with `Pv` zero or one. Cursor style is one value from zero through six followed
by `SP q`; margin replies contain exactly two positive decimal values. SGR
accepts only decimal, semicolon, and colon parameter grammar. Selector spoofs,
extra parameters, and out-of-range values are valid-but-unknown diagnostics,
never recognized identities. Public `StatusResponse` construction enforces the
same name/value grammar; an identity-less failure has unknown name and empty
value.

`XtermGetCap.Query` emits `DCS + q Pt ST` for a finite public name enumeration.
Names and values in `DCS 1 + r Pt ST` are strict two-digit hexadecimal,
semicolon-delimited pairs. `QueryLimits.MaxCapabilityItems` and
`QueryLimits.MaxCapabilityValueBytes` bound retained state. Duplicate names, odd
or non-hex fields, unknown names, trailing separators, and oversized values are
rejected as typed capability evidence. The allowlist contains `Co`, `TN`, `RGB`,
and selected special-key capabilities; it cannot name environment variables or
arbitrary resources. Startup asks only for `RGB` when xterm is a hint,
authoritative description color evidence is absent, and `Settings.ColorDepth` is
not explicit. Suppression emits no bytes and consumes no tracker slot. A
validated positive reply may replace a default or environment-only color
heuristic with true color and `Origin.Query`; it never replaces database,
existing query, or override evidence, installs returned bytes as a terminfo
program, or mutates the active key map.

Both DCS families retain the exact requested selector or capability name in
`QueryTracker` and its bounded duplicate/late grace history. A valid reply for a
different identity is observable but cannot consume the active request.
Identity-less `0$r` and `0+r` replies likewise remain unknown until the exact
request expires. Generic family-only DCS registration is rejected. The existing
typed tmux unwrap seam can restore a wrapped representative reply before
routing; general multiplexer policy remains owned by the tmux/screen contract.

## Enhanced keyboard fallback

`XtermModifyOtherKeys` encodes query `CSI ? 4 m`, level set `CSI > 4 ; Pv m`,
and initial-resource restore `CSI > 4 m`. The input decoder recognizes the
legacy `CSI 27 ; modifier ; key ~` form and the compatible
`CSI key ; modifier u` form as typed strokes, with the existing Kitty CSI-u
grammar retaining precedence. Runtime negotiation probes `>4m` through DECRQSS
only for an xterm hint when Kitty is not hinted; on an approved outer route,
that hint is the route's own outer-terminal identity rather than the inner
pane's `TERM` (see #260). An unrouted native Windows connection accepts the
built-in `windows-vt` description name as the same hint, since `TERM` is
essentially never set there and conhost answers an unrecognized DECRQSS status
with a safe negative reply. A matched reply produces query-origin
`XtermKeyboard` evidence. `Session` prefers an authorized Kitty keyboard lease;
otherwise it may lease a configured modifyOtherKeys level and always records the
exact initial-value restore before attempting the enable write.

## Quirks and tests

Tests distinguish DEC originals from xterm private modes, verify exact query
responses, exercise every split for both typed DCS replies, reject hostile hex
and duplicate capability fields, prove duplicate/late correlation, and exercise
conservative behavior through tmux, GNU screen, SSH, and misleading `TERM`
values.

## Sources

- [XTerm Control Sequences, Patch #410, 2026-04-19](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  defines the selected xterm extensions and compatibility behavior.

Source accessed 2026-07-28.

## Expected behavior

| Layer     | Required evidence                                                               |
| --------- | ------------------------------------------------------------------------------- |
| Queries   | Exact DA/DSR/DECRQSS/XTGETTCAP bytes, correlation, limits, and typed results.   |
| Input     | Enhanced fallback, legacy coexistence, every split, and hostile-field recovery. |
| Discovery | Terminal names remain hints across direct, SSH, tmux, and GNU screen contexts.  |
