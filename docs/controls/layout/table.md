# Table

## Table contract

`Table : ItemsControl` owns typed rows of ordinary controls and aligns them
against titled fixed, automatic, percentage, or proportional columns. It
measures, arranges, and renders cells through the normal control pipeline, so
marked text, links, buttons, and input controls can appear in a table without a
separate rendering model.

## API

| Member group                                            | Default                             | Purpose                                                        |
| ------------------------------------------------------- | ----------------------------------- | -------------------------------------------------------------- |
| `Columns`, `Rows`                                       | Empty typed collections             | Own column definitions and rows of ordinary detached controls. |
| `ShowHeader`                                            | `true`                              | Includes the titled header row.                                |
| `CellPadding`, `RowSpacing`, `ColumnSpacing`            | Zero                                | Reserve cells inside cells and between rows or columns.        |
| `ShowGridLines`                                         | `true`                              | Draws code-owned separators in available gaps.                 |
| `HeaderForeground`, `HeaderBackground`, `GridLineColor` | ControlText, Surface, ControlBorder | Nullable concrete color overrides over semantic defaults.      |
| `ScrollBars`, `ShowScrollBars`, rail appearance         | Presenter defaults                  | Configure the private scrolling table viewport.                |
| `HorizontalOffset`, `VerticalOffset`                    | `0`                                 | Inspect or set committed scroll positions.                     |
| `Extent`, `Viewport`                                    | Read-only                           | Report complete content and visible terminal-cell dimensions.  |
| `SelectionMode`                                         | `Row`                               | Select rows or cells with pointer and keyboard input.          |
| `SelectedRows`, `SelectedCells`                         | Empty                               | Report the committed selection in display order.               |
| `ActiveRow`, `ActiveColumnIndex`                        | No active cell                      | Report the current keyboard and pointer navigation cell.       |
| `SortColumnIndex`, `SortDirection`                      | Reset                               | Report the current stable column sort state.                   |

## Behavior

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

## Interaction and editing

An interactive table is focusable and tab-stop eligible. Pointer presses select
the hit row or cell and make the clicked cell active; `Up`, `Down`, `Left`,
`Right`, `Home`, and `End` move the active cell. `Enter` activates the active
row, and begins editing when that cell is an editable `TextInput`. While
editing, `Enter` commits, `Escape` restores the original text, and `Tab` commits
then moves to the next cell. A `TableColumn` marked `isReadOnly` and a read-only
`TextInput` reject editing. `Ctrl+A` selects every row or cell when the active
selection mode supports it.

`SelectRow`, `SelectCell`, `ClearSelection`, and `SelectAll` commit selection
state and raise `SelectionChanged`. `RowInvoked` reports pointer and keyboard
activation. `SortBy` cycles a column through ascending, descending, and reset;
`SetSort` selects an explicit state and raises `SortChanged`. A supplied
`SortKey` is compared with culture-independent ordering, and equal keys retain
their original insertion order in both directions.

`CopySelection()` returns selected rows or cells as deterministic tab-separated
text with LF row separators. Hosts can pass that text through the existing
application clipboard service; the control does not emit clipboard protocol
bytes itself.

## Code-owned glyphs

`HorizontalGridGlyph`, `VerticalGridGlyph`, and `CrossGridGlyph` are validated
one-cell local overrides. Otherwise table chrome resolves the corresponding
code-owned separator glyph values and terminal-safe fallbacks.
`ResetGridGlyphs()` clears all three overrides.

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

Arrangement reuses the column and row measurement committed for its current
width basis. A pure scroll-origin, focus, or pointer-state arrangement does not
repeat the unbounded and constrained cell probes; doing so would let child
measurement re-invalidate the presenter during its own arrange pass and create
an unbounded frame loop. A genuinely different final width, such as resize,
earns exactly one final constrained measurement pass.

`Table` uses the intrinsic
[`Container` scrolling contract](../../concepts/scrolling.md#scrolling-contract).
The translated content rectangle is the single origin for headers, grid lines,
cells, and hit testing. Table chrome renders through the same viewport-clipped
content canvas before owned scrollbars render above it, so horizontal, vertical,
and combined offsets cannot separate a header or divider from its row controls.
Running origins are signed because scrolling may move content above or left of
zero; only extents and gaps retain the non-negative accumulation invariant.

A failed row/column count validation leaves the collection and every candidate
cell detached. Removing a row releases its cells for another owner. `Rows` and
`Columns` are the only semantic mutation surfaces: the private scrolling table
presenter owns realized cell controls, so `Table` intentionally exposes no
general `Children` collection.

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

## Expected behavior

Cover column/row ownership and atomic rejection, fixed/percentage/fill/auto
resolution, intrinsic and stretched cell alignment, header and grid cells, cell
padding, rich/wide cells, tiny bounds, headerless tables, both-axis scrolling
with aligned chrome and hit testing, resize, removal/reuse, and final
continuation ownership.

Mounted cross-layer coverage in
[`TableSurfaceTests`](../../../tests/SharpVision.Tests/Controls/Layout/TableSurfaceTests.cs)
proves all four column kinds with exact header/grid/Unicode cells, clickable row
removal and ownership reuse without stale cells, both-axis wheel scrolling, and
resize-driven offset repair. A direct layout regression additionally proves a
pure scroll-origin arrangement neither remeasures cells nor leaves arrange
invalidation pending. The same mounted suite covers focusability, pointer and
keyboard navigation, activation, edit commit/cancel, and read-only policy; unit
coverage proves selection/copy, stable sort ordering, and reset transitions.
