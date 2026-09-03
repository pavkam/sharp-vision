// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies mounted eager Table keyboard navigation, cell editing gestures, pointer
/// selection modifiers, post-layout column and property mutation, wide-cell clipping, and glyph
/// families through rendered cells, focus ownership, and typed event arguments.</summary>
public sealed class TableInteractionTests
{
    private static Table CreateTable(
        TableSelectionMode selectionMode = TableSelectionMode.Row,
        bool showHeader = false,
        bool showGridLines = false,
        int columnWidth = 4)
    {
        var table = new Table
        {
            SelectionMode = selectionMode,
            ShowHeader = showHeader,
            ShowGridLines = showGridLines,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", columnWidth));
        table.Columns.Add(TableColumn.Fixed("Code", columnWidth));
        return table;
    }

    private static TableRow AddTextRow(Table table, string name, string code)
    {
        var row = new TableRow([new ControlText(name), new ControlText(code)]);
        table.Rows.Add(row);
        return row;
    }

    private static TableRow AddInputRow(Table table, string name, string code)
    {
        var row = new TableRow([new TextInput { Text = name }, new TextInput { Text = code }]);
        table.Rows.Add(row);
        return row;
    }

    // The component keyboard has no F2 encoding yet; CSI Q is the shared cursor/function-key
    // grammar the decoder maps to F2 without SS3 disambiguation timing.
    private static Task PressF2Async(ComponentSurface surface) =>
        surface.SendAsync("\u001b[Q"u8.ToArray(), "press F2");

    private static void ShouldHaveCellState(ControlBase cell, bool selected, bool current)
    {
        var state = cell.GetAppearanceState();
        state.HasFlag(VisualState.Selected).ShouldBe(selected);
        state.HasFlag(VisualState.Current).ShouldBe(current);
    }

    #region Keyboard navigation

    /// <summary>Verifies Left/Right move the active column inside the active row and clamp at the
    /// row edges without changing the row selection or publishing extra selection changes.</summary>
    [Fact]
    public async Task Keyboard_WhenLeftOrRightIsPressedUnderRowSelection_MovesActiveColumnAndClampsAtRowEdgesAsync()
    {
        // Arrange
        var table = CreateTable();
        var first = AddTextRow(table, "One", "A");
        _ = AddTextRow(table, "Two", "B");
        var selectionChanges = 0;
        table.SelectionChanged += (_, _) => selectionChanges++;
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        table.ActiveColumnIndex.ShouldBe(0);
        selectionChanges.ShouldBe(1);

        // Act and assert Right moves, then clamps
        await surface.Keyboard.PressAsync(Code.Right);
        table.ActiveColumnIndex.ShouldBe(1);
        table.ActiveRow.ShouldBe(first);
        await surface.Keyboard.PressAsync(Code.Right);
        table.ActiveColumnIndex.ShouldBe(1);

        // Act and assert Left moves, then clamps
        await surface.Keyboard.PressAsync(Code.Left);
        table.ActiveColumnIndex.ShouldBe(0);
        await surface.Keyboard.PressAsync(Code.Left);
        table.ActiveColumnIndex.ShouldBe(0);

        // Assert the row selection never re-published
        table.SelectedRows.ShouldBe([first]);
        selectionChanges.ShouldBe(1);
        surface.ShouldHaveFocus(table);
    }

    /// <summary>Verifies Home/End under a cell selection mode select the very first and very last
    /// cell of the table (row and column both move), not merely the endpoint row.</summary>
    [Theory]
    [InlineData(TableSelectionMode.Cell)]
    [InlineData(TableSelectionMode.MultipleCells)]
    public async Task Keyboard_WhenHomeOrEndIsPressedUnderCellSelection_SelectsTheFirstOrLastCellAsync(
        TableSelectionMode mode)
    {
        // Arrange
        var table = CreateTable(mode);
        var first = AddTextRow(table, "One", "A");
        var second = AddTextRow(table, "Two", "B");
        var third = AddTextRow(table, "Three", "C");
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 1));
        table.ActiveCell.ShouldBe(new TableCellReference(second, 0));

        // Act End
        await surface.Keyboard.PressAsync(Code.End);

        // Assert the last cell
        table.ActiveCell.ShouldBe(new TableCellReference(third, 1));
        table.SelectedCells.ShouldBe([new TableCellReference(third, 1)]);

        // Act Home
        await surface.Keyboard.PressAsync(Code.Home);

