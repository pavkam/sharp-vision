# SharpVision Terminal Debugger

Terminal Debugger is a live, interactive dashboard for answering two different
questions about the terminal in which it runs:

1. What does SharpVision's discovery pipeline believe the terminal supports?
2. What has this session actually observed or confirmed working?

Those answers deliberately remain separate. An environment hint can be useful
without being authoritative, and advertised support can still be broken by a
multiplexer, permissions, configuration, or terminal defect.

## Run it

```bash
dotnet run --project examples/TerminalDebugger/TerminalDebugger.csproj
```

The host treats Ctrl+C as decoded input so the keyboard inspector can display
it. Press Ctrl+Q to quit.

## Dashboard

The **Capabilities** tab lists every optional protocol in the active
`TerminalCapabilities` profile. Each row shows:

- detected state: Supported, Tentative, Unsupported, or Unknown;
- evidence origin: default, environment, terminal database, active query, or
  caller override;
- whether the evidence authorizes optional output;
- live verification: Not run, Observed, Passed, or Failed;
- a plain-language description of what the protocol does.

The header also reports the terminal-description identity, cell size, color
depth, Unicode version, ambiguous-width policy, and public title, bell,
clipboard, and notification service availability.

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
- Kitty clipboard paste notifications and completed clipboard replies.

Control characters are written as explained tokens such as `\e [ESC]`,
`\r [CR]`, and `\n [LF]`. Dynamic values are escaped before entering SharpVision
text markup, so input that resembles a color tag remains literal. The timeline
keeps the newest 500 records. Pause drops new events instead of building a
hidden queue, and Clear releases the retained records. Individual text and byte
renderings are capped at 4,096 units and state the original byte count when
truncated.

The terminal's one-time Kitty clipboard credential is always redacted. Clipboard
content itself remains visible because this is an explicitly launched local
diagnostic application; it is never written to a file by Terminal Debugger.

## Explicit tests

Nothing in the **Tests** tab runs on startup. Buttons explicitly request:

- the terminal bell;
- a temporary window-title change;
- a desktop notification when authoritatively enabled;
- a clipboard write/read round trip after first reading content for restoration;
- color, rendition, underline, and Unicode cell-geometry specimens;
- a generated RGBA checkerboard through SharpVision's public `Image` control and
  the active authorized graphics backend.

Visual and audible checks require Pass or Fail confirmation. Clipboard compares
a unique marker automatically and restores the previous text when the initial
read succeeded. If the initial clipboard read fails, the debugger refuses to
overwrite the clipboard. Graphics fallback reasons are reported through the
public application diagnostic event rather than inferred from a missing image.

## Status meanings

| Status   | Meaning                                                         |
| -------- | --------------------------------------------------------------- |
| Not run  | No event, automatic comparison, or user confirmation exists.    |
| Observed | A matching passive decoded event arrived in this session.       |
| Passed   | An exact comparison or explicit user confirmation succeeded.    |
| Failed   | A comparison failed, the terminal rejected it, or the user did. |

Terminal Debugger uses only public SharpVision APIs. It does not sniff raw
escape bytes, bypass capability authorization, or turn uncertain evidence into a
claim of support.
