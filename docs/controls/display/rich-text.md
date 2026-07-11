# RichText

## RichText contract

`RichText` displays a mutable `Inlines` collection of `Run`, `LineBreak`, and
`Hyperlink` values. It uses typed styles rather than embedded ANSI and supports
wrapping, alignment, selection, and link activation.

## API

- `Inlines` rejects null values and prevents one inline from belonging to two
  documents.
- `Wrapping` and `TextAlignment` match [Text](text.md#text-contract).
- `IsSelectionEnabled`, `Selection`, and `SelectedText` operate on grapheme
  boundaries.
- `LinkInvoked` carries the hyperlink value and routed input source.
- `SelectionChanged` fires after the committed selection changes.

`Run` has `Text`, optional foreground/background/attributes, and semantic
emphasis. `Hyperlink` owns inline children, a non-null target value, enabled
state, and optional command; it never opens a URL automatically.

## Interaction and rendering

Pointer drag and keyboard selection use shared hit testing. Tab navigation or a
document-specific command moves among enabled links. Enter activates the focused
link. Selection, link hover/focus, and disabled styles compose through normal
visual-state precedence.

## Example

```csharp
var description = new RichText();
description.Inlines.Add(new Run("Press "));
description.Inlines.Add(new Run("Enter") { Attributes = TextAttributes.Bold });
description.Inlines.Add(new Run(" to activate."));
```

## Test obligations

Cover inline ownership, style runs across wrapping, line breaks, link focus and
activation, selection over combining/wide clusters, clipboard extraction, resize
reflow, disabled links, clipping, events, and exact cells.
