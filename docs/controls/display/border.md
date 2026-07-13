# Border

## Border contract

`Border` contains zero or one child and draws typed border glyphs, colors,
background, and padding around it. Theme resolution and visual states follow the
shared [styling contract](../../concepts/styling.md#styling-contract).

## API

- `Child` uses managed parent ownership; assigning an already parented control,
  itself, or an ancestor throws before changing the previous child.
- `BorderThickness` supports zero or one terminal cell per edge in the first
  milestone.
- `Padding` is non-negative `Thickness` inside the border.
- `Glyphs` is a validated set of grapheme clusters, each exactly one cell wide.
- `BorderColor`, `Background`, and `Attributes` are nullable style-property
  overrides resolved through the theme cascade.

`BorderThickness` defaults empty and validates every edge is zero or one before
mutation. `Glyphs.Default` aliases the light Unicode set. Heavy, paired-line,
rounded, ASCII, full-block, and light, medium, and dark shade presets are also
available; custom `Rune` values must measure as one printable cell under the
default narrow ambiguous-width policy. When the inherited policy treats a
configured Unicode segment as two cells, rendering substitutes `+`, `-`, or `|`
for that physical corner or edge. The public `Glyphs` value is unchanged; only
the terminal presentation degrades.

Measure reserves active border edges around the child's margin-inclusive
request; base padding is then added by the shared box model. Arrange deflates
padding and active edges before committing the capacity-one child slot. Render
fills the clipped border box first, draws only active complete edges/corners,
and then lets normal child rendering cover its interior. Zero and tiny bounds
saturate without negative geometry or half glyphs.

Thickness, padding, glyph width, and child changes invalidate measure. Color and
attribute changes invalidate render.

## Example

```csharp
var card = new Border
{
    Child = new Text("Details"),
    BorderThickness = Thickness.One,
    Padding = new Thickness(1),
};
```

## Test obligations

Cover no child, replacement and failed replacement atomicity, every edge
combination, zero/tiny bounds, clipping, invalid wide/empty glyphs, Unicode
child content, style states, resize, ownership cleanup, and exact cells.
