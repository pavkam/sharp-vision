# SharpVision Terminal Debugger

Terminal Debugger is a live, interactive dashboard for answering two different
questions about the terminal in which it runs:

1. What does SharpVision's discovery pipeline believe the terminal supports?
2. What has this session actually observed or compared automatically?

Those answers deliberately remain separate. An environment hint can be useful
without being authoritative, and advertised support can still be broken by a
multiplexer, permissions, configuration, or terminal defect.

## Run it

```bash
dotnet run --project examples/TerminalDebugger/TerminalDebugger.csproj
```

The host treats Ctrl+C as decoded input so the keyboard inspector can display
it. Press Ctrl+Q to quit.

## Debugger views

The **Dashboard** tab presents connection, backend, live input-mode, and public
service cards. Each card is a real `Table`, so labels and values remain aligned
without padding one large formatted string. It separates the terminal
description from the fixed VT, xterm, Kitty, or iTerm2 backend identity and
shows configured, authorized, versus successfully activated runtime modes.

The **Features** tab lists the complete protocol surface implemented by
SharpVision. This includes the description database, ECMA-48/ANSI/VT framing,
CSI, OSC, DCS and other bounded strings, DEC modes, SGR, Unicode, key maps,
active query families, tmux and GNU screen routing, plus every optional protocol
in `TerminalCapabilities`. A selectable `Table` shows:

- detected state: Supported, Tentative, Unsupported, or Unknown;
- evidence origin: default, environment, terminal database, active query, or
  caller override;
- whether the session observed the feature or compared it automatically.

Selecting a row opens a semantic `Document` with authorization, session
evidence, and a plain-language explanation of the feature.

The **Discovery** tab shows every final normalized `TerminalQueryDiagnostics`
field. A missing reply remains a dash rather than being rewritten as
unsupported. Color, geometry, private-mode, keyboard, clipboard, rendition, and
graphics results stay visible after startup instead of disappearing with the
negotiator. XTGETTCAP values are reduced to approved capability names before
publication.

The **Routing** tab explains detected multiplexer layers, the explicit outer
profile, passthrough visibility, approved operation families, route bounds, and
the effective decisions for capability queries, clipboard, string terminators,
and graphics. Detecting tmux or GNU screen never silently enables passthrough.

## Input events

The **Input events** tab records the public decoded events delivered to the
SharpVision control tree:

- key press, repeat, and release transitions, including logical and native
  identities, modifiers, shifted identity, and base-layout identity;
- independent Unicode text input with scalar and UTF-8 representations;
- pointer action, buttons, cell, local-cell, and pixel coordinates, motion,
  inferred-coordinate state, wheel deltas, modifiers, and click count;
- bracketed-paste payloads with byte and rune counts, escaped controls, and a
  hexadecimal/UTF-8 representation;
- terminal focus changes and committed cell/pixel resize information;
- Kitty clipboard paste notifications and completed clipboard replies;
- redacted runtime recovery diagnostics and graphics fallback reasons;
- typed device-attribute, private-mode, palette, geometry, status-string, and
  capability-string replies observed after attachment.

Control characters are written as explained tokens such as `\e [ESC]`,
`\r [CR]`, and `\n [LF]`. Dynamic values are escaped before entering SharpVision
text markup, so input that resembles a color tag remains literal. The timeline
keeps the newest 500 records. Pause drops new events instead of building a
hidden queue, and Clear releases the retained records. Individual text and byte
renderings are capped at 4,096 units and state the original byte count when
truncated.

The terminal's one-time Kitty clipboard credential is always redacted. Clipboard
reply content is also redacted from the general timeline: it reports protocol,
selection, MIME type, byte count, result, and diagnostics without retaining the
payload. The explicit round-trip test compares text only in memory and clears
its references when the restoration check finishes.

## Test lab

The **Test lab** renders its visual specimens immediately. Color swatches,
rendition attributes, styled and colored underlines, overline, Unicode cell
geometry, and the generated graphics sample appear beside detected support and
an exact description of the expected result. Synchronized output is already in
use when authorized, so resizing and switching tabs is the test—there is no
separate “show” action.

Buttons remain only for tests that intentionally cause a side effect:

- the terminal bell;
- a window-title change, with an explicit warning that the previous title cannot
  be read or restored through the public API;
- a desktop notification when authoritatively enabled;
- a clipboard write/read round trip after first reading content for restoration.

The same view gives exact passive test instructions for focus, bracketed paste,
cell/pixel mouse, Kitty keyboard, and xterm modifyOtherKeys. Their active state
comes from the runtime diagnostics snapshot, and matching decoded events update
the protocol row to Observed.

Visual inspection is deliberately not stored as a synthetic Pass or Fail state:
the specimen and its expectation stay visible, so the human can judge the output
directly. Clipboard compares a unique marker automatically, restores the
previous text when the initial read succeeded, and reads it back before claiming
restoration. If the initial clipboard read fails, the debugger refuses to
overwrite the clipboard. Graphics fallback reasons come from the public
application diagnostic event and become automatic failure evidence. The selected
`CellFallback`, `Kitty`, or `NonRetained` renderer backend is reported directly
by `Application.TerminalDiagnostics`.

## tmux

Inside tmux the debugger diagnoses the inner `tmux-256color` terminal by default
and shows the detected `Tmux` layer in Routing. That detected layer is not an
outer-terminal identity: capability-query, clipboard, and graphics passthrough
remain visibly blocked unless the host supplied an explicit outer profile and
approved operations. This distinction makes “the terminal supports it” and “the
current route can safely carry it” independently inspectable.

## Session evidence meanings

| Status                 | Meaning                                                   |
| ---------------------- | --------------------------------------------------------- |
| Not exercised          | No matching event or automatic comparison exists.         |
| Observed live          | A matching passive decoded event arrived in this session. |
| Compared successfully  | An exact automatic comparison succeeded.                  |
| Automatic check failed | A comparison, request, or selected graphics route failed. |

Terminal Debugger uses only public SharpVision APIs. It does not sniff raw
escape bytes, bypass capability authorization, expose environment values or
clipboard payloads, or turn uncertain evidence into a claim of support.
