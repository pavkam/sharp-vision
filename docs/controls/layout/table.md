# Table

## Overview

`Table : ItemsControl` owns typed rows of ordinary controls and aligns them
against titled columns whose widths can be fixed, automatic, percentage, or
proportional. Cells measure, arrange, and render through the normal control
pipeline, so marked text, links, buttons, and input controls can all appear in a
table without a separate rendering model.

## API

| Member group                                            | Default                             | Purpose                                                                |
| ------------------------------------------------------- | ----------------------------------- | ---------------------------------------------------------------------- |
| `Columns`, `Rows`                                       | Empty typed collections             | Own column definitions and rows of ordinary detached controls.         |
| `ShowHeader`                                            | `true`                              | Includes the titled header row.                                        |
| `CellPadding`, `RowSpacing`, `ColumnSpacing`            | Zero                                | Reserve cells inside cells and between rows or columns.                |
| `ShowGridLines`                                         | `true`                              | Draws code-owned separators in available gaps.                         |
| `HeaderForeground`, `HeaderBackground`, `GridLineColor` | ControlText, Surface, ControlBorder | Nullable literal-or-theme-role color overrides over semantic defaults. |
| `ScrollBars`, `ShowScrollBars`, rail appearance         | Presenter defaults                  | Configure the private scrolling table viewport.                        |
| `HorizontalOffset`, `VerticalOffset`                    | `0`                                 | Inspect or set committed scroll positions.                             |
| `Extent`, `Viewport`                                    | Read-only                           | Report complete content and visible terminal-cell dimensions.          |
| `SelectionMode`                                         | `Row`                               | Select rows or cells with pointer and keyboard input.                  |
| `SelectedRows`, `SelectedCells`                         | Empty                               | Report the committed selection in display order.                       |
| `ActiveRow`, `ActiveColumnIndex`                        | No active cell                      | Report the current keyboard and pointer navigation cell.               |
| `SortColumnIndex`, `SortDirection`                      | Reset                               | Report the current stable column sort state.                           |

## Behavior

- `Columns` owns non-empty `TableColumn` definitions. Every column has a
  non-empty header and an automatic, fixed-cell, percentage, or fill width.
- `Rows` owns `TableRow` values. Each row must be non-null, must contain exactly
  as many cells as there are columns, publishes its cells as an immutable
  snapshot, and transfers those unique detached cells to the table. Inserting or
  replacing a null row fails at the public collection boundary.
- `ShowHeader`, `HeaderForeground`, and `HeaderBackground` control the header
  chrome.
- `CellPadding`, `RowSpacing`, and `ColumnSpacing` define the physical cell
  gaps.
- `ShowGridLines` and `GridLineColor` draw light Unicode lines in the available
  gaps without covering child controls.
- `HeaderForeground`, `HeaderBackground`, and `GridLineColor` accept either a
  concrete `Color` or a `ThemeColor` role, so an override can either pin a
  literal color or continue following theme swaps through a named role.

## Interaction and editing

An interactive table is focusable and eligible as a tab stop. A pointer press
selects the hit row or cell and makes the clicked cell active. `Up`, `Down`,
`Left`, `Right`, `Home`, and `End` move the active cell, and `PageUp` and
`PageDown` move by as many rows as fill the committed viewport height minus
`PageOverlap`. The paging keys are handled even when the active cell cannot move
any further, so the keystroke never escapes to page an enclosing scrollable
container. Every move — including `Home` and `End` — brings the active cell into
view.

> [!IMPORTANT]
>
> **Implementation gap:** Table currently marks these keys handled only when the
> active cell actually moved, so PageUp at the first row (or PageDown at the
> last) still escapes and pages an enclosing scrollable container. TreeView and
> NavigationView already handle the boundary press as documented here. Issue
> #222 tracks the fix.

`Enter` activates the active row, and begins editing when the active cell is an
editable `TextInput`. While editing, `Enter` commits, `Escape` restores the
original text, and `Tab` commits and then moves to the next cell. A
`TableColumn` marked `isReadOnly` and a read-only `TextInput` both refuse
editing. `Ctrl+A` selects every row or cell when the active selection mode
supports it.

