// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies mounted progressive Table behavior: keyboard activation against loaded and
/// placeholder rows, empty sources, pointer resolution in gaps and beyond the data, header sort
/// cycling, pointer selection modifiers, progressive grid lines, load-state and failure event
/// arguments, retry and stale-generation rejection, source change marshaling, and column
/// mutation while bound.</summary>
public sealed class TableDataInteractionTests
{
    private sealed record Item(int Id, string Name);

    private static TableRow BuildRow(Item item) => new([new ControlText(item.Name)]);

    private static TableRow BuildTwoColumnRow(Item item) =>
        new([new ControlText(item.Name), new ControlText($"#{item.Id}")]);

    private static FakeTableDataSource<Item> CreateSource(int count) =>
        new(Enumerable.Range(0, count).Select(static id => new Item(id, $"Row{id}")), static item => item.Id, count);

    private static Table CreateHost(
        bool showHeader = false,
        TableSelectionMode selectionMode = TableSelectionMode.Row,
        int rowSpacing = 0,
        bool showGridLines = false,
        int columns = 1)
    {
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowHeader = showHeader,
            ShowGridLines = showGridLines,
            RowSpacing = rowSpacing,
            SelectionMode = selectionMode
        };
        table.Columns.Add(TableColumn.Fixed("Name", 6));

        if (columns > 1)
        {
            table.Columns.Add(TableColumn.Fixed("Id", 3));
        }

