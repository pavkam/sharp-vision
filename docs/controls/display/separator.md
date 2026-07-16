# Separator

## Separator contract

`Separator` draws one non-interactive horizontal or vertical divider line. It
cannot receive focus, is excluded from hit testing, and owns no children.

## API

- `Orientation` controls horizontal or vertical drawing. Default is
  `Horizontal`.
- `HorizontalGlyph` is the printable one-cell Rune repeated left-to-right for a
  horizontal line. Default is `─`.
- `VerticalGlyph` is the printable one-cell Rune repeated top-to-bottom for a
  vertical line. Default is `│`.

Both glyph setters validate before mutation. A control Rune or a grapheme that
is not exactly one cell under the narrow policy throws `ArgumentException`. If a
configured glyph becomes wide under the inherited ambiguous-width policy,
rendering preserves the configured property and substitutes portable `-` or `|`
cells for that frame.

The intrinsic desired size is one cell by one cell. Both inherited alignment
axes default to `Stretch`, and parent layout determines the final line length:
horizontal drawing fills the first content row and vertical drawing fills the
first content column. Callers may replace either alignment normally. Zero
content bounds draw nothing. Orientation and glyph changes require rendering but
do not change intrinsic size.

The line uses the resolved visual-state style and inherited semantic cell
policy. Separator participates in shared intrinsic chrome when callers set
border, body fill, or shadow properties, and draws its line inside
`ContentBounds`. It never handles pointer or keyboard input.

## Example

```csharp
var separator = new Separator
{
    Orientation = Orientation.Horizontal,
    HorizontalGlyph = new Rune('─'),
};
```

## Test obligations

Cover horizontal and vertical rendering, zero bounds, orientation changes,
resize, style inheritance, invalid and ambiguous-width glyphs, non-interactive
hit testing, and final cells. `SeparatorSurfaceTests` must drive pointer
movement, dispatcher-affine mutation, and terminal resize through a mounted
application while asserting exact terminal-visible rows and representative
styles.
