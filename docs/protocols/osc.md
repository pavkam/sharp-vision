# Operating System Commands

## Overview

OSC uses `ESC ]`, a numeric selector, semicolon-delimited content, and a string
terminator. SharpVision emits `ST` (`ESC \`) by default. A compatibility option
may accept BEL termination, but embedded BEL is never part of a payload.

Primary modern behavior is cross-checked against
[xterm control sequences](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html),
accessed 2026-07-11.

Payloads are bounded before allocation. Text is UTF-8. Control characters,
invalid Base64, invalid color syntax, and unterminated payloads generate
diagnostics and deterministic recovery. Diagnostics redact clipboard data and
sensitive query responses.

## Supported features

Typed support covers selectors 0/2 for titles, 4/10/11 for palette/default color
queries where capabilities allow, 8 for hyperlinks, 9/777 for desktop
notifications, 52 for clipboard text, and 5522 through the dedicated
[Kitty clipboard contract](kitty-clipboard.md#overview). Shell-integration
prompt marks (OSC 133) and working-directory reports (OSC 7) are out of scope:
they describe a shell session rather than a full-screen application.

Desktop notifications are opt-in only: there is no reliable environment or query
signal for OSC 9 / OSC 777 support, so `Application.Terminal.Notifications` is
authorized exclusively by an explicit `CapabilityOverrides.Notifications`
override, never by environment or default evidence.

## Typed API and behavior

`Osc` implements selectors 0 and 2 for titles, selector 8 hyperlink open/close,
selectors 9 and 777 for desktop notifications, selector 4 palette queries,
selectors 10 and 11 default-color queries, and the selector 1337 iTerm2
capability query. `Osc.Notify(writer, body)` writes a bare OSC 9 payload;
`Osc.Notify(writer, title, body)` writes an OSC 777 payload framed as
`notify;title;body`, the urxvt/foot convention. Because the urxvt/foot receiver
splits that payload once after the `notify;` prefix, `title` rejects a literal
`;` byte with an `ArgumentException` to avoid shifting the title/body boundary
and truncating the title; `body` permits `;`, since everything after the first
split is treated as body. The raw `ProtocolWriter` validates the complete
payload before advancing an `IBufferWriter<byte>` and always emits ST.

`XtermResponses.TryOsc` decodes one bounded OSC 4 index/color pair or one OSC
10/11 default color into an immutable `PaletteResponse`. Palette indices are
limited to 0 through 255. Each RGB component contains one through four
hexadecimal digits and is normalized to a 16-bit component before the typed
value exists. Decimal index parsing rejects overflow before arithmetic, discards
the malformed OSC value, and recovers at the next parser boundary.

Negotiation publishes default colors for diagnostics and opt-in application
theme adaptation. It does not silently replace semantic theme colors or change
the active terminal color-depth capability.

`Osc52` implements typed clipboard/primary/secondary/select/cut-buffer text,
strict canonical Base64, UTF-8 validation, query payloads, owned decode results,
and ST/BEL parser integration. OSC 5522 is handled separately because it is a
correlated, MIME-aware transaction protocol with chunking, permissions, and
paste events rather than a single-payload text write, through
`Kitty.Clipboard.KittyClipboardPacket`, `Kitty.Clipboard.KittyClipboardWriter`,
and `Kitty.Clipboard.KittyClipboardTransaction`.

## Security and tests

Hyperlink targets and terminal replies are untrusted data. APIs do not execute
or automatically open them. Tests cover ST/BEL input, split terminators, payload
bounds, invalid UTF-8/Base64, redaction, and recovery into following
text/control events.

## Sources

- [XTerm Control Sequences, patch level 410](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  defines the supported OSC selectors and BEL/ST compatibility behavior.

Source accessed 2026-07-28.

## Expected behavior

| Layer    | Required evidence                                                             |
| -------- | ----------------------------------------------------------------------------- |
| Encoder  | Exact selectors, delimiters, UTF-8/Base64 content, bounds, and ST output.     |
| Parser   | BEL/ST input, every split, malformed/oversized recovery, and following input. |
| Security | Control rejection, payload redaction, no target execution, and owned values.  |
