# Text

## Text contract

`Text` is a non-focusable display control that formats Unicode text and applies
semantic terminal style through compact inline markup. It derives from
`Control`, draws only to the semantic cell canvas, and never emits terminal
protocol bytes. Markup is the only styled-text authoring surface; there is no
mutable run or inline object model.

## API and defaults

- `Content` is a non-null markup string and defaults to empty. Assigning null
  throws `ArgumentNullException` before mutation. Unknown or malformed markup
  renders literally rather than throwing.
- `Overflow` defaults to `Visible`. `Wrap` prefers whitespace and falls back to
  grapheme boundaries; `WrapAnywhere` breaks only between graphemes; `Clip`
  keeps the complete clusters that fit; `Ellipsis` reserves the ellipsis width,
  prefers a word boundary, and falls back to a grapheme boundary; `Visible`
  preserves each complete logical line and reports its full cell width.
- `TextAlignment` defaults to `Start` and places each formatted line at start,
  center, or end within its arranged content width.
- `AmbiguousWidth` inherits the application cell policy until explicitly set. An
  explicit narrow or wide value remains local to the control.
- `Lines` exposes the committed visible-text line metrics as
  `ReadOnlyMemory<Line>` until the next successful layout.
- `Text.Escape(string)` escapes dynamic visible text for interpolation into
  markup. A null argument throws `ArgumentNullException`.

Unknown enum values throw `ArgumentOutOfRangeException` before observable state
changes. Attached-control mutation remains dispatcher-affine and may throw the
base control's documented `InvalidOperationException` or
`ObjectDisposedException`.

Content, overflow, and ambiguous-width changes invalidate measure. Alignment
changes invalidate arrange. Resolved control style, theme, or visual-state
changes invalidate render through the base styling contract.

## Markup grammar

A tag is `<name>`, `<name=value>`, `</name>`, or the generic close `</>`. Names,
named colors, theme roles, and named values are case-insensitive. One tag
controls one facet; stacking tags composes facets. `</name>` removes the nearest
still-open tag with that exact name, so independent facets may overlap. `</>`
removes the most recently opened tag.

| Facet            | Accepted tags                                                                         |
| ---------------- | ------------------------------------------------------------------------------------- |
| Bold             | `<b>`, `<bold>`                                                                       |
| Dim              | `<d>`, `<dim>`                                                                        |
| Italic           | `<i>`, `<italic>`                                                                     |
| Underline        | `<u>`, `<underline>`                                                                  |
| Strike           | `<s>`, `<strike>`                                                                     |
| Other attributes | `<reverse>`, `<blink>`, `<rapidblink>`, `<hidden>`, `<conceal>`, `<overline>`         |
| Foreground       | `<fg=value>`, `<color=value>`, or a bare color/role tag such as `<red>` or `<accent>` |
| Background       | `<bg=value>`                                                                          |
| Underline color  | `<uc=value>`                                                                          |
| Underline shape  | `<u=straight>`, `<u=double>`, `<u=curly>`, `<u=dotted>`, `<u=dashed>`                 |
| Hyperlink        | `<link=target>`, `<a=target>`                                                         |

Bare `<u>` and valued `<u=...>` tags are one underline facet. The most recently
opened underline wins until it closes; `double` maps to the semantic paired
underline. Slow and rapid blink are likewise mutually exclusive, with the most
recently opened blink tag winning. Other attribute flags combine.

Logical line breaks are literal LF, CR, or CRLF content. There is deliberately
no `<br>` tag.

### Color values

Color-valued tags accept:

- ANSI palette names `black` through `white` and `brightblack` through
  `brightwhite`; `gray` and `grey` alias `brightblack`;
- semantic roles `foreground`, `background`, `surface`, `border`, `accent`,
  `muted`, `selectionbackground`, `selectionforeground`, `error`, `warning`,
  `success`, and `info`;
- decimal palette indices from 0 through 255; or
- RGB values `#rgb` and `#rrggbb`.

Semantic roles resolve through the active theme at render time. Unresolved role
values never reach terminal cells. Parsing retains concrete colors and semantic
roles as `ThemeColor` values through each style span; `Text` resolves them only
while drawing into the frame.

