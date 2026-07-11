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

## Tests

Exact bytes cover every attribute on/off pair, combined states, default/indexed
/RGB colors, unsupported fallback, reset interactions, and transitions between
frames. A virtual-terminal model proves the resulting style state.
