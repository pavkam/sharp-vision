# FigletText

## FigletText contract

`FigletText` renders Unicode source text through an immutable `FigletFont` and
the semantic terminal canvas. Generated rows are cached until `Content`, `Font`,
or `Options` changes.

`Content` and `Font` are non-null. `Options` may override the font's direction
and layout bits. Foreground, background, and attributes are direct nullable
overrides over normal resolved appearance.

Measure reports the maximum generated terminal-cell width and generated row
count under the inherited application cell policy. The control does not scale or
wrap FIGlet glyphs. Parent clipping and a `ScrollView` provide bounded
presentation for large fonts.

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
wide output Runes, and exact cells.
