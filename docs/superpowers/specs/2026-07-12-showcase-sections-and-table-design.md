# Showcase Sections and Table Design

## 1. Purpose

The SharpVision showcase is the product's runnable documentation gallery. Each
page must teach a control through several focused examples rather than showing
one composite specimen followed by a detached property dump. This design
introduces a reusable public `Table` control and changes the showcase page model
so every example explains its own configuration while a final technical section
records the complete API contract and interaction behavior.

The design preserves the existing traditional mutable control model, dispatcher
affinity, cell-based layout, and public-control-only showcase rule. It does not
introduce a virtual tree, a second layout engine, or showcase-specific rendering
shortcuts.

## 2. Goals and non-goals

### Goals

- Give every component page a title, concise description, rich-text usage
  explanation, meaningful example sections, and a complete technical section.
- Put each example's preview beside its configured properties and the meaning of
  those values.
- Put the full property reference and complete interaction guidance after the
  examples in a visually distinct technical block.
- Add a public `Table` control that supports headers, arbitrary control cells,
  fixed columns, percentage columns, automatic columns, and proportional fill
  columns.
- Use `Table` in the showcase's property documentation and give `Table` its own
  showcase page.
- Prove ownership, validation, sizing, clipping, resizing, rendering, and page
  structure with observable tests and updated normative documentation.

### Non-goals

- Sorting, filtering, virtualized rows, editable cells, or column reordering.
- A separate data-binding or virtualized-items framework.
- A bespoke renderer that duplicates `Grid` track allocation.
- Replacing traditional mutable controls with function components or a virtual
  DOM.

## 3. Public Table control

### 3.1 Surface

`SharpVision.Controls.Table` is a `Container` that owns a composed `Grid`. Its
public model consists of:

- `Table.Columns`, a dispatcher-aware `TableColumnCollection`.
- `Table.Rows`, a dispatcher-aware `TableRowCollection`.
- `TableColumn`, an immutable value containing a non-empty `Header` and a
  validated `Length Width`.
- `TableRow`, a reference object containing an ordered, immutable snapshot of
  detached `Control` cells.

`TableColumn` accepts `Length.Auto`, `Length.Cells`, `Length.Percent`, and
`Length.Star`. The semantic factories `TableColumn.Auto`, `TableColumn.Fixed`,
`TableColumn.Percent`, and `TableColumn.Fill` make the common forms discoverable
without requiring callers to know the underlying track vocabulary. `Fill`
creates a proportional `Length.Star` column and accepts a positive weight.

Every public constructor and mutation validates before observable state changes:

- Headers are non-null and non-whitespace.
- Widths are finite and use one of the four supported `Length` kinds.
- A table cannot commit a row whose cell count differs from its column count.
- A `TableRow` rejects null, duplicated, disposed, attached, or dispatcher-bound
  cell controls.
- A column or row collection rejects null values, duplicates, and invalid
  mutations atomically.
- Attached collection mutation requires the table dispatcher and rejects
  mutation after disposal.

Rows are reusable model objects. When a row is added, the table attaches its
cell controls through internal cell borders. Removing a row detaches those
controls and returns ownership of the detached row to the caller. Disposing a
table disposes the table-owned layout wrappers and any still-attached cell
controls, following the existing container ownership rules.

### 3.2 Appearance and layout

The table exposes the following visual and layout properties:

- `ShowHeader`, default `true`, controls whether column headers occupy the first
  row.
- `CellPadding`, default one cell horizontally and zero cells vertically,
  deflates each cell's content box.
- `RowSpacing` and `ColumnSpacing`, default zero, reserve non-negative cells
  between table tracks.
- `ShowGridLines`, default `true`, controls whether each cell draws its table
  border.
- `GridLineColor`, `HeaderForeground`, `HeaderBackground`, and
  `HeaderAttributes` provide optional header and separator styling without
  changing child control styles.

The internal grid uses one column track per `TableColumn.Width`. It creates one
automatic row track for the optional header and one automatic row track per data
row. Header labels are ordinary `Text` controls. Data cells remain the
caller-provided controls, so `Text`, `RichText`, `Button`, or any other detached
control can appear in a cell.

