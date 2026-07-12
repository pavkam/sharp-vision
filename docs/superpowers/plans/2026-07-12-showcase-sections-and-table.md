# Showcase Sections and Table Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a tested public Table control and rebuild every showcase page as
an explained sequence of preview-plus-property sections followed by a full
technical reference and interaction block.

**Architecture:** `Table` is a traditional `Container` that composes the
existing `Grid` track allocator. `TableColumn` is an immutable width/header
value, `TableRow` is a reusable detached-cell model, and owner-aware column and
row collections rebuild one internal grid atomically. Showcase metadata adds
`ExampleSection` and `PropertySetting`; `Page.CreateContent()` renders a title,
rich usage explanation, section-local tables, and a final technical table using
the public `Table` control.

**Tech Stack:** .NET 10, C# 14, SharpVision `Control`/`Container`/`Grid`/
`RichText`, xUnit v3, Shouldly, Microsoft Testing Platform, Markdownlint,
Prettier, and the existing Makefile gates.

---

## Task 1: Define the failing Table public-surface tests

**Files:**

- Create: `tests/SharpVision.Tests/Controls/TableTests.cs`
- Create: `tests/SharpVision.Tests/Controls/TableRenderingTests.cs`
- Create: `tests/SharpVision.Tests/Controls/TableTestSupport.cs`

- [ ] **Step 1: Add constructor and width-factory tests**

Add tests with the existing Arrange/Act/Assert and Shouldly style:

```csharp
[Fact]
public void Constructor_WhenHeaderAndWidthAreValid_PreservesColumnMetadata()
{
    var column = TableColumn.Fixed("Name", 18);

    column.Header.ShouldBe("Name");
    column.Width.Kind.ShouldBe(Kind.Cells);
    column.Width.Value.ShouldBe(18);
}

[Theory]
[InlineData(Kind.Auto)]
[InlineData(Kind.Cells)]
[InlineData(Kind.Percent)]
[InlineData(Kind.Star)]
public void Constructor_WhenSupportedLengthKindIsUsed_PreservesWidth(Kind kind)
{
    var width = kind switch
    {
        Kind.Auto => Length.Auto,
        Kind.Cells => Length.Cells(8),
        Kind.Percent => Length.Percent(35),
        Kind.Star => Length.Star(2),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    var column = new TableColumn("Column", width);

    column.Width.ShouldBe(width);
}

[Theory]
[InlineData("")]
[InlineData(" ")]
public void Constructor_WhenHeaderIsBlank_ThrowsArgumentException(string header)
{
    _ = Should.Throw<ArgumentException>(() => new TableColumn(header, Length.Auto));
}
```

Also cover `TableColumn.Percent`, `TableColumn.Fill`, `TableColumn.Auto`, and
`TableColumn.Fixed`, including invalid negative fixed values, invalid percent
values, and non-positive fill weights through the underlying `Length` errors.

- [ ] **Step 2: Add TableRow ownership and count tests**

Write tests proving a row copies its input list, rejects null and duplicate
controls, rejects an already attached or disposed control, and exposes the
original order through `Cells`. Use a real `Stack` or `Border` to create an
attached control and `using` to create a disposed control. Add a test that a row
with two controls can be added to a table with two columns and a row with one
control throws before `Rows.Count` changes.

- [ ] **Step 3: Add collection atomicity and reuse tests**

Cover these public behaviors:

```csharp
[Fact]
public void Rows_WhenCellCountDoesNotMatchColumns_PreservesExistingRows()
{
    using var table = TableWithColumns();
    var valid = new TableRow([new Text("A"), new Text("B")]);
    table.Rows.Add(valid);

    _ = Should.Throw<ArgumentException>(() =>
        table.Rows.Add(new TableRow([new Text("only")] )));

    table.Rows.Count.ShouldBe(1);
    table.Rows[0].ShouldBeSameAs(valid);
}
```

Test duplicate columns/rows, invalid column removal while rows exist, clearing
rows, removing a row and re-adding it, and mutation after table disposal. Assert
that failed mutations leave the prior collection contents and table ownership
unchanged.

- [ ] **Step 4: Add layout and rendering tests before implementation**

