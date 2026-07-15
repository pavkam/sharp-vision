# Table

## Table contract

`Table` owns typed rows of ordinary controls and aligns them against titled
fixed, automatic, percentage, or proportional columns. It measures, arranges,
and renders cells through the normal control pipeline, so marked text, links,
buttons, and input controls can appear in a table without a separate rendering
model.

## API

- `Columns` owns non-empty `TableColumn` definitions. A column has a non-empty
  header plus automatic, fixed-cell, percentage, or fill width.
- `Rows` owns `TableRow` values. Each row transfers its unique detached cells to
  the table, must be non-null, and must exactly match the column count. Null row
  insertion and replacement fail at the public collection boundary.
- `ShowHeader`, `HeaderForeground`, and `HeaderBackground` control header
  chrome.
- `CellPadding`, `RowSpacing`, and `ColumnSpacing` define physical cell gaps.
- `ShowGridLines` and `GridLineColor` draw light Unicode lines in available gaps
  without covering child controls.

## Layout and ownership

Columns resolve with the shared
[track allocator](../../concepts/layout.md#layout-contract): fixed widths
reserve exact cells, percentage widths resolve from the final table width,
automatic widths use the largest cell/header request, and fill columns receive
the remaining cells. Headers and rows remeasure wrapping controls once their
finite column widths are known.

Each resolved cell rectangle is an ordinary arrange slot, not a forced border
box. The cell's `HorizontalAlignment`, `VerticalAlignment`, explicit lengths,
margin, and desired size therefore retain their normal meaning. An intrinsic
Button or CheckBox stays at its measured size and aligned position inside a
larger track; a cell explicitly set to Stretch consumes the available slot.

`Table` uses the intrinsic
[`Container` scrolling contract](../../concepts/scrolling.md#scrolling-contract).
The translated content rectangle is the single origin for headers, grid lines,
cells, and hit testing. Table chrome renders through the same viewport-clipped
content canvas before owned scrollbars render above it, so horizontal, vertical,
and combined offsets cannot separate a header or divider from its row controls.
Running origins are signed because scrolling may move content above or left of
zero; only extents and gaps retain the non-negative accumulation invariant.

A failed row/column count validation leaves the collection and every candidate
cell detached. Removing a row releases its cells for another owner.

A header-only table measures and renders only its padded header. It reserves no
phantom data-row spacing or grid divider until the first row is present.

## Example

```csharp
var table = new Table { ShowGridLines = true };
table.Columns.Add(TableColumn.Fixed("Name", 14));
table.Columns.Add(TableColumn.Percent("Status", 25));
table.Columns.Add(TableColumn.Fill("Details"));
table.Rows.Add(new TableRow([
    new Text("Renderer"),
    new Text("Stable"),
    new Text("<link=https://example.test>Open documentation</link>"),
]));
```

## Test obligations

Cover column/row ownership and atomic rejection, fixed/percentage/fill/auto
resolution, intrinsic and stretched cell alignment, header and grid cells, cell
padding, rich/wide cells, tiny bounds, headerless tables, both-axis scrolling
with aligned chrome and hit testing, resize, removal/reuse, and final
continuation ownership.
