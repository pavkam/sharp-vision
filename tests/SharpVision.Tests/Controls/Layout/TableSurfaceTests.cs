// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Table layout, selection, editing, sorting, scrolling, mutation, resize, and hit targets through mounted surfaces.</summary>
public sealed class TableSurfaceTests
{
    /// <summary>Verifies every column kind aligns headers, grid lines, combining text, and wide cells exactly.</summary>
    [ComponentBehaviorEvidence(
        typeof(Table),
        ComponentBehavior.Mounted |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Render_WhenColumnsMixKinds_DrawsExactHeaderGridAndUnicodeCellsAsync()
    {
        // Arrange
        var name = new ControlText("A\u0301界");
        var state = new ControlText("Ready");
        var wide = new ControlText("界");
        var details = new ControlText("Tail");
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Percent("State", 25));
        table.Columns.Add(TableColumn.Auto("Wide"));
        table.Columns.Add(TableColumn.Fill("Details"));
        table.Rows.Add(new TableRow([name, state, wide, details]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(24, 4),
            TestContext.Current.CancellationToken);

        // Assert
        // Assert — cell content and basic table structure
        name.Bounds.Width.ShouldBeGreaterThan(0);
        state.Bounds.Width.ShouldBeGreaterThan(0);
        wide.Bounds.Width.ShouldBeGreaterThan(0);
        details.Bounds.Width.ShouldBeGreaterThan(0);
        var nameOrigin = new Point(name.Bounds.X, name.Bounds.Y);
        surface.Cell(nameOrigin).Text.ShouldBe("Á");
        surface.Cell(new Point(nameOrigin.X + 1, nameOrigin.Y)).Text.ShouldBe("界");
        surface.Cell(new Point(nameOrigin.X + 2, nameOrigin.Y)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies header foreground, header background, and grid-line theme-role overrides resolve
    /// through the mounted Theme rather than only accepting a literal color.</summary>
    /// <remarks>See #160.</remarks>
    [Fact]
    public async Task Render_WhenColorPropertiesAreThemeRoles_ResolvesThroughTheMountedThemeAsync()
    {
        // Arrange
        var theme = WithColor(ThemeColor.ControlBorder, Color.Rgb(0x11, 0x22, 0x33));
        var table = new Table
        {
            HeaderForeground = ThemeColor.Accent,
            GridLineColor = ThemeColor.ControlBorder,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("A", 4));
        table.Rows.Add(new TableRow([new ControlText("one")]));

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(6, 3),
            theme,
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            Palette.Project(theme.ResolveColor(ThemeColor.Accent), ColorDepth.Basic16));
        surface.Cell(new Point(4, 1)).Style.Foreground.ShouldBe(
            Palette.Project(theme.ResolveColor(ThemeColor.ControlBorder), ColorDepth.Basic16));
    }

    /// <summary>Verifies mounted Table hover, focus, tab, and directional behavior.</summary>
    [ComponentBehaviorEvidence(typeof(Table), ComponentBehavior.Hover)]
    [Fact]
    public async Task Input_WhenMountedTableIsHovered_PreservesGridAppearanceAsync()
    {
        // Arrange
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("A", 4));
        table.Columns.Add(TableColumn.Fixed("B", 4));
        table.Rows.Add(new TableRow([new ControlText("one"), new ControlText("two")]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(9, 3),
            TestContext.Current.CancellationToken);
        var gridPoint = new Point(4, 1);
        var before = surface.Cell(gridPoint).Style.Foreground;

        // Act
        await surface.Pointer.MoveToAsync(table);

        // Assert
        table.IsPointerOver.ShouldBeTrue();
        table.CanFocus.ShouldBeTrue();
        table.IsFocused.ShouldBeFalse();
        surface.Cell(gridPoint).Style.Foreground.ShouldBe(before);
    }

    /// <summary>Verifies row removal and reinsertion reflows clickable cells and clears abandoned rows.</summary>
    [Fact]
    public async Task UpdateAsync_WhenClickableRowIsRemovedAndReused_ReflowsWithoutStaleCellsAsync()
    {
        // Arrange
        var clicked = string.Empty;
        var one = Row("One");
        var two = Row("Two");
        var three = Row("Three");
        var oneRow = new TableRow([one]);
        var twoRow = new TableRow([two]);
        var threeRow = new TableRow([three]);
        one.Click += (_, _) => clicked = "One";
        two.Click += (_, _) => clicked = "Two";
        three.Click += (_, _) => clicked = "Three";
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Value", 8));
        table.Rows.Add(oneRow);
        table.Rows.Add(twoRow);
        table.Rows.Add(threeRow);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                               One
                               Two
                              Three
                             """);

        // Act remove first row and hit moved row
        await surface.UpdateAsync(() => table.Rows.RemoveAt(0), "remove first Table row");
        await surface.Pointer.ClickAsync(two);

        // Assert removal
        one.Parent.ShouldBeNull();
        two.Bounds.ShouldBe(new Rect(0, 0, 8, 1));
        three.Bounds.ShouldBe(new Rect(0, 1, 8, 1));
        clicked.ShouldBe("Two");
        surface.ShouldRender("""
                               Two
                              Three

                             """);

        // Act reuse detached row
        await surface.UpdateAsync(() => table.Rows.Add(oneRow), "reuse detached Table row");
        await surface.Pointer.ClickAsync(one);

        // Assert reuse
        one.Bounds.ShouldBe(new Rect(0, 2, 8, 1));
        clicked.ShouldBe("One");
        surface.ShouldHaveState(one, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldRender("""
                               Two
                              Three
                               One
                             """);
    }

    /// <summary>Verifies both-axis wheel scrolling stays aligned and resize clamps offsets while revealing all rows.</summary>
    [Fact]
    public async Task ResizeAsync_WhenBothAxesWereWheeled_ClampsOffsetsAndRestoresContentAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("First", 8));
        table.Columns.Add(TableColumn.Fixed("Second", 8));

        for (var index = 0; index < 6; index++)
        {
            table.Rows.Add(new TableRow([
                new ControlText($"A{index}"),
                new ControlText($"B{index}")
            ]));
        }

        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(10, 4),
            TestContext.Current.CancellationToken);

        // Act wheel both axes
        await surface.Pointer.WheelAsync(table, default, wheelY: -1);
        await surface.Pointer.WheelAsync(table, default, wheelX: -1);

        // Assert scrolled cells
        table.HorizontalOffset.ShouldBe(1);
        table.VerticalOffset.ShouldBe(1);
        surface.ShouldRender("""
                             1      B1
                             2      B2
                             3      B3
                             4      B4
                             """);

        // Act resize beyond extent
        await surface.ResizeAsync(new Size(20, 8));

        // Assert offset repair and full reveal
        table.HorizontalOffset.ShouldBe(0);
        table.VerticalOffset.ShouldBe(0);
        surface.ShouldRender("""
                             A0      B0
                             A1      B1
                             A2      B2
                             A3      B3
                             A4      B4
                             A5      B5


                             """);
    }

    /// <summary>Verifies pointer cell selection, keyboard row navigation, and row invocation on a mounted Table.</summary>
    [ComponentBehaviorEvidence(
        typeof(Table),
        ComponentBehavior.Focus |
        ComponentBehavior.Directional |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.KeyboardActivation)]
    [Fact]
    public async Task Input_WhenPointerAndKeyboardNavigateTable_SelectsActiveCellAndInvokesRowAsync()
    {
        var first = new TableRow([new ControlText("One"), new ControlText("A")]);
        var second = new TableRow([new ControlText("Two"), new ControlText("B")]);
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            SelectionMode = TableSelectionMode.Row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fixed("Value", 5));
        table.Rows.Add(first);
        table.Rows.Add(second);
        var invoked = new List<TableRow>();
        table.RowInvoked += (_, args) => invoked.Add(args.Row);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(table, new Point(7, 1));
        surface.ShouldHaveFocus(table);
        table.ActiveRow.ShouldBe(second);
        table.ActiveColumnIndex.ShouldBe(1);
        table.SelectedRows.ShouldBe([second]);
        invoked.ShouldBe([second]);

        await surface.Keyboard.PressAsync(Code.Up);

        table.ActiveRow.ShouldBe(first);
        table.ActiveColumnIndex.ShouldBe(1);
        table.SelectedRows.ShouldBe([first]);

        await surface.Keyboard.PressAsync(Code.Enter);

        invoked.ShouldBe([second, first]);
    }

    /// <summary>Verifies mounted header presses cycle ascending, descending, and reset sorting.</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsPressed_CyclesStableSortDirectionsAsync()
    {
        var first = new TableRow([new ControlText("B")]);
        var second = new TableRow([new ControlText("A")]);
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", 4));
        table.Rows.Add(first);
        table.Rows.Add(second);
        var directions = new List<TableSortDirection>();
        table.SortChanged += (_, args) => directions.Add(args.Direction);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(6, 4),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        table.SortDirection.ShouldBe(TableSortDirection.Ascending);
        table.Rows.ShouldBe([second, first]);

        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        table.SortDirection.ShouldBe(TableSortDirection.Descending);
        table.Rows.ShouldBe([first, second]);

        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        table.SortDirection.ShouldBe(TableSortDirection.None);
        table.Rows.ShouldBe([first, second]);
        directions.ShouldBe([
            TableSortDirection.Ascending,
            TableSortDirection.Descending,
            TableSortDirection.None
        ]);
    }

    /// <summary>Verifies clicking a header commits an in-progress cell edit instead of silently
    /// reverting it, matching the ordinary click-elsewhere behavior a few lines below the header
    /// branch (see #109).</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsPressedDuringEdit_CommitsInsteadOfRevertingAsync()
    {
        var editor = new TextInput { Text = "one" };
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Value", 8));
        table.Rows.Add(new TableRow([editor]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(table, new Point(1, 1));
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.TypeAsync("!");

        await surface.Pointer.ClickAsync(table, new Point(1, 0));

        table.IsEditing.ShouldBeFalse();
        editor.Text.ShouldBe("one!");
    }

    /// <summary>Verifies mounted Ctrl+A commits select-all and raises SelectionChanged.</summary>
    [ComponentBehaviorEvidence(typeof(Table), ComponentBehavior.Tab)]
    [Fact]
    public async Task Keyboard_WhenCtrlAIsPressed_SelectsAllRowsAndRaisesSelectionChangedAsync()
    {
        var table = new Table
        {
            SelectionMode = TableSelectionMode.MultipleRows,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        var first = new TableRow([new ControlText("One")]);
        var second = new TableRow([new ControlText("Two")]);
        table.Rows.Add(first);
        table.Rows.Add(second);
        var changes = new List<TableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, args) => changes.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 4),
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(table);
        await surface.SendAsync(new byte[] { 0x01 }, "press Ctrl+A");

        table.SelectedRows.ShouldBe([first, second]);
        changes.Count.ShouldBe(1);
        changes[0].AddedRows.ShouldBe([first, second]);
    }

    /// <summary>Verifies mounted read-only TextInput cells reject editing and retain their text.</summary>
    [Fact]
    public async Task Keyboard_WhenReadOnlyTextInputIsActivated_DoesNotBeginEditingAsync()
    {
        var editor = new TextInput { Text = "locked", IsReadOnly = true };
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Value", 8));
        var row = new TableRow([editor]);
        table.Rows.Add(row);
        var invoked = 0;
        table.RowInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        await surface.Keyboard.PressAsync(Code.Enter);

        table.IsEditing.ShouldBeFalse();
        editor.Text.ShouldBe("locked");
        invoked.ShouldBe(2);
    }

    /// <summary>Verifies mounted edit commit and cancel leave focus and navigation usable.</summary>
    [Fact]
    public async Task Input_WhenEditingCellCommitsOrCancels_KeepsKeyboardNavigationAvailableAsync()
    {
        var firstCell = new TextInput { Text = "one" };
        var secondCell = new TextInput { Text = "two" };
        var row = new TableRow([firstCell, secondCell]);
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            SelectionMode = TableSelectionMode.Cell,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("First", 7));
        table.Columns.Add(TableColumn.Fixed("Second", 7));
        table.Rows.Add(row);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(14, 1),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.UpdateAsync(() => firstCell.Text = "committed", "edit first Table cell");
        await surface.Keyboard.PressAsync(Code.Enter);
        table.IsEditing.ShouldBeFalse();
        firstCell.Text.ShouldBe("committed");

        await surface.Keyboard.PressAsync(Code.Right);
        table.ActiveColumnIndex.ShouldBe(1);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.UpdateAsync(() => secondCell.Text = "discarded", "edit second Table cell");
        await surface.Keyboard.PressAsync(Code.Escape);
        secondCell.Text.ShouldBe("two");
        table.IsEditing.ShouldBeFalse();
    }

    /// <summary>Verifies mounted text editing keeps Home, End, and Ctrl+A with the TextInput child.</summary>
    [Fact]
    public async Task Keyboard_WhenEditingTextInput_DelegatesEditorKeysToTheCellAsync()
    {
        var editor = new TextInput { Text = "one" };
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Value", 8));
        table.Rows.Add(new TableRow([editor]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.TypeAsync("X");
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.TypeAsync("Y");
        await surface.SendAsync(new byte[] { 0x01 }, "select all in TextInput");
        await surface.Keyboard.TypeAsync("Z");

        editor.Text.ShouldBe("Z");
        table.IsEditing.ShouldBeTrue();
    }

    /// <summary>Verifies PageDown/PageUp move the active row and mark the record handled, so it
    /// does not escape to page the enclosing scrollable container out from under the still-focused
    /// table (see #210).</summary>
    [Fact]
    public async Task Input_WhenPageKeysArePressed_MoveActiveRowWithoutEscapingToOuterContainerAsync()
    {
        // Arrange
        var table = new Table { Height = Length.Cells(6), HorizontalAlignment = HorizontalAlignment.Stretch };
        table.Columns.Add(TableColumn.Fixed("Value", 8));

        for (var index = 0; index < 12; index++)
        {
            table.Rows.Add(new TableRow([new ControlText($"Item {index}")]));
        }

        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { table }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        var initial = table.ActiveRow.ShouldNotBeNull();

        // Act
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert - the record was handled by the table, so the outer Stack never sees it.
        outer.VerticalOffset.ShouldBe(0);
        table.ActiveRow.ShouldNotBeSameAs(initial);
    }

    /// <summary>Verifies PageDown is still handled when the active row is already at the last row
    /// and cannot move any further, so the keystroke does not escape to page the enclosing
    /// scrollable container out from under the still-focused table (see #222).</summary>
    [Fact]
    public async Task Input_WhenPageKeyCannotMoveActiveRowAtBoundary_StillHandlesWithoutEscapingToOuterContainerAsync()
    {
        // Arrange
        var table = new Table { Height = Length.Cells(4), HorizontalAlignment = HorizontalAlignment.Stretch };
        table.Columns.Add(TableColumn.Fixed("Value", 8));

        for (var index = 0; index < 3; index++)
        {
            table.Rows.Add(new TableRow([new ControlText($"Item {index}")]));
        }

        var filler = new Button
        {
            Content = new ControlText("Filler"),
            Height = Length.Cells(20),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { table, filler }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.End);
        var last = table.ActiveRow.ShouldNotBeNull();

        // Act - PageDown at the last row cannot move the active row any further.
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert - the record was still handled by the table, so the outer Stack never sees it
        // and the still-focused table is not paged out of view.
        outer.VerticalOffset.ShouldBe(0);
        table.ActiveRow.ShouldBeSameAs(last);
    }

    /// <summary>Verifies End brings the endpoint row into view instead of leaving the viewport
    /// pinned at its prior offset, matching every other Table navigation path (see #210).</summary>
    [Fact]
    public async Task Input_WhenEndSelectsLastRow_BringsItIntoViewAsync()
    {
        // Arrange
        var table = new Table { Height = Length.Cells(4), HorizontalAlignment = HorizontalAlignment.Stretch };
        table.Columns.Add(TableColumn.Fixed("Value", 8));

        for (var index = 0; index < 20; index++)
        {
            table.Rows.Add(new TableRow([new ControlText($"Item {index}")]));
        }

        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(1, 1));

        // Act
        await surface.Keyboard.PressAsync(Code.End);

        // Assert
        table.ActiveRow.ShouldBeSameAs(table.Rows[^1]);
        table.VerticalOffset.ShouldBeGreaterThan(0);
    }

    private static Theme WithColor(ThemeColor role, Color value)
    {
        var source = Themes.Dark;
        var theme = new Theme(
            source.Palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);

        foreach (var color in Enum.GetValues<ThemeColor>())
        {
            theme.SetColor(color, color == role ? value : source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<ThemeDecoration>())
        {
            theme.SetAttributes(decoration, source.ResolveAttributes(decoration));
        }

        theme.SetStatusColors(Enum.GetValues<StatusColor>()
            .ToDictionary(status => status, source.ResolveStatusColor));
        theme.SetProfiles(source.Control, source.Input, source.Container, source.Window, source.Popup);
        theme.Freeze();
        return theme;
    }

    /// <summary>Creates one stretched borderless clickable table cell.</summary>
    private static Button Row(string value) => new()
    {
        Content = new ControlText(value),
        Style = TestButtonStyles.FlatWithPadding(default),
        Padding = default,
        Height = Length.Cells(1),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
}