Create deterministic tests that build a table with:

```csharp
table.Columns.Add(TableColumn.Fixed("Fixed", 5));
table.Columns.Add(TableColumn.Percent("Percent", 25));
table.Columns.Add(TableColumn.Fill("Fill"));
table.Rows.Add(new TableRow([
    new Text("A"),
    new Text("B"),
    new Text("C"),
]));
```

Layout at `new Size(20, 4)` and assert the three cell bounds are contained, the
fixed column is five cells, the percentage column is five cells, and the fill
column receives the remainder. Render a header and row into `Frame` and assert
exact header and cell text with `FrameOracle`. Add tests for hidden headers,
grid-line color, cell padding, zero/tiny bounds, and a second layout at a
different width to prove percentage/fill recomputation.

- [ ] **Step 5: Run the focused tests and confirm the expected failure**

Run:

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*Table*Tests" --timeout 60s
```

Expected: compilation fails because `Table`, `TableColumn`, and `TableRow` do
not exist. Do not weaken the tests to make the missing implementation pass.

## Task 2: Implement the public Table model and owner-aware collections

**Files:**

- Create: `src/SharpVision/Controls/TableColumn.cs`
- Create: `src/SharpVision/Controls/TableRow.cs`
- Create: `src/SharpVision/Controls/TableColumnCollection.cs`
- Create: `src/SharpVision/Controls/TableRowCollection.cs`
- Create: `src/SharpVision/Controls/Table.cs`

- [ ] **Step 1: Implement immutable TableColumn**

Implement `TableColumn` as a `readonly record struct` with an explicit
validating constructor and these members:

```csharp
public TableColumn(string header, Length width);
public string Header { get; }
public Length Width { get; }
public static TableColumn Auto(string header);
public static TableColumn Fixed(string header, int cells);
public static TableColumn Percent(string header, double percentage);
public static TableColumn Fill(string header, double weight = 1);
```

Reject a width kind outside `Auto`, `Cells`, `Percent`, and `Star`. Delegate
numeric range validation to `Length` after validating the header. Document the
header, width units, fill semantics, exceptions, and examples in XML docs.

- [ ] **Step 2: Implement reusable TableRow**

Implement `TableRow` as a sealed reference type with an explicit constructor:

```csharp
public TableRow(IEnumerable<Control> cells);
public IReadOnlyList<Control> Cells { get; }
public int Count { get; }
```

Copy the enumerable before assigning state. Reject null, duplicate, attached,
dispatcher-bound, or disposed controls. Keep an internal owner marker used by
`TableRowCollection`; removing a row clears the marker after detaching its
cells. Document that the row transfers cell ownership while attached and can be
reused after removal.

- [ ] **Step 3: Implement TableColumnCollection**

Follow `TrackCollection`'s `IList<T>`/`IReadOnlyList<T>` shape, but retain a
`Table` owner callback. Before every add, insert, replace, remove, or clear, ask
the owner to validate the candidate column count against every existing row.
Reject duplicate column values by header identity and preserve the collection
when validation fails. Call the owner once after a real mutation so the internal
grid is rebuilt and measure is invalidated once.

- [ ] **Step 4: Implement TableRowCollection**

Follow `Children`'s validation and `TrackCollection`'s collection contract.
Before mutation, require the candidate row cell count to equal the current
column count, reject a row already owned by another table, and validate every
cell remains available. After a successful mutation, ask `Table` to rebuild its
internal grid. On remove, detach the row's cells from their internal borders,
clear the row owner marker, and leave the row reusable.

- [ ] **Step 5: Implement Table as a Grid composition**

Create a capacity-one internal `Grid` child and expose:

```csharp
public TableColumnCollection Columns { get; }
public TableRowCollection Rows { get; }
public bool ShowHeader { get; set; }
public Thickness CellPadding { get; set; }
public int RowSpacing { get; set; }
public int ColumnSpacing { get; set; }
public bool ShowGridLines { get; set; }
public Color? GridLineColor { get; set; }
public Color? HeaderForeground { get; set; }
public Color? HeaderBackground { get; set; }
public Attributes? HeaderAttributes { get; set; }
```

Use `Grid.Columns` to mirror `TableColumn.Width`, create automatic row tracks,
and create one internal `Border` per header or data cell. Header cells contain
ordinary `Text` controls using the configured header style. Data borders contain
the row's caller-provided control. Use `CellPadding`, `ShowGridLines`, and
`GridLineColor` only on the wrapper borders; never write terminal sequences.

Implement `MeasureCore`, `ArrangeCore`, `RenderChildren`, `VisitChildren`,
`DisposeChildren`, and `HitTest` through the internal grid and existing
container semantics. Rebuild only after validated mutation. Ensure an empty
table has zero/implicit-grid behavior that is safe at zero and tiny bounds.

- [ ] **Step 6: Run Table tests and fix only public-behavior failures**

Run:

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*Table*Tests" --timeout 60s
```

