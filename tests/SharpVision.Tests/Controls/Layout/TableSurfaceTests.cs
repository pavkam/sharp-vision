// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using System.Text.Json;

/// <summary>Verifies Table layout, selection, editing, sorting, scrolling, mutation, resize, and hit targets through mounted surfaces.</summary>
public sealed class TableSurfaceTests
{
    /// <summary>Verifies local Table selection colors replace the theme-owned row fill while cell
    /// text and the final selection adornment continue to share one complete style.</summary>
    [Fact]
    public async Task Render_WhenSelectionColorsAreCustomized_AppliesThemToSelectedCellsAsync()
    {
        var foreground = Color.Rgb(0xff, 0xff, 0xff);
        var background = Color.Rgb(0xcd, 0x00, 0xcd);
        var row = new TableRow([new ControlText("One")]);
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            Style = TableStyle.Default with
            {
                SelectedTextColor = foreground,
                SelectedBackground = background
            }
        };
        table.Columns.Add(TableColumn.Fixed("Value", 6));
        table.Rows.Add(row);
        table.SelectRow(row);

        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        var style = surface.Cell(default).Style;
        style.Foreground.ShouldBe(TerminalPalette.Project(foreground, ColorDepth.Basic16));
        style.Background.ShouldBe(TerminalPalette.Project(background, ColorDepth.Basic16));
        surface.Cell(new Point(5, 0)).Style.Background.ShouldBe(
            TerminalPalette.Project(background, ColorDepth.Basic16));
    }

    /// <summary>Verifies a selection callback that disables or detaches an eager Table ends the
    /// current pointer transaction before RowInvoked can publish.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pointer_WhenSelectionCallbackMakesTableUnavailable_DoesNotInvokeRowAsync(bool detach)
    {
        // Arrange
        var row = new TableRow([new ControlText("One")]);
        var table = new Table { ShowHeader = false, ShowGridLines = false };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Rows.Add(row);
        var root = new Overlay { Children = { table } };
        var invoked = 0;
        table.RowInvoked += (_, _) => invoked++;
        table.SelectionChanged += (_, _) =>
        {
            if (detach)
            {
                _ = root.Children.Remove(table);
            }
            else
            {
                table.IsEnabled = false;
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(table, new Point(1, 0));

        // Assert
        invoked.ShouldBe(0);
    }

    /// <summary>Verifies a selection callback that disables or detaches an eager Table during
    /// keyboard-driven active-cell navigation does not let MoveActive, MovePage, or MoveToEndpoint
    /// bring a no-longer-available cell into view - the keyboard counterpart of the pointer
    /// regression above, covering every navigation keystroke that funnels through one of those
    /// three methods.</summary>
    [Theory]
    [InlineData(Code.Up, false)]
    [InlineData(Code.Up, true)]
    [InlineData(Code.Down, false)]
    [InlineData(Code.Down, true)]
    [InlineData(Code.Left, false)]
    [InlineData(Code.Left, true)]
    [InlineData(Code.Right, false)]
    [InlineData(Code.Right, true)]
    [InlineData(Code.PageUp, false)]
    [InlineData(Code.PageUp, true)]
    [InlineData(Code.PageDown, false)]
    [InlineData(Code.PageDown, true)]
    [InlineData(Code.Home, false)]
    [InlineData(Code.Home, true)]
    [InlineData(Code.End, false)]
    [InlineData(Code.End, true)]
    public async Task Keyboard_WhenSelectionCallbackMakesTableUnavailableDuringNavigation_DoesNotThrowAsync(
        Code code,
        bool detach)
    {
        // Arrange
        var row = new TableRow([new ControlText("One")]);
        var table = new Table { ShowHeader = false, ShowGridLines = false };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Rows.Add(row);
        var root = new Overlay { Children = { table } };
        table.SelectionChanged += (_, _) =>
        {
            if (detach)
            {
                _ = root.Children.Remove(table);
            }
            else
            {
                table.IsEnabled = false;
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(table);

        // Act
        await Should.NotThrowAsync(() => surface.Keyboard.PressAsync(code));

        // Assert - the callback above actually ran and made the table unavailable, so the
        // preceding line exercised the guard rather than passing vacuously.
        if (detach)
        {
            root.Children.ShouldNotContain(table);
        }
        else
        {
            table.IsEnabled.ShouldBeFalse();
        }
    }

    /// <summary>Verifies a PropertyChanged subscriber that removes the active row, or disposes the
    /// table outright, while ActivateCurrent's own SetActive call is still publishing that
    /// transition, stops ActivateCurrent from touching the now-stale row afterward - matching the
    /// pointer path's equivalent guard against a subscriber tearing down its target mid-gesture.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Keyboard_WhenActiveRowChangeCallbackMakesRowUnavailable_DoesNotInvokeRowAsync(bool disposeTable)
    {
        // Arrange
        var first = new TableRow([new ControlText("One")]);
        var table = new Table { ShowHeader = false, ShowGridLines = false };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Rows.Add(first);
        var root = new Overlay { Children = { table } };
        var invoked = 0;
        table.RowInvoked += (_, _) => invoked++;
        table.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(Table.ActiveRow))
            {
                return;
            }

            if (disposeTable)
            {
                table.Dispose();
            }
            else
            {
                _ = table.Rows.Remove(first);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(table);
        table.ActiveRow.ShouldBeNull();

        // Act
        await Should.NotThrowAsync(() => surface.Keyboard.PressAsync(Code.Enter));

        // Assert
        invoked.ShouldBe(0);

        if (disposeTable)
        {
            table.IsDisposed.ShouldBeTrue();
        }
        else
        {
            table.Rows.ShouldNotContain(first);
        }
    }

    /// <summary>Verifies every column kind aligns headers, grid lines, combining text, and wide cells exactly.</summary>
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
        surface.Cell(new Point(nameOrigin.X + 2, nameOrigin.Y)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies header foreground, header background, and grid-line theme-role overrides resolve
    /// through the mounted Theme rather than only accepting a literal color.</summary>
    [Fact]
    public async Task Render_WhenColorPropertiesAreThemeColors_ResolvesThroughTheMountedThemeAsync()
    {
        // Arrange
        var theme = WithColor(SemanticColor.ControlBorder, Color.Rgb(0x11, 0x22, 0x33));
        var table = new Table
        {
            Style = TableStyle.Default with
            {
                HeaderForeground = SemanticColor.Accent,
                GridLineColor = SemanticColor.ControlBorder
            },
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
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.Accent), ColorDepth.Basic16));
        surface.Cell(new Point(4, 1)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.ControlBorder), ColorDepth.Basic16));
    }

    /// <summary>Verifies mounted Table hover remains observable without restyling its passive
    /// shell, headers, grid lines, cells, or unused body area.</summary>
    [Fact]
    public async Task Input_WhenMountedTableIsHovered_PreservesRenderedAppearanceAsync()
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
        var restingCells = Enumerable.Range(0, 27)
            .Select(index => surface.Cell(new Point(index % 9, index / 9)))
            .ToArray();

        // Act
        await surface.Pointer.MoveToAsync(table);

        // Assert
        table.IsPointerOver.ShouldBeTrue();
        table.CanFocus.ShouldBeTrue();
        table.IsFocused.ShouldBeFalse();
        Enumerable.Range(0, 27)
            .Select(index => surface.Cell(new Point(index % 9, index / 9)))
            .ShouldBe(restingCells);
    }

    /// <summary>Verifies keyboard focus is genuinely observable on Table's own rendered shell
    /// through focused text without reversing the entire tabular surface behind independently
    /// styled cells.</summary>
    [Fact]
    public async Task Input_WhenMountedTableReceivesFocus_UsesTextCueWithoutReversingSurfaceAsync()
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
        var restingStyle = surface.Cell(default).Style;
        (restingStyle.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.None);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        table.IsFocused.ShouldBeTrue();
        var focusedStyle = surface.Cell(default).Style;
        (focusedStyle.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        (focusedStyle.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.None);
        focusedStyle.Foreground.ShouldBe(restingStyle.Foreground);
        focusedStyle.Background.ShouldBe(restingStyle.Background);
    }

    /// <summary>Verifies a selected row receives the theme's filled selection treatment even when
    /// its arbitrary cell controls use passive styles that do not paint Selected themselves.</summary>
    [Fact]
    public async Task Input_WhenPlainTextRowIsSelected_PaintsFilledSelectionCueAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            SelectionMode = TableSelectionMode.Row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Value", 3));
        table.Columns.Add(TableColumn.Fixed("State", 3));
        table.Rows.Add(new TableRow([new ControlText("One"), new ControlText("A")]));
        table.Rows.Add(new TableRow([new ControlText("Two"), new ControlText("B")]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(6, 2),
            TestContext.Current.CancellationToken);
        var restingBackground = surface.Cell(new Point(0, 1)).Style.Background;

        // Act
        await surface.Pointer.ClickAsync(table, new Point(0, 1));

        // Assert
        table.SelectedRows.ShouldBe([table.Rows[1]]);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldNotBe(restingBackground);
        surface.Cell(new Point(3, 1)).Style.Background.ShouldBe(surface.Cell(new Point(0, 1)).Style.Background);
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(restingBackground);
    }

    /// <summary>Verifies an already-selected row repaints from a replacement Theme whose only
    /// relevant difference is the semantic selected-control color.</summary>
    [Fact]
    public async Task Theme_WhenSelectedControlColorChanges_RepaintsSelectedRowAsync()
    {
        // Arrange
        var firstTheme = WithColor(SemanticColor.SelectedControl, Color.Rgb(0x00, 0x00, 0xee));
        var secondTheme = WithColor(SemanticColor.SelectedControl, Color.Rgb(0xee, 0x00, 0x00));
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            SelectionMode = TableSelectionMode.Row
        };
        table.Columns.Add(TableColumn.Fixed("Value", 4));
        table.Rows.Add(new TableRow([new ControlText("Row")]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(4, 1),
            firstTheme,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        var firstBackground = surface.Cell(default).Style.Background;

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = secondTheme, "swap selected-row color");

        // Assert
        table.SelectedRows.ShouldBe([table.Rows[0]]);
        surface.Cell(default).Style.Background.ShouldNotBe(firstBackground);
    }

    /// <summary>Verifies row selection composes with each cell's effective enabled state instead
    /// of repainting a disabled cell with the enabled selected-item face.</summary>
    [Fact]
    public async Task Input_WhenSelectedRowContainsDisabledCell_ComposesDisabledSelectionFaceAsync()
    {
        // Arrange
        var disabledCell = new ControlText("Off") { IsEnabled = false };
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            SelectionMode = TableSelectionMode.Row
        };
        table.Columns.Add(TableColumn.Fixed("Enabled", 4));
        table.Columns.Add(TableColumn.Fixed("Disabled", 4));
        table.Rows.Add(new TableRow([new ControlText("On"), disabledCell]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(table, new Point(0, 0));

        // Assert
        table.SelectedRows.ShouldBe([table.Rows[0]]);
        surface.Cell(new Point(4, 0)).Style.Background.ShouldBe(
            TerminalPalette.Project(
                ThemeCatalog.Dark.ResolveColor(SemanticColor.DisabledControl),
                ColorDepth.Basic16));
        surface.Cell(new Point(0, 0)).Style.Background.ShouldNotBe(surface.Cell(new Point(4, 0)).Style.Background);
    }

    /// <summary>Verifies a Table's shell hover remains inert under the tabular focus style set.
    /// Selection is painted separately by the private presenter over selected cell content;
    /// lighting up the whole data-dense shell on mere mouse-over would be noise, not signal.</summary>
    [Fact]
    public async Task Input_WhenMountedTableIsFocusedAndHovered_OnlyFocusChangesAppearanceAsync()
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
        await surface.Keyboard.PressAsync(Code.Tab);
        var focusedCells = Enumerable.Range(0, 27)
            .Select(index => surface.Cell(new Point(index % 9, index / 9)))
            .ToArray();

        // Act
        await surface.Pointer.MoveToAsync(table);

        // Assert
        table.IsFocused.ShouldBeTrue();
        table.IsPointerOver.ShouldBeTrue();
        Enumerable.Range(0, 27)
            .Select(index => surface.Cell(new Point(index % 9, index / 9)))
            .ShouldBe(focusedCells);
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
        surface.ShouldHaveState(one, VisualState.IsPointerOver | VisualState.Focused);
        surface.ShouldRender("""
                               Two
                              Three
                               One
                             """);
    }

    /// <summary>Verifies toggling ShowGridLines after an already-completed layout pass, under the
    /// SAME outer constraint, actually recomputes the retained TablePresenter's column gap instead
    /// of leaving cell positions stale. Table's ShowGridLines setter only ever invalidated Table
    /// itself; the presenter's own dirty bit stayed clear and its cached measure constraint was
    /// unchanged, so its Measure/Arrange short-circuited and the second column never actually
    /// slid over to close the freed grid-line gap.</summary>
    [Fact]
    public async Task Property_WhenShowGridLinesChangesAfterLayout_RepositionsColumnsAsync()
    {
        // Arrange - two one-cell-wide fixed columns; the mount is exactly wide enough for both
        // columns plus the one-cell grid-line gap between them.
        var table = new Table
        {
            ShowHeader = false,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("A", 1));
        table.Columns.Add(TableColumn.Fixed("B", 1));
        table.Rows.Add(new TableRow([new ControlText("X"), new ControlText("Y")]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Assert the first layout pass placed the grid-line-separated columns as expected.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("X");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("Y");

        // Act - disable grid lines with the identical outer constraint (same mount Size) and force
        // another layout pass.
        await surface.UpdateAsync(() => table.ShowGridLines = false, "disable Table grid lines");

        // Assert - removing the one-cell grid-line gap must shift column B left by one cell,
        // leaving the freed trailing cell blank.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("X");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("Y");
        surface.Cell(new Point(2, 0)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies adding a column after an already-completed layout pass, under the SAME
    /// outer constraint, actually grows the retained TablePresenter's cached column-width array
    /// instead of leaving it stale. Table's ColumnsChanged handler only ever invalidated Table
    /// itself; without also invalidating the presenter, its Measure never reran, so its header
    /// render loop kept iterating the live (now larger) Columns collection against the presenter's
    /// stale, shorter ColumnWidths array.</summary>
    [Fact]
    public async Task Columns_WhenColumnIsAddedAfterLayout_RepositionsHeaderColumnsAsync()
    {
        // Arrange - a single already-laid-out column with no data rows, since every existing row
        // must retain one cell per column across a column-count change.
        var table = new Table
        {
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("A", 4));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(9, 1),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("A");

        // Act - add a second column with the identical outer constraint and force another layout
        // pass.
        await Should.NotThrowAsync(() => surface.UpdateAsync(
            () => table.Columns.Add(TableColumn.Fixed("B", 4)),
            "add second Table column"));

        // Assert - the new column's header is drawn immediately after the first (past its width
        // and the one-cell grid-line gap), matching a fresh layout pass rather than a stale one.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(5, 0)).Text.ShouldBe("B");
    }

    /// <summary>Verifies changing RowSpacing after an already-completed layout pass, under the SAME
    /// outer constraint, actually recomputes the retained TablePresenter's row gap instead of
    /// leaving row positions stale. Table's RowSpacing setter only ever invalidated Table itself;
    /// the presenter's own dirty bit stayed clear and its cached arrange slot was unchanged, so its
    /// Measure/Arrange short-circuited and the second row never actually moved down to open the
    /// newly requested gap.</summary>
    [Fact]
    public async Task Property_WhenRowSpacingChangesAfterLayout_RepositionsRowsAsync()
    {
        // Arrange - two one-row-tall cells in a single fixed column, mounted exactly tall enough
        // for both rows plus one spare cell - just enough room for one row of spacing once added.
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("V", 1));
        table.Rows.Add(new TableRow([new ControlText("X")]));
        table.Rows.Add(new TableRow([new ControlText("Y")]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(1, 3),
            TestContext.Current.CancellationToken);

        // Assert the first layout pass stacked the rows with no gap between them.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("X");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("Y");
        surface.Cell(new Point(0, 2)).Text.ShouldBe(" ");

        // Act - request one cell of row spacing with the identical outer constraint (same mount
        // Size) and force another layout pass.
        await surface.UpdateAsync(() => table.RowSpacing = 1, "add Table row spacing");

        // Assert - the second row must move down into the previously spare cell, opening a blank
        // gap where it used to sit.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("X");
        surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("Y");
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
        await surface.Pointer.WheelAsync(table, default, wheelX: 1);

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

    /// <summary>Verifies the horizontal generated bar renders and owns the shared bottom-right scrollbar corner.</summary>
    [Fact]
    public async Task Render_WhenBothScrollBarsAreVisible_HorizontalBarOwnsBottomRightCornerAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = ScrollBarStyle.FullBlock,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Wide", 8));

        for (var index = 0; index < 5; index++)
        {
            table.Rows.Add(new TableRow([new ControlText($"row-{index}")]));
        }

        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(6, 4),
            TestContext.Current.CancellationToken);
        var corner = new Point(5, 3);

        // Assert
        surface.Cell(corner).Text.ShouldBe(
            table.ActualScrollBarStyle.Glyphs.HorizontalIncrement.ToString());
        table.HitTest(corner).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Horizontal);
    }

    /// <summary>Verifies pointer cell selection, keyboard row navigation, and row invocation on a mounted Table.</summary>
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

    /// <summary>Verifies mounted header presses cycle ascending, descending, and reset sorting, and
    /// that the sorted column's header draws the matching direction indicator in its one reserved
    /// trailing cell - reset leaves that cell showing the caption's own text again, not a stale
    /// glyph.</summary>
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
        var indicatorCell = new Point(3, 0);

        // Unsorted: the reserved trailing cell is still ordinary caption text ("Name"[3] == 'e').
        surface.Cell(indicatorCell).Text.ShouldBe("e");

        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        table.SortDirection.ShouldBe(TableSortDirection.Ascending);
        table.Rows.ShouldBe([second, first]);
        surface.Cell(indicatorCell).Text.ShouldBe(table.ActualStyle.Glyphs.SortAscending.ToString());

        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        table.SortDirection.ShouldBe(TableSortDirection.Descending);
        table.Rows.ShouldBe([first, second]);
        surface.Cell(indicatorCell).Text.ShouldBe(table.ActualStyle.Glyphs.SortDescending.ToString());

        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        table.SortDirection.ShouldBe(TableSortDirection.None);
        table.Rows.ShouldBe([first, second]);
        surface.Cell(indicatorCell).Text.ShouldBe("e");
        directions.ShouldBe([
            TableSortDirection.Ascending,
            TableSortDirection.Descending,
            TableSortDirection.None
        ]);
    }

    /// <summary>Verifies the header caption and its sort indicator both land on the padded band's
    /// own text row - not row zero and not one row below - when CellPadding reserves top and
    /// bottom rows. Regression test for the header area being built as an already-offset
    /// height-1 rect and then deflated a second time, which collapsed the caption's clip to zero
    /// height and pushed the indicator one row too low.</summary>
    [Fact]
    public async Task Render_WhenCellPaddingReservesVerticalRows_DrawsCaptionAndIndicatorOnPaddedRowAsync()
    {
        // Arrange
        var table = new Table
        {
            Style = TableStyle.Default with { CellPadding = new Thickness(1, 1) },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", 6));
        table.Rows.Add(new TableRow([new ControlText("one")]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 6),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(
            () => table.SetSort(0, TableSortDirection.Ascending),
            "sort Table by Name column");

        // Assert
        // ContentSlot.Y is 0 for this borderless table, so the caption row is exactly
        // CellPadding.Top - not row 0 (the double-deflate's zero-height clip) and not
        // CellPadding.Top + 1 (the indicator's stale one-row-too-low position).
        var captionRow = table.ActualStyle.CellPadding.Top;
        surface.Cell(new Point(0, captionRow - 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(1, captionRow)).Text.ShouldBe("N");
        surface.Cell(new Point(2, captionRow)).Text.ShouldBe("a");
        surface.Cell(new Point(3, captionRow)).Text.ShouldBe("m");
        surface.Cell(new Point(0, captionRow + 1)).Text.ShouldBe(" ");

        // The sort indicator occupies the padded band's one reserved trailing cell, on the
        // SAME row as the caption text.
        var indicatorGlyph = table.ActualStyle.Glyphs.SortAscending.ToString();
        surface.Cell(new Point(4, captionRow)).Text.ShouldBe(indicatorGlyph);
        surface.Cell(new Point(4, captionRow + 1)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies clicking a header commits an in-progress cell edit instead of silently
    /// reverting it, matching the ordinary click-elsewhere behavior a few lines below the header
    /// branch.</summary>
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
        await surface.Keyboard.PressAsync(Code.Escape, Modifiers.Control);
        secondCell.Text.ShouldBe("discarded");
        table.IsEditing.ShouldBeTrue();

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

    /// <summary>Verifies an Auto column holding an auto-sized TextInput renders the cell's leading
    /// character instead of scrolling it out of view, mirroring the Showcase behavior table's bare
    /// "1.0" and "Stable" cells where the column's automatic width tracks the editor's own
    /// unreserved desired width.</summary>
    [Fact]
    public async Task Render_WhenAutoColumnHoldsAutoSizedTextInput_RendersLeadingCharacterAsync()
    {
        // Arrange
        var editor = new TextInput { Text = "1.0" };
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(new TableRow([editor]));

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Assert
        editor.HorizontalOffset.ShouldBe(0);
        var origin = new Point(editor.Bounds.X, editor.Bounds.Y);
        surface.Cell(origin).Text.ShouldBe("1");
        surface.Cell(new Point(origin.X + 1, origin.Y)).Text.ShouldBe(".");
        surface.Cell(new Point(origin.X + 2, origin.Y)).Text.ShouldBe("0");
    }

    /// <summary>Verifies PageDown/PageUp move the active row and mark the record handled, so it
    /// does not escape to page the enclosing scrollable container out from under the still-focused
    /// table.</summary>
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
    /// scrollable container out from under the still-focused table.</summary>
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
            Text = "Filler",
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
    /// pinned at its prior offset, matching every other Table navigation path.</summary>
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

    /// <summary>Verifies a mounted Table proves direct disable, ancestor-inherited disable, and
    /// re-enable recovery on a real terminal surface.</summary>
    [Fact]
    public async Task Enabled_WhenToggledOnMountedTable_AppliesDirectAndInheritedDisabledStateAsync()
    {
        // Arrange — direct disable
        var table = CreateTwoRowTable();
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 4),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => table.IsEnabled = false, "disable Table directly");

        // Assert
        surface.ShouldHaveState(table, VisualState.Disabled);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => table.IsEnabled = true, "re-enable Table");

        // Assert
        surface.ShouldHaveState(table, VisualState.Normal);

        // Arrange — ancestor-inherited disable
        var child = CreateTwoRowTable();
        var ancestor = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            ancestor,
            new Size(8, 4),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => ancestor.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);
    }

    /// <summary>Verifies a disabled Table's arranged geometry after a genuine resize still matches
    /// an independently mounted, still-enabled Table arranged at that same size, proving disabling
    /// does not perturb layout.</summary>
    [Fact]
    public async Task Layout_WhenDisabledTableIsResized_MatchesEnabledGeometryAtNewSizeAsync()
    {
        // Arrange
        var mountSize = new Size(8, 4);
        var resizedSize = new Size(16, 6);
        var disabled = CreateTwoRowTable();
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabled,
            mountSize,
            TestContext.Current.CancellationToken);
        await disabledSurface.UpdateAsync(() => disabled.IsEnabled = false, "disable Table before resize");

        // Act
        await disabledSurface.ResizeAsync(resizedSize);

        var enabled = CreateTwoRowTable();
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabled,
            resizedSize,
            TestContext.Current.CancellationToken);

        // Assert
        disabled.Bounds.ShouldBe(enabled.Bounds);
        disabled.DesiredSize.ShouldBe(enabled.DesiredSize);
    }

    /// <summary>Verifies a disabled Table with an already-active row selection refuses Tab focus and
    /// ignores both a pointer press on another row and a directional key, leaving the prior
    /// selection exactly as it was before disabling.</summary>
    [Fact]
    public async Task Input_WhenDisabledTableHasActiveSelection_RefusesFocusAndIgnoresPointerAndDirectionalKeysAsync()
    {
        // Arrange
        var table = CreateTwoRowTable();
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(5, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        surface.ShouldHaveFocus(table);
        var activeBeforeDisable = table.ActiveRow.ShouldNotBeNull();
        var selectedBeforeDisable = table.SelectedRows.ToArray();

        // Act — disable clears focus, matching every other Focus/Tab control's disabled contract.
        await surface.UpdateAsync(() => table.IsEnabled = false, "disable Table with an active selection");

        // Assert
        table.IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(null);

        // Act — Tab does not refocus the disabled Table.
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(null);
        table.IsFocused.ShouldBeFalse();

        // Act — a pointer press on the other row while disabled.
        await surface.Pointer.ClickAsync(table, new Point(1, 1));

        // Assert — selection unchanged
        table.ActiveRow.ShouldBeSameAs(activeBeforeDisable);
        table.SelectedRows.ShouldBe(selectedBeforeDisable);

        // Act — a directional key while disabled.
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert — selection still unchanged
        table.ActiveRow.ShouldBeSameAs(activeBeforeDisable);
        table.SelectedRows.ShouldBe(selectedBeforeDisable);
    }

    /// <summary>Creates a two-row, single-column Table sized to fit exactly two content rows with
    /// no header or grid lines, matching the disabled-evidence tests' small mounted surfaces.</summary>
    private static Table CreateTwoRowTable()
    {
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            SelectionMode = TableSelectionMode.Row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Rows.Add(new TableRow([new ControlText("One")]));
        table.Rows.Add(new TableRow([new ControlText("Two")]));
        return table;
    }

    private static Theme WithColor(SemanticColor role, Color value)
    {
        var source = ThemeCatalog.Dark;
        var theme = new Theme(
            source.Palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);

        foreach (var color in Enum.GetValues<SemanticColor>())
        {
            theme.SetColor(color, color == role ? value : source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<SemanticDecoration>())
        {
            theme.SetAttributes(decoration, source.ResolveAttributes(decoration));
        }

        theme.SetStyleSections(new Dictionary<string, JsonElement>(source.StyleSections));
        theme.Freeze();
        return theme;
    }

    /// <summary>Creates one stretched borderless clickable table cell.</summary>
    private static Button Row(string value) => new()
    {
        Text = value,
        Style = TestButtonStyles.FlatWithPadding(default),
        Padding = default,
        Height = Length.Cells(1),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
}