### Escaping and recovery

The markup metacharacters in visible text are `<` and `\`. Write `\<` for a
literal opening angle and `\\` for a literal backslash. `>` is literal outside a
tag. `Text.Escape` performs the required escaping for dynamic visible text:

```csharp
var user = "2 < 3";
var message = new Text($"<b>Value:</b> {Text.Escape(user)}")
{
    Overflow = Overflow.Wrap,
};
```

Parsing is lenient and deterministic:

- an unknown name, bad value, nested `<`, invalid hyperlink target, or missing
  `>` preserves the complete raw fragment as visible text;
- a stray named close is ignored;
- open tags automatically end at the end of `Content`; and
- a hyperlink target must be non-empty and contain no control code unit.

Hyperlinks become OSC 8 metadata on cells but never open a URL automatically.

## Theme glyphs

`EllipsisGlyph` is a validated one-cell local override. When absent, ellipsis
overflow resolves `Theme.Glyphs.Text.Ellipsis`; width reservation remains one
cell and runtime repair uses the theme-owned fallback. `ResetEllipsisGlyph()`
clears the override.

## Unicode, layout, and rendering

The parser produces one visible string plus internal non-overlapping semantic
style spans. `SharpVision.Text.Layout.Format` accepts that visible string, a
non-negative cell width, one `Overflow`, `Alignment`, explicit `Ambiguous`, and
caller-owned `Span<Line>` storage. It returns the complete required line count
even when the destination stores only a prefix.

Each immutable `Line` records a visible-string UTF-16 `Offset` and `Length`,
rendered `Cells`, alignment `Leading`, and `HasEllipsis`. Delimiters are
excluded from slices. Empty content and trailing logical newlines produce stable
empty lines. Tabs advance to four-cell stops.

Segmentation follows the
[Unicode geometry contract](../../concepts/unicode-cell-geometry.md#unicode-cell-geometry-contract).
Wrapping, clipping, ellipsis, and drawing never split a surrogate pair, extended
grapheme cluster, or wide-cell owner. If a markup boundary occurs inside one
grapheme, the style active at the cluster's first UTF-16 code unit applies to
the complete cluster.

Markup foreground, background, attributes, underline, underline color, and
hyperlink overlay the control's resolved visual-state style. An explicit markup
background draws opaquely; otherwise text preserves an already painted surface
unless the control has an explicit `Background`. A non-null `Background`
supplies the color used by an opaque fill but does not by itself make passive
text replace the destination surface.

`Text` reparses only after content changes, reuses its owned line array, and
reformats only after visible text, width, overflow, or ambiguous-width changes.
Alignment-only changes rewrite leading-cell metrics without re-enumerating
graphemes. No pooled or borrowed parser memory crosses layout or frame
boundaries.

## Test obligations

Cover every tag and concrete or semantic color form, named and generic closes,
overlap, auto-close, complete-fragment malformed recovery, invalid links,
escaping round trips, and fixed-seed span tiling. Cover all overflow modes,
invalid values, empty and multiline text, resize reflow, alignment, tabs,
combining marks, variation selectors, ZWJ emoji, wide cells, invalid UTF-16, and
both ambiguous-width policies.

Rendering proof includes exact semantic foreground/background/attributes, typed
underline and underline color, theme-role resolution, OSC 8 metadata, mutually
exclusive blink precedence, markup boundaries inside graphemes, transparent
background preservation, ellipsis ownership, and multi-frame terminal output.
`TextSurfaceTests` additionally mounts the control beneath a real application
and proves combining and wide-grapheme ownership, terminal-visible markup
styles, ellipsis-to-alignment mutation with stale-cell clearing, and transparent
composition over an opaque parent surface. A mounted resize proves Unicode-safe
wrap reflow and removal of the obsolete row. The showcase owns one merged `Text`
page with live markup, Unicode, overflow, hyperlink, and mutation specimens. A
warmed unchanged 80-column Unicode measure/render loop must include a
zero-allocation measured window.