Expected: all Table unit, layout, and rendering tests pass with zero warnings.

## Task 3: Add normative Table documentation

**Files:**

- Create: `docs/controls/layout/table.md`
- Modify: `docs/controls/index.md`
- Modify: `docs/concepts/layout.md`
- Modify: `docs/index.md`

- [ ] **Step 1: Write the Table contract**

Document purpose, inheritance, columns, rows, header generation, arbitrary cell
controls, ownership transfer, collection atomicity, dispatcher affinity,
disposal, and invalid argument exceptions. Explain the exact meaning and units
of `Auto`, fixed cells, percentage, and proportional fill widths.

- [ ] **Step 2: Document appearance and rendering**

Document default header/grid-line styles, padding and spacing, grid composition,
clipping, tiny bounds, resize behavior, and the fact that cells render to the
canvas while the control itself emits no ANSI/CSI/OSC bytes. Add a complete C#
example using fixed, percentage, and fill columns with two rows.

- [ ] **Step 3: Link the control and layout concept**

Add Table to the layout-control catalog and link its width contract from the
shared layout concept. Link the showcase and test obligations to the exact
sections that own those rules. Run Markdown formatting/link checks for the
changed docs.

## Task 4: Add showcase section metadata and property tables

**Files:**

- Create: `src/SharpVision.Showcase/PropertySetting.cs`
- Create: `src/SharpVision.Showcase/ExampleSection.cs`
- Modify: `src/SharpVision.Showcase/PropertyDescription.cs`
- Modify: `src/SharpVision.Showcase/Page.cs`
- Create: `tests/SharpVision.Showcase.Tests/ExampleSectionTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/PropertyDescriptionTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/PageTests.cs`

- [ ] **Step 1: Add PropertySetting validation tests**

Add tests requiring non-empty name, type, configured value, and explanation;
assert exact preservation; and assert blank fields throw before state exists.
Keep `PropertyDescription` focused on technical defaults and add tests for its
unchanged four-field contract.

- [ ] **Step 2: Add ExampleSection validation tests**

Test that a section requires a title, explanation factory, preview factory, and
at least one `PropertySetting`. Call `CreatePreview()` twice and assert distinct
detached trees. Build section content and assert it contains the section title,
preview, and a `Table` with `Property`, `Configured value`, and `Meaning`
headers.

- [ ] **Step 3: Implement immutable metadata and section composition**

Implement `PropertySetting` as a `readonly record struct`. Implement
`ExampleSection` as a sealed reference type owning copied settings and
factories. Its content builder must create a bordered card containing a spanning
title and rich explanation, then a two-column `Grid`: the preview card in a
percentage column and a `Table` in a fill column. The settings table has these
columns:

```csharp
TableColumn.Fixed("Property", 18),
TableColumn.Fixed("Configured value", 20),
TableColumn.Fill("Meaning"),
```

Each row contains ordinary `Text` or `RichText` controls and is rebuilt fresh.

- [ ] **Step 4: Rebuild Page.CreateContent**

Change `Page` to accept ordered sections plus usage and interaction factories.
Render this exact tree order:

```text
Title and description
How to use it (RichText)
Examples
  ExampleSection 1
  ExampleSection 2
  ...
Technical details
  Table: Property | Type | Default | Meaning
  Interaction (RichText)
```

