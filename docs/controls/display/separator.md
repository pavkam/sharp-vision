# Separator

## Separator contract

`Separator` draws one non-interactive horizontal or vertical divider line. It
cannot receive focus, is excluded from hit testing, and owns no children.

## API

- `Orientation` controls horizontal or vertical drawing. Default is
  `Horizontal`.
Horizontal separators repeat the built-in `─` cell from left to right. Vertical
separators repeat the built-in `│` cell from top to bottom. These cells are
fixed presentation rather than caller-configurable glyph state.

The intrinsic desired size is one cell by one cell. Both inherited alignment
axes default to `Stretch`, and parent layout determines the final line length:
horizontal drawing fills the first content row and vertical drawing fills the
first content column. Callers may replace either alignment normally. Zero
content bounds draw nothing. Orientation changes require measure and rendering
because the active axis changes while intrinsic size remains one cell by one
cell.

The line uses the resolved visual-state style and inherited semantic cell
policy. Separator participates in shared intrinsic chrome when callers set
border, body fill, or shadow properties, and draws its line inside
`ContentBounds`. It never handles pointer or keyboard input.

## Example

```csharp
var separator = new Separator
{
    Orientation = Orientation.Horizontal,
};
```

## Test obligations

Cover horizontal and vertical rendering, zero bounds, orientation changes,
resize, appearance inheritance, non-interactive hit testing, and final cells.
`SeparatorSurfaceTests` must drive pointer movement, dispatcher-affine mutation,
and terminal resize through a mounted application while asserting exact
terminal-visible rows and representative styles.
