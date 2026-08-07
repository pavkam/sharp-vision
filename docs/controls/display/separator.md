# Separator

## Overview

`Separator` draws a single non-interactive horizontal or vertical divider line.
It cannot receive focus, is excluded from hit testing, and owns no children.

## API

| Property            | Default              | Purpose                                                                     |
| ------------------- | -------------------- | --------------------------------------------------------------------------- |
| `Orientation`       | `Horizontal`         | Draws a line across the first content row or down the first content column. |
| `Style`             | `null`               | Optional complete developer-authored `SeparatorStyle`.                      |
| `ActualStyle`       | Theme                | The resolved style; always present.                                         |
| Inherited alignment | `Stretch`, `Stretch` | Lets the parent determine the final line length.                            |

`Style`/`ActualStyle` (`SeparatorStyle`) own `HorizontalGlyph` and
`VerticalGlyph` - the required validated one-cell glyph for each orientation -
alongside the inherited `Face`/`Border`/`Shadow`. A `with` expression creates a
validated member-wise copy of `SeparatorStyle.Default`; assigning `null` to
`Style` restores the Theme-owned presentation.

## Behavior

- `Orientation` selects horizontal or vertical drawing and defaults to
  `Horizontal`.

A horizontal separator repeats the code-owned horizontal-separator glyph from
left to right; a vertical separator repeats the code-owned vertical-separator
glyph from top to bottom.

The intrinsic desired size is one cell by one cell. Because both inherited
alignment axes default to `Stretch`, parent layout determines the final line
length: horizontal drawing fills the first content row and vertical drawing
fills the first content column. Either alignment can be replaced normally. Zero
content bounds draw nothing. Changing the orientation invalidates measure and
rendering — the active axis changes even though the intrinsic size stays one
cell by one cell.

By default the line uses the normal theme border color, combined with the
resolved visual-state style and the inherited semantic cell policy. Separator
participates in shared intrinsic chrome when border, body fill, or shadow
properties are set, and draws its line inside `ContentBounds`. It never handles
pointer or keyboard input.

## Glyphs

`SeparatorStyle.HorizontalGlyph` and `SeparatorStyle.VerticalGlyph` are
validated one-cell runes. Without a local `Style`, `Separator` resolves them
from the active theme, falling back to the code-owned separator glyph defaults.

## Example

![The Separator control rendered in the live showcase](../../images/controls/separator.png)

```csharp
var separator = new Separator
{
    Orientation = Orientation.Horizontal,
};
```

## Expected behavior

Callers can rely on the following: horizontal and vertical lines render as
documented; zero bounds draw nothing; orientation changes, resize, and
appearance inheritance behave as described; the control stays out of hit
testing; and the rendered output matches exact final cells.
`SeparatorSurfaceTests` drives pointer movement, dispatcher-affine mutation, and
terminal resize through a mounted application while asserting exact
terminal-visible rows and representative styles.
