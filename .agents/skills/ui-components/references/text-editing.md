# Text Editing

## Load this reference when

Changing TextInput, NumberInput or CurrencyInput transient editing, Edit,
selection, cursor movement, insertion, deletion, undo/redo, clipboard, password
masking, word navigation, markup, or text editing interaction.

## Normative documentation

- [TextInput](../../../../docs/controls/input/text-input.md#overview)
- [Edit model](../../../../docs/controls/input/text-input.md#edit-model-api)
- [TextInput behavior](../../../../docs/controls/input/text-input.md#behavior)
- [NumberInput](../../../../docs/controls/input/number-input.md#overview)
- [CurrencyInput](../../../../docs/controls/input/currency-input.md#overview)
- [Unicode shared consumers](../../../../docs/concepts/unicode-cell-geometry.md#shared-consumers)
- [Control evidence](../../../../docs/testing/controls-integration.md#controls-with-state-machines)

## Code map

- Editing and text layout: `src/SharpVision/Text/`
- Concrete controls: `src/SharpVision/Controls/Input/TextInput.cs`,
  `src/SharpVision/Controls/Input/NumberInput.cs`, and
  `src/SharpVision/Controls/Input/CurrencyInput.cs`
- Shared numeric editing: `src/SharpVision/Controls/InputBase.cs` and
  `src/SharpVision/Input/NumericEdit*`
- Unit and randomized text tests: `tests/SharpVision.Tests/Text/`
- Interaction and mounted tests:
  `tests/SharpVision.Tests/Controls/Input/TextInput*`

## Workflow

1. State whether positions are scalar, grapheme, string-index, or cell
   coordinates at every boundary.
2. Add failures for grapheme-safe edits, selection direction, cursor placement,
   undo/redo grouping, clipboard, password mode, wide cells, resize, and tiny
   bounds.
3. Keep Edit state and rendered TextInput behavior independently testable.
4. Load `rendering-and-text` only when shared segmentation or cell geometry
   changes.

## Project-specific traps

- Never split surrogate pairs or grapheme clusters during editing.
- Selection styling may span multiple cells while remaining one grapheme.
- Password masking must not leak original text through layout, clipboard, or
  diagnostics.
- Numeric editors keep culture-specific formatting in the concrete control, but
  share modifier classification, selection presentation, focused placeholders,
  affix layout, and semantic cursor replay through `InputBase`.
- Application command chords that are not explicitly supported remain unhandled
  and never mutate a transient numeric buffer.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Text*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*TextInputTests" "*NumberInputTests" "*CurrencyInputTests" \
  --minimum-expected-tests 1 --timeout 60s
```
