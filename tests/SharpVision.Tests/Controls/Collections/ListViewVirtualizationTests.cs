// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies windowed (RowHeight-driven) ListView realization through mounted surfaces:
/// layout convergence inside scrolling ancestors, relative row geometry, offset remapping,
/// keyboard and pointer reach across unrealized rows, and resize behavior.</summary>
public sealed class ListViewVirtualizationTests
{
    /// <summary>Verifies a percentage-row ListView with percentage height and Min/Max clamps
    /// settles when it is one of many children of an auto-scrolling Stack. The scrolling Stack
    /// measures the list unbounded (so its measure-time height is only its MaxHeight) but
    /// arranges it against the Stack viewport, so a row height resolved from the measure
    /// constraint disagrees with the one resolved from the arranged viewport; each disagreement
    /// used to invalidate the host from inside layout and the tree never reached idle.</summary>
    /// <param name="surfaceHeight">The terminal height. The header Dock takes four rows and the
    /// body's padding two more, so the list arranges at 40% of the remaining rows, clamped to
    /// 4..12.</param>
    /// <param name="expectedHeight">The exact arranged list height for that surface. The 40-row
    /// case is the agreeing control: its 40% share (14) clamps to the same MaxHeight the
    /// unbounded measure already produced, so measure and arrange agree there.</param>
    [Theory]
    [InlineData(30, 10)]
    [InlineData(24, 7)]
    [InlineData(20, 6)]
    [InlineData(40, 12)]
    public async Task Layout_WhenHostedInAutoScrollingStackWithRelativeRows_SettlesAsync(int surfaceHeight, int expectedHeight)
    {
        // Arrange
        var list = CreateShowcaseStyleList();
        var root = CreateShowcaseStyleHost(list);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(94, surfaceHeight),
            TestContext.Current.CancellationToken);

        // Assert the arranged geometry is self-consistent and stable.
        surface.Application.Failure.ShouldBeNull();
        var frames = 0;
        surface.Application.FrameRendered += (_, _) => Interlocked.Increment(ref frames);
        await surface.UpdateAsync(static () => { }, "settled no-op");
        frames.ShouldBe(0);

