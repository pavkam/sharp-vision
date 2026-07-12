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
reverse, hidden, strike, and their standard group resets. `Color` supports
default, indexed 0-255, and RGB foreground/background output. `BasicColor`
separately models the sixteen ANSI/aixterm entries and emits 30-37, 40-47,
90-97, or 100-107 rather than pretending basic colors require the indexed-color
extension.

The frame encoder consumes one immutable capability snapshot. True-color output
preserves RGB, indexed-256 output projects RGB into the xterm-compatible cube or
grayscale ramp, basic-16 output projects through a documented xterm reference
palette and emits `BasicColor`, and monochrome output omits color selection.
Squared sRGB distance with a lower-index tie break makes degradation stable; the
physical first-sixteen palette remains terminal-configurable and therefore
cannot promise exact RGB. Transition minimization compares projected styles so
different semantic colors that collapse to one terminal color emit one SGR
transition.

The degradation reference entries for indices 0-15 are, in order: `#000000`,
`#cd0000`, `#00cd00`, `#cdcd00`, `#0000ee`, `#cd00cd`, `#00cdcd`, `#e5e5e5`,
`#7f7f7f`, `#ff0000`, `#00ff00`, `#ffff00`, `#5c5cff`, `#ff00ff`, `#00ffff`, and
`#ffffff`. Indices 16-231 use xterm's levels 0, 95, 135, 175, 215, and 255 in a
6x6x6 cube. Indices 232-255 use `8 + 10n` grayscale levels. Projection compares
squared sRGB distance and selects the lower index on a tie.

Every shipped rendition and color form has independent exact-byte coverage. The
low-level protocol API also emits typed underline variants through `4:0` to
`4:5`, underline default/indexed/RGB color through 59 and 58, rapid blink
through 6, and overline enable/disable through 53/55. The semantic frame model,
capability projection, and report-backed optional-style evidence remain renderer
work; low-level availability alone is not a support claim for controls.

## Tests

Exact bytes cover every attribute on/off pair, combined states, default/indexed
/RGB colors, unsupported fallback, reset interactions, and transitions between
frames. A virtual-terminal model proves the resulting style state.

## Sources

- [ECMA-48 5th edition (June 1991)](https://www.ecma-international.org/wp-content/uploads/ECMA-48_5th_edition_june_1991.pdf),
  section 8.3.117, for standard SGR rendition and group-reset semantics.
- [xterm control sequences, patch 410 (2026-04-19)](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  for ANSI/aixterm basic colors and indexed/direct-color forms.
- [xterm color FAQ](https://invisible-island.net/xterm/xterm.faq.html) for the
  configurable first-sixteen entries, 6x6x6 cube, grayscale ramp, and the lack
  of a universal physical terminal palette.
