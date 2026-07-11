# Text

## Text contract

`Text` displays immutable-at-render string content using shared grapheme and
cell geometry. It is not focusable by default and emits no terminal protocols.

## API

- `Content` is a non-null string; setting null throws `ArgumentNullException`.
- `Wrapping` is none, word, or grapheme.
- `Trimming` is none, clip, character ellipsis, or word ellipsis.
- `TextAlignment` is start, center, or end within the arranged line width.
- `Foreground`, `Background`, and `Attributes` optionally override style values.
- `Lines` exposes read-only committed line metrics after layout.

Content and wrapping changes invalidate measure. Pure color/attribute changes
invalidate render. Alignment invalidates arrange/render without re-segmenting
content when the width constraint is unchanged.

## Rendering

Segmentation follows the
[Unicode geometry contract](../../concepts/unicode-cell-geometry.md#unicode-cell-geometry-contract).
Wrapping and ellipsis never split a grapheme or wide-cell ownership range.
Newlines create logical lines; tab behavior follows an explicit tab policy.

## Example

```csharp
var title = new Text("SharpVision")
{
    Wrapping = TextWrapping.None,
    TextAlignment = TextAlignment.Center,
};
```

## Test obligations

Cover empty/multiline text, every wrapping/trimming mode, resize reflow,
combining/emoji/wide clusters, alignment, inherited/direct style, clipping,
invalid null assignment, allocation reuse, and exact cell output.
