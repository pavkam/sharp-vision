# RichText

## RichText contract

`RichText` displays a mutable `Inlines` collection of `Run`, `LineBreak`, and
`Hyperlink` values. It uses typed styles rather than embedded ANSI and supports
grapheme wrapping, line alignment, and semantic terminal hyperlinks.

## API

- `Inlines` rejects null values and prevents one inline from belonging to two
  documents.
- `Wrapping` defaults to `Word` so documents reflow with their arranged cell
  width; applications may select `None` or grapheme wrapping explicitly.
  `TextAlignment` matches [Text](text.md#text-contract).
- Every inline has at most one document owner. Collections reject null,
  duplicates, and cross-document insertion before mutation.
- Inline content and style changes invalidate document measurement.

`Run` has `Content` and optional foreground, background, and attributes.
`Hyperlink` has visible `Content`, the same optional style values, and a
non-empty control-free `Target`. It writes semantic hyperlink metadata but never
opens a URL automatically.

## Interaction and rendering

Runs retain styles across line wrapping. Explicit breaks and embedded newlines
advance the visual line. A wide grapheme that does not fit moves as a complete
owner; it is never split across rows. Measurement, word wrapping, rendering, and
alignment use the immutable cell policy inherited from the application.

## Example

```csharp
var description = new RichText();
description.Inlines.Add(new Run("Read the "));
description.Inlines.Add(new Hyperlink("documentation", "https://example.test"));
description.Inlines.Add(new LineBreak());
description.Inlines.Add(new Run("Resize to see grapheme-safe wrapping."));
```

## Test obligations

Cover inline ownership, failed insertion atomicity, style runs across wrapping,
line breaks, semantic link metadata, combining and wide clusters, resize reflow,
clipping, mutation invalidation, and exact cells.