        return table;
    }

    private static async Task SettleUntilAsync<T>(
        ComponentSurface surface,
        FakeTableDataSource<T> source,
        Func<bool> satisfied,
        int maxAttempts = 10)
    {
        for (var attempt = 0; attempt < maxAttempts && !satisfied(); attempt++)
        {
            await surface.UpdateAsync(source.ReleaseAll, $"settle attempt {attempt}");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        // A released fetch can commit on a thread-pool continuation after the loop observes the
        // model change; one more idle-gated round trip guarantees the render landed.
        await surface.UpdateAsync(static () => { }, "settle: drain pending render");
    }

    private static async Task AdvanceUntilLoadStateAsync(
        ComponentSurface surface,
        Table table,
        TableLoadState expected,
        int maxAdvances = 8)
    {
        for (var attempt = 0; attempt < maxAdvances && table.LoadState != expected; attempt++)
        {
            await surface.AdvanceAsync(TimeSpan.FromMilliseconds(250), $"advance toward {expected} (attempt {attempt})");
        }
    }

    // The component keyboard only encodes an unmodified Enter press; these are the Kitty
    // encodings for Control+Enter and for a held-key Enter repeat.
    private static Task PressControlEnterAsync(ComponentSurface surface) =>
        surface.SendAsync("\u001b[13;5u"u8.ToArray(), "press Control+Enter");

    private static Task RepeatEnterAsync(ComponentSurface surface) =>
        surface.SendAsync("\u001b[13;1:2u"u8.ToArray(), "repeat Enter");

    #region Keyboard

    /// <summary>Verifies Enter on a loaded progressive row reports the realized row, its logical
    /// index, and the keyboard cause, while a held-key repeat and Control+Enter invoke nothing.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterIsPressedOnALoadedRow_InvokesWithKeyboardCauseAndIndexAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(20);
        var invoked = new List<TableRowInvokedEventArgs>();
        table.RowInvoked += (_, args) => invoked.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.Pointer.ClickAsync(table, new Point(0, 2));
        table.ActiveIndex.ShouldBe(2);
        invoked.Count.ShouldBe(1);
        invoked[0].Cause.ShouldBe(ActivationCause.Pointer);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        invoked.Count.ShouldBe(2);
        invoked[1].Row.ShouldBe(table.ProgressiveController!.RowAt(2));
        invoked[1].RowIndex.ShouldBe(2);
        invoked[1].Cause.ShouldBe(ActivationCause.Keyboard);

        // Act repeat and modified Enter
        await RepeatEnterAsync(surface);
        await PressControlEnterAsync(surface);

        // Assert
        invoked.Count.ShouldBe(2);
        table.ActiveIndex.ShouldBe(2);
    }

    /// <summary>Verifies pointer and Enter activation skip a still-unloaded placeholder row and
    /// invoke it only once its fetch resolves.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterTargetsAPlaceholder_InvokesOnlyAfterTheRowLoadsAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(20);
        source.Gate();
        var invoked = 0;
        table.RowInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        // Act against the placeholder
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        table.ActiveIndex.ShouldBe(0);
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeTrue();
        invoked.ShouldBe(0);

        // Act once loaded
        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(0));
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        invoked.ShouldBe(1);
    }

    /// <summary>Verifies a progressive table bound to an empty source renders only its header,
    /// issues no fetch, and leaves navigation keys to the enclosing scroll host.</summary>
    [Fact]
    public async Task Keyboard_WhenProgressiveSourceIsEmpty_LeavesNavigationKeysToTheEnclosingScrollHostAsync()
    {
        // Arrange
        var table = CreateHost(showHeader: true);
        table.VerticalAlignment = VerticalAlignment.Top;
        table.Height = Length.Cells(2);
        var source = CreateSource(0);
        var host = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { table, new ControlText("Filler") { Height = Length.Cells(20) } }
        };
        await using var surface = await ComponentSurface.MountAsync(
            host, new Size(10, 4), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        surface.ShouldHaveFocus(table);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        host.VerticalOffset.ShouldBe(1);
        table.ActiveIndex.ShouldBe(-1);
        source.LoadCallCount.ShouldBe(0);
        table.LoadState.ShouldBe(TableLoadState.Idle);
        surface.Cell(new Point(0, 1)).Text.ShouldBe("F");
    }

    /// <summary>Verifies Left/Right have no progressive mapping: they leave the active index alone
    /// and stay unhandled so an enclosing horizontal scroll host receives them.</summary>
    [Fact]
    public async Task Keyboard_WhenLeftOrRightIsPressedWhileProgressive_IsLeftToTheEnclosingScrollHostAsync()
    {
        // Arrange
        var table = CreateHost();
        table.HorizontalAlignment = HorizontalAlignment.Left;
        table.VerticalAlignment = VerticalAlignment.Top;
        table.Width = Length.Cells(6);
        table.Height = Length.Cells(3);
        var source = CreateSource(5);
        var host = new Stack
        {
            Orientation = Orientation.Horizontal,
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { table, new ControlText("Filler") { Width = Length.Cells(40) } }
        };
        await using var surface = await ComponentSurface.MountAsync(
            host, new Size(10, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.Pointer.ClickAsync(table, new Point(0, 1));
        table.ActiveIndex.ShouldBe(1);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        host.HorizontalOffset.ShouldBe(1);
        table.ActiveIndex.ShouldBe(1);
    }

    /// <summary>Verifies Control+A under MultipleRows selects every loaded key and nothing at all
    /// while every row is still a placeholder.</summary>
    [Fact]
    public async Task Keyboard_WhenControlAIsPressedUnderMultipleRows_SelectsOnlyLoadedKeysAsync()
    {
        // Arrange
        var table = CreateHost(selectionMode: TableSelectionMode.MultipleRows);
        var source = CreateSource(5);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(table);

        // Act while unloaded
        await surface.SendAsync(new byte[] { 0x01 }, "press Ctrl+A");

        // Assert
        table.SelectedKeys.ShouldBeEmpty();

        // Act once loaded
        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(4));
        await surface.SendAsync(new byte[] { 0x01 }, "press Ctrl+A");

        // Assert
        table.SelectedKeys.Count.ShouldBe(5);
        table.SelectedKeys.ShouldBe([0, 1, 2, 3, 4], ignoreOrder: true);
    }

    #endregion

    #region Pointer

    /// <summary>Verifies a progressive press resolves by arithmetic geometry: an inter-row gap,
    /// a point below the last logical row, and a point past the last column select nothing.</summary>
    [Fact]
    public async Task Pointer_WhenPressLandsOutsideAnyDataCell_DoesNotSelectAsync()
    {
        // Arrange
        var table = CreateHost(rowSpacing: 1);
        var source = CreateSource(3);
        var selectionChanges = 0;
        table.SelectionChanged += (_, _) => selectionChanges++;
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 8), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        surface.Cell(new Point(0, 4)).Text.ShouldBe("R");

        // Act and assert the gap between the first two rows
        await surface.Pointer.ClickAsync(table, new Point(0, 1));
        table.ActiveIndex.ShouldBe(-1);

        // Act and assert past the last column
        await surface.Pointer.ClickAsync(table, new Point(8, 0));
        table.ActiveIndex.ShouldBe(-1);

        // Act and assert below the last row
        await surface.Pointer.ClickAsync(table, new Point(0, 7));
        table.ActiveIndex.ShouldBe(-1);
        selectionChanges.ShouldBe(0);

        // Act and assert a real row
        await surface.Pointer.ClickAsync(table, new Point(0, 2));
        table.ActiveIndex.ShouldBe(1);
        table.SelectedKeys.ShouldBe([1]);
        selectionChanges.ShouldBe(1);
    }

    /// <summary>Verifies repeated header presses while progressive cycle ascending, descending,
    /// and reset through SortRequested, reload the source each time, and draw then clear the
    /// header indicator.</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsPressedRepeatedlyWhileProgressive_CyclesSortRequestsAndIndicatorAsync()
    {
        // Arrange
        var table = CreateHost(showHeader: true);
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 6), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var requests = new List<(int Column, TableSortDirection Direction)>();
        table.SortRequested += (_, args) => requests.Add((args.ColumnIndex, args.Direction));
        var sortChanged = 0;
        table.SortChanged += (_, _) => sortChanged++;
        var indicator = new Point(5, 0);
        var loads = source.LoadCallCount;

        // Act ascending
        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        surface.Cell(indicator).Text.ShouldBe(table.ActualStyle.Glyphs.SortAscending.ToString());
        source.LoadCallCount.ShouldBeGreaterThan(loads);
        loads = source.LoadCallCount;

        // Act descending
        await surface.Pointer.ClickAsync(table, new Point(1, 0));
        surface.Cell(indicator).Text.ShouldBe(table.ActualStyle.Glyphs.SortDescending.ToString());
        source.LoadCallCount.ShouldBeGreaterThan(loads);
        loads = source.LoadCallCount;

        // Act reset
        await surface.Pointer.ClickAsync(table, new Point(1, 0));

        // Assert
        surface.Cell(indicator).Text.ShouldBe(" ");
        source.LoadCallCount.ShouldBeGreaterThan(loads);
        requests.ShouldBe([
            (0, TableSortDirection.Ascending),
            (0, TableSortDirection.Descending),
            (-1, TableSortDirection.None)
        ]);
        table.SortColumnIndex.ShouldBe(-1);
        table.SortDirection.ShouldBe(TableSortDirection.None);
        sortChanged.ShouldBe(0);
    }

    /// <summary>Verifies pointer selection modifiers under progressive MultipleRows: a plain click
    /// replaces the selection, Shift-click selects the loaded key range from the anchor, and
    /// Control-click toggles one key in and out.</summary>
    [Fact]
    public async Task Pointer_WhenRowsAreClickedWithModifiersUnderMultipleRows_RangesAndTogglesKeysAsync()
    {
        // Arrange
        var table = CreateHost(selectionMode: TableSelectionMode.MultipleRows);
        var source = CreateSource(5);
        var changes = 0;
        table.SelectionChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var controller = table.ProgressiveController!;

        // Act plain clicks replace
        await surface.Pointer.ClickAsync(controller.RowAt(3)!.Cells[0]);
        await surface.Pointer.ClickAsync(controller.RowAt(0)!.Cells[0]);
        table.SelectedKeys.ShouldBe([0]);

        // Act Shift-click ranges from the anchor
        await surface.Pointer.ClickAsync(controller.RowAt(2)!.Cells[0], Modifiers.Shift);
        table.SelectedKeys.ShouldBe([0, 1, 2], ignoreOrder: true);
        await surface.Pointer.ClickAsync(controller.RowAt(1)!.Cells[0], Modifiers.Control);

        // Assert the toggle removed the key
        table.SelectedKeys.ShouldBe([0, 2], ignoreOrder: true);
        table.ActiveIndex.ShouldBe(1);
        controller.RowAt(1)!.Cells[0].GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeFalse();
        controller.RowAt(2)!.Cells[0].GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeTrue();

        // Act toggle back
        await surface.Pointer.ClickAsync(controller.RowAt(1)!.Cells[0], Modifiers.Control);

        // Assert
        table.SelectedKeys.ShouldBe([0, 1, 2], ignoreOrder: true);
        changes.ShouldBe(5);
    }

    /// <summary>Verifies a Shift gesture whose target index is still unloaded moves the active
    /// index but leaves the selected keys untouched, since no key exists yet to range to.</summary>
    [Fact]
    public async Task SelectIndex_WhenShiftTargetsAnUnloadedIndex_MovesActiveIndexWithoutChangingSelectionAsync()
    {
        // Arrange
        var table = CreateHost(selectionMode: TableSelectionMode.MultipleRows);
        var source = CreateSource(200);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.UpdateAsync(() => table.SelectIndex(0), "anchor on the first row");
        table.ProgressiveController!.IsPlaceholder(150).ShouldBeTrue();

        // Act
        await surface.UpdateAsync(() => table.SelectIndex(150, Modifiers.Shift), "shift toward an unloaded index");

        // Assert
        table.ActiveIndex.ShouldBe(150);
        table.ActiveKey.ShouldBeNull();
        table.SelectedKeys.ShouldBe([0]);
    }

    #endregion

    #region Rendering

    /// <summary>Verifies progressive grid lines draw a full-width horizontal separator after every
    /// realized row except the last, with the vertical rule spanning the viewport and crossing
    /// each separator in the column gap.</summary>
    [Fact]
    public async Task Render_WhenGridLinesAreShownWhileProgressive_DrawsSeparatorsBetweenRealizedRowsAsync()
    {
        // Arrange
        var table = CreateHost(showGridLines: true, columns: 2);
        var source = CreateSource(3);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(12, 6), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                table.Style = table.ActualStyle with { Glyphs = new TableGlyphs(new Rune('='), new Rune('!'), new Rune('+')) };
                table.SetDataSource(source, BuildTwoColumnRow, Length.Cells(1));
            },
            "style and bind");

        // Assert
        surface.ShouldRender(
            "Row0  !#0   \n" +
            "======+=====\n" +
            "Row1  !#1   \n" +
            "======+=====\n" +
            "Row2  !#2   \n" +
            "      !     ");
    }

    /// <summary>Verifies clearing every column while progressive derealizes the window and
    /// renders nothing, and re-adding a fixed column restores the rows.</summary>
    [Fact]
    public async Task Columns_WhenClearedWhileProgressive_DerealizesEveryRowThenRestoresAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(3);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");

        // Act
        await surface.UpdateAsync(table.Columns.Clear, "clear the columns");

        // Assert
        surface.ShouldRender("          \n          \n          ");
        table.ProgressiveController!.WindowCount.ShouldBe(0);
        table.IsProgressive.ShouldBeTrue();

        // Act restore
        await surface.UpdateAsync(() => table.Columns.Add(TableColumn.Fixed("Name", 6)), "add a column back");

        // Assert
        surface.Cell(new Point(0, 2)).Text.ShouldBe("R");
    }

    /// <summary>Verifies an automatic-width column is rejected by Add and by the indexer while
    /// progressive, leaving the column set untouched.</summary>
    [Fact]
    public async Task Columns_WhenAutoColumnIsAddedOrAssignedWhileProgressive_ThrowsBeforeMutationAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(3);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        // Act and assert
        await surface.UpdateAsync(
            () =>
            {
                var add = Should.Throw<ArgumentException>(() => table.Columns.Add(TableColumn.Auto("Extra")));
                add.ParamName.ShouldBe("column");
                var assign = Should.Throw<ArgumentException>(() => table.Columns[0] = TableColumn.Auto("Name"));
                assign.ParamName.ShouldBe("column");
            },
            "reject automatic columns");
        table.Columns.Count.ShouldBe(1);
        table.Columns[0].Width.Kind.ShouldBe(LengthKind.Cells);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");
    }

    #endregion

    #region Loading lifecycle

    /// <summary>Verifies a gated bind publishes Idle to Loading, and the released fetch publishes
    /// Loading to Idle, through the typed transition arguments.</summary>
    [Fact]
    public async Task LoadStateChanged_WhenGatedFetchResolves_PublishesIdleLoadingIdleTransitionsAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(20);
        source.Gate();
        var transitions = new List<(TableLoadState Previous, TableLoadState State)>();
        table.LoadStateChanged += (_, args) => transitions.Add((args.PreviousState, args.State));
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);

        // Act bind
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        // Assert
        transitions.ShouldBe([(TableLoadState.Idle, TableLoadState.Loading)]);
        table.LoadState.ShouldBe(TableLoadState.Loading);

        // Act release
        await SettleUntilAsync(surface, source, () => table.LoadState == TableLoadState.Idle);

        // Assert
        transitions.ShouldBe([
            (TableLoadState.Idle, TableLoadState.Loading),
            (TableLoadState.Loading, TableLoadState.Idle)
        ]);
    }

    /// <summary>Verifies an exhausted range reports the exact failed request and the source's
    /// exception after the bounded number of attempts.</summary>
    [Fact]
    public async Task LoadFailed_WhenRangeExhaustsRetries_ReportsRequestAndExceptionAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var table = CreateHost();
        var source = CreateSource(20);
        source.FailWhen = _ => true;
        var failures = new List<TableLoadFailedEventArgs>();
        table.LoadFailed += (_, args) => failures.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), clock, TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await AdvanceUntilLoadStateAsync(surface, table, TableLoadState.Failed);

        // Assert
        var failure = failures.ShouldHaveSingleItem();
        failure.Request.ShouldBe(source.Requests[0]);
        failure.Request.StartIndex.ShouldBe(0);
        _ = failure.Exception.ShouldBeOfType<InvalidOperationException>();
        failure.Exception.Message.ShouldBe("Simulated fetch failure.");
        source.LoadCallCount.ShouldBe(3);
        table.LoadState.ShouldBe(TableLoadState.Failed);
    }

    /// <summary>Verifies a Reload issued while a retry timer is pending supersedes that retry: the
    /// fresh fetch succeeds and the old range never re-issues when its timer fires.</summary>
    [Fact]
    public async Task Reload_WhenARetryIsPendingBeforeItsTimerFires_DropsTheStaleRetryAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var table = CreateHost();
        var source = CreateSource(20);
        var failing = true;
        source.FailWhen = _ => failing;
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), clock, TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        source.LoadCallCount.ShouldBe(1);
        table.LoadState.ShouldBe(TableLoadState.Loading);

        // Act reload before the retry elapses
        failing = false;
        await surface.UpdateAsync(table.Reload, "reload while the retry is pending");
        await SettleUntilAsync(surface, source, () => table.LoadState == TableLoadState.Idle);
        var loadsAfterReload = source.LoadCallCount;
        loadsAfterReload.ShouldBe(2);

        // Act let the stale timer fire
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(500), "elapse the stale retry timer");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(500), "elapse again");

        // Assert
        source.LoadCallCount.ShouldBe(loadsAfterReload);
        table.LoadState.ShouldBe(TableLoadState.Idle);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");
    }

    /// <summary>Verifies a source that ignores cancellation and completes a superseded fetch after
    /// Reload cannot populate the cache: the row stays a placeholder until the fresh fetch
    /// resolves.</summary>
    [Fact]
    public async Task Reload_WhenASupersededFetchCompletesLate_RejectsItsRowsAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(20);
        source.HonorCancellation = false;
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        source.HeldCount.ShouldBe(1);
        var placeholder = table.ActualStyle.Glyphs.Placeholder.ToString();

        // Act reload, then complete the superseded request first
        await surface.UpdateAsync(table.Reload, "reload");
        source.HeldCount.ShouldBe(2);
        await surface.UpdateAsync(() => source.Release(0), "complete the superseded fetch");
        await Task.Delay(30, TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "drain");

        // Assert the stale rows were rejected
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeTrue();
        table.LoadState.ShouldBe(TableLoadState.Loading);
        surface.Cell(new Point(0, 0)).Text.ShouldBe(placeholder);

        // Act complete the fresh request
        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(0));

        // Assert
        table.LoadState.ShouldBe(TableLoadState.Idle);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");
    }

    /// <summary>Verifies a superseded fetch that faults late neither publishes LoadFailed nor
    /// schedules a retry against the current generation.</summary>
    [Fact]
    public async Task Reload_WhenASupersededFetchFaultsLate_PublishesNoFailureAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(20);
        source.HonorCancellation = false;
        source.Gate();
        var failures = 0;
        table.LoadFailed += (_, _) => failures++;
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        // Act
        await surface.UpdateAsync(table.Reload, "reload");
        await surface.UpdateAsync(() => source.Fail(0), "fault the superseded fetch");
        await Task.Delay(30, TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "drain");

        // Assert
        failures.ShouldBe(0);
        table.LoadState.ShouldBe(TableLoadState.Loading);
        source.LoadCallCount.ShouldBe(2);
        surface.Cell(new Point(0, 0)).Text.ShouldBe(table.ActualStyle.Glyphs.Placeholder.ToString());

        // Act complete the fresh request
        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(0));
        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");
        failures.ShouldBe(0);
    }

    /// <summary>Verifies ClearDataSource during an in-flight fetch cancels it, returns the table
    /// to eager mode without a final load-state transition, and accepts eager rows again.</summary>
    [Fact]
    public async Task ClearDataSource_WhenAFetchIsInFlight_ReturnsToEagerWithoutPublishingLoadStateAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(20);
        source.Gate();
        var transitions = 0;
        var properties = new List<string?>();
        table.LoadStateChanged += (_, _) => transitions++;
        table.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        transitions.ShouldBe(1);
        source.HeldCount.ShouldBe(1);

        // Act
        await surface.UpdateAsync(table.ClearDataSource, "clear the data source");

        // Assert
        table.IsProgressive.ShouldBeFalse();
        table.LoadState.ShouldBe(TableLoadState.Idle);
        transitions.ShouldBe(1);
        source.HeldCount.ShouldBe(0);
        properties.Count(name => name == nameof(Table.IsProgressive)).ShouldBe(2);
        surface.ShouldRender("          \n          \n          ");

        // Act use eager rows again
        await surface.UpdateAsync(() => table.Rows.Add(new TableRow([new ControlText("Eager")])), "add an eager row");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("E");
        table.Rows.Count.ShouldBe(1);
    }

    /// <summary>Verifies ClearDataSource on an eager table is a silent no-op.</summary>
    [Fact]
    public void ClearDataSource_WhenNotProgressive_IsANoOp()
    {
        // Arrange
        var table = CreateHost();
        var properties = 0;
        table.PropertyChanged += (_, _) => properties++;

        // Act
        table.ClearDataSource();

        // Assert
        table.IsProgressive.ShouldBeFalse();
        properties.ShouldBe(0);
    }

    /// <summary>Verifies a source Changed notification raised off the dispatcher thread is
    /// marshaled and reloads the visible window on the dispatcher.</summary>
    [Fact]
    public async Task Changed_WhenRaisedOffTheDispatcherThread_ReloadsOnTheDispatcherAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(5);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var loads = source.LoadCallCount;
        source.Replace(0, new Item(0, "Fresh"));

        // Act
        await Task.Run(source.RaiseChanged, TestContext.Current.CancellationToken);
        await SettleUntilAsync(surface, source, () => source.LoadCallCount > loads && surface.Cell(default).Text == "F");

        // Assert
        source.LoadCallCount.ShouldBeGreaterThan(loads);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("F");
        table.LoadState.ShouldBe(TableLoadState.Idle);
    }

    /// <summary>Verifies a source Changed notification while the table is detached is ignored
    /// without faulting, and the table resumes loading once reattached.</summary>
    [Fact]
    public async Task Changed_WhenTableIsDetached_IsIgnoredAndResumesAfterReattachAsync()
    {
        // Arrange
        var table = CreateHost();
        var source = CreateSource(5);
        var host = new Overlay { Children = { table } };
        await using var surface = await ComponentSurface.MountAsync(
            host, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.UpdateAsync(() => host.Children.Remove(table).ShouldBeTrue(), "detach the table");
        var loads = source.LoadCallCount;

        // Act
        source.RaiseChanged();
        await surface.UpdateAsync(static () => { }, "drain");

        // Assert nothing happened while detached
        source.LoadCallCount.ShouldBe(loads);
        surface.ShouldRender("          \n          \n          \n          \n          ");

        // Act reattach
        await surface.UpdateAsync(() => host.Children.Add(table), "reattach the table");
        await SettleUntilAsync(surface, source, () => surface.Cell(default).Text == "R");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");
        table.IsProgressive.ShouldBeTrue();
    }

    /// <summary>Verifies a source whose result carries a null item list fails the range with an
    /// InvalidOperationException naming the contract, after the bounded retries.</summary>
    [Fact]
    public async Task LoadAsync_WhenSourceReturnsNullItems_FailsTheRangeAfterRetriesAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var table = CreateHost();
        var source = new NullItemsSource();
        var failures = new List<TableLoadFailedEventArgs>();
        table.LoadFailed += (_, args) => failures.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), clock, TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => table.SetDataSource(source, static id => new TableRow([new ControlText(id.ToString(CultureInfo.InvariantCulture))]), Length.Cells(1)), "bind source");
        await AdvanceUntilLoadStateAsync(surface, table, TableLoadState.Failed);

        // Assert
        var failure = failures.ShouldHaveSingleItem();
        _ = failure.Exception.ShouldBeOfType<InvalidOperationException>();
        failure.Exception.Message.ShouldContain("null Items");
        source.Calls.ShouldBe(3);
        surface.Cell(new Point(0, 0)).Text.ShouldBe(table.ActualStyle.Glyphs.PlaceholderError.ToString());
    }

    private sealed class NullItemsSource: ITableDataSource<int>
    {
        public int Calls { get; private set; }

        public int? Count => 10;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public object GetKey(int item) => item;

        public ValueTask<TableDataResult<int>> LoadAsync(TableDataRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(new TableDataResult<int> { Items = null!, IsEndOfData = false });
        }
    }

    #endregion
}