The technical table uses `TableColumn.Fixed("Property", 18)`,
`TableColumn.Fixed("Type", 16)`, `TableColumn.Fixed("Default", 18)`, and
`TableColumn.Fill("Meaning")`. Preserve fresh detached page trees and existing
navigation-facing `Name`, `Summary`, `Interaction`, and `Properties` accessors
where current tests or diagnostics depend on them.

- [ ] **Step 5: Run focused showcase metadata tests**

Run:

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*Page*Tests|*ExampleSection*Tests|*PropertyDescription*Tests" \
  --timeout 60s
```

Expected: metadata and composition tests pass before catalog migration begins.

## Task 5: Migrate the catalog and split examples into meaningful sections

**Files:**

- Modify: `src/SharpVision.Showcase/Catalog.cs`
- Modify: `src/SharpVision.Showcase/Examples.cs`
- Modify: `src/SharpVision.Showcase/Palette.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`

- [ ] **Step 1: Add the Table page and update catalog invariants**

Register `Table` after `Stack` and before `Text` in the layout catalog, then
update navigation and interaction tests to use the new final index. Give the
page technical properties for `Columns`, `Rows`, `ShowHeader`, `CellPadding`,
`RowSpacing`, `ColumnSpacing`, `ShowGridLines`, and the header/grid-line styles.

- [ ] **Step 2: Add rich usage and interaction factories**

For every catalog page, create fresh `RichText` factories. Use bold accent
lead-ins such as `Use it when`, `Watch for`, and `Interaction`, with ordinary
word-wrapped runs for the explanatory prose. Include links only where a
normative document is relevant. Keep the complete existing interaction guidance
and move it to the technical block rather than shortening it.

- [ ] **Step 3: Split every grouped example into sections**

Give each page the meaningful sections below, using existing public factories
and adding small focused factories where necessary:

| Page        | Sections                                                         |
| ----------- | ---------------------------------------------------------------- |
| Border      | Glyph families; edge/background styling                          |
| Button      | Enabled/default/cancel states; activation feedback               |
| Canvas      | Fixed and percentage placement; constraints/clipping             |
| CheckBox    | Two-state and three-state values; marks and disabled state       |
| Dock        | Four edge assignments; fill and spacing                          |
| FigletText  | Font and direction; editable/catalog preview                     |
| Grid        | Mixed tracks; spans, spacing, and rounding                       |
| List        | Selection and activation; scrolling and disabled item            |
| Overlay     | Z-order; clipping and hit-test layers                            |
| RadioButton | Named group selection; disabled navigation                       |
| RichText    | Styled runs and line breaks; links and wrapping                  |
| ScrollBar   | Keyboard/track changes; pointer thumb and orientation            |
| ScrollView  | Automatic bars; nested content and bring-into-view               |
| Shadow      | Composite mode; block-glyph mode and clipping                    |
| Stack       | Orientation/spacing; reverse and proportional sizing             |
| Table       | Fixed/percentage/fill columns; rich cells and styling; long rows |
| Text        | Wrapping/trimming; alignment and Unicode width                   |
| TextInput   | Editable/read-only/password; limits and multiline                |

For every section, record each non-default property actually set in a
`PropertySetting` with its exact display value and a user-facing explanation. Do
not put a page-global property list under the examples.

- [ ] **Step 4: Add the Table showcase examples**

Build the Table page through public APIs only. Use a fixed `Name` column, a
percentage `Status` column, a fill `Details` column, a RichText cell, styled
headers, visible grid lines, and enough rows to make the surrounding page
scroll. Add a second example with `ShowHeader = false`, `Length.Auto`, cell
padding, and custom colors.

- [ ] **Step 5: Update Gallery only where the new page shell requires it**

Keep the sidebar behavior, focus routing, pointer selection, and main
`ScrollView` behavior unchanged. Ensure selecting any page disposes the old
content tree and creates a fresh section-rich tree. Preserve the real sidebar
navigation path used by the interaction tests.

- [ ] **Step 6: Run showcase metadata and navigation tests**

Run:

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*Gallery*Tests|*Page*Tests" --timeout 60s
```

