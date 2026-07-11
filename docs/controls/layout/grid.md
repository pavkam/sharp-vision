# Grid

## Grid contract

`Grid` arranges managed children in fixed, percentage, automatic, and
proportional row/column tracks with spacing and spans.

## API

- `Rows` and `Columns` are non-null track collections; each track validates
  length and min/max.
- `RowSpacing` and `ColumnSpacing` are non-negative cells.
- Attached `Row`, `Column`, `RowSpan`, and `ColumnSpan` require in-range origins
  and positive spans after definitions are committed.
- `Children` follows managed ownership.

Measure resolves fixed tracks, gathers intrinsic non-spanning requirements, then
satisfies spanning requirements deterministically. Arrange resolves percent
tracks against final inner size and distributes remaining cells to proportional
tracks using cumulative rounding.

## Example

```csharp
var grid = new Grid
{
    Columns = { GridLength.Cells(20), GridLength.Star(1) },
    ColumnSpacing = 1,
};
```

## Test obligations

Cover all track kinds/mixes, min/max, spacing, spans, competing intrinsic
requirements, rounding/remainders, collapsed children, invalid attached values,
zero/tiny/overflow sizes, wrapping remeasure, resize, ownership, and exact
bounds/cells.
