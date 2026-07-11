# Grid

## Grid contract

`Grid` arranges managed children in fixed, percentage, automatic, and
proportional row/column tracks with spacing and spans.

## API

- `Rows` and `Columns` are permanent non-null `TrackCollection` values. Empty
  definitions mean one implicit automatic track.
- Immutable `Track` stores `Length`, `Minimum`, and `Maximum` and provides
  `Auto`, `Cells`, `Percent`, and `Star` factories. Limits are non-negative and
  maximum cannot be below minimum.
- `RowSpacing` and `ColumnSpacing` default zero and are non-negative cells.
- Attached `Row`, `Column`, `RowSpan`, and `ColumnSpan` require in-range origins
  and positive spans after definitions are committed. Defaults are row/column
  zero and spans one.
- `Children` follows managed ownership.

Track collection and placement mutation validates dispatcher affinity before
observable state changes, then invalidates measure once after a real change.

Measure resolves fixed tracks, gathers intrinsic non-spanning requirements, then
satisfies spanning requirements deterministically. Arrange resolves percent
tracks against final inner size and distributes remaining cells to proportional
tracks using cumulative rounding.

## Example

```csharp
var grid = new Grid
{
    Columns = { Track.Cells(20), Track.Star(1) },
    ColumnSpacing = 1,
};
```

## Test obligations

Cover all track kinds/mixes, min/max, spacing, spans, competing intrinsic
requirements, rounding/remainders, collapsed children, invalid attached values,
zero/tiny/overflow sizes, wrapping remeasure, resize, ownership, and exact
bounds/cells.
