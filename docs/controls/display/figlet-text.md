# FigletText

## Overview

`FigletText` renders Unicode source text as FIGlet art through an immutable
`FigletFont`, drawing to the semantic terminal canvas. The generated rows are
cached and only regenerated when `Content`, `Font`, or `Options` changes.

`Content` and `Font` must not be null. `Options` can override the font's text
direction and its fitting or smushing layout bits. Appearance comes from the
inherited `Face` and `ActualFace` surfaces, which cover foreground, background,
attributes, underline, and underline color.

During measurement the control reports the widest generated row in terminal
cells and the number of generated rows, both under the inherited application
cell policy. FIGlet glyphs are never scaled or wrapped; for large fonts, rely on
parent clipping or place the control in an ancestor `Container`, whose intrinsic
`AutoScroll` provides bounded presentation.

Generated FIGlet cells keep the destination background, so the art blends into
whatever surface its parent painted. To draw on a solid background instead,
assign a complete local `Face` with an opaque background: the control fills its
ordinary border box first, and the transparent FIGlet cells draw on top.

## API

| Property                       | Default                       | Purpose                                                                        |
| ------------------------------ | ----------------------------- | ------------------------------------------------------------------------------ |
| `Content`                      | `string.Empty`                | The non-null source text rendered through the selected FIGfont.                |
| `Font`                         | Required constructor argument | The immutable parsed `FigletFont` to render with.                              |
| `Options`                      | `default`                     | Overrides the font's direction and its fitting or smushing layout bits.        |
| Inherited `Face`, `ActualFace` | semantic role                 | Assign a complete local appearance, or inspect the fully resolved current one. |

## Example

![The FigletText control rendered in the live showcase](../../images/controls/figlet-text.png)

```csharp
var title = new FigletText(FigletCatalog.Default.Load("Standard"))
{
    Content = "SharpVision",
};
```

## FigletCatalog

`FigletCatalog` resolves a font name to a parsed `FigletFont`. `Default` is the
audited embedded 400-font archive; its `fonts.manifest.json` records each
entry's provenance and license classification, and that classification is
conservative, not a legal conclusion - confirm redistribution terms before
depending on `Default` in a distributed package. An application can source its
own fonts instead, through the same lookup and `Load` surface:

| Member                              | Source                                                          |
| ----------------------------------- | --------------------------------------------------------------- |
| `FigletCatalog.Default`             | The audited embedded archive.                                   |
| `FigletCatalog.FromDirectory(path)` | Every `.flf`/`.tlf` file directly inside a directory.           |
| `FigletCatalog.FromZip(stream)`     | Every `.flf`/`.tlf` entry in a caller-supplied Zip archive.     |
| `FigletCatalog.FromFonts(fonts)`    | Already-parsed `FigletFont` instances, keyed by their own name. |

```csharp
var catalog = FigletCatalog.FromDirectory("/opt/myapp/fonts");
var title = new FigletText(catalog.Load("Banner")) { Content = "Hello" };
```

Fonts loaded through `FromDirectory` or `FromZip` are named after their file or
entry name, without extension. `GetInfo` still reports provenance metadata for
every entry regardless of source; entries from `FromFonts` report placeholder
`"unaudited"` metadata, since an already-parsed font carries no source file or
byte sequence to hash.

## Expected behavior

Callers can rely on the following: null `Content` or `Font` is rejected; the row
cache is invalidated exactly when `Content`, `Font`, or `Options` changes; every
catalog font renders, including scalar fallback, hardblank handling, both
directions, and the fitting and smushing layout modes; oversized art clips and
responds to resize as documented; style inheritance applies to generated cells;
wide output Runes occupy their correct cells; and the rendered result matches
exact cells. `FigletTextSurfaceTests` mounts the audited `Small` font beneath a
real application and demonstrates the exact terminal-visible art, style
projection, clipping, resize exposure, content mutation, and clearing of stale
cells.
