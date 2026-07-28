# Select Graphic Rendition

## SGR contract

SGR is `CSI Pm m`. Empty or zero resets rendition. SharpVision models attributes
and colors as typed values, then emits the smallest unambiguous transition
supported by the effective capabilities.

The model includes bold, faint, italic, underline variants/color, blink,
inverse, conceal, strike, overline, 16-color, 256-color, RGB, and default
foreground/background. Unsupported attributes degrade by omission; colors
degrade through the configured palette strategy.

Reset 22 clears bold and faint together in common terminals. The encoder must
not pretend it can independently clear one without an extension such as the
[Kitty independent intensity controls](https://sw.kovidgoyal.net/kitty/misc-protocol/).

## First milestone contract

Provide deterministic full-style and transition encoders, semantic equality, and
a conservative reset path. Colon and semicolon extended-color forms are decoded;
output uses the profile's preferred form.

## Phase 2 implementation

`Sgr` currently emits reset, bold, dim, italic, underline, slow/rapid blink,
reverse, hidden, strike, and their standard group resets. Public `Color`
supports default and RGB foreground/background output. `BasicColor` separately
models the sixteen ANSI/aixterm wire entries and emits 30-37, 40-47, 90-97, or
100-107. Bounded 256-color positions exist only in internal encoder-to-SGR
methods.

The frame encoder consumes one immutable capability snapshot. Cells retain RGB.
True-color output preserves RGB, indexed-256 output privately projects RGB into
the xterm-compatible cube or grayscale ramp, basic-16 output projects through a
documented xterm reference palette and emits `BasicColor`, and monochrome output
omits color selection. Squared sRGB distance with a lower-index tie break makes
degradation stable; the physical first-sixteen palette remains
terminal-configurable and therefore cannot promise exact RGB. Transition
minimization compares projected styles so different RGB colors that collapse to
one terminal color emit one SGR transition.

The degradation reference entries for indices 0-15 are, in order: `#000000`,
`#cd0000`, `#00cd00`, `#cdcd00`, `#0000ee`, `#cd00cd`, `#00cdcd`, `#e5e5e5`,
`#7f7f7f`, `#ff0000`, `#00ff00`, `#ffff00`, `#5c5cff`, `#ff00ff`, `#00ffff`, and
`#ffffff`. Indices 16-231 use xterm's levels 0, 95, 135, 175, 215, and 255 in a
6x6x6 cube. Indices 232-255 use `8 + 10n` grayscale levels. Projection compares
squared sRGB distance and selects the lower index on a tie.

Every shipped rendition and color form has independent exact-byte coverage. The
low-level protocol API also emits typed underline variants through `4:0` to
`4:5`, underline default/RGB color through 59 and 58, rapid blink through 6, and
overline enable/disable through 53/55. Capability projection may select the
internal `58;5` form without publishing an indexed `Color`.

The semantic `Rendering.Style` stores rapid blink and overline attributes plus a
typed `Underline` and independent `UnderlineColor`. Legacy
`Attributes.Underline` continues to mean standard SGR 4. Slow and rapid blink
are mutually exclusive; legacy and typed underline selections are mutually
exclusive; an underline color requires an active legacy or typed underline.
Public constructors reject an invalid combination before publishing state.

Styled underline variants, underline color, and overline require a supported
capability. Unknown, tentative, or unsupported styled underlines degrade to
legacy straight underline. Unsupported underline color and overline are omitted.
Rapid blink is standard ECMA-48 output and does not require extension evidence.
Projection affects only emitted bytes: cells retain their richer semantic style
so a later capability refresh and full redraw can improve presentation.

## Tests

Exact bytes cover every attribute on/off pair, typed underline variant,
default/RGB and internally projected underline color, unsupported fallback,
reset interaction, and frame transition. The independent virtual-terminal model
parses `4:x`, 58, 59, 53, 55, slow/rapid blink, and their resets. Targeted and
fixed-seed random frame transitions prove the resulting complete semantic style
state.

## Sources

- [ECMA-48 5th edition (June 1991)](https://www.ecma-international.org/wp-content/uploads/ECMA-48_5th_edition_june_1991.pdf),
  section 8.3.117, for standard SGR rendition and group-reset semantics.
- [xterm control sequences, patch 410 (2026-04-19)](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  for ANSI/aixterm basic colors and indexed/direct-color forms.
- [xterm color FAQ](https://invisible-island.net/xterm/xterm.faq.html) for the
  configurable first-sixteen entries, 6x6x6 cube, grayscale ramp, and the lack
  of a universal physical terminal palette.

## Test obligations

| Layer     | Required evidence                                                                               |
| --------- | ----------------------------------------------------------------------------------------------- |
| Value     | Rendition validation, mutually exclusive groups, typed underline, and deterministic projection. |
| Encoder   | Exact basic/indexed/RGB/default/reset bytes and redundant-transition suppression.               |
| Rendering | Final terminal style matches semantic cells across full and incremental frames.                 |
