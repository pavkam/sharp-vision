# ProgressBar

## ProgressBar contract

`ProgressBar` displays a non-interactive visual progress indicator as a filled
bar. It cannot receive focus, is excluded from hit testing, and owns no
children.

## API

- `Minimum` and `Maximum` are finite `double` endpoints with
  `Minimum < Maximum`. Their defaults are zero and one.
- `Value` is finite and clamps into the inclusive range. Its default is zero.
- `IsIndeterminate` selects the deterministic unknown-duration presentation.
- `Orientation` controls horizontal left-to-right or vertical bottom-to-top
  filling. Default is `Horizontal`.
- `FillGlyph`, `TrackGlyph`, and `IndeterminateGlyph` are printable one-cell
  Runes. Their defaults are `█`, `░`, and `▒`.

Non-finite values throw `ArgumentOutOfRangeException`. An endpoint that would
make the range empty or reversed throws `ArgumentException`. All validation
occurs before mutation. Changing an endpoint clamps the current value in the
same transaction; endpoint observers already see the clamped value, followed by
`Value` notification when clamping changed it.

Determinate rendering normalizes `(Value - Minimum) / (Maximum - Minimum)` and
fills `floor(normalized * axisCells)` complete cells. The maximum fills every
cell. Remaining cells use `TrackGlyph`. Horizontal fill starts at the left;
vertical fill starts at the bottom. Indeterminate mode draws
`IndeterminateGlyph` in every available axis cell and does not claim animation.

Glyph setters validate under the narrow policy. If a configured glyph becomes
wide under the inherited ambiguous-width policy, that frame uses portable `#`,
`.`, or `?` cells without changing the configured property.

The intrinsic desired size is one cell by one cell and both alignment axes
default to `Stretch`. Rendering uses the resolved visual-state style, draws
inside `ContentBounds`, and participates in shared intrinsic chrome. Zero
content bounds draw nothing. The control never handles pointer or keyboard
input.

## Example

```csharp
var bar = new ProgressBar
{
    Maximum = 100,
    Value = 42,
    Orientation = Orientation.Horizontal,
};
```

## Test obligations

Cover finite validation, range ordering, value clamping, endpoint notification
visibility, horizontal and vertical fill direction, empty/partial/full values,
deterministic indeterminate rendering, glyph validation and ambiguous fallback,
zero/tiny bounds, mutation, resize, style inheritance, non-interactive hit
testing, and final cells. `ProgressBarSurfaceTests` must prove the
terminal-visible determinate and indeterminate states through a mounted
application.
