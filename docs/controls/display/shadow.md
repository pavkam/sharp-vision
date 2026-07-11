# Shadow

## Shadow contract

`Shadow` is a capacity-one decorator that draws visual overflow without
reserving layout space or expanding pointer hit testing.

## API

- `Child` uses normal managed ownership and may be null.
- `Mode` selects `Composite` or `BlockGlyph` behavior.
- `Offset` is a signed cell offset and defaults to `(2, 1)`.
- `Glyph` is a printable one-cell `Rune` and defaults to dark shade `▓`.
- `Foreground`, `Background`, and `Attributes` optionally override the resolved
  appearance. Attributes default to `Dim`.

Composite mode preserves every underlying grapheme and replaces its semantic
style. Touching a wide grapheme styles its complete owner only when that owner
is inside the effective ancestor clip. Block-glyph mode replaces cells with the
configured Rune.

The footprint is the shifted arranged rectangle minus the original rectangle.
Positive offsets produce the familiar right and bottom Turbo Vision strips;
negative offsets produce top and left strips. Offset does not affect desired
size or the child slot. Resize and arrange recompute the footprint from current
bounds.

## Example

```csharp
var card = new Shadow
{
    Child = new Border
    {
        Child = new Text("Details"),
        BorderThickness = new Thickness(1),
    },
    Mode = ShadowMode.Composite,
    Offset = new Point(2, 1),
    Background = Color.Indexed(0),
};
```

## Test obligations

Cover ownership, both modes, positive and negative offsets, zero/tiny bounds,
ancestor clipping, wide graphemes, overlapping siblings, z-order, hit testing,
style inheritance, resize, and exact semantic cells.
