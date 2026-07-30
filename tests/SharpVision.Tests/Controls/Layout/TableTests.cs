// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies table ownership, track geometry, headers, grid cells, and row validation.</summary>
public sealed class TableTests
{
    /// <summary>Verifies private rail local mechanics publish exact resolved-style notifications.</summary>
    [Fact]
    public void ScrollBarStyle_WhenOwnershipChanges_PublishesLocalAndActualNotifications()
    {
        var table = new Table();
        List<string?> notifications = [];
        table.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(Table.ScrollBarStyle) or nameof(Table.ActualScrollBarStyle))
            {
                notifications.Add(eventArgs.PropertyName);
            }
        };
        table.ScrollBarStyle = ScrollBarStyle.ThinLine;
        table.ScrollBarStyle = null;
        notifications.ShouldBe([
            nameof(Table.ScrollBarStyle),
            nameof(Table.ActualScrollBarStyle),
            nameof(Table.ScrollBarStyle),
            nameof(Table.ActualScrollBarStyle)
        ]);
        notifications.Clear();

        table.SetTheme(Themes.White);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies stable row selection, active cell tracking, clearing, and deterministic copy.</summary>
    [Fact]
    public void Selection_WhenRowsAndCellsAreSelected_TracksActiveStateAndCopiesTabSeparatedText()
    {
        var first = new TableRow([new ControlText("Alice"), new ControlText("Ready")]);
        var second = new TableRow([new ControlText("Bob"), new ControlText("Busy")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Columns.Add(TableColumn.Auto("State"));
        table.Rows.Add(first);
        table.Rows.Add(second);

        table.SelectRow(first);
        table.SelectRow(second, Modifiers.Control);

        table.SelectedRows.ShouldBe([first, second]);
        table.ActiveRow.ShouldBe(second);
        table.ActiveColumnIndex.ShouldBe(0);
        table.CopySelection().ShouldBe("Alice\tReady\nBob\tBusy");

        table.ClearSelection();

        table.SelectedRows.ShouldBeEmpty();
        table.SelectedCells.ShouldBeEmpty();
        table.CopySelection().ShouldBeEmpty();
    }

    /// <summary>Verifies cell selection and select-all preserve row and column order.</summary>
    [Fact]
    public void SelectAll_WhenCellSelectionIsActive_SelectsEveryCellInDeterministicOrder()
    {
        var first = new TableRow([new ControlText("A1"), new ControlText("B1")]);
        var second = new TableRow([new ControlText("A2"), new ControlText("B2")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleCells };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Columns.Add(TableColumn.Auto("B"));
        table.Rows.Add(first);
        table.Rows.Add(second);

        table.SelectAll();

        table.SelectedCells.ShouldBe([
            new TableCellReference(first, 0),
            new TableCellReference(first, 1),
            new TableCellReference(second, 0),
            new TableCellReference(second, 1)
        ]);
        table.CopySelection().ShouldBe("A1\tB1\nA2\tB2");
    }

    /// <summary>Verifies sorting preserves row selection instead of silently clearing it — the
    /// reorder relocates the exact same row instances rather than removing and re-adding new
    /// ones, so selection referencing those instances must survive (see #109).</summary>
    [Fact]
    public void SortBy_WhenRowsAreSelected_PreservesSelection()
    {
        var first = new TableRow([new ControlText("B")]);
        var second = new TableRow([new ControlText("A")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.SelectRow(first);
        table.SelectRow(second, Modifiers.Control);

        table.SortBy(0);

        table.Rows.ShouldBe([second, first]);
        table.SelectedRows.ShouldBe([second, first]);
    }

    /// <summary>Verifies sorting does not cancel an in-progress edit on a row that survives the
    /// reorder — the row instance is only relocated, not removed (see #109).</summary>
    [Fact]
    public void SortBy_WhenRowIsBeingEdited_DoesNotCancelEdit()
    {
        var editor = new TextInput { Text = "one" };
        var other = new TableRow([new ControlText("two")]);
        var edited = new TableRow([editor]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(other);
        table.Rows.Add(edited);
        table.BeginEdit(edited, 0).ShouldBeTrue();
        editor.Text = "changed";

        table.SortBy(0);

        table.IsEditing.ShouldBeTrue();
        editor.Text.ShouldBe("changed");
    }

    /// <summary>Verifies default text sorting is ordinal and stable across the direction cycle.</summary>
    [Fact]
    public void SortBy_WhenDefaultTextKeysAreComparedUnderCulture_PreservesOrdinalStableOrder()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var first = new TableRow([new ControlText("z"), new ControlText("first")]);
            var second = new TableRow([new ControlText("same"), new ControlText("second")]);
            var third = new TableRow([new ControlText("same"), new ControlText("third")]);
            var fourth = new TableRow([new ControlText("ä"), new ControlText("fourth")]);
            var table = new Table();
            table.Columns.Add(TableColumn.Auto("Key"));
            table.Columns.Add(TableColumn.Auto("Value"));
            table.Rows.Add(first);
            table.Rows.Add(second);
            table.Rows.Add(third);
            table.Rows.Add(fourth);
            var changes = new List<(int Column, TableSortDirection Direction)>();
            table.SortChanged += (_, args) => changes.Add((args.ColumnIndex, args.Direction));

            table.SortBy(0);
            table.Rows.ShouldBe([second, third, first, fourth]);
            table.SortBy(0);
            table.Rows.ShouldBe([fourth, first, second, third]);
            table.SortBy(0);
            table.Rows.ShouldBe([first, second, third, fourth]);
            table.SortDirection.ShouldBe(TableSortDirection.None);
            changes.ShouldBe([
                (0, TableSortDirection.Ascending),
                (0, TableSortDirection.Descending),
                (-1, TableSortDirection.None)
            ]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>Verifies removing or replacing an edited row cancels and detaches its editor callback.</summary>
    [Fact]
    public void Rows_WhenEditedRowIsRemovedOrReplaced_CancelsBeforeOwnershipChanges()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        var removedEditor = new TextInput { Text = "removed" };
        var removedRow = new TableRow([removedEditor]);
        table.Rows.Add(removedRow);

        table.BeginEdit(removedRow, 0).ShouldBeTrue();
        removedEditor.Text = "changed";
        _ = table.Rows.Remove(removedRow);

        table.IsEditing.ShouldBeFalse();
        removedEditor.Text.ShouldBe("removed");
        Should.NotThrow(() => Key(removedEditor, Code.Enter));

        var replacedEditor = new TextInput { Text = "replaced" };
        var previousRow = new TableRow([replacedEditor]);
        var replacement = new TableRow([new TextInput { Text = "new" }]);
        table.Rows.Add(previousRow);

        table.BeginEdit(previousRow, 0).ShouldBeTrue();
        replacedEditor.Text = "changed";
        table.Rows[0] = replacement;

        table.IsEditing.ShouldBeFalse();
        replacedEditor.Text.ShouldBe("replaced");
        Should.NotThrow(() => Key(replacedEditor, Code.Enter));
        table.Rows.ShouldBe([replacement]);
    }

    /// <summary>Verifies replacing a selected row removes stale references and preserves a valid range anchor.</summary>
    [Fact]
    public void Rows_WhenSelectedRowIsReplaced_ClearsOldSelectionAndRepairsAnchor()
    {
        var previous = new TableRow([new ControlText("previous")]);
        var middle = new TableRow([new ControlText("middle")]);
        var last = new TableRow([new ControlText("last")]);
        var replacement = new TableRow([new ControlText("replacement")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(previous);
        table.Rows.Add(middle);
        table.Rows.Add(last);
        table.SelectRow(previous);
        TableSelectionChangedEventArgs? change = null;
        table.SelectionChanged += (_, args) => change = args;

        table.Rows[0] = replacement;

        table.SelectedRows.ShouldBeEmpty();
        var snapshot = change.ShouldNotBeNull();
        snapshot.RemovedRows.ShouldBe([previous]);
        table.SelectRow(last, Modifiers.Shift);
        table.SelectedRows.ShouldBe([replacement, middle, last]);
    }

    /// <summary>Verifies row removal publishes one selection change and leaves shift selection anchored safely.</summary>
    [Fact]
    public void Rows_WhenSelectedAnchorIsRemoved_PublishesOneChangeAndRepairsShiftSelection()
    {
        var first = new TableRow([new ControlText("first")]);
        var middle = new TableRow([new ControlText("middle")]);
        var last = new TableRow([new ControlText("last")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(first);
        table.Rows.Add(middle);
        table.Rows.Add(last);
        table.SelectRow(first);
        var changes = new List<TableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, args) => changes.Add(args);

        table.Rows.RemoveAt(0);

        changes.Count.ShouldBe(1);
        changes[0].RemovedRows.ShouldBe([first]);
        table.SelectedRows.ShouldBeEmpty();
        table.SelectRow(last, Modifiers.Shift);
        table.SelectedRows.ShouldBe([middle, last]);
    }

    /// <summary>Verifies clearing rows publishes one coherent selection change for all selected rows and cells.</summary>
    [Fact]
    public void Rows_WhenCleared_PublishesOneSelectionChangeAndClearsSelection()
    {
        var first = new TableRow([new ControlText("first"), new ControlText("one")]);
        var second = new TableRow([new ControlText("second"), new ControlText("two")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.SelectRow(first);
        table.SelectRow(second, Modifiers.Control);
        var changes = new List<TableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, args) => changes.Add(args);

        table.Rows.Clear();

        changes.Count.ShouldBe(1);
        changes[0].RemovedRows.ShouldBe([first, second]);
        table.SelectedRows.ShouldBeEmpty();
        table.SelectedCells.ShouldBeEmpty();
        table.Rows.ShouldBeEmpty();
    }

    /// <summary>Verifies removing a sorted zero-row column resets sort before later row insertion.</summary>
    [Fact]
    public void Columns_WhenSortedColumnIsRemovedWithNoRows_ResetsSortBeforeRowsAreAdded()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("First"));
        table.Columns.Add(TableColumn.Auto("Second"));
        table.SetSort(1, TableSortDirection.Ascending);

        table.Columns.RemoveAt(1);

        table.SortColumnIndex.ShouldBe(-1);
        table.SortDirection.ShouldBe(TableSortDirection.None);
        Should.NotThrow(() => table.Rows.Add(new TableRow([new ControlText("value")])));
    }

    /// <summary>Verifies clearing columns resets sorting before later row insertion.</summary>
    [Fact]
    public void Columns_WhenClearedWhileSorted_ResetsSortBeforeRowsAreAdded()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("First"));
        table.Columns.Add(TableColumn.Auto("Second"));
        table.SetSort(1, TableSortDirection.Ascending);

        table.Columns.Clear();

        table.SortColumnIndex.ShouldBe(-1);
        table.SortDirection.ShouldBe(TableSortDirection.None);
        table.Columns.Add(TableColumn.Auto("Replacement"));
        Should.NotThrow(() => table.Rows.Add(new TableRow([new ControlText("value")])));
    }

    /// <summary>Verifies inserting before a sorted column preserves its identity and remaps its index.</summary>
    [Fact]
    public void Columns_WhenInsertedBeforeSortedColumn_PreservesSortIdentity()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("First"));
        table.Columns.Add(TableColumn.Auto("Sorted"));
        table.SetSort(1, TableSortDirection.Descending);

        table.Columns.Insert(0, TableColumn.Auto("Inserted"));

        table.SortColumnIndex.ShouldBe(2);
        table.SortDirection.ShouldBe(TableSortDirection.Descending);
        Should.NotThrow(() => table.Rows.Add(new TableRow([
            new ControlText("inserted"),
            new ControlText("first"),
            new ControlText("sorted")
        ])));
    }

    /// <summary>Verifies the public column collection rejects the invalid default value.</summary>
    [Fact]
    public void Columns_WhenDefaultValueIsAdded_RejectsMissingHeader()
    {
        var table = new Table();

        _ = Should.Throw<ArgumentException>(() => table.Columns.Add(default));
        table.Columns.ShouldBeEmpty();
    }

    /// <summary>Verifies selection event arguments retain an immutable snapshot of caller lists.</summary>
    [Fact]
    public void SelectionChangedEventArgs_WhenSourceListsMutate_RetainsOriginalSnapshot()
    {
        var row = new TableRow([new ControlText("value")]);
        var addedRows = new List<TableRow> { row };
        var addedCells = new List<TableCellReference> { new(row, 0) };
        var args = new TableSelectionChangedEventArgs(addedRows, [], addedCells, []);

        addedRows.Clear();
        addedCells.Clear();

        args.AddedRows.ShouldBe([row]);
        args.AddedCells.ShouldBe([new TableCellReference(row, 0)]);
        _ = Should.Throw<NotSupportedException>(() => ((IList<TableRow>) args.AddedRows).Clear());
        _ = Should.Throw<NotSupportedException>(() => ((IList<TableCellReference>) args.AddedCells).Clear());
    }

    /// <summary>Verifies TextInput editing commits, cancels, and rejects read-only columns.</summary>
    [Fact]
    public void Edit_WhenCellIsTextInput_CommitsAndCancelsWithoutOpeningReadOnlyColumn()
    {
        var editable = new TextInput { Text = "before" };
        var locked = new TextInput { Text = "locked" };
        var row = new TableRow([editable, locked]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Editable"));
        table.Columns.Add(TableColumn.Auto("Locked", isReadOnly: true));
        table.Rows.Add(row);

        table.BeginEdit(row, 0).ShouldBeTrue();
        editable.Text = "after";
        table.CommitEdit().ShouldBeTrue();
        editable.Text.ShouldBe("after");

        table.BeginEdit(row, 0).ShouldBeTrue();
        editable.Text = "discarded";
        table.CancelEdit().ShouldBeTrue();
        editable.Text.ShouldBe("after");
        table.BeginEdit(row, 1).ShouldBeFalse();
    }

    private static void Key(TextInput control, Code code) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(code, null, nativeCode: 0, Modifiers.None, KeyAction.Press)));

    /// <summary>Verifies every public row insertion boundary reports its own null parameter.</summary>
    [Fact]
    public void Rows_WhenNullIsInserted_ReportsPublicParameterName()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));

        var add = Should.Throw<ArgumentNullException>(() => table.Rows.Add(null!));
        var insert = Should.Throw<ArgumentNullException>(() => table.Rows.Insert(0, null!));

        add.ParamName.ShouldBe("item");
        insert.ParamName.ShouldBe("item");
        table.Rows.ShouldBeEmpty();
    }

    /// <summary>Verifies row replacement reports the public indexer value parameter before mutation.</summary>
    [Fact]
    public void Rows_WhenNullReplacesRow_ReportsValueParameterWithoutMutation()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        var original = new TableRow([new ControlText("Original")]);
        table.Rows.Add(original);

        var exception = Should.Throw<ArgumentNullException>(() => table.Rows[0] = null!);

        exception.ParamName.ShouldBe("value");
        table.Rows.ShouldBe([original]);
    }

    /// <summary>Verifies fixed, percentage, and fill columns resolve exact contained cell slots.</summary>
    [Fact]
    public void Layout_WhenColumnsMixFixedPercentAndFill_ResolvesContainedCellSlots()
    {
        var first = new ControlText("Alpha");
        var second = new ControlText("Ready");
        var third = new ControlText("Details");
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Percent("Status", 50));
        table.Columns.Add(TableColumn.Fill("Details"));
        table.Rows.Add(new TableRow([first, second, third]));

        new Engine().Layout(table, new Size(20, 4));

        first.Bounds.ShouldBe(new Rect(0, 2, 5, 1));
        second.Bounds.ShouldBe(new Rect(6, 2, 5, 1));
        third.Bounds.ShouldBe(new Rect(16, 2, 4, 1));
        table.DesiredSize.ShouldBe(new Size(20, 3));
    }

    /// <summary>Verifies an ordinary interactive cell keeps its measured size inside a larger row slot.</summary>
    [Fact]
    public void Layout_WhenCellUsesIntrinsicAlignment_KeepsMeasuredBounds()
    {
        var option = new CheckBox
        {
            Content = new ControlText("Include integration tests"),
            VerticalAlignment = VerticalAlignment.Top
        };
        var table = new Table { Width = Length.Cells(48), CellPadding = new Thickness(1, 0) };
        table.Columns.Add(TableColumn.Fixed("Action", 16));
        table.Columns.Add(TableColumn.Fill("Configuration"));
        table.Rows.Add(new TableRow([
            new Button { Content = new ControlText("Run checks") },
            option
        ]));

        new Engine().Layout(table, new Size(48, 8));

        option.Bounds.Width.ShouldBe(option.DesiredSize.Width);
        option.Bounds.Height.ShouldBe(option.DesiredSize.Height);
    }

    /// <summary>Verifies an explicitly stretched cell continues to consume its complete resolved track slot.</summary>
    [Fact]
    public void Layout_WhenCellExplicitlyStretches_FillsResolvedTrackSlot()
    {
        var option = new CheckBox
        {
            Content = new ControlText("Option"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var table = new Table { Width = Length.Cells(20), ShowHeader = false, ShowGridLines = false };
        table.Columns.Add(TableColumn.Fixed("Action", 10));
        table.Columns.Add(TableColumn.Fixed("Choice", 10));
        table.Rows.Add(new TableRow([
            new Button { Content = new ControlText("Run") },
            option
        ]));

        new Engine().Layout(table, new Size(20, 3));

        option.Bounds.ShouldBe(new Rect(10, 0, 10, 3));
    }

    /// <summary>Verifies horizontally scrolled headers, grid lines, row cells, hit testing, and rail chrome stay aligned.</summary>
    [Fact]
    public void Render_WhenHorizontallyScrolled_TranslatesCompleteTableContent()
    {
        var first = new ControlText("12345678");
        var table = new Table { ScrollBars = ScrollBars.Both };
        table.Columns.Add(TableColumn.Fixed("ABCDEFGH", 8));
        table.Columns.Add(TableColumn.Fixed("IJKLMNOP", 8));
        table.Rows.Add(new TableRow([first, new ControlText("abcdefgh")]));
        var size = new Size(10, 4);
        var engine = new Engine();
        engine.Layout(table, size);
        table.HorizontalOffset = 3;

        engine.Layout(table, size);
        using Frame frame = new(size);
        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("D");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("4");
        table.HitTest(new Point(0, 2)).ShouldBeSameAs(first);
        _ = table.HitTest(new Point(0, 3)).ShouldBeOfType<ScrollBar>();
    }

    /// <summary>Verifies simultaneous offsets may move the content origin above and left of the viewport.</summary>
    [Fact]
    public void Layout_WhenBothAxesScroll_AllowsSignedContentOrigin()
    {
        var table = new Table { ScrollBars = ScrollBars.Both };
        table.Columns.Add(TableColumn.Fixed("First", 8));
        table.Columns.Add(TableColumn.Fixed("Second", 8));

        for (var index = 0; index < 8; index++)
        {
            table.Rows.Add(new TableRow([
                new ControlText($"A{index}"),
                new ControlText($"B{index}")
            ]));
        }

        var engine = new Engine();
        var size = new Size(10, 5);
        engine.Layout(table, size);
        table.HorizontalOffset = 3;
        table.VerticalOffset = 3;

        engine.Layout(table, size);

        table.HorizontalOffset.ShouldBe(3);
        table.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies a pure scroll-origin arrangement neither remeasures cells nor remains invalidated.</summary>
    [Fact]
    public void Layout_WhenOnlyScrollOriginChanges_DoesNotRemeasureCellsOrRemainArrangeInvalidated()
    {
        var first = new ProbeControl(new Size(2, 1));
        var table = new Table { ScrollBars = ScrollBars.Both, ShowScrollBars = ShowScrollBars.Never };
        table.Columns.Add(TableColumn.Fixed("First", 8));
        table.Columns.Add(TableColumn.Fixed("Second", 8));
        table.Rows.Add(new TableRow([first, new ProbeControl(new Size(2, 1))]));
        var engine = new Engine();
        var size = new Size(10, 3);
        engine.Layout(table, size);
        var measurements = first.MeasureConstraints.Count;

        table.HorizontalOffset = 1;
        engine.Layout(table, size);

        first.MeasureConstraints.Count.ShouldBe(measurements);
        table.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies headers and light grid lines render around ordinary owned cell controls.</summary>
    [Fact]
    public void Render_WhenHeaderAndGridLinesAreEnabled_WritesHeaderCellsAndIntersections()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        var size = new Size(14, 4);
        new Engine().Layout(table, size);
        using Frame frame = new(size);

        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("N");
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("V");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(0, 1)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(6, 2)).ShouldBe("B");
    }

    /// <summary>Verifies an offset table keeps its header divider in the table's absolute coordinate space.</summary>
    [Fact]
    public void Render_WhenTableIsOffset_DrawsHeaderDividerBelowItsOwnHeader()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        table.Measure(new Constraint(width: 14, height: 4));
        table.Arrange(new Rect(2, 3, 14, 4));
        using Frame frame = new(new Size(20, 10));

        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 4)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies a header-only table has no phantom row gap or divider beneath its header.</summary>
    [Fact]
    public void Layout_WhenTableHasNoRows_UsesOnlyTheHeaderHeight()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fixed("Value", 5));
        var size = new Size(12, 4);
        new Engine().Layout(table, size);
        using Frame frame = new(size);

        table.Render(frame.Canvas);

        table.DesiredSize.ShouldBe(new Size(11, 1));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("N");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies a table taller than its viewport exposes vertical scroll via the intrinsic scroll surface.</summary>
    [Fact]
    public void Extent_WhenRowsExceedViewport_ExposesVerticalScroll()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Columns.Add(TableColumn.Fill("Value"));

        for (var index = 0; index < 40; index++)
        {
            table.Rows.Add(new TableRow([new ControlText($"Row {index}"), new ControlText("Value")]));
        }

        new Engine().Layout(table, new Size(30, 10));

        table.Extent.Height.ShouldBeGreaterThan(table.Viewport.Height);
    }

    /// <summary>Verifies a row must match the complete column count before any cells are attached.</summary>
    [Fact]
    public void Rows_WhenCellCountDiffersFromColumns_RejectsRowWithoutOwnershipTransfer()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("One"));
        table.Columns.Add(TableColumn.Auto("Two"));
        var cell = new ControlText("Only one");
        var row = new TableRow([cell]);

        _ = Should.Throw<ArgumentException>(() => table.Rows.Add(row));

        table.Rows.Count.ShouldBe(0);
        table.GetType().GetProperty("Children").ShouldBeNull();
        cell.Parent.ShouldBeNull();
    }

    /// <summary>Verifies column list operations preserve row shape before committing mutation.</summary>
    [Fact]
    public void Columns_WhenRowsExist_RejectCountChangesBeforeMutation()
    {
        // Arrange
        var name = TableColumn.Auto("Name");
        var value = TableColumn.Fill("Value");
        var replacement = TableColumn.Fixed("Replacement", 8);
        var table = new Table();
        table.Columns.Add(name);
        table.Columns.Insert(1, value);
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        var copy = new TableColumn[2];

        // Act and assert the non-structural IList surface
        table.Columns.CopyTo(copy, 0);
        copy.ShouldBe([name, value]);
        table.Columns.Contains(value).ShouldBeTrue();
        table.Columns.IndexOf(value).ShouldBe(1);
        table.Columns[1] = replacement;
        table.Columns[1].ShouldBe(replacement);

        // Act and assert rejected structural changes
        _ = Should.Throw<ArgumentException>(() => table.Columns.Add(value));
        _ = Should.Throw<ArgumentException>(() => table.Columns.Insert(0, value));
        _ = Should.Throw<ArgumentException>(() => table.Columns.RemoveAt(0));
        _ = Should.Throw<ArgumentException>(table.Columns.Clear);

        // Assert validation happened before mutation
        table.Columns.ShouldBe([name, replacement]);
    }

    /// <summary>Verifies row list operations atomically transfer every cell's retained ownership.</summary>
    [Fact]
    public void Rows_WhenListIsMutated_TransfersAndReleasesCellOwnership()
    {
        // Arrange
        var firstCell = new ControlText("First");
        var secondCell = new ControlText("Second");
        var replacementCell = new ControlText("Replacement");
        var first = new TableRow([firstCell]);
        var second = new TableRow([secondCell]);
        var replacement = new TableRow([replacementCell]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));

        // Act insertion and inspection
        table.Rows.Add(second);
        table.Rows.Insert(0, first);
        var copy = new TableRow[2];
        table.Rows.CopyTo(copy, 0);

        // Assert attached list contract
        copy.ShouldBe([first, second]);
        table.Rows.Contains(second).ShouldBeTrue();
        table.Rows.IndexOf(second).ShouldBe(1);
        _ = firstCell.Parent.ShouldNotBeNull();
        _ = secondCell.Parent.ShouldNotBeNull();

        // Act replacement, removal, and clear
        table.Rows[0] = replacement;
        table.Rows.Remove(second).ShouldBeTrue();
        table.Rows.Remove(second).ShouldBeFalse();
        table.Rows.Clear();

        // Assert every former cell is detached
        table.Rows.ShouldBeEmpty();
        firstCell.Parent.ShouldBeNull();
        secondCell.Parent.ShouldBeNull();
        replacementCell.Parent.ShouldBeNull();
    }

}
