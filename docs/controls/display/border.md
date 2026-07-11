# Border

## Border contract

`Border` contains zero or one child and draws typed border glyphs, colors,
background, and padding around it.

## API

- `Child` uses managed parent ownership; assigning an already parented control,
  itself, or an ancestor throws before changing the previous child.
- `BorderThickness` supports zero or one terminal cell per edge in the first
  milestone.
- `Padding` is non-negative `Thickness` inside the border.
- `Glyphs` is a validated set of grapheme clusters, each exactly one cell wide.
- Border/background style values may inherit from resources.

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