        var expectedRowHeight = Math.Max(1, (int) Math.Round(expectedHeight * 0.25, MidpointRounding.AwayFromZero));
        list.DesiredSize.Height.ShouldBe(12, "the scrolling Stack measures the list unbounded, so only MaxHeight bounds it");
        list.Bounds.Height.ShouldBe(expectedHeight);
        list.Viewport.Height.ShouldBe(expectedHeight);
        var realized = OwnedTree.FindAll<ListItem>(list);
        realized.Count.ShouldBeGreaterThan(0);
        realized.Count.ShouldBeLessThanOrEqualTo(3 * expectedHeight);
        realized.ShouldAllBe(item => item.Bounds.Height == expectedRowHeight);
        list.Extent.Height.ShouldBe(20_000 * expectedRowHeight);
        list.Bounds.Y.ShouldBeGreaterThan(surfaceHeight, "the list sits below the body viewport until the user scrolls to it");
    }

    /// <summary>Verifies the minimal shape of the same hazard without any scrolling ancestor: a
    /// plain Stack measures the list unbounded (its measure-time height is its MaxHeight, 12,
    /// so measure resolves a 3-cell row) but arranges it at 40% of a 10-row surface (4 cells,
    /// so arrange resolves a 1-cell row). The final row height must follow the arranged
    /// viewport and the disagreement must not keep the tree from reaching idle.</summary>
    [Fact]
    public async Task Layout_WhenMeasuredTallerThanArranged_ResolvesRowsFromArrangedViewportAndSettlesAsync()
    {
        // Arrange
        var list = CreateShowcaseStyleList();
        var stack = new Stack { Children = { new ControlText("Above"), list } };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(30, 10),
            TestContext.Current.CancellationToken);

        // Assert
        var frames = 0;
        surface.Application.FrameRendered += (_, _) => Interlocked.Increment(ref frames);
        await surface.UpdateAsync(static () => { }, "settled no-op");
        frames.ShouldBe(0);
        list.DesiredSize.Height.ShouldBe(12);
        list.Bounds.Height.ShouldBe(4);
        list.Viewport.Height.ShouldBe(4);
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.Bounds.Height == 1);
        list.Extent.Height.ShouldBe(20_000);
        RowText(surface, 0, 9).ShouldBe("Above    ");
        RowText(surface, 1, 9).ShouldBe("Row 00000");
        RowText(surface, 4, 9).ShouldBe("Row 00003");
        RowText(surface, 5, 9).ShouldBe("         ");
    }

    private static string RowText(ComponentSurface surface, int y, int width)
    {
        var text = new StringBuilder();

        for (var x = 0; x < width; x++)
        {
            _ = text.Append(surface.Cell(new Point(x, y)).Text);
        }

        return text.ToString();
    }

    /// <summary>Verifies End, Home, PageDown, and Ctrl+A reach across twenty thousand rows through
    /// pure arithmetic: only a bounded window is ever realized, the offset lands exactly, and the
    /// rendered bottom row is the last logical item.</summary>
    [Fact]
    public async Task Keyboard_WhenEndHomePageAndSelectAllCrossUnrealizedRows_RealizeOnlyTheTargetWindowAsync()
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = Length.Cells(1),
            SelectionMode = ListSelectionMode.Multiple,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            Items = Enumerable.Range(0, 20_000).Select(value => (object?) $"Row {value:D5}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(list, new Size(20, 10), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act End
        await surface.Keyboard.PressAsync(Code.End);

        // Assert
        list.SelectedIndex.ShouldBe(19_999);
        list.ActiveIndex.ShouldBe(19_999);
        list.VerticalOffset.ShouldBe(19_990);
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBeLessThan(100);
        OwnedTree.FindAll<ListItem>(list).ShouldContain(item => item.Index == 19_999);
        RowText(surface, 9, 9).ShouldBe("Row 19999");

        // Act Home then PageDown
        await surface.Keyboard.PressAsync(Code.Home);
        list.ActiveIndex.ShouldBe(0);
        list.VerticalOffset.ShouldBe(0);
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert one viewport of rows
        list.ActiveIndex.ShouldBe(10);
        list.SelectedIndex.ShouldBe(10);
        list.VerticalOffset.ShouldBe(1);
        RowText(surface, 9, 9).ShouldBe("Row 00010");

        // Act select all
        await surface.SendAsync(Encoding.ASCII.GetBytes($"{(char) 27}[97;5u"), "press Ctrl+A");

        // Assert every logical index without realizing it
        list.SelectedItems.Count.ShouldBe(20_000);
        list.SelectedIndex.ShouldBe(0);
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBeLessThan(100);
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.IsSelected);
    }

    /// <summary>Verifies a list whose vertical axis cannot scroll (ScrollBars.None) commits
    /// selection, the active row, and Enter activation as pure index state identically in
    /// windowed and eager mode: End, PageDown, Enter, Space, and a programmatic off-window
    /// selection all reach rows the viewport can never show, without realizing them and without
    /// any dispatcher failure. A key handler exception is surfaced as a named assertion here
    /// rather than as the harness's settle timeout.</summary>
    /// <param name="windowed">Whether RowHeight enables windowed realization.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Keyboard_WhenAxisCannotScroll_CommitsIndexStateIdenticallyInBothModesAsync(bool windowed)
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = windowed ? Length.Cells(1) : Length.Auto,
            ScrollBars = ScrollBars.None,
            SelectionMode = ListSelectionMode.Multiple,
            Items = Enumerable.Range(0, 300).Select(value => (object?) $"Row {value:D3}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var invoked = new List<int>();
        var selections = new List<string>();
        list.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);
        list.SelectionChanged += (_, eventArgs) =>
            selections.Add($"{string.Join(',', eventArgs.AddedIndexes.ToArray())}:{string.Join(',', eventArgs.RemovedIndexes.ToArray())}");
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 4), TestContext.Current.CancellationToken);
        var failures = new List<Exception>();
        surface.Application.UnhandledException += (_, eventArgs) => failures.Add(eventArgs.Exception);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act End on a row the viewport can never show
        await surface.Keyboard.PressAsync(Code.End);

        // Assert the index state committed without a realized row or a scroll
        failures.ShouldBeEmpty();
        list.ActiveIndex.ShouldBe(299);
        list.SelectedIndex.ShouldBe(299);
        selections.ShouldBe(["299:"]);
        list.VerticalOffset.ShouldBe(0);
        RowText(surface, 0, 7).ShouldBe("Row 000");
        RowText(surface, 3, 7).ShouldBe("Row 003");
        surface.ShouldHaveFocus(list);

        if (windowed)
        {
            OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.Index < 100);
        }

        // Act Enter invokes the off-window active row and Space toggles it out of the selection
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.TypeAsync(" ");

        // Assert
        failures.ShouldBeEmpty();
        invoked.ShouldBe([299]);
        selections.ShouldBe(["299:", ":299"]);
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(299);

        // Act navigation back into and past the window
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert the second page (four rows each) lands beyond the visible rows and still invokes
        failures.ShouldBeEmpty();
        list.ActiveIndex.ShouldBe(9);
        list.SelectedIndex.ShouldBe(9);
        invoked.ShouldBe([299, 9]);
        list.VerticalOffset.ShouldBe(0);
        RowText(surface, 0, 7).ShouldBe("Row 000");

        // Act a programmatic off-window selection followed by Enter
        await surface.UpdateAsync(() => list.SelectedIndex = 200, "select an off-window row");
        list.ActiveIndex.ShouldBe(200);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        failures.ShouldBeEmpty();
        invoked.ShouldBe([299, 9, 200]);
        list.SelectedIndex.ShouldBe(200);
        list.VerticalOffset.ShouldBe(0);
        surface.Application.Failure.ShouldBeNull();
    }

    /// <summary>Verifies the wheel re-windows the realized rows and a click after scrolling
    /// selects the logical row now under the pointer, not the row that used to be there.</summary>
    [Fact]
    public async Task Pointer_WhenWheelScrollsThenRowIsClicked_SelectsTheLogicalRowUnderThePointerAsync()
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = Length.Cells(1),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Items = Enumerable.Range(0, 5_000).Select(value => (object?) $"Row {value:D4}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 4), TestContext.Current.CancellationToken);

        // Act
        for (var notch = 0; notch < 3; notch++)
        {
            await surface.Pointer.WheelAsync(list, new Point(1, 1), wheelY: -1);
        }

        await surface.Pointer.ClickAsync(list, new Point(1, 0));

        // Assert
        list.VerticalOffset.ShouldBe(3);
        list.SelectedIndex.ShouldBe(3);
        list.SelectedItem.ShouldBe("Row 0003");
        RowText(surface, 0, 8).ShouldBe("Row 0003");
        RowText(surface, 3, 8).ShouldBe("Row 0006");
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.Index >= 0 && item.Index < 40);
    }

    /// <summary>Verifies a mounted resize that changes the resolved percentage row height remaps
    /// the offset so the same logical first row stays anchored, re-realizes at the new height,
    /// and settles in both directions.</summary>
    [Fact]
    public async Task Resize_WhenRelativeRowHeightChanges_RemapsOffsetOntoTheSameLogicalRowAsync()
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = Length.Percent(50),
            ItemTemplate = item => new ControlText((string) item!) { Height = Length.Star(1) },
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Items = Enumerable.Range(0, 1_000).Select(value => (object?) $"Row {value}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 8), TestContext.Current.CancellationToken);
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.Bounds.Height == 4);
        await surface.UpdateAsync(() => list.ScrollBy(0, 40).ShouldBeTrue(), "scroll to row 10");
        RowText(surface, 0, 6).ShouldBe("Row 10");

        // Act shrink
        await surface.ResizeAsync(new Size(12, 4));

        // Assert the same logical row is still first
        list.VerticalOffset.ShouldBe(20);
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.Bounds.Height == 2);
        RowText(surface, 0, 6).ShouldBe("Row 10");
        RowText(surface, 2, 6).ShouldBe("Row 11");

        // Act grow back
        await surface.ResizeAsync(new Size(12, 8));

        // Assert
        list.VerticalOffset.ShouldBe(40);
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.Bounds.Height == 4);
        RowText(surface, 0, 6).ShouldBe("Row 10");
        RowText(surface, 4, 6).ShouldBe("Row 11");
    }

    /// <summary>Verifies a selection assigned before mount inside the scrolling composition is
    /// revealed once the first non-empty viewport commits, without ever realizing the rows
    /// between the top and the target.</summary>
    [Fact]
    public async Task SelectedIndex_WhenAssignedBeforeMountInsideScrollingStack_RevealsRowAfterFirstLayoutAsync()
    {
        // Arrange
        var list = CreateShowcaseStyleList();
        list.SelectedIndex = 15_000;
        var root = CreateShowcaseStyleHost(list);

        // Act
        await using var surface = await ComponentSurface.MountAsync(root, new Size(94, 30), TestContext.Current.CancellationToken);

        // Assert
        list.SelectedIndex.ShouldBe(15_000);
        list.ActiveIndex.ShouldBe(15_000);
        var rowHeight = OwnedTree.FindAll<ListItem>(list).Select(item => item.Bounds.Height).Distinct().ShouldHaveSingleItem();
        var top = 15_000 * rowHeight;
        list.VerticalOffset.ShouldBeInRange(top + rowHeight - list.Viewport.Height, top);
        OwnedTree.FindAll<ListItem>(list).ShouldContain(item => item.Index == 15_000 && item.IsSelected);
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBeLessThan(100);
    }

    /// <summary>Verifies replacing the snapshot with a far shorter one while scrolled to the end
    /// drops the vanished selection, clamps the offset into the new extent, re-windows, and
    /// settles.</summary>
    [Fact]
    public async Task Items_WhenReplacedWithShorterSnapshotWhileScrolledToEnd_ClampsOffsetAndRewindowsAsync()
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = Length.Cells(1),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Items = Enumerable.Range(0, 20_000).Select(value => (object?) $"Row {value:D5}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var changes = new List<string>();
        list.SelectionChanged += (_, eventArgs) =>
            changes.Add($"{string.Join(',', eventArgs.AddedIndexes.ToArray())}:{string.Join(',', eventArgs.RemovedIndexes.ToArray())}");
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);
        changes.Clear();

        // Act
        await surface.UpdateAsync(
            () => list.Items = Enumerable.Range(0, 50).Select(value => (object?) $"Row {value:D5}").ToArray(),
            "replace with fifty rows");

        // Assert
        list.Items.Count.ShouldBe(50);
        list.SelectedIndex.ShouldBe(-1);
        changes.ShouldBe([":19999"]);
        list.Extent.Height.ShouldBe(50);
        list.VerticalOffset.ShouldBe(45, "the offset clamps into the new extent rather than resetting");
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBeLessThanOrEqualTo(50);
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.Index < 50);
        RowText(surface, 0, 9).ShouldBe("Row 00045");
        RowText(surface, 4, 9).ShouldBe("Row 00049");

        // Act keyboard still works on the replacement
        await surface.Keyboard.PressAsync(Code.End);
        list.SelectedIndex.ShouldBe(49);
        list.VerticalOffset.ShouldBe(45);
        RowText(surface, 4, 9).ShouldBe("Row 00049");
    }

    /// <summary>Verifies toggling RowHeight on a mounted, populated list switches between eager
    /// and windowed realization in both directions while the selection, the active row, and its
    /// visibility survive.</summary>
    [Fact]
    public async Task RowHeight_WhenToggledWhileMounted_SwitchesRealizationAndKeepsSelectionAsync()
    {
        // Arrange
        var list = new UiListView
        {
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Items = Enumerable.Range(0, 200).Select(value => (object?) $"Row {value:D3}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 5), TestContext.Current.CancellationToken);
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBe(200);
        await surface.UpdateAsync(() => list.SelectedIndex = 100, "select row 100");
        list.VerticalOffset.ShouldBe(96);

        // Act windowed
        await surface.UpdateAsync(() => list.RowHeight = Length.Cells(1), "enable windowing");

        // Assert
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBeLessThan(200);
        list.SelectedIndex.ShouldBe(100);
        list.ActiveIndex.ShouldBe(100);
        list.VerticalOffset.ShouldBe(96);
        RowText(surface, 4, 7).ShouldBe("Row 100");
        OwnedTree.FindAll<ListItem>(list).Single(item => item.Index == 100).IsSelected.ShouldBeTrue();

        // Act eager again
        await surface.UpdateAsync(() => list.RowHeight = Length.Auto, "disable windowing");

        // Assert
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBe(200);
        list.SelectedIndex.ShouldBe(100);
        list.ActiveIndex.ShouldBe(100);
        RowText(surface, 4, 7).ShouldBe("Row 100");
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        list.SelectedIndex.ShouldBe(101);
    }

    /// <summary>Verifies a SelectionChanged handler that detaches the windowed list from its
    /// parent mid-navigation completes without throwing and leaves nothing focused on the
    /// detached tree.</summary>
    [Fact]
    public async Task SelectionChanged_WhenHandlerDetachesWindowedList_CompletesWithoutThrowingAsync()
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = Length.Cells(1),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Items = Enumerable.Range(0, 1_000).Select(value => (object?) $"Row {value}").ToArray()
        };
        var host = new Stack { Children = { list } };
        list.SelectionChanged += (_, _) => host.Children.Remove(list);
        await using var surface = await ComponentSurface.MountAsync(host, new Size(12, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(list);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        list.Parent.ShouldBeNull();
        list.IsDisposed.ShouldBeFalse();
        list.SelectedIndex.ShouldBe(1);
        list.IsFocused.ShouldBeFalse();
        surface.Application.Focus.Focused.ShouldNotBeSameAs(list);
        surface.ShouldRender("");
    }

    /// <summary>Verifies navigation and the wheel saturate at both ends of a twenty-thousand row
    /// windowed list: PageDown, Down, and wheel notches at the end keep the last row selected and
    /// rendered on the bottom line, and PageUp, Up, and wheel notches at the top keep the first
    /// row on the top line.</summary>
    [Fact]
    public async Task Input_WhenPagingOrWheelingPastEitherEnd_SaturatesWithoutMovingAsync()
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = Length.Cells(1),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Items = Enumerable.Range(0, 20_000).Select(value => (object?) $"Row {value:D5}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 10), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);
        list.VerticalOffset.ShouldBe(19_990);

        // Act past the end
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Pointer.WheelAsync(list, new Point(1, 5), wheelY: -1);
        await surface.Pointer.WheelAsync(list, new Point(1, 5), wheelY: -1);

        // Assert
        list.SelectedIndex.ShouldBe(19_999);
        list.ActiveIndex.ShouldBe(19_999);
        list.VerticalOffset.ShouldBe(19_990);
        RowText(surface, 9, 9).ShouldBe("Row 19999");
        RowText(surface, 0, 9).ShouldBe("Row 19990");
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBeLessThan(100);

        // Act past the start
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.PageUp);
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Pointer.WheelAsync(list, new Point(1, 5), wheelY: 1);

        // Assert
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
        list.VerticalOffset.ShouldBe(0);
        RowText(surface, 0, 9).ShouldBe("Row 00000");
        RowText(surface, 9, 9).ShouldBe("Row 00009");
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.Index < 100);
    }

    /// <summary>Verifies a Shift-click whose anchor row has long left the realized window ranges
    /// over every logical index in between, and a later Control-click removes one row from that
    /// range - both without realizing the rows the range crosses.</summary>
    [Fact]
    public async Task Pointer_WhenShiftClickSpansUnrealizedRows_SelectsTheWholeLogicalRangeAsync()
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = Length.Cells(1),
            SelectionMode = ListSelectionMode.Multiple,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Items = Enumerable.Range(0, 20_000).Select(value => (object?) $"Row {value:D5}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var added = new List<int>();
        list.SelectionChanged += (_, eventArgs) => added.Add(eventArgs.AddedIndexes.Length);
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 10), TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(list, new Point(1, 0));
        list.SelectedIndex.ShouldBe(0);
        await surface.UpdateAsync(() => list.ScrollBy(0, 19_990).ShouldBeTrue(), "scroll to the end");
        RowText(surface, 9, 9).ShouldBe("Row 19999");
        OwnedTree.FindAll<ListItem>(list).ShouldNotContain(item => item.Index == 0);

        // Act
        var last = OwnedTree.FindAll<ListItem>(list).Single(item => item.Index == 19_999);
        await surface.Pointer.ClickAsync(last, Modifiers.Shift);

        // Assert the whole logical range without realizing it
        list.SelectedItems.Count.ShouldBe(20_000);
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(19_999);
        added.ShouldBe([1, 19_999]);
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBeLessThan(100);
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.IsSelected);
        var selectionBackground = ThemeColorHelper.SelectionBackground(list.Theme.ShouldNotBeNull());
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(selectionBackground);
        surface.Cell(new Point(0, 9)).Style.Background.ShouldBe(selectionBackground);

        // Act a Control-click removes one row from the range
        var middle = OwnedTree.FindAll<ListItem>(list).Single(item => item.Index == 19_995);
        await surface.Pointer.ClickAsync(middle, Modifiers.Control);

        // Assert
        list.SelectedItems.Count.ShouldBe(19_999);
        middle.IsSelected.ShouldBeFalse();
        surface.Cell(new Point(0, 5)).Style.Background.ShouldNotBe(selectionBackground);
        surface.Cell(new Point(0, 4)).Style.Background.ShouldBe(selectionBackground);
    }

    /// <summary>Verifies an insertion or removal above the realized window while the mounted list
    /// is scrolled keeps the same logical row on the first line and moves the selected index with
    /// its item, while a removal inside the window reflows the rendered rows.</summary>
    [Fact]
    public async Task Items_WhenInsertedOrRemovedAboveTheWindowWhileScrolled_KeepsTheFirstVisibleRowAsync()
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = Length.Cells(1),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Items = Enumerable.Range(0, 1_000).Select(value => (object?) $"Row {value:D4}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => list.SelectedIndex = 500, "select row 500");
        list.VerticalOffset.ShouldBe(496);
        RowText(surface, 0, 8).ShouldBe("Row 0496");
        RowText(surface, 4, 8).ShouldBe("Row 0500");

        // Act insert above the window
        await surface.UpdateAsync(() => list.InsertItem(0, "New"), "insert at the top");

        // Assert the same rows stay on screen and the selection followed its item
        list.Items.Count.ShouldBe(1_001);
        list.VerticalOffset.ShouldBe(497);
        list.SelectedIndex.ShouldBe(501);
        list.SelectedItem.ShouldBe("Row 0500");
        list.ActiveIndex.ShouldBe(501);
        RowText(surface, 0, 8).ShouldBe("Row 0496");
        RowText(surface, 4, 8).ShouldBe("Row 0500");
        var selectionBackground = ThemeColorHelper.SelectionBackground(list.Theme.ShouldNotBeNull());
        surface.Cell(new Point(0, 4)).Style.Background.ShouldBe(selectionBackground);
        OwnedTree.FindAll<ListItem>(list).ShouldNotContain(item => item.Index == 0);

        // Act remove above the window
        await surface.UpdateAsync(() => list.RemoveItem(10), "remove a row above the window");

        // Assert
        list.Items.Count.ShouldBe(1_000);
        list.VerticalOffset.ShouldBe(496);
        list.SelectedIndex.ShouldBe(500);
        list.SelectedItem.ShouldBe("Row 0500");
        RowText(surface, 0, 8).ShouldBe("Row 0496");
        RowText(surface, 4, 8).ShouldBe("Row 0500");

        // Act remove inside the window
        await surface.UpdateAsync(() => list.RemoveItem(497), "remove a visible row");

        // Assert the rows below slide up and the selection still follows its item
        list.VerticalOffset.ShouldBe(496);
        list.SelectedIndex.ShouldBe(499);
        list.SelectedItem.ShouldBe("Row 0500");
        RowText(surface, 0, 8).ShouldBe("Row 0496");
        RowText(surface, 1, 8).ShouldBe("Row 0498");
        RowText(surface, 3, 8).ShouldBe("Row 0500");
        surface.Cell(new Point(0, 3)).Style.Background.ShouldBe(selectionBackground);
        surface.Cell(new Point(0, 4)).Style.Background.ShouldNotBe(selectionBackground);
    }

    /// <summary>Verifies disabled rows outside the realized window are optimistically eligible
    /// until realization proves otherwise: Ctrl+A selects every unrealized index, a programmatic
    /// selection of an off-window disabled row is repaired once it realizes, and End skips a
    /// disabled last row only after that row has been seen.</summary>
    [Fact]
    public async Task Selection_WhenDisabledRowsLieOutsideTheWindow_RepairsOnRealizationAsync()
    {
        // Arrange
        var list = new UiListView
        {
            RowHeight = Length.Cells(1),
            SelectionMode = ListSelectionMode.Multiple,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            ItemTemplate = item => new ControlText((string) item!) { IsEnabled = !((string) item!).EndsWith("000", StringComparison.Ordinal) },
            Items = Enumerable.Range(0, 20_000).Select(value => (object?) $"Row {value:D5}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var failures = new List<Exception>();
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 5), TestContext.Current.CancellationToken);
        surface.Application.UnhandledException += (_, eventArgs) => failures.Add(eventArgs.Exception);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveState(OwnedTree.FindAll<ListItem>(list).Single(item => item.Index == 0).Content!, VisualState.Disabled);

        // Act select all
        await surface.SendAsync(Encoding.ASCII.GetBytes($"{(char) 27}[97;5u"), "press Ctrl+A");

        // Assert the realized disabled row is excluded while every unrealized one is optimistic
        failures.ShouldBeEmpty();
        list.SelectedItems.ShouldNotContain("Row 00000");
        list.SelectedItems.ShouldContain("Row 05000");
        list.SelectedItems.Count.ShouldBe(19_999);

        // Act reveal a disabled off-window row through a programmatic selection
        await surface.UpdateAsync(() => list.SelectedIndex = 5_000, "select an off-window disabled row");

        // Assert realization repaired the selection and the row renders disabled
        failures.ShouldBeEmpty();
        list.SelectedItems.ShouldNotContain("Row 05000");
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldNotBe(5_000);
        var revealed = OwnedTree.FindAll<ListItem>(list).Single(item => item.Index == 5_000);
        surface.ShouldHaveState(revealed.Content!, VisualState.Disabled);

        // Act End twice: the first realizes the disabled last row, the second lands before it
        await surface.UpdateAsync(() => list.Items = Enumerable.Range(0, 20_001).Select(value => (object?) $"Row {value:D5}").ToArray(), "append a disabled last row");
        await surface.Keyboard.PressAsync(Code.End);
        failures.ShouldBeEmpty();
        list.SelectedItems.ShouldNotContain("Row 20000");
        await surface.Keyboard.PressAsync(Code.End);

        // Assert
        failures.ShouldBeEmpty();
        list.SelectedIndex.ShouldBe(19_999);
        list.ActiveIndex.ShouldBe(19_999);
        RowText(surface, 4, 9).ShouldBe("Row 20000");
        RowText(surface, 3, 9).ShouldBe("Row 19999");
        var selectionBackground = ThemeColorHelper.SelectionBackground(list.Theme.ShouldNotBeNull());
        surface.Cell(new Point(0, 3)).Style.Background.ShouldBe(selectionBackground);
        surface.Cell(new Point(0, 4)).Style.Background.ShouldNotBe(selectionBackground);
    }

    private static UiListView CreateShowcaseStyleList() => new()
    {
        Width = Length.Cells(20),
        Height = Length.Percent(40),
        MinHeight = Length.Cells(4),
        MaxHeight = Length.Cells(12),
        RowHeight = Length.Percent(25),
        ItemTemplate = item => new ControlText((string) item!) { Height = Length.Star(1) },
        ScrollBars = ScrollBars.Vertical,
        ShowScrollBars = ShowScrollBars.Always,
        ScrollBarStyle = ScrollBarStyle.ThinLine,
        Items = Enumerable.Range(0, 20_000).Select(value => (object?) $"Row {value:D5}").ToArray()
    };

    private static Dock CreateShowcaseStyleHost(UiListView list)
    {
        var body = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            Padding = new Thickness(1),
            Spacing = 1
        };

        for (var index = 0; index < 30; index++)
        {
            body.Children.Add(new ControlText($"Filler paragraph {index}") { Overflow = Overflow.Wrap });
        }

        body.Children.Add(list);
        var header = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { new ControlText("ListView\nRealizes selectable items.") { Overflow = Overflow.Wrap } }
        };
        Dock.SetSide(header, DockSide.Top);
        return new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { header, body }
        };
    }
}
