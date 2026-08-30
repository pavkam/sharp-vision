// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies progressive Table loading: SetDataSource preconditions, fetch scheduling,
/// generation and cancellation discipline, cache eviction, and key-based selection - independent of
/// rendered surface content (see <see cref="TableDataControllerSurfaceTests"/> for that).</summary>
public sealed class TableDataControllerTests
{
    private sealed record Item(int Id, string Name);

    private static TableRow BuildRow(Item item) => new([new ControlText(item.Name)]);

    private static FakeTableDataSource<Item> CreateSource(int count, int? total = null) =>
        new(Enumerable.Range(0, count).Select(static id => new Item(id, $"Row{id}")), static item => item.Id, total ?? count);

    private static Table CreateHost(TableSelectionMode selectionMode = TableSelectionMode.Row)
    {
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowHeader = false,
            ShowGridLines = false,
            SelectionMode = selectionMode
        };
        table.Columns.Add(TableColumn.Fixed("Name", 10));
        return table;
    }

    /// <summary>Verifies a selection callback that disables or detaches a progressive Table ends
    /// the current pointer transaction before RowInvoked can publish.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pointer_WhenProgressiveSelectionCallbackMakesTableUnavailable_DoesNotInvokeRowAsync(bool detach)
    {
        // Arrange
        var table = CreateHost();
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
        await surface.UpdateAsync(
            () => table.SetDataSource(CreateSource(1), BuildRow, Length.Cells(1)),
            "bind one progressive row");

        // Act
        await surface.Pointer.ClickAsync(table, new Point(1, 0));

        // Assert
        invoked.ShouldBe(0);
    }

    #region SetDataSource preconditions

    /// <summary>Verifies a non-empty Rows collection is rejected and leaves an eager table eager.</summary>
    [Fact]
    public async Task SetDataSource_WhenRowsIsNotEmpty_ThrowsAndLeavesEagerModeUntouchedAsync()
    {
        var table = CreateHost();
        table.Rows.Add(new TableRow([new ControlText("existing")]));
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        var source = CreateSource(10);

        _ = await surface.Application.Dispatcher.InvokeAsync(
            () => Should.Throw<InvalidOperationException>(() => table.SetDataSource(source, BuildRow, Length.Cells(1))),
            TestContext.Current.CancellationToken);

        table.IsProgressive.ShouldBeFalse();
        table.Rows.Count.ShouldBe(1);
    }

    /// <summary>Verifies Cell and MultipleCells selection modes are rejected before mutation.</summary>
    [Theory]
    [InlineData(TableSelectionMode.Cell)]
    [InlineData(TableSelectionMode.MultipleCells)]
    public async Task SetDataSource_WhenSelectionModeIsCellBased_ThrowsAndLeavesModeUntouchedAsync(TableSelectionMode mode)
    {
        var table = CreateHost(mode);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        var source = CreateSource(10);

        _ = await surface.Application.Dispatcher.InvokeAsync(
            () => Should.Throw<InvalidOperationException>(() => table.SetDataSource(source, BuildRow, Length.Cells(1))),
            TestContext.Current.CancellationToken);

        table.IsProgressive.ShouldBeFalse();
        table.SelectionMode.ShouldBe(mode);
    }

    /// <summary>Verifies an automatic-width column is rejected before mutation.</summary>
    [Fact]
    public async Task SetDataSource_WhenColumnIsAutoWidth_ThrowsAndLeavesModeUntouchedAsync()
    {
        var table = CreateHost();
        table.Columns.Add(TableColumn.Auto("Wide"));
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        var source = CreateSource(10);

        _ = await surface.Application.Dispatcher.InvokeAsync(
            () => Should.Throw<ArgumentException>(() => table.SetDataSource(source, BuildRow, Length.Cells(1))),
            TestContext.Current.CancellationToken);

        table.IsProgressive.ShouldBeFalse();
    }

    /// <summary>Verifies a table with no attached dispatcher rejects SetDataSource.</summary>
    [Fact]
    public void SetDataSource_WhenNoDispatcher_Throws()
    {
        var table = CreateHost();
        var source = CreateSource(10);

        _ = Should.Throw<InvalidOperationException>(() => table.SetDataSource(source, BuildRow, Length.Cells(1)));
        table.IsProgressive.ShouldBeFalse();
    }

    /// <summary>Verifies accepted extreme row spacing resolves one positive saturated stride for
    /// realization, arrangement, hit testing, and paging.</summary>
    [Fact]
    public async Task Geometry_WhenRowStrideOverflowsInt32_RemainsConsistentAsync()
    {
        var table = CreateHost();
        table.RowSpacing = int.MaxValue;
        var source = CreateSource(3);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind extreme stride");

        var controller = table.ProgressiveController!;
        controller.WindowCount.ShouldBeGreaterThan(0);
        controller.WindowStart.ShouldBe(0);
        controller.TryResolvePoint(new Point(0, 0), out var logicalIndex, out _).ShouldBeTrue();
        logicalIndex.ShouldBe(0);
        await surface.UpdateAsync(() => table.SelectIndex(1), "select with extreme stride");
        table.VerticalOffset.ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies bottom-window prefetch arithmetic saturates instead of wrapping past an
    /// accepted maximum logical count.</summary>
    [Fact]
    public async Task Rewindow_WhenScrolledToSaturatedBottom_RealizesFinalWindowAsync()
    {
        var table = CreateHost();
        var source = new BrokenIntSource(
            request => new TableDataResult<int>
            {
                Items = Enumerable.Range(request.StartIndex, request.Count).ToArray(),
                IsEndOfData = false
            },
            count: int.MaxValue);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, static value => new TableRow([new ControlText(value.ToString(CultureInfo.InvariantCulture))]), Length.Cells(1)), "bind maximum count");

        await surface.UpdateAsync(
            () => table.VerticalOffset = Math.Max(0, table.Extent.Height - table.Viewport.Height),
            "scroll to saturated bottom");

        var controller = table.ProgressiveController!;
        controller.WindowCount.ShouldBeGreaterThan(0);
        controller.WindowStart.ShouldBeGreaterThan(int.MaxValue - 20);
    }

    /// <summary>Verifies a rejected SetDataSource call while already progressive leaves the original
    /// controller and source bound, rather than tearing it down.</summary>
    [Fact]
    public async Task SetDataSource_WhenCandidateIsRejectedWhileProgressive_LeavesPriorSourceBoundAsync()
    {
        var table = CreateHost();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        var first = CreateSource(10);
        await surface.UpdateAsync(() => table.SetDataSource(first, BuildRow, Length.Cells(1)), "bind first source");
        var originalController = table.ProgressiveController;

        var second = CreateSource(5);

        _ = await surface.Application.Dispatcher.InvokeAsync(
            () => Should.Throw<ArgumentOutOfRangeException>(() => table.SetDataSource(second, BuildRow, rowHeight: Length.Cells(0))),
            TestContext.Current.CancellationToken);

        table.IsProgressive.ShouldBeTrue();
        table.ProgressiveController.ShouldBeSameAs(originalController);
    }

    /// <summary>Verifies progressive mode rejects row requests that cannot resolve to one bounded
    /// uniform stride before replacing the current source.</summary>
    [Theory]
    [MemberData(nameof(InvalidProgressiveRowHeights))]
    public async Task SetDataSource_WhenRowHeightKindIsUnsupported_ThrowsBeforeMutationAsync(Length rowHeight)
    {
        var table = CreateHost();
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var source = CreateSource(10);

        _ = await surface.Application.Dispatcher.InvokeAsync(
            () => Should.Throw<ArgumentException>(() => table.SetDataSource(source, BuildRow, rowHeight)),
            TestContext.Current.CancellationToken);

        table.IsProgressive.ShouldBeFalse();
    }

    /// <summary>Gets row requests that cannot define a positive progressive uniform stride.</summary>
    public static TheoryData<Length> InvalidProgressiveRowHeights =>
    [
        Length.Auto,
        Length.Cells(0),
        Length.Percent(0),
        Length.Star(1)
    ];

    /// <summary>Verifies replacing a progressive source derealizes the prior presenter's cells and
    /// realizes a functional replacement window.</summary>
    [Fact]
    public async Task SetDataSource_WhenReplacingProgressiveSource_DerealizesPriorCellsAsync()
    {
        var table = CreateHost();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(CreateSource(10), BuildRow, Length.Cells(1)), "bind first source");
        var previousCell = table.ProgressiveController!.RowAt(0)!.Cells[0];

        await surface.UpdateAsync(
            () => table.SetDataSource(CreateSource(5), BuildRow, Length.Cells(1)),
            "replace the progressive source");

        previousCell.Parent.ShouldBeNull();
        var replacementCell = table.ProgressiveController!.RowAt(0)!.Cells[0];
        replacementCell.ShouldNotBeSameAs(previousCell);
        _ = replacementCell.Parent.ShouldNotBeNull();
    }

    /// <summary>Verifies clearing a progressive source derealizes its presenter cells and returns
    /// the table to eager mode.</summary>
    [Fact]
    public async Task ClearDataSource_WhenProgressiveRowsAreRealized_DerealizesPresenterCellsAsync()
    {
        var table = CreateHost();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(CreateSource(10), BuildRow, Length.Cells(1)), "bind source");
        var previousCell = table.ProgressiveController!.RowAt(0)!.Cells[0];

        await surface.UpdateAsync(table.ClearDataSource, "clear the progressive source");

        table.IsProgressive.ShouldBeFalse();
        previousCell.Parent.ShouldBeNull();
    }

    #endregion

    #region Fetch scheduling

    /// <summary>Verifies a single visible range issues exactly one fetch instead of one per row.</summary>
    [Fact]
    public async Task Rewindow_WhenWindowIsEntirelyUncached_IssuesOneCoalescedFetchAsync()
    {
        var table = CreateHost();
        var source = CreateSource(200);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        source.LoadCallCount.ShouldBe(1);
        source.Requests.ShouldHaveSingleItem().StartIndex.ShouldBe(0);
    }

    /// <summary>Verifies a scroll that overlaps an already-pending range only fetches the net-new gap.</summary>
    [Fact]
    public async Task Rewindow_WhenNewWindowOverlapsPendingRange_FetchesOnlyTheNewGapAsync()
    {
        var table = CreateHost();
        var source = CreateSource(200);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var firstRequest = source.Requests.ShouldHaveSingleItem();

        // Scroll forward while the first request is still held/pending - the new window overlaps
        // it, so only the genuinely new tail should be requested.
        await surface.UpdateAsync(() => table.VerticalOffset = firstRequest.Count / 2, "scroll into pending range");

        source.LoadCallCount.ShouldBe(2);
        var secondRequest = source.Requests[1];
        secondRequest.StartIndex.ShouldBeGreaterThanOrEqualTo(firstRequest.StartIndex + firstRequest.Count);
    }

    /// <summary>Verifies more than four simultaneous gaps never exceed four concurrent fetches.</summary>
    /// <remarks>
    /// The viewport only ever grows from a fixed offset of zero, so nothing already
    /// cached/pending is ever evicted or canceled between steps (both are always relative to the
    /// still-widening window). Each growth step below reveals exactly one new trailing gap - by the
    /// fifth step, four fetches are already held (gated, unresolved) and the concurrency cap must
    /// refuse to issue a fifth, leaving that trailing gap genuinely unfetched.
    /// </remarks>
    [Fact]
    public async Task Rewindow_WhenMoreThanFourGapsAreVisible_CapsConcurrentFetchesAtFourAsync()
    {
        var table = CreateHost();
        var source = CreateSource(1000);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 2), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        // Heights 4, 6, 8, 10 each grow the window by exactly one new row-count worth of trailing
        // gap (window = [0, 2*height - 1] at offset zero); the fifth growth's gap is skipped once
        // four fetches are already held.
        foreach (var height in new[] { 4, 6, 8, 10 })
        {
            await surface.ResizeAsync(new Size(20, height));
        }

        source.HeldCount.ShouldBe(4);
        source.LoadCallCount.ShouldBe(4);
    }

    /// <summary>Verifies a throwing failure observer cannot strand a visible gap after the failed
    /// request releases one of the controller's four admission slots.</summary>
    [Fact]
    public async Task LoadFailed_WhenObserverThrows_StillRewindowsAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var table = CreateHost();
        var source = CreateSource(1000);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 2), clock, TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        foreach (var height in new[] { 4, 6, 8, 10 })
        {
            await surface.ResizeAsync(new Size(20, height));
        }

        var failedStart = source.Requests[0].StartIndex;
        table.LoadFailed += (_, _) => throw new InvalidOperationException("observer failure");

        // Act: two failures schedule retries; the third exhausts the range and invokes the observer.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await surface.UpdateAsync(() => source.Fail(failedStart), $"fail attempt {attempt}");
            await WaitUntilDispatcherStateAsync(
                surface,
                () => table.ProgressiveController!.ScheduledRetryCount == 1);
            await surface.AdvanceAsync(TimeSpan.FromMilliseconds(250), $"retry attempt {attempt}");
            var expectedCalls = 5 + attempt;
            await WaitUntilDispatcherStateAsync(surface, () => source.LoadCallCount >= expectedCalls);
        }

        await surface.UpdateAsync(() => source.Fail(failedStart), "exhaust failed range");

        await WaitUntilDispatcherStateAsync(surface, () => source.LoadCallCount >= 7);

        // Assert: the uncovered fifth range was admitted in the observer-invocation finally path.
        source.LoadCallCount.ShouldBe(7);
        source.HeldCount.ShouldBe(4);
    }

    private static async Task WaitUntilDispatcherStateAsync(ComponentSurface surface, Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            if (await surface.Application.Dispatcher.InvokeAsync(
                    predicate,
                    TestContext.Current.CancellationToken))
            {
                return;
            }

            await Task.Yield();
        }

        throw new TimeoutException("The dispatcher state did not settle within the bounded yield loop.");
    }

    #endregion

    #region Rewindow stride

    /// <summary>Verifies Rewindow resolves its realized [first, last] window using the
    /// gap-inclusive stride (RowHeight + RowGap) once RowSpacing makes that gap positive, matching
    /// the stride ArrangeWindow and TryResolvePoint already use to position and hit-test those same
    /// rows - not RowHeight alone, which drifts the resolved window away from what actually gets
    /// arranged as soon as the offset is deep enough for the two strides to disagree.</summary>
    [Fact]
    public async Task Rewindow_WhenRowGapIsPositive_ResolvesWindowUsingGapInclusiveStrideAsync()
    {
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowHeader = false,
            ShowGridLines = false,
            RowSpacing = 2
        };
        table.Columns.Add(TableColumn.Fixed("Name", 10));
        var source = CreateSource(2000);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var controller = table.ProgressiveController!;

        // Build up a substantially scrolled position the same way
        // Reload_WhenScrolledDeepAgainstUnknownCountSource_DoesNotThrowAndRecoversAsync does: each
        // step scrolls to the current legitimate maximum (VerticalOffset's setter rejects anything
        // past it) and lets the resulting fetch cascade settle.
        for (var step = 0; step < 5; step++)
        {
            var maximumOffset = Math.Max(0, table.Extent.Height - table.Viewport.Height);
            await surface.UpdateAsync(() => table.VerticalOffset = maximumOffset, $"scroll to current maximum (step {step})");
        }

        table.VerticalOffset.ShouldBeGreaterThan(0);

        // Independently recompute the expected window using the corrected, gap-inclusive stride -
        // deliberately not by calling into any production code - so this pins the window Rewindow
        // must resolve to, not merely whatever it currently happens to produce.
        var stride = 1 + table.RowSpacing;
        var viewportHeight = table.Viewport.Height;
        var verticalOffset = table.VerticalOffset;
        var logicalCount = controller.LogicalCount;

        var viewportRows = Math.Max(1, viewportHeight / stride);
        var expectedFirst = Math.Max(0, (verticalOffset / stride) - viewportRows);
        var expectedLast = Math.Min(
            logicalCount - 1,
            ((verticalOffset + Math.Max(0, viewportHeight - 1)) / stride) + viewportRows);

        controller.WindowStart.ShouldBe(expectedFirst);
        controller.WindowCount.ShouldBe(expectedLast - expectedFirst + 1);
    }

    #endregion

    #region Generation and cancellation

    /// <summary>Verifies a fetch that completes successfully after Reload() bumped the generation is
    /// discarded without mutating the cache or disturbing the fresh, still-pending request Reload()
    /// itself issued for the same range - regression coverage for OnFetchSucceeded's guard/removal
    /// restructuring (adversarial review flagged the pre-fix version for folding _pending removal
    /// into the same guard that rejects a stale generation, unlike OnFetchFailed's unconditional
    /// removal-before-check; BumpGeneration's own unconditional _pending.Clear() means this exact
    /// scenario was not actually able to strand the stale range's own slot, but the restructuring
    /// still removes a latent fragility - this pins the resulting behavior exactly).</summary>
    [Fact]
    public async Task OnFetchSucceeded_WhenGenerationIsStaleDespiteIgnoredCancellation_NeverMutatesCacheOrDisturbsFreshPendingAsync()
    {
        var table = CreateHost();
        var source = CreateSource(50);
        source.Gate();
        source.HonorCancellation = false;
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var staleRequest = source.Requests.ShouldHaveSingleItem();
        var controller = table.ProgressiveController!;

        // Reload() bumps the generation and cancels _lifetime, but the misbehaving source never
        // observes that cancellation (HonorCancellation is false) - so the original held request
        // stays outstanding, and Reload() immediately reissues a fresh request for the same
        // (still-visible) window on top of it. Both now share the same start index, held
        // independently in issue order, so releasing by that index resolves the stale one first.
        await surface.UpdateAsync(table.Reload, "reload before the held fetch resolves");
        controller.PendingCount.ShouldBe(1);
        source.HeldCount.ShouldBe(2);

        await surface.UpdateAsync(() => source.Release(staleRequest.StartIndex), "resolve the stale fetch");

        // A single settle pass is not guaranteed to observe the controller's own dispatcher-marshaled
        // reaction to a fetch released from inside that same UpdateAsync action, so this polls a
        // couple of extra idle-drain passes (each a harmless no-op once truly settled) instead of
        // assuming one pass always suffices.
        for (var attempt = 0; attempt < 5 && source.HeldCount != 1; attempt++)
        {
            await surface.UpdateAsync(static () => { }, $"settle attempt {attempt}");
        }

        // The stale completion must never have mutated the cache...
        controller.IsPlaceholder(0).ShouldBeTrue();

        // ...and the fresh, generation-matching request Reload() issued for the same range must be
        // completely undisturbed: still exactly one pending range, still held, never double-removed
        // or otherwise corrupted by the stale completion's own guard/removal handling.
        controller.PendingCount.ShouldBe(1);
        source.HeldCount.ShouldBe(1);
    }

    /// <summary>Verifies canceling an in-flight fetch removes it from pending without side effects.</summary>
    [Fact]
    public async Task Rewindow_WhenPendingRangeScrollsOutOfView_CancelsWithoutSideEffectsAsync()
    {
        var table = CreateHost();
        var source = CreateSource(500);
        source.Gate();
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        source.HeldCount.ShouldBe(1);

        // Jump far away - the original pending range is now entirely out of the reconciled window
        // and must be canceled, not merely superseded.
        await surface.UpdateAsync(() => table.VerticalOffset = 400, "scroll far away");

        table.LoadState.ShouldBe(TableLoadState.Loading);
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeTrue();
    }

    #endregion

    #region Extent and phantom rows

    /// <summary>Verifies the elastic unknown-count extent exposes exactly one phantom row past the
    /// confirmed frontier while the source has more rows than the viewport window can request, and
    /// collapses to the exact count once the window's own requests exhaust a smaller source.</summary>
    /// <remarks>
    /// With the total count unknown, the controller only ever discovers one new index per
    /// successful fetch (the window is clamped to <c>LogicalCount - 1</c>, which only grows once
    /// the frontier does) - an ungated source lets this cascade settle on its own, deterministically,
    /// at whichever fixed point the viewport size and source length produce, instead of depending on
    /// manually releasing one gated fetch at a time.
    /// </remarks>
    [Fact]
    public async Task LogicalCount_WhenCountIsUnknown_ExposesPhantomRowUntilEndOfDataAsync()
    {
        // A viewport of height 10 caps the window at logical index 19 regardless of how far
        // LogicalCount would otherwise grow; with far more rows available than that, the cascade
        // settles once it has confirmed exactly indices 0..19 (frontier 19), still short of the
        // source's own end, so LogicalCount keeps exposing one phantom row past it (19 + 2 = 21).
        var phantomTable = CreateHost();
        var phantomSource = new FakeTableDataSource<Item>(
            Enumerable.Range(0, 100).Select(static id => new Item(id, $"Row{id}")),
            static item => item.Id);
        await using var phantomSurface = await ComponentSurface.MountAsync(
            phantomTable, new Size(20, 10), TestContext.Current.CancellationToken);
        await phantomSurface.UpdateAsync(() => phantomTable.SetDataSource(phantomSource, BuildRow, Length.Cells(1)), "bind source");
        var phantomController = phantomTable.ProgressiveController!;

        phantomController.LogicalCount.ShouldBe(21);
        phantomController.IsEndOfData.ShouldBeFalse();

        // A source shorter than the viewport window exhausts itself: the request just past its
        // last real item resolves with zero items and IsEndOfData - collapsing the extent to the
        // exact confirmed count, with no phantom row left.
        var exactTable = CreateHost();
        var exactSource = new FakeTableDataSource<Item>(
            [new Item(0, "Row0"), new Item(1, "Row1"), new Item(2, "Row2")],
            static item => item.Id);
        await using var exactSurface = await ComponentSurface.MountAsync(
            exactTable, new Size(20, 10), TestContext.Current.CancellationToken);
        await exactSurface.UpdateAsync(() => exactTable.SetDataSource(exactSource, BuildRow, Length.Cells(1)), "bind source");
        var exactController = exactTable.ProgressiveController!;

        exactController.LogicalCount.ShouldBe(3);
        exactController.IsEndOfData.ShouldBeTrue();
    }

    /// <summary>Verifies a known total count is reported directly, without a phantom row.</summary>
    [Fact]
    public async Task LogicalCount_WhenCountIsKnown_ReportsExactCountAsync()
    {
        var table = CreateHost();
        var source = CreateSource(500, total: 500);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        table.ProgressiveController!.LogicalCount.ShouldBe(500);
    }

    #endregion

    #region Reload safety

    /// <summary>Verifies Reload() tolerates a table scrolled deep into an unknown-count progressive
    /// source instead of crashing on a negative-size window allocation (regression:
    /// <c>TableDataController.Rewindow</c> computed its first/last window bounds against
    /// <c>LogicalCount</c> with no first &lt;= last guard, so <c>RealizeWindow</c>'s
    /// <c>new TableRow?[last - first + 1]</c> could allocate a negative length once Reload()
    /// collapsed <c>LogicalCount</c> back down to a bare phantom row while <c>VerticalOffset</c>
    /// was still large - synchronously, inside the same call, before any layout pass got a chance
    /// to re-clamp the offset against the shrunk extent).</summary>
    [Fact]
    public async Task Reload_WhenScrolledDeepAgainstUnknownCountSource_DoesNotThrowAndRecoversAsync()
    {
        var table = CreateHost();
        var source = new FakeTableDataSource<Item>(
            Enumerable.Range(0, 5000).Select(static id => new Item(id, $"Row{id}")),
            static item => item.Id);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var controller = table.ProgressiveController!;

        // Each step scrolls to the current legitimate maximum offset - VerticalOffset's setter
        // rejects anything past it - and lets the resulting fetch cascade settle, which extends
        // the confirmed frontier (and so the extent) just enough to make a larger maximum
        // reachable next time. A handful of these legitimate increments builds up a substantially
        // scrolled position while the source's total count is still unknown, without ever
        // force-setting an out-of-range value the setter would reject outright.
        for (var step = 0; step < 5; step++)
        {
            var maximumOffset = Math.Max(0, table.Extent.Height - table.Viewport.Height);
            await surface.UpdateAsync(() => table.VerticalOffset = maximumOffset, $"scroll to current maximum (step {step})");
        }

        var offsetBeforeReload = table.VerticalOffset;
        offsetBeforeReload.ShouldBeGreaterThan(0);

        // Reload() resets _knownFrontier and IsEndOfData, collapsing LogicalCount down to a bare
        // phantom row (1) synchronously, inside the same call that immediately rewindows against
        // the still-large, now-stale VerticalOffset - previously an unguarded negative-size array
        // allocation in RealizeWindow.
        await surface.UpdateAsync(table.Reload, "reload while scrolled deep");

        // The stale offset must have been reclamped down against the freshly-collapsed extent
        // (not left dangling past an allocation-triggering window), and loading must have resumed
        // normally from a legitimate window instead of getting stuck.
        table.VerticalOffset.ShouldBeLessThan(offsetBeforeReload);
        controller.LogicalCount.ShouldBeGreaterThan(1);
        controller.IsPlaceholder(0).ShouldBeFalse();
    }

    #endregion

    #region Malformed results

    /// <summary>Verifies a result reporting more items than requested retries then fails, without
    /// corrupting already-committed rows.</summary>
    [Fact]
    public async Task LoadAsync_WhenResultReportsMoreItemsThanRequested_RetriesThenFailsAsync()
    {
        await AssertsMalformedResultFailsAfterRetriesAsync(static request =>
            new TableDataResult<int> { Items = Enumerable.Range(0, request.Count + 1).ToArray(), IsEndOfData = false });
    }

    /// <summary>Verifies a result reporting fewer items than requested without IsEndOfData retries
    /// then fails, without corrupting already-committed rows.</summary>
    [Fact]
    public async Task LoadAsync_WhenResultUnderCountsWithoutEndOfData_RetriesThenFailsAsync()
    {
        await AssertsMalformedResultFailsAfterRetriesAsync(static request =>
            new TableDataResult<int> { Items = Enumerable.Range(0, Math.Max(0, request.Count - 1)).ToArray(), IsEndOfData = false });
    }

    /// <summary>Verifies an empty result without IsEndOfData retries then fails, without corrupting
    /// already-committed rows.</summary>
    [Fact]
    public async Task LoadAsync_WhenResultIsEmptyWithoutEndOfData_RetriesThenFailsAsync()
    {
        await AssertsMalformedResultFailsAfterRetriesAsync(static _ =>
            new TableDataResult<int> { Items = [], IsEndOfData = false });
    }

    /// <summary>Verifies a result containing a duplicate key within one batch retries then fails.</summary>
    [Fact]
    public async Task LoadAsync_WhenResultHasDuplicateKeys_RetriesThenFailsAsync()
    {
        await AssertsMalformedResultFailsAfterRetriesAsync(static request =>
            new TableDataResult<int> { Items = Enumerable.Repeat(0, request.Count).ToArray(), IsEndOfData = false });
    }

    /// <summary>Verifies a result whose key collides with a different already-cached index retries
    /// then fails.</summary>
    [Fact]
    public async Task LoadAsync_WhenKeyCollidesWithADifferentCachedIndexAsync()
    {
        var clock = new ManualTimeProvider();
        var table = CreateHost();
        var malformed = false;

        // Normally each request returns exactly the items it was asked for, keyed by their own
        // index. Once armed, any request past index 0 instead returns key 0 - already cached at
        // index 0 - to provoke the collision check.
        var source = new BrokenIntSource(
            request => malformed && request.StartIndex > 0
                ? new TableDataResult<int> { Items = [0, .. Enumerable.Range(request.StartIndex + 1, request.Count - 1)], IsEndOfData = false }
                : new TableDataResult<int> { Items = Enumerable.Range(request.StartIndex, request.Count).ToArray(), IsEndOfData = false },
            count: 500);

        // A tall viewport keeps index 0 - the collision target - well inside the eviction radius
        // even after scrolling forward, so the malformed response has something to collide with
        // instead of colliding with an already-evicted key.
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 30), clock, TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BrokenRow, Length.Cells(1)), "bind source");
        var loadFailedCount = 0;
        table.LoadFailed += (_, _) => loadFailedCount++;
        malformed = true;

        await surface.UpdateAsync(() => table.VerticalOffset = 50, "scroll to a colliding range");
        await AdvanceUntilLoadStateAsync(surface, table, TableLoadState.Failed);

        loadFailedCount.ShouldBe(1);
        table.LoadState.ShouldBe(TableLoadState.Failed);

        // Index 0's own committed row - the collision target, never itself part of the failed
        // request - must survive untouched, proving the failure never corrupted already-committed
        // cache state.
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeFalse();
    }

    private static async Task AssertsMalformedResultFailsAfterRetriesAsync(Func<TableDataRequest, TableDataResult<int>> respond)
    {
        var clock = new ManualTimeProvider();
        var table = CreateHost();

        // A known, moderately sized count keeps the very first request's Count above 1 - with an
        // unknown count the initial window would request exactly one item (LogicalCount starts at
        // the bare phantom row), and a single-item batch can never actually contain a within-batch
        // duplicate key, silently defeating that one malformed scenario.
        var source = new BrokenIntSource(respond, count: 500);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), clock, TestContext.Current.CancellationToken);
        var loadFailedCount = 0;
        table.LoadFailed += (_, _) => loadFailedCount++;

        await surface.UpdateAsync(() => table.SetDataSource(source, BrokenRow, Length.Cells(1)), "bind source");

        await AdvanceUntilLoadStateAsync(surface, table, TableLoadState.Failed);

        loadFailedCount.ShouldBe(1);
        table.LoadState.ShouldBe(TableLoadState.Failed);
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeTrue();
    }

    private static TableRow BrokenRow(int id) => new([new ControlText(id.ToString(CultureInfo.InvariantCulture))]);

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

    /// <summary>A minimal int-keyed <see cref="ITableDataSource{T}"/> whose response is fully
    /// controlled per-request, for exercising malformed-result validation deterministically.</summary>
    private sealed class BrokenIntSource(Func<TableDataRequest, TableDataResult<int>> respond, int? count = null): ITableDataSource<int>
    {
        public int? Count => count;

        // Never raised: nothing in this fixture needs to signal staleness.
        public event EventHandler? Changed { add { } remove { } }

        public object GetKey(int item) => item;

        public ValueTask<TableDataResult<int>> LoadAsync(TableDataRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(respond(request));
    }

    #endregion

    #region Cache eviction

    /// <summary>Verifies eviction bounds both the row cache and the error-index set (regression for
    /// the previously unbounded _errorIndices).</summary>
    [Fact]
    public async Task EvictCache_WhenFailedRangeScrollsFarOutOfView_PrunesErrorIndicesAsync()
    {
        var clock = new ManualTimeProvider();
        var table = CreateHost();

        // Index 0 always fails; every other index resolves normally, so the table has somewhere to
        // scroll to once the top range is exhausted and marked failed.
        var source = new BrokenIntSource(
            request => request.StartIndex == 0
                ? new TableDataResult<int> { Items = [], IsEndOfData = false }
                : new TableDataResult<int> { Items = Enumerable.Range(request.StartIndex, request.Count).ToArray(), IsEndOfData = false },
            count: 500);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 3), clock, TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BrokenRow, Length.Cells(1)), "bind source");
        await AdvanceUntilLoadStateAsync(surface, table, TableLoadState.Failed);
        table.LoadState.ShouldBe(TableLoadState.Failed);

        await surface.UpdateAsync(() => table.VerticalOffset = 400, "scroll far away from the failed range");

        // The failed range at the top is now far outside the eviction radius; LoadState must
        // recompute from a fresh (non-failing) region and settle away from Failed.
        table.LoadState.ShouldNotBe(TableLoadState.Failed);
    }

    #endregion

    #region Selection

    /// <summary>Verifies selecting an unloaded index clears the previous key, avoids selecting an
    /// unrelated cached fallback, and resolves the complementary key when that index loads.</summary>
    [Fact]
    public async Task SelectIndex_WhenTargetIsUnloaded_ClearsKeyThenResolvesAfterLoadAsync()
    {
        var table = CreateHost();
        var source = CreateSource(500);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.UpdateAsync(() => table.SelectIndex(0), "select loaded row");
        List<(int Index, object? Key)> observations = [];
        table.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(Table.ActiveIndex) or nameof(Table.ActiveKey))
            {
                observations.Add((table.ActiveIndex, table.ActiveKey));
            }
        };

        await surface.UpdateAsync(
            () =>
            {
                table.SelectIndex(400);
                table.SelectAll();
            },
            "select unloaded row without cached fallback");

        table.ActiveIndex.ShouldBe(400);
        table.ActiveKey.ShouldBeNull();
        table.SelectedKeys.ShouldBeEmpty();
        observations.ShouldNotBeEmpty();
        observations.ShouldAllBe(observation => observation.Index == 400 && observation.Key == null);

        observations.Clear();
        await surface.UpdateAsync(() => table.VerticalOffset = 400, "load active row");

        table.ProgressiveController!.IsPlaceholder(400).ShouldBeFalse();
        table.ActiveIndex.ShouldBe(400);
        table.ActiveKey.ShouldBe(400);
        observations.ShouldNotBeEmpty();
        observations.ShouldAllBe(observation => observation.Index == 400 && Equals(observation.Key, 400));

        await surface.UpdateAsync(() => table.VerticalOffset = 0, "evict active row");
        table.ActiveIndex.ShouldBe(400);
        table.ActiveKey.ShouldBe(400);
    }

    /// <summary>Verifies selecting an unresolved key clears the previous index and resolves the
    /// complementary index when that key's row loads.</summary>
    [Fact]
    public async Task SelectKey_WhenTargetIsUnloaded_ClearsIndexThenResolvesAfterLoadAsync()
    {
        var table = CreateHost();
        var source = CreateSource(500);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.UpdateAsync(() => table.SelectIndex(0), "select loaded row");

        await surface.UpdateAsync(
            () =>
            {
                table.SelectKey(400);
                table.SelectAll();
            },
            "select unresolved key without cached fallback");

        table.ActiveIndex.ShouldBe(-1);
        table.ActiveKey.ShouldBe(400);
        table.SelectedKeys.ShouldBeEmpty();

        await surface.UpdateAsync(() => table.VerticalOffset = 400, "load active key");

        table.ProgressiveController!.IsPlaceholder(400).ShouldBeFalse();
        table.ActiveIndex.ShouldBe(400);
        table.ActiveKey.ShouldBe(400);
    }

    /// <summary>Verifies a stable key remains selected after its cached entry is evicted and later
    /// reloaded, since selection tracks keys independently of the cache.</summary>
    [Fact]
    public async Task SelectKey_WhenSelectedEntryIsEvictedThenReturns_StaysSelectedAsync()
    {
        var table = CreateHost();
        var source = CreateSource(500);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.UpdateAsync(() => table.SelectIndex(0), "select the first row");
        table.SelectedKeys.ShouldContain(0);

        await surface.UpdateAsync(() => table.VerticalOffset = 450, "scroll far enough to evict index 0");
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeTrue();
        table.SelectedKeys.ShouldContain(0);

        await surface.UpdateAsync(() => table.VerticalOffset = 0, "scroll back");

        table.SelectedKeys.ShouldContain(0);
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeFalse();
    }

    /// <summary>Verifies every actual progressive selection mutation raises SelectionChanged and the
    /// ActiveIndex/ActiveKey/SelectedKeys property notifications (regression for defect a).</summary>
    [Fact]
    public async Task SelectIndex_WhenIndexResolvesToACachedKey_RaisesSelectionChangedAndPropertyNotificationsAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var selectionChangedCount = 0;
        List<string?> propertyNotifications = [];
        table.SelectionChanged += (_, _) => selectionChangedCount++;
        table.PropertyChanged += (_, eventArgs) => propertyNotifications.Add(eventArgs.PropertyName);

        await surface.UpdateAsync(() => table.SelectIndex(2), "select index 2");

        selectionChangedCount.ShouldBe(1);
        propertyNotifications.ShouldContain(nameof(Table.ActiveIndex));
        propertyNotifications.ShouldContain(nameof(Table.ActiveKey));
        propertyNotifications.ShouldContain(nameof(Table.SelectedKeys));
        table.ActiveIndex.ShouldBe(2);
        table.ActiveKey.ShouldBe(2);
        table.SelectedKeys.ShouldBe([2]);

        propertyNotifications.Clear();
        selectionChangedCount = 0;

        // Re-selecting the exact same index is not an actual change and must not re-raise.
        await surface.UpdateAsync(() => table.SelectIndex(2), "re-select the same index");

        selectionChangedCount.ShouldBe(0);
        propertyNotifications.ShouldNotContain(nameof(Table.SelectedKeys));
    }

    /// <summary>Verifies SelectAll respects SelectionMode instead of always selecting every cached
    /// key (regression for defect d).</summary>
    [Theory]
    [InlineData(TableSelectionMode.None)]
    [InlineData(TableSelectionMode.Row)]
    [InlineData(TableSelectionMode.MultipleRows)]
    public async Task SelectAll_WhenProgressive_RespectsSelectionModeAsync(TableSelectionMode mode)
    {
        var table = CreateHost(TableSelectionMode.MultipleRows);
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.UpdateAsync(() => table.SelectionMode = mode, "set selection mode");

        await surface.UpdateAsync(table.SelectAll, "select all");

        switch (mode)
        {
            case TableSelectionMode.None:
                table.SelectedKeys.ShouldBeEmpty();
                break;
            case TableSelectionMode.Row:
                table.SelectedKeys.Count.ShouldBe(1);
                break;
            case TableSelectionMode.MultipleRows:
                table.SelectedKeys.Count.ShouldBeGreaterThan(1);
                break;
            case TableSelectionMode.Cell:
            case TableSelectionMode.MultipleCells:
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Verifies ClearSelection actually clears progressive selection (regression for defect c).</summary>
    [Fact]
    public async Task ClearSelection_WhenProgressive_ClearsSelectedKeysAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.UpdateAsync(() => table.SelectIndex(0), "select the first row");
        table.SelectedKeys.ShouldNotBeEmpty();

        await surface.UpdateAsync(table.ClearSelection, "clear selection");

        table.SelectedKeys.ShouldBeEmpty();
    }

    /// <summary>Verifies the neutral-default getters instead of throwing while not progressive.</summary>
    [Fact]
    public void ActiveIndex_WhenNotProgressive_ReportsNeutralDefaults()
    {
        var table = new Table();

        table.ActiveIndex.ShouldBe(-1);
        table.ActiveKey.ShouldBeNull();
        table.SelectedKeys.ShouldBeEmpty();
    }

    #endregion

    #region IsEditing and lifecycle

    /// <summary>Verifies BeginEdit reports false while progressive instead of throwing.</summary>
    [Fact]
    public async Task BeginEdit_WhenProgressive_ReturnsFalseAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var row = table.ProgressiveController!.RowAt(0)!;

        var result = await surface.Application.Dispatcher.InvokeAsync(
            () => table.BeginEdit(row, 0), TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    /// <summary>Verifies leaving the tree cancels in-flight fetches but retains cache and selection,
    /// while disposal tears the controller down fully.</summary>
    [Fact]
    public async Task Detach_WhenTableLeavesTree_CancelsFetchesButRetainsCacheAndSelectionAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        var host = new Overlay { IsFocusable = true };
        host.Children.Add(table);
        await using var surface = await ComponentSurface.MountAsync(
            host, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        await surface.UpdateAsync(() => table.SelectIndex(0), "select the first row");

        await surface.UpdateAsync(() => host.Children.Remove(table), "detach the table");

        table.IsProgressive.ShouldBeTrue();
        table.SelectedKeys.ShouldContain(0);
        table.ProgressiveController!.IsPlaceholder(0).ShouldBeFalse();
    }

    /// <summary>Verifies disposal cancels progressive work without publishing a live load-state
    /// transition to subscribers whose surrounding surface may already be disposed.</summary>
    [Fact]
    public async Task Dispose_WhenProgressiveFetchIsInFlight_DoesNotPublishLoadStateChangedAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        source.Gate();
        var host = new Overlay { IsFocusable = true };
        host.Children.Add(table);
        await using var surface = await ComponentSurface.MountAsync(
            host, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var loadStateChanges = 0;
        table.LoadStateChanged += (_, _) => loadStateChanges++;
        source.HeldCount.ShouldBe(1);

        await surface.UpdateAsync(table.Dispose, "dispose progressive table mid-fetch");

        loadStateChanges.ShouldBe(0);
        table.IsDisposed.ShouldBeTrue();
        source.HeldCount.ShouldBe(0);
    }

    /// <summary>Verifies disposing a detached progressive table with realized rows leaves owned
    /// presenter-cell teardown to the table's active disposal transaction.</summary>
    [Fact]
    public async Task Dispose_WhenProgressiveRowsAreRealized_DoesNotReenterOwnedControlMutationAsync()
    {
        var table = CreateHost();
        var source = CreateSource(20);
        var host = new Overlay { IsFocusable = true };
        host.Children.Add(table);
        await using var surface = await ComponentSurface.MountAsync(
            host, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var realizedCell = table.ProgressiveController!.RowAt(0)!.Cells[0];

        await surface.UpdateAsync(
            () =>
            {
                _ = host.Children.Remove(table);
                table.Dispose();
            },
            "detach and dispose the progressive table");

        table.IsDisposed.ShouldBeTrue();
        realizedCell.IsDisposed.ShouldBeTrue();
    }

    #endregion

    #region Eager-only members while progressive

    /// <summary>Verifies SelectRow rejects use while progressive, directing callers to the
    /// key-based selection API instead of falling through to owned-row validation.</summary>
    [Fact]
    public async Task SelectRow_WhenProgressive_ThrowsInvalidOperationExceptionAsync()
    {
        var table = CreateHost();
        var source = CreateSource(5);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var detached = new TableRow([new ControlText("detached")]);

        var exception = await surface.Application.Dispatcher.InvokeAsync(
            () => Should.Throw<InvalidOperationException>(() => table.SelectRow(detached)),
            TestContext.Current.CancellationToken);

        exception.Message.ShouldContain("SelectRow is unavailable");
        table.ActiveIndex.ShouldBe(-1);
    }

    /// <summary>Verifies SelectCell rejects use while progressive, directing callers to the
    /// key-based selection API.</summary>
    [Fact]
    public async Task SelectCell_WhenProgressive_ThrowsInvalidOperationExceptionAsync()
    {
        var table = CreateHost();
        var source = CreateSource(5);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");
        var detached = new TableRow([new ControlText("detached")]);

        var exception = await surface.Application.Dispatcher.InvokeAsync(
            () => Should.Throw<InvalidOperationException>(() => table.SelectCell(detached, 0)),
            TestContext.Current.CancellationToken);

        exception.Message.ShouldContain("SelectCell is unavailable");
        table.ActiveIndex.ShouldBe(-1);
    }

    /// <summary>Verifies SetSort (and, transitively, SortBy) rejects use while progressive,
    /// since the data source - not Table - owns sort order in that mode.</summary>
    [Fact]
    public async Task SetSort_WhenProgressive_ThrowsInvalidOperationExceptionAsync()
    {
        var table = CreateHost();
        var source = CreateSource(5);
        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind source");

        var exception = await surface.Application.Dispatcher.InvokeAsync(
            () => Should.Throw<InvalidOperationException>(
                () => table.SetSort(0, TableSortDirection.Ascending)),
            TestContext.Current.CancellationToken);

        exception.Message.ShouldContain("Sorting is unavailable");
        table.SortColumnIndex.ShouldBe(-1);

        var sortByException = await surface.Application.Dispatcher.InvokeAsync(
            () => Should.Throw<InvalidOperationException>(() => table.SortBy(0)),
            TestContext.Current.CancellationToken);

        sortByException.Message.ShouldContain("Sorting is unavailable");
    }

    #endregion

    #region Progressive horizontal scrolling

    private sealed record TwoColumnItem(int Id, string First, string Second);

    private static TableRow BuildTwoColumnRow(TwoColumnItem item) =>
        new([new ControlText(item.First), new ControlText(item.Second)]);

    /// <summary>Verifies TryResolvePoint resolves the offset-shifted column once a progressive
    /// table is scrolled horizontally - regression for TryResolvePoint searching columns from the
    /// un-shifted ProgressiveOrigin.X alone, unlike its own Y baseline, which already subtracts
    /// VerticalOffset the same way. Before the fix, this same screen point still resolved against
    /// the un-shifted origin and landed on column 0 instead.</summary>
    [Fact]
    public async Task TryResolvePoint_WhenHorizontallyScrolled_ResolvesTheShiftedColumnAsync()
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

        var controller = table.ProgressiveController!;

        // Column 2 sits at the un-shifted x range [8, 16); after scrolling 3 cells right it starts
        // at screen x=5.
        controller.TryResolvePoint(new Point(5, 0), out var logicalIndex, out var columnIndex).ShouldBeTrue();
        logicalIndex.ShouldBe(0);
        columnIndex.ShouldBe(1);
    }

    #endregion
}
