# Text

## Text contract

`Text` displays immutable-at-render string content using shared grapheme and
cell geometry. It is not focusable by default and emits no terminal protocols.

## API

- `Content` is a non-null string; setting null throws `ArgumentNullException`.
- `Wrapping` is none, word, or grapheme.
- `Trimming` is none, clip, character ellipsis, or word ellipsis.
- `TextAlignment` is start, center, or end within the arranged line width.
- `AmbiguousWidth` inherits the application cell policy until explicitly set; an
  explicit value remains a control-local measurement and rendering override.
- `Foreground`, `Background`, and `Attributes` optionally override style values.
- `Lines` exposes read-only committed line metrics after layout.

Content and wrapping changes invalidate measure. Pure color/attribute changes
invalidate render. Alignment invalidates arrange/render without re-segmenting
content when the width constraint is unchanged.

`Text` caches layout by content identity, final width, wrapping, trimming, and
ambiguous-width policy. Its reusable `Line[]` grows only when required capacity
increases. Alignment-only changes rewrite leading-cell metrics without
re-enumerating graphemes. `Lines` is a `ReadOnlyMemory<Line>` view of the
current commit and remains valid until the next successful layout.

## Rendering

Segmentation follows the
[Unicode geometry contract](../../concepts/unicode-cell-geometry.md#unicode-cell-geometry-contract).
Wrapping and ellipsis never split a grapheme or wide-cell ownership range.
Newlines create logical lines; tab behavior follows an explicit tab policy.

## Layout engine

`SharpVision.Text.Layout.Format` is the shared allocation-conscious formatting
boundary. It accepts borrowed `ReadOnlySpan<char>` content, a non-negative cell
width, `Wrapping`, `Trimming`, `Alignment`, the explicit terminal `Ambiguous`
width policy, and caller-owned `Span<Line>` storage. Its return value is the
complete required line count even when the destination stores only a prefix, so
controls can size a reusable buffer without borrowing pooled memory.

Each immutable `Line` records a UTF-16 `Offset` and `Length`, rendered `Cells`,
alignment `Leading` cells, and `HasEllipsis`. Numeric values are non-negative;
constructing invalid metrics throws `ArgumentOutOfRangeException`. Source slices
exclude CR, LF, and CRLF delimiters and always start and end on extended
grapheme boundaries. Empty content and trailing logical newlines produce stable
empty lines.

`Wrapping.None` preserves the logical line. `Wrapping.Word` prefers the last
complete Unicode whitespace boundary and falls back to a grapheme break;
`Wrapping.Grapheme` breaks only between clusters. `Trimming.Clip` removes only
complete overflowing clusters. Grapheme and word ellipsis modes reserve the
ellipsis glyph's width under the selected ambiguous-width policy and never split
a wide cluster. A cluster that cannot fit an empty positive-width line is
consumed as a clipped empty line rather than emitting a half-wide glyph. Tabs
advance to explicit four-cell stops.

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

Pure formatting additionally runs seed `0x007E875A` over 5,000 mixed valid and
invalid UTF-16 inputs. It proves deterministic results, monotonic source
consumption, grapheme-aligned slices, independently measured cell counts, and
finite-width containment for wrapping and trimming modes.

The control tests assert exact semantic graphemes and styles for multiline,
wrapped, clipped, hidden, collapsed, narrow-ellipsis, and wide-ellipsis cases. A
warmed unchanged 80-column Unicode measure/render loop samples five windows of
1,000 iterations and requires zero managed allocation.
