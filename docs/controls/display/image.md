# Image

## Image contract

`Image` is declared `public sealed class Image : Control`. It displays one
borrowed immutable
[`Terminal.Graphics.Image`](../../concepts/images.md#image-ownership-contract)
without exposing a terminal protocol to control code. It is passive,
non-focusable, and has no input events. The caller retains ownership of the
source when it is assigned, replaced, cleared, or when the control is disposed.

## API

| Property        | Type              | Default   | Effect  |
| --------------- | ----------------- | --------- | ------- |
| `Source`        | `Graphics.Image?` | `null`    | Measure |
| `AlternateText` | `string`          | empty     | Measure |
| `Stretch`       | `ImageStretch`    | `Contain` | Render  |

`AlternateText` rejects null or any terminal control character before mutation.
`Stretch` accepts only `Contain`, `Cover`, or `Stretch` and rejects an unknown
enum value before mutation. Changed properties follow ordinary dispatcher
affinity, invalidation, and `PropertyChanged` rules. Equivalent assignments are
no-ops.

## Measurement

When a source and exact cell-pixel metrics are available, desired size is the
smallest cell rectangle covering the complete pixel source. Exact uneven grids
use cumulative integer division, so a partial final cell is counted without
assuming uniform cell dimensions. If metrics are unavailable, alternate-text
width is used; a source with no alternate text still requests one deterministic
preview cell. A null source uses alternate-text width. Null source plus empty
alternate text requests `0x0`.

Inherited metrics are updated before resize layout and reach children added
later. A metric change remeasures a source-backed Image.

## Rendering and fallback

A source-backed Image first fills its complete `ContentBounds` with semantic
`Shade.Light`, then draws nonempty alternate text, then records one
backend-neutral `Canvas.DrawImage` placement using the selected stretch mode. It
never writes CSI, OSC, DCS, APC, or other protocol bytes. A source-free Image
draws only alternate text; an empty source-free Image paints nothing.

The placement records the cell-paint revision at which it was added. Any later
cell paint intersecting its destination makes that placement ineffective for the
frame. Ordinary later siblings, Window content, and the elevated Popup pass
therefore occlude an image without protocol knowledge. Clone and copy preserve
this internal provenance; public placement equality, hashing, and encoded bytes
do not expose it. Retained Kitty output deletes a placement that becomes
ineffective. Non-retained sixel and iTerm2 output repair cells when occlusion
changes effective visibility.

Unsupported capability evidence, unavailable exact geometry, an unauthorized
multiplexer route, or an unsupported source/stretch combination leaves the
already-painted cell fallback visible.

## Example

```csharp
var preview = new Image
{
    Source = Graphics.Image.FromRgba(pixelSize, rgbaBytes),
    AlternateText = "Photo preview",
    Stretch = ImageStretch.Contain,
};
```

The runnable showcase includes real RGBA and PNG sources, contain/cover/stretch,
a source-backed fallback deliberately suppressed by a later semantic cell
overlay, and another cell badge overlapping an image. Its live status reports
the inherited Kitty, sixel, and iTerm2 support state plus evidence origin; it
does not guess which backend the host selected.

## Test obligations

`ImageTests` covers defaults, validation atomicity, precise invalidation,
dispatcher affinity, borrowed ownership, uniform and uneven metric measurement,
zero-size behavior, complete fallback paint, placement modes and provenance, and
Window/Popup occlusion. `ImageSurfaceTests` proves mounted unsupported fallback.
`ApplicationGraphicsTests` drives public RGBA and PNG Images through exact
resize metrics to final cell, sixel, Kitty, and explicit iTerm2 3.5 multipart
bytes. It proves fallback precedes graphics, a paused Kitty flush commits before
profile revocation removes it, three-layer fallback blocks overlapping lower
Kitty output, and delete/flush completes before borrowed transport disposal even
when cleanup fails. The Image showcase surface suite mounts narrow, normal, and
wide layouts, validates its real PNG chunk CRCs and zlib scan stream, proves the
source-backed forced fallback is occluded, and updates live inherited evidence.
