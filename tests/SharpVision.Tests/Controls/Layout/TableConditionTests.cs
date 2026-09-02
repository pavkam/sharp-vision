// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies detached eager Table conditions: sort validation and key comparison rules,
/// edit transaction guards, foreign-row rejection, clipboard text extraction per cell type, row
/// validation after construction, SelectAll under empty or None conditions, glyph family
/// validation and cloning, and column/row collection edge cases.</summary>
public sealed class TableConditionTests
{
    private static Table CreateSortableTable(params string[] values)
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 6));

        foreach (var value in values)
        {
            table.Rows.Add(new TableRow([new ControlText(value)]));
        }

        return table;
    }

    private static string[] Names(Table table) =>
        [.. table.Rows.Select(row => ((ControlText) row.Cells[0]).Content)];

    #region Sorting

    /// <summary>Verifies resetting through an out-of-range column index throws before touching the
    /// active sort.</summary>
    [Theory]
    [InlineData(-2)]
    [InlineData(1)]
    public void SetSort_WhenResettingWithAnOutOfRangeColumn_ThrowsBeforeMutation(int columnIndex)
    {
        // Arrange
        var table = CreateSortableTable("B", "A");
        table.SortBy(0);
        var changes = 0;
        table.SortChanged += (_, _) => changes++;

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => table.SetSort(columnIndex, TableSortDirection.None));

        // Assert
        exception.ParamName.ShouldBe("columnIndex");
        table.SortColumnIndex.ShouldBe(0);
        table.SortDirection.ShouldBe(TableSortDirection.Ascending);
        Names(table).ShouldBe(["A", "B"]);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies a SortDirection observer that commits a newer sort on the same column
    /// suppresses the superseded transaction's SortChanged, publishing only the newer one.</summary>
    [Fact]
    public void SetSort_WhenDirectionObserverCommitsNewerSort_PublishesOnlyTheNewerSortChanged()
    {
        // Arrange
        var table = CreateSortableTable("B", "A", "C");
        table.SetSort(0, TableSortDirection.Ascending);
        var published = new List<(int Column, TableSortDirection Direction)>();
        table.SortChanged += (_, args) => published.Add((args.ColumnIndex, args.Direction));
        var reentered = false;
        table.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Table.SortDirection) && !reentered)
            {
                reentered = true;
                table.SetSort(0, TableSortDirection.Ascending);
            }
        };

        // Act
        table.SetSort(0, TableSortDirection.Descending);

        // Assert
        reentered.ShouldBeTrue();
        published.ShouldBe([(0, TableSortDirection.Ascending)]);
        table.SortDirection.ShouldBe(TableSortDirection.Ascending);
        Names(table).ShouldBe(["A", "B", "C"]);
    }

    /// <summary>Verifies a sort-key selector that returns null for a cell falls back to that
    /// cell's own text, so such rows still order deterministically (ordinal, uppercase first)
    /// among keyed rows in both directions.</summary>
    [Fact]
    public void SortBy_WhenSortKeySelectorReturnsNull_FallsBackToTheCellText()
    {
        // Arrange
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed(
            "Name",
            6,
            sortKey: cell => ((ControlText) cell).Content is "N1" or "N2" ? null : ((ControlText) cell).Content));
        foreach (var value in new[] { "b", "N1", "a", "N2" })
        {
            table.Rows.Add(new TableRow([new ControlText(value)]));
        }

        // Act ascending
        table.SortBy(0);

        // Assert
        Names(table).ShouldBe(["N1", "N2", "a", "b"]);

        // Act descending
        table.SortBy(0);

        // Assert
        Names(table).ShouldBe(["b", "a", "N2", "N1"]);
    }

    /// <summary>Verifies keys of unlike comparable types fall back to invariant ordinal text
    /// comparison instead of throwing from IComparable.CompareTo.</summary>
    [Fact]
    public void SortBy_WhenSortKeysMixIncomparableTypes_FallsBackToOrdinalText()
    {
        // Arrange
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed(
            "Value",
            6,
            sortKey: cell => ((ControlText) cell).Content switch
            {
                "ten" => 10,
                "two" => 2,
                _ => "9"
            }));
        foreach (var value in new[] { "ten", "nine", "two" })
        {
            table.Rows.Add(new TableRow([new ControlText(value)]));
        }

        // Act
        table.SortBy(0);

        // Assert: 2 < 10 numerically, and "10" and "2" both precede "9" ordinally
        Names(table).ShouldBe(["two", "ten", "nine"]);

        // Act descending
        table.SortBy(0);

        // Assert
        Names(table).ShouldBe(["nine", "ten", "two"]);
    }

    /// <summary>Verifies rows inserted while sorted descending splice into descending position,
    /// and a tie lands after the already-present equal row.</summary>
    [Fact]
    public void Rows_WhenInsertedWhileSortedDescending_SplicesIntoDescendingOrderAfterTies()
    {
        // Arrange
        var table = CreateSortableTable("a", "b");
        table.SetSort(0, TableSortDirection.Descending);
        Names(table).ShouldBe(["b", "a"]);
        var existingB = table.Rows[0];
        var sortChanges = 0;
        table.SortChanged += (_, _) => sortChanges++;

        // Act
        var c = new TableRow([new ControlText("c")]);
        var tie = new TableRow([new ControlText("b")]);
        table.Rows.Add(c);
        table.Rows.Add(tie);

        // Assert
        Names(table).ShouldBe(["c", "b", "b", "a"]);
        table.Rows[1].ShouldBe(existingB);
        table.Rows[2].ShouldBe(tie);
        sortChanges.ShouldBe(0);
    }

    #endregion

    #region Editing

    /// <summary>Verifies beginning an edit while another is open commits the prior edit (keeping
    /// its text) and moves the transaction to the new cell.</summary>
    [Fact]
    public void BeginEdit_WhenAnotherEditIsOpen_CommitsThePriorEditFirst()
    {
        // Arrange
        var first = new TextInput { Text = "one" };
        var second = new TextInput { Text = "two" };
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 6));
        var firstRow = new TableRow([first]);
        var secondRow = new TableRow([second]);
        table.Rows.Add(firstRow);
        table.Rows.Add(secondRow);
        var editing = new List<bool>();
        table.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Table.IsEditing))
            {
                editing.Add(table.IsEditing);
            }
        };
        table.BeginEdit(firstRow, 0).ShouldBeTrue();
        first.Text = "changed";

        // Act
        var began = table.BeginEdit(secondRow, 0);

        // Assert
        began.ShouldBeTrue();
        table.IsEditing.ShouldBeTrue();
        first.Text.ShouldBe("changed");
        table.ActiveCell.ShouldBe(new TableCellReference(secondRow, 0));
        editing.ShouldBe([true, false, true]);

        // Act cancel affects only the open transaction
        second.Text = "discarded";
        table.CancelEdit().ShouldBeTrue();

        // Assert
        second.Text.ShouldBe("two");
        first.Text.ShouldBe("changed");
        table.IsEditing.ShouldBeFalse();
    }

    /// <summary>Verifies CommitEdit and CancelEdit report false without a transaction and publish
    /// no IsEditing change.</summary>
    [Fact]
    public void CommitEditAndCancelEdit_WhenNotEditing_ReturnFalseWithoutNotification()
    {
        // Arrange
        var table = CreateSortableTable("a");
        var notifications = 0;
        table.PropertyChanged += (_, args) => notifications += args.PropertyName == nameof(Table.IsEditing) ? 1 : 0;

        // Act and assert
        table.CommitEdit().ShouldBeFalse();
        table.CancelEdit().ShouldBeFalse();
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies SelectRow, SelectCell, and BeginEdit reject a row owned by another table
    /// before touching any state.</summary>
    [Fact]
    public void Selection_WhenRowBelongsToAnotherTable_ThrowsArgumentExceptionBeforeMutation()
    {
        // Arrange
        var table = CreateSortableTable("a");
        var other = new Table();
        other.Columns.Add(TableColumn.Fixed("Name", 6));
        var foreign = new TableRow([new TextInput { Text = "x" }]);
        other.Rows.Add(foreign);
        var changes = 0;
        table.SelectionChanged += (_, _) => changes++;

        // Act and assert
        Should.Throw<ArgumentException>(() => table.SelectRow(foreign)).ParamName.ShouldBe("row");
        Should.Throw<ArgumentException>(() => table.SelectCell(foreign, 0)).ParamName.ShouldBe("row");
        Should.Throw<ArgumentException>(() => table.BeginEdit(foreign, 0)).ParamName.ShouldBe("row");
        table.ActiveRow.ShouldBeNull();
        table.SelectedRows.ShouldBeEmpty();
        table.IsEditing.ShouldBeFalse();
        changes.ShouldBe(0);
    }

    #endregion

    #region Clipboard and validation

    /// <summary>Verifies CopySelection reads the caption of input controls, the text of content
    /// controls wrapping Text or TextInput, and an empty string for any other cell.</summary>
    [Fact]
    public void CopySelection_WhenCellsAreInputsAndContentControls_UsesTheirTextOrEmpty()
    {
        // Arrange
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Fixed("Button", 4));
        table.Columns.Add(TableColumn.Fixed("Check", 4));
        table.Columns.Add(TableColumn.Fixed("Text", 4));
        table.Columns.Add(TableColumn.Fixed("Input", 4));
        table.Columns.Add(TableColumn.Fixed("Other", 4));
        var row = new TableRow([
            new Button("b"),
            new CheckBox { Text = "c" },
            new GroupBox { Content = new ControlText("g") },
            new GroupBox { Content = new TextInput { Text = "t" } },
            new ProbeControl(new Size(1, 1))
        ]);
        table.Rows.Add(row);
        table.SelectAll();

        // Act
        var copied = table.CopySelection();

        // Assert
        copied.ShouldBe("b\tc\tg\tt\t");
    }

    /// <summary>Verifies a row whose cell was disposed or adopted elsewhere after construction is
    /// rejected by Rows.Add without transferring ownership of its other cells.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rows_WhenACellBecameUnavailableAfterConstruction_RejectsTheRow(bool dispose)
    {
        // Arrange
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("A", 4));
        table.Columns.Add(TableColumn.Fixed("B", 4));
        var healthy = new ControlText("ok");
        var broken = new ControlText("broken");
        var row = new TableRow([healthy, broken]);

        if (dispose)
        {
            broken.Dispose();
        }
        else
        {
            _ = new Stack { Children = { broken } };
        }

        // Act
        var exception = Should.Throw<ArgumentException>(() => table.Rows.Add(row));

        // Assert
        exception.ParamName.ShouldBe("row");
        table.Rows.Count.ShouldBe(0);
        healthy.Parent.ShouldBeNull();
    }

    /// <summary>Verifies SelectAll with no rows, or under SelectionMode.None, leaves no selection
    /// and no active cell, and clears an earlier selection that survived a mode switch.</summary>
    [Fact]
    public void SelectAll_WhenNoRowsOrSelectionModeIsNone_LeavesNothingSelected()
    {
        // Arrange
        var empty = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        empty.Columns.Add(TableColumn.Fixed("Name", 6));
        var none = CreateSortableTable("a", "b");
        none.SelectAll();
        none.SelectedRows.Count.ShouldBe(1);
        none.SelectionMode = TableSelectionMode.None;
        var changes = 0;
        none.SelectionChanged += (_, _) => changes++;

        // Act
        empty.SelectAll();
        none.SelectAll();

        // Assert
        empty.SelectedRows.ShouldBeEmpty();
        empty.ActiveRow.ShouldBeNull();
        none.SelectedRows.ShouldBeEmpty();
        none.SelectedCells.ShouldBeEmpty();
        changes.ShouldBe(0);
    }

    #endregion

    #region Glyphs

    /// <summary>Verifies every TableGlyphs constructor rejects a glyph wider than one cell or a
    /// control rune, naming the offending parameter.</summary>
    [Fact]
    public void TableGlyphs_WhenAGlyphIsNotOneCellWide_ThrowsNamingTheParameter()
    {
        // Arrange
        var wide = new Rune('日');
        var control = new Rune('\t');
        var plain = new Rune('-');

        // Act and assert
        Should.Throw<ArgumentException>(() => new TableGlyphs(wide, plain, plain)).ParamName.ShouldBe("horizontal");
        Should.Throw<ArgumentException>(() => new TableGlyphs(plain, control, plain)).ParamName.ShouldBe("vertical");
        Should.Throw<ArgumentException>(() => new TableGlyphs(plain, plain, wide)).ParamName.ShouldBe("cross");
        Should.Throw<ArgumentException>(() => new TableGlyphs(plain, plain, plain, wide, plain)).ParamName.ShouldBe("sortAscending");
        Should.Throw<ArgumentException>(() => new TableGlyphs(plain, plain, plain, plain, control)).ParamName.ShouldBe("sortDescending");
        Should.Throw<ArgumentException>(() => new TableGlyphs(plain, plain, plain, plain, plain, wide, plain)).ParamName.ShouldBe("placeholder");
        Should.Throw<ArgumentException>(() => new TableGlyphs(plain, plain, plain, plain, plain, plain, wide)).ParamName.ShouldBe("placeholderError");
    }

    /// <summary>Verifies the shorter constructors keep the code-owned defaults for the glyphs they
    /// do not take, and the fragment clone is an equal but distinct instance.</summary>
    [Fact]
    public void TableGlyphs_WhenConstructedPartially_KeepsDefaultsAndClonesByValue()
    {
        // Arrange
        var defaults = TableStyle.Default.Glyphs;

        // Act
        var gridOnly = new TableGlyphs(new Rune('-'), new Rune('|'), new Rune('+'));
        var withSort = new TableGlyphs(new Rune('-'), new Rune('|'), new Rune('+'), new Rune('^'), new Rune('v'));
        var clone = ((IAppearanceFragment) withSort).Clone();

        // Assert
        gridOnly.SortAscending.ShouldBe(defaults.SortAscending);
        gridOnly.SortDescending.ShouldBe(defaults.SortDescending);
        gridOnly.Placeholder.ShouldBe(defaults.Placeholder);
        gridOnly.PlaceholderError.ShouldBe(defaults.PlaceholderError);
        withSort.SortAscending.ShouldBe(new Rune('^'));
        withSort.Placeholder.ShouldBe(defaults.Placeholder);
        clone.ShouldBe(withSort);
        clone.ShouldNotBeSameAs(withSort);
    }

    #endregion

    #region Collections

    /// <summary>Verifies assigning the identical column through the indexer is a no-op that leaves
    /// a laid-out table uninvalidated, while a different column invalidates measure.</summary>
    [Fact]
    public void Columns_WhenIndexerAssignsTheSameColumn_DoesNotInvalidate()
    {
        // Arrange
        var table = CreateSortableTable("a");
        new LayoutEngine().Layout(table, new Size(10, 3));
        table.Clear(Invalidation.All);

        // Act same value
        table.Columns[0] = table.Columns[0];

        // Assert
        table.Pending.ShouldBe(Invalidation.None);

        // Act different value
        table.Columns[0] = TableColumn.Fixed("Renamed", 6);

        // Assert
        table.Pending.HasFlag(Invalidation.Measure).ShouldBeTrue();
        table.Columns[0].Header.ShouldBe("Renamed");
    }

    /// <summary>Verifies the column and row collections report writable, enumerate through the
    /// non-generic interface, and treat clearing an empty column set as a no-op.</summary>
    [Fact]
    public void Collections_WhenQueriedThroughTheirInterfaces_ReportWritableAndEnumerate()
    {
        // Arrange
        var table = new Table();
        new LayoutEngine().Layout(table, new Size(10, 3));
        table.Clear(Invalidation.All);

        // Act clear on empty
        table.Columns.Clear();

        // Assert
        table.Pending.ShouldBe(Invalidation.None);
        table.Columns.IsReadOnly.ShouldBeFalse();
        table.Rows.IsReadOnly.ShouldBeFalse();

        // Act populate
        table.Columns.Add(TableColumn.Fixed("Name", 6));
        var first = new TableRow([new ControlText("a")]);
        var second = new TableRow([new ControlText("b")]);
        table.Rows.Add(second);
        table.Rows.Insert(0, first);

        // Assert
        System.Collections.IEnumerable columns = table.Columns;
        System.Collections.IEnumerable rowsSequence = table.Rows;
        columns.Cast<object>().Count().ShouldBe(1);
        rowsSequence.Cast<object>().ShouldBe([first, second]);
        table.Rows.IndexOf(second).ShouldBe(1);
        table.Rows.Contains(first).ShouldBeTrue();
        table.Columns.Contains(TableColumn.Fixed("Name", 6)).ShouldBeTrue();
        table.Columns.IndexOf(TableColumn.Fixed("Other", 6)).ShouldBe(-1);
        var rows = new TableRow[3];
        table.Rows.CopyTo(rows, 1);
        rows[0].ShouldBeNull();
        rows[1].ShouldBe(first);
        rows[2].ShouldBe(second);
    }

    #endregion
}