Expected: every page includes at least one example section, section-local
property tables, the technical table, the interaction block, and a Table page.

## Task 6: Prove rendered page hierarchy and real interactions

**Files:**

- Modify: `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/Screen.cs` only if a semantic table
  assertion needs a narrowly scoped helper

- [ ] **Step 1: Add semantic page hierarchy assertions**

For every catalog page, render at `80x24` and assert the selected title,
`How to use it`, `Examples`, `Technical details`, `Interaction`, at least one
preview section, and at least one `Table` exist in the current content tree.
Count the section-local tables and require one more technical table than the
number of sections. Assert every section preview factory produces a distinct
tree and every table row has the expected column count.

- [ ] **Step 2: Add responsive cell and continuation assertions**

Render every page at `30x8`, `80x24`, and `140x40`. Require no throw, root
containment, valid continuation cells, and selected-page stability. At normal
size assert the technical table's headers and a configured-value header are
visible. At large size assert a representative preview and its adjacent table
are both visible in the same section.

- [ ] **Step 3: Update real input-path tests**

Keep the existing pointer sidebar, arrow, Enter, Button, TextInput, ScrollView,
ScrollBar, hover, and resize tests. Update indices and selectors for the Table
page. Add a Table-page navigation test through the actual sidebar and assert the
rendered Table header and configured-value content after selection.

- [ ] **Step 4: Run the full showcase test project**

Run:

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --timeout 60s
```

Expected: all metadata, render, navigation, pointer, scrolling, editing, and
resize tests pass with no application failure.

## Task 7: Update showcase architecture and testing documentation

**Files:**

- Modify: `docs/architecture/showcase.md`
- Modify: `docs/testing/showcase.md`
- Modify: `docs/controls/index.md` if the Table link order changed during
  implementation
- Modify: `docs/index.md` if a new section link is required

- [ ] **Step 1: Document the page composition contract**

Replace the single-example/property-footer description with the title, rich
usage, ordered sections, section-local property tables, technical table, and
full interaction contract. Document that every factory creates fresh detached
controls and that showcase pages use only public production APIs.

- [ ] **Step 2: Document observable showcase proof**

Add the section-count, Table-header, configured-value, technical-table,
continuation, tiny-size, resize, and real-sidebar interaction obligations. State
that visual captures supplement semantic cell tests rather than replacing them.

- [ ] **Step 3: Run documentation checks**

Run:

```bash
npm run format:check
npm run lint:markdown
npm run lint:links
npm run test:docs
```

Expected: all changed Markdown is formatted, links resolve, and documentation
tests pass.

## Task 8: Full verification and completion audit

**Files:**

- Modify only files required by verification failures.

- [ ] **Step 1: Format and inspect the intentional diff**

Run:

```bash
make format
git diff --check
git status --short
```

Preserve the pre-existing user changes in
`src/SharpVision/Controls/ScrollBar.cs`,
`src/SharpVision/Controls/ScrollView.cs`, and the four untracked scroll layout
files. Do not stage or rewrite them as part of this task.

- [ ] **Step 2: Run all repository gates**

Run:

```bash
make lint
make build
make test
```

Require zero format, lint, Markdown, link, documentation, build warnings, build
errors, and test failures. Record the discovered test count.

- [ ] **Step 3: Audit the requested end state against evidence**

Check the current source and test output for every requirement: all pages have
the requested hierarchy; each meaningful example has a preview-plus-property
table; the technical block is last; interaction text is complete; Table is a
public component with headers, arbitrary rows/cells, fixed/percentage/fill
sizing, styling, resize, and tests; and the Table page is navigable and
rendered.

- [ ] **Step 4: Commit only the verified task files**

Stage the Table source/tests, showcase source/tests, normative docs, design
plan, and any formatting corrections belonging to this task. Leave unrelated
scroll work unstaged. Use these scoped commits:

```bash
git commit -m "feat: add reusable table control"
git commit -m "feat(showcase): explain examples with property sections"
git commit -m "docs: specify table and showcase sections"
```

If the repository convention prefers one final commit, combine only these
intentional files into that commit; never include unrelated worktree changes.
