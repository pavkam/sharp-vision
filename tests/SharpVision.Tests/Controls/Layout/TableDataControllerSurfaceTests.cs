// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using System.Text.Json;

/// <summary>Verifies progressive Table rendering: placeholder and loaded glyphs, visible
/// selection/current highlighting, failure recovery, keyboard navigation on unloaded rows, header
/// sort requests, detach/reattach, and equivalence with an eager table over the same data.</summary>
public sealed class TableDataControllerSurfaceTests
{
    private sealed record Item(int Id, string Name);

    private static TableRow BuildRow(Item item) => new([new ControlText(item.Name)]);

    private static FakeTableDataSource<Item> CreateSource(int count) =>
        new(Enumerable.Range(0, count).Select(static id => new Item(id, $"Row{id}")), static item => item.Id, count);

    private static Table CreateHost(bool showHeader = false, TableSelectionMode selectionMode = TableSelectionMode.Row)
    {
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowHeader = showHeader,
            ShowGridLines = false,
            SelectionMode = selectionMode
        };
        table.Columns.Add(TableColumn.Fixed("Name", 6));
        return table;
    }

    /// <summary>Verifies progressive rows freeze one viewport-relative height per layout and
    /// atomically rewindow after resize without losing the active logical row.</summary>
    [Fact]
    public async Task ResizeAsync_WhenProgressiveRowHeightIsRelative_RewindowsWithResolvedStrideAsync()
    {
        var table = CreateHost();
        table.RowSpacing = 1;
        var source = CreateSource(10_000);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => table.SetDataSource(source, BuildRow, Length.Percent(50)),
            "bind relative-row source");
        await surface.UpdateAsync(() => table.SelectIndex(2), "select progressive row");
        var controller = table.ProgressiveController!;

        controller.RowHeight.ShouldBe(4);
        controller.WindowCount.ShouldBeLessThan(10);
        await surface.UpdateAsync(() => table.ScrollBy(0, 40), "scroll relative-row table");
        var anchoredIndex = table.VerticalOffset / 5;

        await surface.ResizeAsync(new Size(20, 4));

        controller.RowHeight.ShouldBe(2);
        controller.WindowCount.ShouldBeLessThan(10);
        table.ActiveIndex.ShouldBe(2);
        (table.VerticalOffset / 3).ShouldBe(anchoredIndex);

        await SettleUntilAsync(
            surface,
            source,
            () => controller.PendingCount == 0 && !controller.IsPlaceholder(controller.WindowStart));

        source.HeldCount.ShouldBe(0);
        controller.PendingCount.ShouldBe(0);
        controller.RowAt(controller.WindowStart)!.Cells[0].Bounds.Height.ShouldBe(2);
    }

    /// <summary>Releases whatever is currently held and settles the dispatcher, repeatedly, until a
    /// predicate is satisfied or a generous iteration bound is exhausted. A gated fetch's completion
    /// still has to cross the controller's own dispatcher-marshaled commit and any resulting
    /// follow-up fetch before the effect a test cares about becomes observable; a bounded poll is
    /// more robust than assuming a single release-and-settle always lands within one pass.</summary>
    /// <remarks>
    /// The short delay after each release is deliberate, not padding: <see
    /// cref="FakeTableDataSource{T}.ReleaseAll"/> completes its <see
    /// cref="TaskCompletionSource{TResult}"/> synchronously, but the CLR is still free to run that
    /// completion's continuation (the controller's own await chain, down to the dispatcher-marshaled
    /// commit) on a thread-pool thread rather than inline - the TPL's stack-depth guard against
    /// unbounded synchronous continuation chains can force this hop, and a thread-pool thread is not
    /// guaranteed to pick the work up before <see cref="ComponentSurface.UpdateAsync"/>'s own settle
    /// wait for that same release already resolved (the dispatcher's queue was genuinely empty at
    /// that instant - correctly so, since a table fetch deliberately does not hold the dispatcher
    /// "busy" while it is in flight; that is what lets a placeholder render while a slow fetch is
    /// still outstanding). Without the delay, this loop can burn through every attempt release-and-
    /// checking before the deferred continuation ever gets a thread-pool turn.
    /// </remarks>
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

        // The loop above exits the instant `satisfied` observes the fetch's model-level effect
        // (e.g. IsPlaceholder flipping false), which FakeTableDataSource's completion can make
        // true before the dispatcher has actually painted and written that change to the terminal:
        // ReleaseAll's continuation does not always resume inline on the dispatcher thread (see
        // FakeTableDataSource's own remarks on the TPL's stack-depth guard), and when it resumes on
        // a thread-pool thread instead, it posts the controller's dispatcher-marshaled commit
        // independently of any UpdateAsync call this method is actively awaiting - so the loop can
        // observe the model change and return before the dispatcher's own next idle check has run
        // ProcessInvalidation for it. One more no-op UpdateAsync forces a real dispatcher round
        // trip gated on Application.Idle, which only fires once Root.Pending is fully drained -
        // guaranteeing any render that last release triggered has actually landed before a caller
        // inspects rendered surface content.
        await surface.UpdateAsync(static () => { }, "settle: drain pending render");
    }

    /// <summary>Verifies unloaded rows render the themed placeholder glyph immediately on mount.</summary>
    [Fact]
    public async Task Render_WhenSourceIsGated_ShowsPlaceholderGlyphImmediatelyAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        surface.Cell(new Point(0, 0)).Text.ShouldBe(table.ActualStyle.Glyphs.Placeholder.ToString());
    }

    /// <summary>Verifies a pending placeholder cell's foreground follows a
    /// <see cref="TableStyle.PlaceholderForeground"/> theme-role override through the mounted
    /// Theme, rather than the fixed <see cref="SemanticColor.Muted"/> constant the placeholder
    /// foreground was previously hardcoded to regardless of style or theme.</summary>
    [Fact]
    public async Task Render_WhenPlaceholderForegroundIsThemeColor_ResolvesThroughTheMountedThemeAsync()
    {
        var theme = WithColor(SemanticColor.Accent, Color.Rgb(0x11, 0x22, 0x33));
        var table = CreateHost();
        table.Style = TableStyle.Default with { PlaceholderForeground = SemanticColor.Accent };
        var source = CreateSource(20);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), theme, TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.Accent), ColorDepth.Basic16));
    }

    /// <summary>Creates a Dark-based theme with exactly one semantic role remapped, matching the
    /// helper of the same name in <c>TableSurfaceTests</c>.</summary>
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

    /// <summary>Verifies a resolved fetch replaces the placeholder with the loaded row's own text.</summary>
    [Fact]
    public async Task Render_WhenGatedFetchResolves_ReplacesPlaceholderWithLoadedTextAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(0));

        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");
        var loadCallCountAfterFirstResolve = source.LoadCallCount;

        // Settling an already-resolved window must not storm the source with redundant per-cell or
        // per-tick refetches.
        await surface.UpdateAsync(static () => { }, "settle");
        source.LoadCallCount.ShouldBe(loadCallCountAfterFirstResolve);
    }

    /// <summary>Verifies selecting a progressive row visibly changes its rendered style, instead of
    /// only updating internal state invisibly (regression for defect b).</summary>
    [Fact]
    public async Task Render_WhenIndexIsSelected_ChangesRenderedCellStyleAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var cell = table.ProgressiveController!.RowAt(0)!.Cells[0];

        var restingBackground = surface.Cell(new Point(0, 0)).Style.Background;
        cell.GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeFalse();

        await surface.UpdateAsync(() => table.SelectIndex(0), "select the first row");

        cell.GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeTrue();
        cell.GetAppearanceState().HasFlag(VisualState.Current).ShouldBeTrue();
        surface.Cell(new Point(0, 0)).Style.Background.ShouldNotBe(restingBackground);
    }

    /// <summary>Verifies moving the active index without selecting marks the current row's cell's
    /// rendering-driving state, distinctly from selection (regression for defect b).</summary>
    [Fact]
    public async Task Render_WhenIndexBecomesActiveUnderNoneSelectionMode_ChangesRenderedCellStyleAsync()
    {
        var table = CreateHost(selectionMode: TableSelectionMode.None);
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var cell = table.ProgressiveController!.RowAt(1)!.Cells[0];
        cell.GetAppearanceState().HasFlag(VisualState.Current).ShouldBeFalse();

        await surface.UpdateAsync(() => table.SelectIndex(1), "move the active index without selecting");

        cell.GetAppearanceState().HasFlag(VisualState.Current).ShouldBeTrue();
        cell.GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeFalse();
        table.SelectedKeys.ShouldBeEmpty();
    }

    /// <summary>Verifies CopySelection skips unloaded rows instead of blocking on or fabricating
    /// their content (regression for defect b's placeholder-exclusion contract).</summary>
    [Fact]
    public async Task CopySelection_WhenSomeSelectedRowsAreUnloaded_SkipsUnloadedRowsAsync()
    {
        var table = CreateHost(selectionMode: TableSelectionMode.MultipleRows);
        var source = CreateSource(200);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(0));
        await surface.UpdateAsync(table.SelectAll, "select every currently loaded row");

        var copied = await surface.Application.Dispatcher.InvokeAsync(
            table.CopySelection, TestContext.Current.CancellationToken);

        copied.ShouldNotBeNullOrEmpty();
        copied.ShouldNotContain("Row199");
        table.ProgressiveController!.IsPlaceholder(199).ShouldBeTrue();
    }

    /// <summary>Verifies an exhausted range renders the themed error glyph, raises LoadFailed exactly
    /// once, and recovers to loaded text after a successful Reload.</summary>
    [Fact]
    public async Task Render_WhenRangeExhaustsRetries_ShowsErrorGlyphThenRecoversAsync()
    {
        var clock = new ManualTimeProvider();
        var table = CreateHost();
        var source = CreateSource(20);
        var failing = true;
        source.FailWhen = _ => failing;
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), clock, TestContext.Current.CancellationToken);
        var loadFailedCount = 0;
        table.LoadFailed += (_, _) => loadFailedCount++;

        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await AdvanceUntilLoadStateAsync(surface, table, TableLoadState.Failed);

        loadFailedCount.ShouldBe(1);
        surface.Cell(new Point(0, 0)).Text.ShouldBe(table.ActualStyle.Glyphs.PlaceholderError.ToString());

        failing = false;
        await surface.UpdateAsync(table.Reload, "reload once the source recovers");

        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");
        loadFailedCount.ShouldBe(1);
    }

    /// <summary>Advances the deterministic clock by the retry delay, repeatedly, until LoadState
    /// reaches an expected value or a generous iteration bound is exhausted. Each advance only
    /// reliably drains the retry timers already due at its start - a reissued fetch's own failure
    /// schedules its next retry timer on a later dispatcher turn, after the current advance's timer
    /// walk already finished - so reaching a deeper retry can take more than one advance call.</summary>
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

    /// <summary>Verifies Home/End move the active index immediately against unloaded rows, without
    /// blocking on their fetch, and that the fetch is still issued asynchronously.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeAndEndTargetUnloadedIndexes_MoveImmediatelyAndFetchAsynchronouslyAsync()
    {
        var table = CreateHost();
        var source = CreateSource(500);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.Pointer.ClickAsync(table, new Point(0, 0));
        var loadCallCountBeforeEnd = source.LoadCallCount;

        await surface.Keyboard.PressAsync(Code.End);

        table.ActiveIndex.ShouldBe(499);
        table.ProgressiveController!.IsPlaceholder(499).ShouldBeTrue();
        source.LoadCallCount.ShouldBeGreaterThan(loadCallCountBeforeEnd);

        var loadCallCountBeforeHome = source.LoadCallCount;

        await surface.Keyboard.PressAsync(Code.Home);

        table.ActiveIndex.ShouldBe(0);
        source.LoadCallCount.ShouldBeGreaterThan(loadCallCountBeforeHome);
    }

    /// <summary>Verifies keyboard navigation to a far, currently off-window row scrolls it fully
    /// inside the viewport once RowGap is positive - regression for BringIntoProgressiveView
    /// computing its scroll target from RowHeight alone instead of the gap-inclusive stride the
    /// resolved window is actually arranged with, which stranded the newly active row outside the
    /// viewport it had just supposedly been scrolled into.</summary>
    [Fact]
    public async Task Keyboard_WhenEndTargetsFarRowWithPositiveRowGap_ScrollsItFullyIntoViewportAsync()
    {
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowHeader = false,
            ShowGridLines = false,
            RowSpacing = 2
        };
        table.Columns.Add(TableColumn.Fixed("Name", 6));
        var source = CreateSource(100);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.Pointer.ClickAsync(table, new Point(0, 0));

        await surface.Keyboard.PressAsync(Code.End);

        table.ActiveIndex.ShouldBe(99);

        // The active row's own screen position - independently derived from the same
        // gap-inclusive stride ArrangeWindow uses to place it - must land fully inside the
        // viewport, not merely somewhere VerticalOffset changed to.
        var stride = 1 + table.RowSpacing;
        var screenRow = (table.ActiveIndex * stride) - table.VerticalOffset;
        screenRow.ShouldBeGreaterThanOrEqualTo(0);
        screenRow.ShouldBeLessThan(table.Viewport.Height);

        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(99));

        surface.Cell(new Point(0, screenRow)).Text.ShouldBe("R");
    }

    /// <summary>Verifies the progressive path's PageDown distance matches what eager mode's
    /// PagingStep.Accumulate produces for the same viewport/row-height combination: RowHeight = Length.Cells(3)
    /// against a Viewport.Height of 10 accumulates 3, 6, 9, 12 and stops at 12 (the fourth row),
    /// so PageDown must advance ActiveIndex by 4 rows rather than floor(10/3) = 3.</summary>
    [Fact]
    public async Task Keyboard_WhenPageDownWithFixedRowHeightLeavesRemainder_RoundsUpLikeEagerAccumulateAsync()
    {
        var table = CreateHost();
        var source = CreateSource(50);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(3)), "bind source");
        await surface.Pointer.ClickAsync(table, new Point(0, 0));

        await surface.Keyboard.PressAsync(Code.PageDown);

        table.ActiveIndex.ShouldBe(4);
    }

    /// <summary>Verifies the progressive path's PageDown distance accounts for RowGap - regression
    /// for StepPageRows computing the page step from RowHeight alone instead of the gap-inclusive
    /// stride ArrangeWindow/TryResolvePoint/BringIntoProgressiveView all use, which advanced
    /// ActiveIndex by far more rows than actually fit in one viewport page. RowHeight = Length.Cells(3), RowGap=2
    /// (via RowSpacing) gives a stride of 5 against a Viewport.Height of 10, so PageDown must
    /// advance ActiveIndex by 2 rows (floor(10/5) = 2), not floor(10/3) = 3 or ceil(10/3) = 4.</summary>
    [Fact]
    public async Task Keyboard_WhenPageDownWithPositiveRowGap_AccountsForGapInclusiveStrideAsync()
    {
        var table = CreateHost();
        table.RowSpacing = 2;
        var source = CreateSource(50);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(3)), "bind source");
        await surface.Pointer.ClickAsync(table, new Point(0, 0));

        await surface.Keyboard.PressAsync(Code.PageDown);

        table.ActiveIndex.ShouldBe(2);
    }

    /// <summary>Verifies a sortable header click while progressive updates the sort indicator,
    /// raises SortRequested with the cycled column and direction, and reloads - without Table ever
    /// reordering rows itself (regression for defect e).</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsPressedWhileProgressive_RaisesSortRequestedAndReloadsAsync()
    {
        var table = CreateHost(showHeader: true);
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 6), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        List<TableSortChangedEventArgs> requests = [];
        table.SortRequested += (_, args) => requests.Add(args);
        var loadCallCountBeforeClick = source.LoadCallCount;

        await surface.Pointer.ClickAsync(table, new Point(1, 0));

        _ = requests.ShouldHaveSingleItem();
        requests[0].ColumnIndex.ShouldBe(0);
        requests[0].Direction.ShouldBe(TableSortDirection.Ascending);
        table.SortColumnIndex.ShouldBe(0);
        table.SortDirection.ShouldBe(TableSortDirection.Ascending);

        // Reload() discards the cache and re-fetches the visible window - a fresh LoadAsync call
        // proves it actually ran, not merely that the indicator changed.
        source.LoadCallCount.ShouldBeGreaterThan(loadCallCountBeforeClick);
    }

    /// <summary>Verifies sort callbacks may remove, replace, or dispose progressive state without
    /// the superseded outer request reloading through invalidated controller ownership.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Pointer_WhenSortRequestedInvalidatesProgressiveController_DoesNotReloadStaleStateAsync(
        int mutation)
    {
        var table = CreateHost(showHeader: true);
        var source = CreateSource(20);
        var replacement = CreateSource(20);
        var host = new Overlay { Children = { table } };
        await using var surface = await ComponentSurface.MountAsync(
            host, new Size(10, 6), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var replacementLoadsAfterMutation = 0;
        table.SortRequested += (_, _) =>
        {
            switch (mutation)
            {
                case 0:
                    table.ClearDataSource();
                    break;
                case 1:
                    table.SetDataSource(replacement, BuildRow, Length.Cells(1));
                    replacementLoadsAfterMutation = replacement.LoadCallCount;
                    break;
                case 2:
                    table.Dispose();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        };

        await Should.NotThrowAsync(() => surface.Pointer.ClickAsync(table, new Point(1, 0)));

        if (mutation == 1)
        {
            replacement.LoadCallCount.ShouldBe(replacementLoadsAfterMutation);
        }
    }

    /// <summary>Verifies a newer reentrant sort owns the sole reload and the outer request does not
    /// cancel and fetch the same controller a second time.</summary>
    [Fact]
    public async Task RequestProgressiveSort_WhenHandlerStartsNewerSort_ReloadsNewestRequestOnceAsync()
    {
        var table = CreateHost(showHeader: true);
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 6), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var reentered = false;
        table.SortRequested += (_, _) =>
        {
            if (!reentered)
            {
                reentered = true;
                table.RequestProgressiveSortForLifecycleTest(0);
            }
        };
        var loadsBeforeSort = source.LoadCallCount;

        await surface.UpdateAsync(
            () => table.RequestProgressiveSortForLifecycleTest(0),
            "request reentrant progressive sort");

        table.SortDirection.ShouldBe(TableSortDirection.Descending);
        source.LoadCallCount.ShouldBe(loadsBeforeSort + 1);
    }

    /// <summary>Verifies leaving the tree mid-fetch cancels the in-flight request, and reattaching
    /// resumes progressive loading with a fresh fetch that still resolves normally.</summary>
    [Fact]
    public async Task Detach_WhenTableLeavesTreeMidFetchAndReattaches_ResumesLoadingAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        source.Gate();
        var host = new Overlay { IsFocusable = true };
        host.Children.Add(table);
        await using var surface = await ComponentSurface.MountAsync(
            host, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        source.HeldCount.ShouldBe(1);

        await surface.UpdateAsync(() => host.Children.Remove(table), "detach mid-fetch");
        source.HeldCount.ShouldBe(0);

        await surface.UpdateAsync(() => host.Children.Add(table), "reattach");

        source.HeldCount.ShouldBe(1);

        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(0));

        table.ProgressiveController!.IsPlaceholder(0).ShouldBeFalse();
    }

    /// <summary>Verifies a one-cell viewport neither crashes nor hangs.</summary>
    [Fact]
    public async Task Render_WhenViewportIsTiny_DoesNotCrashAsync()
    {
        var table = CreateHost();
        var source = CreateSource(50);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(1, 1), TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        table.IsProgressive.ShouldBeTrue();
    }

    /// <summary>Verifies an adapter-driven <c>ITableDataSource&lt;T&gt;.Changed</c> event - raised
    /// directly, not through the <see cref="FakeTableDataSource{T}.Count"/> setter - actually issues
    /// a fresh fetch and briefly shows a placeholder again for an already-realized row, instead of
    /// silently clearing state with nothing to ever repaint it (OnAdapterChanged must rewindow, not
    /// just clear the cache).</summary>
    [Fact]
    public async Task AdapterChanged_WhenSourceRaisesChangedDirectly_IssuesFetchAndRefreshesRowAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeFalse();
        var loadCallCountBeforeChange = source.LoadCallCount;

        source.Gate();
        await surface.UpdateAsync(source.RaiseChanged, "raise adapter Changed directly");

        // A new fetch must be issued immediately (OnAdapterChanged must rewindow, not merely
        // clear state), and the row that was already realized before the event must go back to
        // showing a placeholder until that fetch resolves.
        source.LoadCallCount.ShouldBeGreaterThan(loadCallCountBeforeChange);
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeTrue();

        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(0));

        table.ProgressiveController!.IsPlaceholder(0).ShouldBeFalse();
        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");
    }

    /// <summary>Verifies the <see cref="FakeTableDataSource{T}.Count"/> setter - a second production
    /// path that raises <c>ITableDataSource&lt;T&gt;.Changed</c> alongside direct raises - shares the
    /// same fix: a fresh fetch is issued without waiting on an unrelated scroll, resize, or attach.</summary>
    [Fact]
    public async Task AdapterChanged_WhenCountSetterRaisesChanged_IssuesFetchAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeFalse();
        var loadCallCountBeforeChange = source.LoadCallCount;

        source.Gate();
        await surface.UpdateAsync(() => source.Count = 30, "grow the reported count via its setter");

        source.LoadCallCount.ShouldBeGreaterThan(loadCallCountBeforeChange);
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeTrue();

        await SettleUntilAsync(surface, source, () => !table.ProgressiveController!.IsPlaceholder(0));

        table.ProgressiveController!.IsPlaceholder(0).ShouldBeFalse();
    }

    /// <summary>Verifies a source notification queued on one dispatcher cannot reload the same
    /// progressive controller after its table migrates to another dispatcher attachment.</summary>
    [Fact]
    public async Task AdapterChanged_WhenTableMigratesBeforeQueuedCallback_IgnoresPreviousDispatcherAsync()
    {
        await using var previousDispatcher = Dispatcher.Start();
        await using var currentDispatcher = Dispatcher.Start();
        var table = CreateHost();
        var source = CreateSource(20);
        var previousRoot = new Overlay { Children = { table } };
        var currentRoot = new Overlay();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var detached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim changed = new();
        using ManualResetEventSlim release = new();
        previousDispatcher.Post(() =>
        {
            previousRoot.Attach(previousDispatcher);
            table.SetDataSource(source, BuildRow, Length.Cells(1));
            ready.SetResult();
            changed.Wait();
            previousRoot.Children.Remove(table).ShouldBeTrue();
            detached.SetResult();
            release.Wait();
        });
        await ready.Task.WaitAsync(TestContext.Current.CancellationToken);

        source.RaiseChanged();
        changed.Set();
        await detached.Task.WaitAsync(TestContext.Current.CancellationToken);
        await currentDispatcher.InvokeAsync(
            () =>
            {
                currentRoot.Children.Add(table);
                currentRoot.Attach(currentDispatcher);
            },
            TestContext.Current.CancellationToken);
        var loadCountBeforeStaleCallback = source.LoadCallCount;

        release.Set();
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        source.LoadCallCount.ShouldBe(loadCountBeforeStaleCallback);
        await currentDispatcher.InvokeAsync(currentRoot.Dispose, TestContext.Current.CancellationToken);
        await previousDispatcher.InvokeAsync(previousRoot.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failed fetch queued on a former dispatcher cannot resurrect its removed
    /// pending range or schedule a retry after the table migrates to a new attachment.</summary>
    [Fact]
    public async Task FetchFailure_WhenTableMigratesBeforeQueuedCallback_IgnoresPreviousDispatcherAsync()
    {
        await using var previousDispatcher = Dispatcher.Start();
        await using var currentDispatcher = Dispatcher.Start();
        var table = CreateHost();
        var source = CreateSource(20);
        source.Gate();
        source.HonorCancellation = false;
        var previousRoot = new Overlay { Children = { table } };
        var currentRoot = new Overlay();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var detached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim failed = new();
        using ManualResetEventSlim release = new();
        previousDispatcher.Post(() =>
        {
            previousRoot.Attach(previousDispatcher);
            table.SetDataSource(source, BuildRow, Length.Cells(1));
            ready.SetResult();
            failed.Wait();
            previousRoot.Children.Remove(table).ShouldBeTrue();
            detached.SetResult();
            release.Wait();
        });
        await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
        var request = source.Requests.ShouldHaveSingleItem();

        source.Fail(request.StartIndex);
        failed.Set();
        await detached.Task.WaitAsync(TestContext.Current.CancellationToken);
        var pendingBeforeStaleCallback = await currentDispatcher.InvokeAsync(() =>
        {
            currentRoot.Children.Add(table);
            currentRoot.Attach(currentDispatcher);
            return table.ProgressiveController!.PendingCount;
        }, TestContext.Current.CancellationToken);

        release.Set();
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        await currentDispatcher.InvokeAsync(() =>
        {
            table.ProgressiveController!.PendingCount.ShouldBe(pendingBeforeStaleCallback);
            currentRoot.Dispose();
        }, TestContext.Current.CancellationToken);
        previousDispatcher.FatalException.ShouldBeNull();
        await previousDispatcher.InvokeAsync(previousRoot.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the public <see cref="Table.Reload"/> path actually repaints an
    /// already-realized row whose backing item changed at the same key/index and scroll position -
    /// not only placeholder/error cells - proving the fix must touch <c>Reload()</c>'s own
    /// realize/derealize handling, not merely <c>OnAdapterChanged</c>'s wiring.</summary>
    [Fact]
    public async Task Reload_WhenBackingItemChangesAtSameKeyAndPosition_RefreshesRealizedRowTextAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        table.ProgressiveController!.IsPlaceholder(0).ShouldBeFalse();
        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");

        // Same key (Id: 0), same logical index, only the displayed text changes - no scroll, no
        // window movement, nothing but a plain Reload() to pick the new content up.
        source.Replace(0, new Item(0, "Changed"));
        await surface.UpdateAsync(table.Reload, "reload after mutating the backing item in place");

        surface.Cell(new Point(0, 0)).Text.ShouldBe("C");
    }

    /// <summary>Verifies a progressive table over a small, fully resolved finite source renders
    /// identically to an equivalent eager table over the same rows and selection.</summary>
    [Fact]
    public async Task Render_WhenSmallSourceIsFullyResolved_MatchesEquivalentEagerTableAsync()
    {
        var size = new Size(10, 6);
        var eager = CreateHost(selectionMode: TableSelectionMode.Row);

        foreach (var id in Enumerable.Range(0, 5))
        {
            eager.Rows.Add(new TableRow([new ControlText($"Row{id}")]));
        }

        await using var eagerSurface = await ComponentSurface.MountAsync(
            eager, size, TestContext.Current.CancellationToken);
        await eagerSurface.UpdateAsync(() => eager.SelectRow(eager.Rows[2]), "select row 2 (eager)");

        var progressive = CreateHost(selectionMode: TableSelectionMode.Row);
        var source = CreateSource(5);
        await using var progressiveSurface = await ComponentSurface.MountAsync(
            progressive, size, TestContext.Current.CancellationToken);
        await progressiveSurface.UpdateAsync(() => progressive.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await progressiveSurface.UpdateAsync(() => progressive.SelectIndex(2), "select index 2 (progressive)");

        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var point = new Point(x, y);
                var eagerCell = eagerSurface.Cell(point);
                var progressiveCell = progressiveSurface.Cell(point);
                progressiveCell.Text.ShouldBe(eagerCell.Text, $"mismatch at ({x},{y})");
                progressiveCell.Style.ShouldBe(eagerCell.Style, $"style mismatch at ({x},{y})");
            }
        }
    }

    /// <summary>Verifies scrolling a gated, row-spaced progressive table several viewports deep,
    /// then releasing the pending fetch and settling, renders the target row's own text at the
    /// exact screen row the corrected, gap-inclusive stride predicts - not merely that its
    /// placeholder flag flipped. A fetch can genuinely resolve and the cache can genuinely hold the
    /// right item while the resolved window (and so the arranged row) still lands at the wrong
    /// logical index or drifts entirely outside the viewport, which an <c>IsPlaceholder</c>-only
    /// assertion would never catch.</summary>
    [Fact]
    public async Task Render_WhenScrolledDeepWithPositiveRowGap_RendersTargetRowAtItsPredictedScreenRowAsync()
    {
        static TableRow BuildIndexRow(Item item) => new([new ControlText(item.Id.ToString(CultureInfo.InvariantCulture))]);

        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowHeader = false,
            ShowGridLines = false,
            RowSpacing = 1
        };
        table.Columns.Add(TableColumn.Fixed("Name", 6));

        const int rowHeight = 1;
        const int rowSpacing = 1;
        const int stride = rowHeight + rowSpacing;
        const int targetIndex = 200;
        const int targetScreenRow = 2;
        var verticalOffset = (targetIndex * stride) - targetScreenRow;

        var source = new FakeTableDataSource<Item>(
            Enumerable.Range(0, 500).Select(static id => new Item(id, id.ToString(CultureInfo.InvariantCulture))),
            static item => item.Id,
            count: 500);
        source.Gate();

        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildIndexRow, Length.Cells(rowHeight)), "bind source");

        // The source reports a known, fixed total count from the start, so Extent already
        // reflects the full 500 rows before any fetch resolves - the jump below is a legitimate
        // scroll several viewports deep, not one that outruns a still-elastic extent.
        await surface.UpdateAsync(() => table.VerticalOffset = verticalOffset, "scroll several viewports deep");

        var controller = table.ProgressiveController!;
        await SettleUntilAsync(surface, source, () => !controller.IsPlaceholder(targetIndex));

        controller.IsPlaceholder(targetIndex).ShouldBeFalse();

        var expectedText = targetIndex.ToString(CultureInfo.InvariantCulture);

        for (var i = 0; i < expectedText.Length; i++)
        {
            surface.Cell(new Point(i, targetScreenRow)).Text.ShouldBe(expectedText[i].ToString());
        }
    }

    private sealed record TwoColumnItem(int Id, string First, string Second);

    private static TableRow BuildTwoColumnRow(TwoColumnItem item) =>
        new([new ControlText(item.First), new ControlText(item.Second)]);

    /// <summary>Verifies a horizontally scrolled progressive table renders its cells at the
    /// offset-shifted screen position, mirroring eager <see
    /// cref="TableTests.Render_WhenHorizontallyScrolled_TranslatesCompleteTableContent"/> for the
    /// progressive path - regression for ArrangeWindow computing every column's X from the
    /// un-shifted ProgressiveOrigin.X alone, unlike its own Y arithmetic, which already subtracts
    /// VerticalOffset the same way.</summary>
    [Fact]
    public async Task Render_WhenHorizontallyScrolledWhileProgressive_TranslatesCompleteTableContentAsync()
    {
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowHeader = false,
            ShowGridLines = false,
            ScrollBars = ScrollBars.Both
        };
        table.Columns.Add(TableColumn.Fixed("First", 8));
        table.Columns.Add(TableColumn.Fixed("Second", 8));
        var source = new FakeTableDataSource<TwoColumnItem>(
            [new TwoColumnItem(0, "12345678", "abcdefgh")], static item => item.Id, count: 1);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(10, 4), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildTwoColumnRow, Length.Cells(1)), "bind source");

        await surface.UpdateAsync(() => table.HorizontalOffset = 3, "scroll horizontally");

        // Column 1 ("12345678") is shifted 3 cells left by the scroll, so screen x=0 shows its
        // fourth character. Column 2 ("abcdefgh") then starts exactly where column 1 would have
        // ended un-shifted, at screen x=5.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("4");
        surface.Cell(new Point(5, 0)).Text.ShouldBe("a");
    }
}