        // Assert the first cell
        table.ActiveCell.ShouldBe(new TableCellReference(first, 0));
        table.SelectedCells.ShouldBe([new TableCellReference(first, 0)]);
    }

    /// <summary>Verifies Home/End under row selection jump to the endpoint rows while keeping the
    /// active column the user was on, selecting the endpoint row.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeOrEndIsPressedUnderRowSelection_KeepsTheActiveColumnAsync()
    {
        // Arrange
        var table = CreateTable();
        var first = AddTextRow(table, "One", "A");
        var second = AddTextRow(table, "Two", "B");
        var third = AddTextRow(table, "Three", "C");
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(5, 1));
        table.ActiveCell.ShouldBe(new TableCellReference(second, 1));

        // Act and assert End
        await surface.Keyboard.PressAsync(Code.End);
        table.ActiveCell.ShouldBe(new TableCellReference(third, 1));
        table.SelectedRows.ShouldBe([third]);

        // Act and assert Home
        await surface.Keyboard.PressAsync(Code.Home);
        table.ActiveCell.ShouldBe(new TableCellReference(first, 1));
        table.SelectedRows.ShouldBe([first]);
    }

    /// <summary>Verifies an arrow move under single-cell selection reports exactly the cell that
    /// joined and the cell that left through the typed selection arguments.</summary>
    [Fact]
    public async Task Keyboard_WhenArrowMovesUnderCellSelection_ReportsExactAddedAndRemovedCellsAsync()
    {
        // Arrange
        var table = CreateTable(TableSelectionMode.Cell);
        var first = AddTextRow(table, "One", "A");
        var second = AddTextRow(table, "Two", "B");
        var changes = new List<TableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, args) => changes.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 0));

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        changes.Count.ShouldBe(2);
        changes[1].AddedCells.ShouldBe([new TableCellReference(second, 0)]);
        changes[1].RemovedCells.ShouldBe([new TableCellReference(first, 0)]);
        changes[1].AddedRows.ShouldBeEmpty();
        changes[1].RemovedRows.ShouldBeEmpty();
        table.SelectedCells.ShouldBe([new TableCellReference(second, 0)]);
        ShouldHaveCellState(second.Cells[0], selected: true, current: true);
        ShouldHaveCellState(first.Cells[0], selected: false, current: false);
    }

    /// <summary>Verifies PageDown/PageUp under a multi-cell selection mode page the active row by
    /// the accumulated visible row extent while keeping the active column, selecting only the
    /// landing cell.</summary>
    [Fact]
    public async Task Keyboard_WhenPageKeysArePressedUnderMultipleCellsSelection_PageTheRowAndKeepTheColumnAsync()
    {
        // Arrange
        var table = CreateTable(TableSelectionMode.MultipleCells);
        var rows = Enumerable.Range(0, 6).Select(index => AddTextRow(table, $"R{index}", $"{index}")).ToArray();
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(5, 0));
        table.ActiveCell.ShouldBe(new TableCellReference(rows[0], 1));

        // Act PageDown
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert one viewport of rows was accumulated
        table.ActiveCell.ShouldBe(new TableCellReference(rows[3], 1));
        table.SelectedCells.ShouldBe([new TableCellReference(rows[3], 1)]);
        table.VerticalOffset.ShouldBe(1);

        // Act PageUp
        await surface.Keyboard.PressAsync(Code.PageUp);

        // Assert
        table.ActiveCell.ShouldBe(new TableCellReference(rows[0], 1));
        table.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies a focused table with columns but no rows leaves navigation keys
    /// unhandled, so an enclosing scroll host still receives them.</summary>
    [Fact]
    public async Task Keyboard_WhenTableHasNoRows_LeavesNavigationKeysToTheEnclosingScrollHostAsync()
    {
        // Arrange
        var table = CreateTable(showHeader: true);
        table.VerticalAlignment = VerticalAlignment.Top;
        table.Height = Length.Cells(1);
        var host = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children =
            {
                table,
                new ControlText("Filler") { Height = Length.Cells(20) }
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(8, 4),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        surface.ShouldHaveFocus(table);
        host.VerticalOffset.ShouldBe(0);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        host.VerticalOffset.ShouldBe(1);
        table.ActiveRow.ShouldBeNull();
    }

    /// <summary>Verifies arrow keys at an edge of a populated table stay handled - the enclosing
    /// scroll host never scrolls out from under the still-focused table.</summary>
    [Theory]
    [InlineData(Code.Up)]
    [InlineData(Code.Down)]
    [InlineData(Code.Left)]
    [InlineData(Code.Right)]
    public async Task Keyboard_WhenActiveCellIsAtAnEdge_KeepsArrowKeysFromScrollingTheEnclosingHostAsync(Code code)
    {
        // Arrange
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Height = Length.Cells(1)
        };
        table.Columns.Add(TableColumn.Fixed("Name", 4));
        var only = new TableRow([new ControlText("One")]);
        table.Rows.Add(only);
        var host = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children =
            {
                table,
                new ControlText("Filler") { Height = Length.Cells(20) }
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(8, 4),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        table.ActiveRow.ShouldBe(only);

        // Act
        await surface.Keyboard.PressAsync(code);

        // Assert
        host.VerticalOffset.ShouldBe(0);
        host.HorizontalOffset.ShouldBe(0);
        table.ActiveCell.ShouldBe(new TableCellReference(only, 0));
    }

    /// <summary>Verifies Enter on a freshly focused table with no active cell activates the first
    /// row, reporting the row, its index, and the keyboard cause.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterIsPressedBeforeAnyCellIsActive_InvokesTheFirstRowWithKeyboardCauseAsync()
    {
        // Arrange
        var table = CreateTable();
        var first = AddTextRow(table, "One", "A");
        _ = AddTextRow(table, "Two", "B");
        var invoked = new List<TableRowInvokedEventArgs>();
        table.RowInvoked += (_, args) => invoked.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(table);
        table.ActiveRow.ShouldBeNull();

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        var args = invoked.ShouldHaveSingleItem();
        args.Row.ShouldBe(first);
        args.RowIndex.ShouldBe(0);
        args.Cause.ShouldBe(ActivationCause.Keyboard);
        table.ActiveCell.ShouldBe(new TableCellReference(first, 0));
    }

    /// <summary>Verifies Enter on an active editable cell opens an edit transaction (focusing the
    /// editor with its text selected) instead of invoking the row again.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterIsPressedOnAnEditableCell_BeginsEditingInsteadOfInvokingAsync()
    {
        // Arrange
        var table = CreateTable(columnWidth: 8);
        var row = AddInputRow(table, "One", "A");
        var editor = (TextInput) row.Cells[0];
        var invoked = 0;
        table.RowInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 1));
        invoked.ShouldBe(1);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        table.IsEditing.ShouldBeTrue();
        invoked.ShouldBe(1);
        surface.ShouldHaveFocus(editor);
        editor.SelectionLength.ShouldBe(3);
    }

    /// <summary>Verifies F2 begins editing only for an active, writable TextInput cell: a plain
    /// text cell, a read-only column, and a read-only editor all stay out of edit mode.</summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public async Task Keyboard_WhenF2IsPressed_BeginsEditingOnlyForAWritableTextInputCellAsync(int column, bool expected)
    {
        // Arrange
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Editable", 6));
        table.Columns.Add(TableColumn.Fixed("Text", 6));
        table.Columns.Add(TableColumn.Fixed("Locked", 6, isReadOnly: true));
        table.Columns.Add(TableColumn.Fixed("ReadOnly", 6));
        var row = new TableRow([
            new TextInput { Text = "a" },
            new ControlText("b"),
            new TextInput { Text = "c" },
            new TextInput { Text = "d", IsReadOnly = true }
        ]);
        table.Rows.Add(row);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(24, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point((column * 6) + 1, 1));
        table.ActiveColumnIndex.ShouldBe(column);

        // Act
        await PressF2Async(surface);

        // Assert
        table.IsEditing.ShouldBe(expected);
        surface.ShouldHaveFocus(expected ? row.Cells[column] : table);
    }

    /// <summary>Verifies Tab and Shift+Tab while editing commit the typed text and move the active
    /// cell to the adjacent column without the keystroke leaving the table.</summary>
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    public async Task Keyboard_WhenTabIsPressedWhileEditing_CommitsAndMovesToTheAdjacentCellAsync(bool shift, int expectedColumn)
    {
        // Arrange
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("A", 8));
        table.Columns.Add(TableColumn.Fixed("B", 8));
        table.Columns.Add(TableColumn.Fixed("C", 8));
        var row = new TableRow([
            new TextInput { Text = "one" },
            new TextInput { Text = "two" },
            new TextInput { Text = "six" }
        ]);
        table.Rows.Add(row);
        var edited = (TextInput) row.Cells[1];
        var sibling = new Button("Next");
        var host = new Stack { Children = { table, sibling } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(24, 6),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(9, 1));
        await PressF2Async(surface);
        table.IsEditing.ShouldBeTrue();
        await surface.Keyboard.TypeAsync("x");
        edited.Text.ShouldBe("x");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab, shift ? Modifiers.Shift : Modifiers.None);

        // Assert
        table.IsEditing.ShouldBeFalse();
        edited.Text.ShouldBe("x");
        table.ActiveCell.ShouldBe(new TableCellReference(row, expectedColumn));
        surface.ShouldHaveState(sibling, VisualState.Normal);
        table.ContainsFocus.ShouldBeTrue();
    }

    /// <summary>Verifies a Down arrow whose active-row notification synchronously removes the
    /// just-activated row completes without throwing and leaves the table pointing at a live row,
    /// with nothing left selected for the vanished row.</summary>
    [Fact]
    public async Task Keyboard_WhenActiveRowObserverRemovesTheTargetRow_RepairsToALiveRowWithoutThrowingAsync()
    {
        // Arrange
        var table = CreateTable();
        var first = AddTextRow(table, "One", "A");
        var second = AddTextRow(table, "Two", "B");
        var third = AddTextRow(table, "Three", "C");
        var removed = false;
        table.PropertyChanged += (_, args) =>
        {
            if (!removed && args.PropertyName == nameof(Table.ActiveRow) && ReferenceEquals(table.ActiveRow, second))
            {
                removed = true;
                _ = table.Rows.Remove(second);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        table.ActiveRow.ShouldBe(first);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        removed.ShouldBeTrue();
        table.Rows.ShouldBe([first, third]);
        _ = table.ActiveRow.ShouldNotBeNull();
        table.Rows.ShouldContain(table.ActiveRow);
        table.SelectedRows.ShouldNotContain(second);
        surface.Cell(new Point(0, 1)).Text.ShouldBe("T");
        surface.Cell(new Point(0, 2)).Text.ShouldBe(" ");
    }

    #endregion

    #region Pointer gestures

    /// <summary>Verifies clicking a different cell while an edit is open commits the typed text
    /// rather than reverting it, and moves the active cell to the clicked one.</summary>
    [Fact]
    public async Task Pointer_WhenAnotherCellIsClickedWhileEditing_CommitsTheEditAsync()
    {
        // Arrange
        var table = CreateTable(columnWidth: 8);
        var first = AddInputRow(table, "One", "A");
        var second = AddInputRow(table, "Two", "B");
        var edited = (TextInput) first.Cells[0];
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(16, 6),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 1));
        await PressF2Async(surface);
        await surface.Keyboard.TypeAsync("x");

        // Act
        await surface.Pointer.ClickAsync(table, new Point(0, 4));

        // Assert
        table.IsEditing.ShouldBeFalse();
        edited.Text.ShouldBe("x");
        table.ActiveCell.ShouldBe(new TableCellReference(second, 0));
        table.SelectedRows.ShouldBe([second]);
    }

    /// <summary>Verifies a single click on the very cell being edited (outside the multi-click
    /// window) keeps the edit transaction open with its text intact.</summary>
    [Fact]
    public async Task Pointer_WhenTheEditedCellIsClickedAgain_KeepsTheEditOpenAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var table = CreateTable(columnWidth: 8);
        var row = AddInputRow(table, "One", "A");
        var edited = (TextInput) row.Cells[0];
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(16, 3),
            clock,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 1));
        await PressF2Async(surface);
        await surface.Keyboard.TypeAsync("x");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(2), "leave the multi-click window");

        // Act
        await surface.Pointer.ClickAsync(table, new Point(1, 1));

        // Assert
        table.IsEditing.ShouldBeTrue();
        edited.Text.ShouldBe("x");
        surface.ShouldHaveFocus(edited);
    }

    /// <summary>Verifies a double click on an editable cell begins editing and reports both
    /// activations with the pointer cause.</summary>
    [Fact]
    public async Task Pointer_WhenAnEditableCellIsDoubleClicked_BeginsEditingAndInvokesWithPointerCauseAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var table = CreateTable(columnWidth: 8);
        var row = AddInputRow(table, "One", "A");
        var invoked = new List<TableRowInvokedEventArgs>();
        table.RowInvoked += (_, args) => invoked.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(16, 3),
            clock,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(table, new Point(1, 1));
        table.IsEditing.ShouldBeFalse();
        await surface.Pointer.ClickAsync(table, new Point(1, 1));

        // Assert
        table.IsEditing.ShouldBeTrue();
        surface.ShouldHaveFocus(row.Cells[0]);
        invoked.Count.ShouldBe(2);
        invoked.ShouldAllBe(args => args.Cause == ActivationCause.Pointer && args.Row == row && args.RowIndex == 0);
    }

    /// <summary>Verifies a single click into an editable cell selects and activates it without
    /// opening an edit: typed characters must not reach the cell outside an edit transaction,
    /// and Escape after a later F2 still restores the original text.</summary>
    [Fact]
    public async Task Pointer_WhenAnEditableCellIsSingleClicked_TypingDoesNotBypassTheEditTransactionAsync()
    {
        // Arrange
        var table = CreateTable(columnWidth: 8);
        var row = AddInputRow(table, "One", "A");
        var editor = (TextInput) row.Cells[0];
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(table, new Point(1, 1));
        table.IsEditing.ShouldBeFalse();
        surface.ShouldHaveFocus(table);
        await surface.Keyboard.TypeAsync("z");

        // Assert typing outside a transaction changed nothing
        editor.Text.ShouldBe("One");
        table.IsEditing.ShouldBeFalse();
        table.ActiveCell.ShouldBe(new TableCellReference(row, 0));

        // Act F2, type, Escape
        await PressF2Async(surface);
        table.IsEditing.ShouldBeTrue();
        await surface.Keyboard.TypeAsync("z");
        editor.Text.ShouldBe("z");
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        editor.Text.ShouldBe("One");
        table.IsEditing.ShouldBeFalse();
    }

    /// <summary>Verifies a header press past the last column's trailing edge is not a sort
    /// gesture: no sort is applied and no sort event is published.</summary>
    [Fact]
    public async Task Pointer_WhenHeaderGapBeyondTheLastColumnIsClicked_DoesNotSortAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", 4));
        table.Rows.Add(new TableRow([new ControlText("B")]));
        table.Rows.Add(new TableRow([new ControlText("A")]));
        var sorts = 0;
        table.SortChanged += (_, _) => sorts++;
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(table, new Point(8, 0));

        // Assert
        table.SortDirection.ShouldBe(TableSortDirection.None);
        table.SortColumnIndex.ShouldBe(-1);
        sorts.ShouldBe(0);
        surface.Cell(new Point(0, 1)).Text.ShouldBe("B");
        surface.ShouldHaveFocus(table);
    }

    /// <summary>Verifies a click under SelectionMode.None activates and invokes the row without
    /// selecting anything or publishing a selection change.</summary>
    [Fact]
    public async Task Pointer_WhenCellIsClickedUnderNoneSelection_ActivatesAndInvokesWithoutSelectingAsync()
    {
        // Arrange
        var table = CreateTable(TableSelectionMode.None);
        _ = AddTextRow(table, "One", "A");
        var second = AddTextRow(table, "Two", "B");
        var selectionChanges = 0;
        var invoked = new List<TableRowInvokedEventArgs>();
        table.SelectionChanged += (_, _) => selectionChanges++;
        table.RowInvoked += (_, args) => invoked.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(table, new Point(5, 1));

        // Assert
        table.ActiveCell.ShouldBe(new TableCellReference(second, 1));
        table.SelectedRows.ShouldBeEmpty();
        table.SelectedCells.ShouldBeEmpty();
        selectionChanges.ShouldBe(0);
        var args = invoked.ShouldHaveSingleItem();
        args.Row.ShouldBe(second);
        args.RowIndex.ShouldBe(1);
        args.Cause.ShouldBe(ActivationCause.Pointer);
        ShouldHaveCellState(second.Cells[1], selected: false, current: true);
    }

    /// <summary>Verifies pointer selection under MultipleCells: Shift-click bands the anchor and
    /// target columns across every row between them, and Control-click toggles one cell.</summary>
    [Fact]
    public async Task Pointer_WhenCellsAreClickedUnderMultipleCellsSelection_BandsWithShiftAndTogglesWithControlAsync()
    {
        // Arrange
        var table = CreateTable(TableSelectionMode.MultipleCells);
        var first = AddTextRow(table, "One", "A");
        var second = AddTextRow(table, "Two", "B");
        var third = AddTextRow(table, "Three", "C");
        var changes = new List<TableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, args) => changes.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act anchor then Shift-click the opposite corner
        await surface.Pointer.ClickAsync(first.Cells[0]);
        await surface.Pointer.ClickAsync(third.Cells[1], Modifiers.Shift);

        // Assert the full band in display order
        table.SelectedCells.ShouldBe([
            new TableCellReference(first, 0),
            new TableCellReference(first, 1),
            new TableCellReference(second, 0),
            new TableCellReference(second, 1),
            new TableCellReference(third, 0),
            new TableCellReference(third, 1)
        ]);

        // Act Control-click removes one interior cell
        await surface.Pointer.ClickAsync(second.Cells[0], Modifiers.Control);

        // Assert
        table.SelectedCells.Count.ShouldBe(5);
        table.SelectedCells.ShouldNotContain(new TableCellReference(second, 0));
        changes[^1].RemovedCells.ShouldBe([new TableCellReference(second, 0)]);
        changes[^1].AddedCells.ShouldBeEmpty();
        ShouldHaveCellState(second.Cells[0], selected: false, current: true);
        ShouldHaveCellState(second.Cells[1], selected: true, current: false);

        // Act Control-click re-adds it
        await surface.Pointer.ClickAsync(second.Cells[0], Modifiers.Control);

        // Assert
        table.SelectedCells.Count.ShouldBe(6);
        changes[^1].AddedCells.ShouldBe([new TableCellReference(second, 0)]);
    }

    /// <summary>Verifies pointer selection under MultipleRows: Shift-click selects the row range
    /// and Control-click toggles one row, reporting exactly the removed row.</summary>
    [Fact]
    public async Task Pointer_WhenRowsAreClickedUnderMultipleRowsSelection_RangesWithShiftAndTogglesWithControlAsync()
    {
        // Arrange
        var table = CreateTable(TableSelectionMode.MultipleRows);
        var first = AddTextRow(table, "One", "A");
        var second = AddTextRow(table, "Two", "B");
        var third = AddTextRow(table, "Three", "C");
        var changes = new List<TableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, args) => changes.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(first.Cells[0]);
        await surface.Pointer.ClickAsync(third.Cells[0], Modifiers.Shift);
        table.SelectedRows.ShouldBe([first, second, third]);
        await surface.Pointer.ClickAsync(second.Cells[1], Modifiers.Control);

        // Assert
        table.SelectedRows.ShouldBe([first, third]);
        changes[^1].RemovedRows.ShouldBe([second]);
        changes[^1].AddedRows.ShouldBeEmpty();
        table.ActiveCell.ShouldBe(new TableCellReference(second, 1));
        ShouldHaveCellState(second.Cells[0], selected: false, current: false);
        ShouldHaveCellState(second.Cells[1], selected: false, current: true);
        ShouldHaveCellState(third.Cells[0], selected: true, current: false);
    }

    /// <summary>Verifies a focusable Button cell still participates in the table gesture: the
    /// click selects and invokes the row, and the button itself is clicked too.</summary>
    [Fact]
    public async Task Pointer_WhenAButtonCellIsClicked_SelectsTheRowAndClicksTheButtonAsync()
    {
        // Arrange
        var table = CreateTable();
        var button = new Button("Go");
        var row = new TableRow([new ControlText("One"), button]);
        table.Rows.Add(row);
        var clicks = 0;
        var invoked = 0;
        button.Click += (_, _) => clicks++;
        table.RowInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);
        surface.ShouldHaveState(button, VisualState.IsPointerOver);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(button);
        surface.ShouldHaveState(button, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(1);
        invoked.ShouldBe(1);
        table.SelectedRows.ShouldBe([row]);
        table.ActiveCell.ShouldBe(new TableCellReference(row, 1));
    }

    #endregion

    #region Mutation after layout

    /// <summary>Verifies toggling ShowHeader after layout moves the data rows and their hit targets
    /// up by the header height and back down again.</summary>
    [Fact]
    public async Task Property_WhenShowHeaderIsToggledAfterLayout_ShiftsRowsAndHitTargetsAsync()
    {
        // Arrange
        var table = CreateTable(showHeader: true);
        var first = AddTextRow(table, "One", "A");
        var second = AddTextRow(table, "Two", "B");
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("N");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("O");

        // Act hide the header
        await surface.UpdateAsync(() => table.ShowHeader = false, "hide the header");

        // Assert rows moved up and hit targets follow
        surface.Cell(new Point(0, 0)).Text.ShouldBe("O");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("T");
        surface.Cell(new Point(0, 2)).Text.ShouldBe(" ");
        await surface.Pointer.ClickAsync(table, new Point(0, 1));
        table.ActiveRow.ShouldBe(second);

        // Act restore the header
        await surface.UpdateAsync(() => table.ShowHeader = true, "show the header");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("N");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("O");
        await surface.Pointer.ClickAsync(table, new Point(0, 1));
        table.ActiveRow.ShouldBe(first);
    }

    /// <summary>Verifies changing ColumnSpacing after layout moves the second column and its hit
    /// target by the new gap.</summary>
    [Fact]
    public async Task Property_WhenColumnSpacingChangesAfterLayout_RepositionsTheSecondColumnAsync()
    {
        // Arrange
        var table = CreateTable();
        var row = AddTextRow(table, "One", "AB");
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(4, 0)).Text.ShouldBe("A");

        // Act
        await surface.UpdateAsync(() => table.ColumnSpacing = 2, "widen the column gap");

        // Assert
        surface.Cell(new Point(4, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(5, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(6, 0)).Text.ShouldBe("A");
        await surface.Pointer.ClickAsync(table, new Point(6, 0));
        table.ActiveCell.ShouldBe(new TableCellReference(row, 1));
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        table.ActiveCell.ShouldBe(new TableCellReference(row, 0));
    }

    /// <summary>Verifies removing a column from a row-less table after layout collapses the
    /// remaining header captions left, and rows added afterwards land in the new geometry.</summary>
    [Fact]
    public async Task Columns_WhenColumnIsRemovedAfterLayout_CollapsesTheRemainingHeadersLeftAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("AA", 3));
        table.Columns.Add(TableColumn.Fixed("BB", 3));
        table.Columns.Add(TableColumn.Fixed("CC", 3));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(9, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("AA BB CC ");

        // Act
        await surface.UpdateAsync(() => table.Columns.RemoveAt(1), "remove the middle column");

        // Assert
        surface.ShouldRender("AA CC    ");
        await surface.UpdateAsync(
            () => table.Rows.Add(new TableRow([new ControlText("1"), new ControlText("2")])),
            "add a two-cell row");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("1");
        surface.Cell(new Point(3, 1)).Text.ShouldBe("2");
    }

    /// <summary>Verifies replacing a column through the indexer after layout redraws its caption
    /// and width while the rows keep their cells.</summary>
    [Fact]
    public async Task Columns_WhenIndexerReplacesAColumnAfterLayout_RedrawsTheHeaderAndReflowsCellsAsync()
    {
        // Arrange
        var table = CreateTable(showHeader: true);
        _ = AddTextRow(table, "One", "ABC");
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(10, 2),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(4, 0)).Text.ShouldBe("C");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("A");

        // Act
        await surface.UpdateAsync(() => table.Columns[0] = TableColumn.Fixed("Id", 2), "narrow the first column");

        // Assert
        surface.ShouldRender("IdCode    \nOnABC     ");
    }

    /// <summary>Verifies clearing every column of a row-less table after layout blanks the header
    /// and re-adding columns and rows renders the rebuilt table.</summary>
    [Fact]
    public async Task Columns_WhenClearedAndRebuiltAfterLayout_RendersTheRebuiltTableAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", 4));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(6, 2),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("N");

        // Act clear
        await surface.UpdateAsync(table.Columns.Clear, "clear the columns");

        // Assert blank
        surface.ShouldRender("      \n      ");

        // Act rebuild
        await surface.UpdateAsync(
            () =>
            {
                table.Columns.Add(TableColumn.Fixed("Id", 2));
                table.Rows.Add(new TableRow([new ControlText("7")]));
            },
            "rebuild columns and rows");

        // Assert
        surface.ShouldRender("Id    \n7     ");
    }

    /// <summary>Verifies a table mounted without any column renders nothing and reflows once
    /// columns and rows arrive after layout.</summary>
    [Fact]
    public async Task Render_WhenTableHasNoColumns_RendersNothingThenReflowsWhenContentArrivesAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(6, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("      \n      ");
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        table.ActiveRow.ShouldBeNull();

        // Act
        await surface.UpdateAsync(
            () =>
            {
                table.Columns.Add(TableColumn.Fixed("Id", 2));
                table.Rows.Add(new TableRow([new ControlText("7")]));
            },
            "add a column and a row");

        // Assert
        surface.ShouldRender("Id    \n7     ");
        await surface.Keyboard.PressAsync(Code.Down);
        table.ActiveRow.ShouldBe(table.Rows[0]);
    }

    /// <summary>Verifies a table resized down to a single cell either keeps its first header
    /// cell (no scrollbar reservation) or surrenders that cell to the overflow scrollbar
    /// affordance (automatic bars), and grows back to the full layout on the next resize.</summary>
    [Theory]
    [InlineData(ShowScrollBars.Never, true)]
    [InlineData(ShowScrollBars.WhenNeeded, false)]
    public async Task ResizeAsync_WhenHostShrinksToOneCell_ClipsOrReservesTheOnlyCellAndRecoversAsync(
        ShowScrollBars bars,
        bool showsContent)
    {
        // Arrange
        var table = CreateTable(showHeader: true);
        table.ShowScrollBars = bars;
        _ = AddTextRow(table, "One", "A");
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Act shrink
        await surface.ResizeAsync(new Size(1, 1));

        // Assert
        if (showsContent)
        {
            surface.ShouldRender("N");
            table.Viewport.ShouldBe(new Size(1, 1));
        }
        else
        {
            surface.Cell(default).Text.ShouldNotBe("N");
            table.Viewport.Width.ShouldBe(0);
        }

        // Act grow back
        await surface.ResizeAsync(new Size(8, 2));

        // Assert
        surface.ShouldRender("NameCode\nOne A   ");
    }

    #endregion

    #region Rendering

    /// <summary>Verifies cell text wider than its column - ASCII and double-width CJK alike - is
    /// clipped at the column boundary so the neighboring column's content stays intact.</summary>
    [Fact]
    public async Task Render_WhenCellTextIsWiderThanItsColumn_ClipsAtTheColumnBoundaryAsync()
    {
        // Arrange
        var table = CreateTable();
        _ = AddTextRow(table, "Abcdefgh", "Z");
        _ = AddTextRow(table, "日本語", "Y");
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("AbcdZ   \n日本Y   ");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("Y");
    }

    /// <summary>Verifies the grid-only TableGlyphs constructor drives the rendered rules while
    /// the sort indicators keep their code-owned defaults.</summary>
    [Fact]
    public async Task Render_WhenGlyphsUseTheGridOnlyConstructor_DrawsCustomRulesAndDefaultSortIndicatorAsync()
    {
        // Arrange
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Abc", 3));
        table.Columns.Add(TableColumn.Fixed("Def", 3));
        table.Rows.Add(new TableRow([new ControlText("1"), new ControlText("2")]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(7, 3),
            TestContext.Current.CancellationToken);
        var defaultAscending = table.ActualStyle.Glyphs.SortAscending;

        // Act
        await surface.UpdateAsync(
            () => table.Style = table.ActualStyle with { Glyphs = new TableGlyphs(new Rune('='), new Rune('!'), new Rune('+')) },
            "apply custom grid glyphs");

        // Assert rules
        surface.ShouldRender("Abc!Def\n===+===\n1  !2  ");
        table.ActualStyle.Glyphs.SortAscending.ShouldBe(defaultAscending);

        // Act sort and assert the default indicator survives
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        table.SortDirection.ShouldBe(TableSortDirection.Ascending);
        surface.Cell(new Point(2, 0)).Text.ShouldBe(defaultAscending.ToString());
    }

    /// <summary>Verifies the sort-aware TableGlyphs constructor drives the rendered ascending and
    /// descending indicators while the placeholder glyphs keep their defaults.</summary>
    [Fact]
    public async Task Render_WhenGlyphsUseTheSortConstructor_DrawsCustomSortIndicatorsAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", 4));
        table.Rows.Add(new TableRow([new ControlText("B")]));
        table.Rows.Add(new TableRow([new ControlText("A")]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(6, 3),
            TestContext.Current.CancellationToken);
        var defaultPlaceholder = table.ActualStyle.Glyphs.Placeholder;
        var defaultError = table.ActualStyle.Glyphs.PlaceholderError;

        // Act
        await surface.UpdateAsync(
            () => table.Style = table.ActualStyle with
            {
                Glyphs = new TableGlyphs(new Rune('-'), new Rune('|'), new Rune('+'), new Rune('^'), new Rune('v'))
            },
            "apply custom sort glyphs");
        await surface.Pointer.ClickAsync(table, new Point(0, 0));

        // Assert ascending
        surface.Cell(new Point(3, 0)).Text.ShouldBe("^");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("A");

        // Act and assert descending
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        surface.Cell(new Point(3, 0)).Text.ShouldBe("v");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("B");
        table.ActualStyle.Glyphs.Placeholder.ShouldBe(defaultPlaceholder);
        table.ActualStyle.Glyphs.PlaceholderError.ShouldBe(defaultError);
    }

    #endregion
}