`SelectRow`, `SelectCell`, `ClearSelection`, and `SelectAll` commit selection
state and raise `SelectionChanged`. `RowInvoked` reports pointer and keyboard
activation. `SortBy` cycles a column through ascending, descending, and reset;
`SetSort` selects an explicit state and raises `SortChanged`. A supplied
`SortKey` is compared with culture-independent ordering, and rows with equal
keys keep their original insertion order in both directions.

`CopySelection()` returns the selected rows or cells as deterministic
tab-separated text with LF row separators. A host can pass that text to the
existing application clipboard service; the control does not emit clipboard
protocol bytes itself.

## Code-owned glyphs

`HorizontalGridGlyph`, `VerticalGridGlyph`, and `CrossGridGlyph` are validated
one-cell local overrides. When they are not set, the table chrome resolves the
corresponding code-owned separator glyph values with terminal-safe fallbacks.
`ResetGridGlyphs()` clears all three overrides.

## Layout and ownership

Columns resolve with the shared
[track allocator](../../concepts/layout.md#overview): fixed widths reserve exact
cells, percentage widths resolve from the final table width, automatic widths
take the largest cell or header request, and fill columns receive the remaining
cells. Headers and rows remeasure wrapping controls once their finite column
widths are known.

Each resolved cell rectangle is an ordinary arrange slot, not a forced border
box, so a cell's `HorizontalAlignment`, `VerticalAlignment`, explicit lengths,
margin, and desired size keep their normal meaning. An intrinsically sized
Button or CheckBox stays at its measured size and aligned position inside a
larger track, while a cell explicitly set to Stretch consumes the available
slot.

Arrangement reuses the column and row measurement committed for the current
width basis. An arrangement caused purely by a scroll origin, focus, or
pointer-state change does not repeat the unbounded and constrained cell probes;
repeating them would let child measurement re-invalidate the presenter during
its own arrange pass and create an unbounded frame loop. A genuinely different
final width, such as a resize, earns exactly one final constrained measurement
pass.

`Table` uses the intrinsic
[`Container` scrolling contract](../../concepts/scrolling.md#overview). The
translated content rectangle is the single origin for headers, grid lines,
cells, and hit testing. Table chrome renders through the same viewport-clipped
content canvas before the owned scrollbars render above it, so horizontal,
vertical, and combined offsets can never separate a header or divider from its
row controls. Running origins are signed, because scrolling can move content
above or left of zero; only extents and gaps keep the non-negative accumulation
invariant.

A failed row or column count validation leaves the collection and every
candidate cell detached. Removing a row releases its cells for another owner.
`Rows` and `Columns` are the only semantic mutation surfaces: the private
scrolling table presenter owns the realized cell controls, so `Table`
intentionally exposes no general `Children` collection.

A header-only table measures and renders just its padded header. It reserves no
phantom data-row spacing or grid divider until the first row is present.

## Example

![The Table control rendered in the live showcase](../../images/controls/table.png)

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

Column and row ownership is validated atomically, and a rejected mutation leaves
every candidate cell detached. Fixed, percentage, fill, and automatic widths
resolve as described, intrinsic and stretched cells align normally inside their
slots, and the header, grid lines, and padded cells render exactly. Rich or wide
cell content, tiny bounds, and headerless tables stay well-defined; scrolling on
both axes keeps chrome and hit testing aligned; resize reflows
deterministically; removal releases cells for reuse; and continuation ownership
holds in the final cells.

Mounted cross-layer coverage in
[`TableSurfaceTests`](../../../tests/SharpVision.Tests/Controls/Layout/TableSurfaceTests.cs)
demonstrates all four column kinds with exact header, grid, and Unicode cells;
clickable row removal with ownership reuse and no stale cells; both-axis wheel
scrolling; and resize-driven offset repair. A direct layout regression
additionally shows that a pure scroll-origin arrangement neither remeasures
cells nor leaves arrange invalidation pending. The same mounted suite covers
focusability, pointer and keyboard navigation, activation, edit commit and
cancel, and the read-only policy, while unit coverage proves selection and copy,
stable sort ordering, and reset transitions.