The table delegates fixed, automatic, percentage, proportional, spacing,
rounding, tiny-bounds, clipping, and resize behavior to the existing `Grid`
contract in
[the Grid specification](../../controls/layout/grid.md#grid-contract) and
[the shared layout concept](../../concepts/layout.md#panels). It never emits
terminal control bytes. Grid-line borders are ordinary controls that render to
the cell canvas.

### 3.3 Examples

The table showcase page demonstrates:

1. Fixed, percentage, and fill columns together, with headers and multiple rows.
2. Automatic sizing and rich cell content, including a `RichText` cell.
3. Header and grid-line styling, cell padding, and a table large enough to
   exercise the surrounding `ScrollView`.

The table tests cover the public surface, invalid and atomic mutations, row and
column ownership, every supported width kind, exact cell output, header hiding,
grid-line styling, resize, tiny bounds, and containment of every committed
child.

## 4. Showcase page model

### 4.1 Metadata types

The internal showcase model gains two focused types:

- `ExampleSection` stores a non-empty title, a rich-text explanation factory, a
  fresh preview factory, and at least one `PropertySetting`.
- `PropertySetting` stores the property name, declared type, configured value,
  and a plain-language explanation of that value.

`PropertyDescription` remains the page-level technical reference and continues
to store property name, type, default, and meaning. Its default value is not
used as a substitute for an example's configured value: each example explicitly
records what it set.

`Page` stores a title, concise description, usage-rich-text factory, ordered
example sections, technical property descriptions, and interaction-rich-text
factory. Factories always build fresh detached control trees because a page is
selected repeatedly during one application lifetime.

### 4.2 Page order and composition

Every page is composed in this order:

1. A title block with the concrete control name and concise description.
2. A rich-text `How to use it` explanation with emphasized concepts and links
   where a normative control or concept document is relevant.
3. An `Examples` heading followed by one bordered section per meaningful
   behavior or visual variant.
4. Each example section contains its own heading, rich-text explanation, live
   preview, and a `Table` of the exact configured properties, values, types, and
   meanings for that preview.
5. A `Technical details` block after all examples. It contains a full-width
   `Table` with `Property`, `Type`, `Default`, and `Meaning` columns, followed
   by a full rich-text interaction description.

Example sections use a responsive two-column `Grid`: the preview receives a
percentage track and the property table receives the remaining proportional
track. The section title and explanation span both columns. At tiny widths the
same controls are allowed to clip and wrap through the normal layout contract;
the page must remain render-safe and must not create a second responsive system.

The visual hierarchy uses the existing showcase palette: accent title, warning
section labels, bordered surface cards, muted metadata, and the table's own
header styling. The shell does not repeat page-global properties beneath all
examples; the only page-global reference is the final technical block.

### 4.3 Catalog coverage

All current concrete control pages receive multiple sections where the control
has distinct documented states. Controls with a single coherent display mode may
use one focused section, but their page still has the full title, usage
explanation, preview-plus-properties section, technical table, and interaction
description. Existing grouped `Examples` factories are split or supplemented so
state variants are discoverable rather than hidden in one undifferentiated
stack.

The catalog adds `Table` in layout-control order. The showcase's own property
tables are ordinary `Table` instances, and the Table page therefore documents
the exact component used to explain every other component.

## 5. Documentation and source alignment

The implementation updates:

- `docs/controls/layout/table.md` with the public Table contract, API,
  ownership, sizing, appearance, layout, rendering, and test obligations.
- `docs/controls/index.md` and `docs/concepts/layout.md` with the Table link and
  shared sizing relationship.
- `docs/architecture/showcase.md` with the page composition and factory
  ownership contract.
- `docs/testing/showcase.md` with section-level property-table assertions,
  Table-page coverage, and responsive render obligations.
- `docs/index.md` links when the control catalog or showcase architecture map
  changes.
- XML documentation on every new public and internal type/member, including
  validation, ownership, units, dispatcher affinity, disposal, and side effects.

No page or coverage document claims Table support until typed implementation,
observable tests, and a runnable showcase page all exist.

## 6. Verification contract

Focused checks must prove:

- `TableColumn` width factories map fixed, percentage, automatic, and fill
  values to the expected `Length` kinds and reject invalid input.
- `TableRow` and both table collections reject invalid ownership and preserve
  prior state when a mutation fails.
- Headers, arbitrary cell controls, grid lines, cell padding, spacing, and each
  column sizing mode render expected cells at normal and tiny widths.
- Resizing recomputes column and row geometry without escaped children or
  invalid wide-cell continuations.
- Every page has the required title, usage explanation, one or more example
  sections, section-local property tables, final technical table, and full
  interaction content.
- Every section preview is fresh and detached, and the Table page is present in
  the catalog and navigable through the real sidebar path.
- Showcase rendering remains safe at 30x8, 80x24, and 140x40; interaction tests
  continue to exercise navigation, button activation, text editing, scrolling,
  resize, and pointer behavior through the real application path.

The final repository gates remain `make format`, `make lint`, `make build`, and
`make test`, followed by `git diff --check` and an inspection of the rendered
showcase capture when the capture tooling is available.
