# FigletText

## FigletText contract

`FigletText` renders Unicode source text through an immutable `FigletFont` and
the semantic terminal canvas. Generated rows are cached until `Content`, `Font`,
or `Options` changes.

`Content` and `Font` are non-null. `Options` may override the font's direction
and layout bits. The inherited complete `Face` and `ActualFace` surfaces own
foreground, background, attributes, underline, and underline color.

Measure reports the maximum generated terminal-cell width and generated row
count under the inherited application cell policy. The control does not scale or
wrap FIGlet glyphs. Parent clipping or an ancestor `Container` with intrinsic
`AutoScroll` provides bounded presentation for large fonts.

Generated FIGlet cells preserve the destination background so the art blends
into its parent surface. Assigning a complete local `Face` with an opaque
background fills the ordinary border box before transparent FIGlet cells draw.

## API

| Property                       | Default                       | Purpose                                                                            |
| ------------------------------ | ----------------------------- | ---------------------------------------------------------------------------------- |
| `Content`                      | `string.Empty`                | Supplies the non-null source text rendered through the selected FIGfont.           |
| `Font`                         | Required constructor argument | Selects the immutable parsed `FigletFont`.                                         |
| `Options`                      | `default`                     | Overrides font direction and fitting or smushing layout bits.                      |
| Inherited `Face`, `ActualFace` | semantic role                 | Assign complete local appearance or inspect the fully resolved current appearance. |

## Example

```csharp
var title = new FigletText(FigletCatalog.Default.Load("Standard"))
{
    Content = "SharpVision",
};
```

## Test obligations

Cover null validation, cache invalidation, every catalog font, scalar fallback,
hardblanks, direction, fitting, smushing, clipping, resize, style inheritance,
wide output Runes, and exact cells. `FigletTextSurfaceTests` mounts the audited
`Small` font beneath a real application and proves exact terminal-visible art,
style projection, clipping, resize exposure, content mutation, and stale-cell
clearing.
