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
    /// <param name="surfaceHeight">The terminal height, chosen so the arranged list height differs from its MaxHeight.</param>
    [Theory]
    [InlineData(30)]
    [InlineData(24)]
    [InlineData(20)]
    [InlineData(40)]
    public async Task Layout_WhenHostedInAutoScrollingStackWithRelativeRows_SettlesAsync(int surfaceHeight)
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
        var frames = 0;
        surface.Application.FrameRendered += (_, _) => Interlocked.Increment(ref frames);
        await surface.UpdateAsync(static () => { }, "settled no-op");
        frames.ShouldBe(0);

        var expectedRowHeight = Math.Max(1, (int) Math.Round(list.Viewport.Height * 0.25, MidpointRounding.AwayFromZero));
        list.Bounds.Height.ShouldBeInRange(4, 12);
        list.Viewport.Height.ShouldBe(list.Bounds.Height);
        var realized = OwnedTree.FindAll<ListItem>(list);
        realized.Count.ShouldBeGreaterThan(0);
        realized.Count.ShouldBeLessThan(200);
        realized.ShouldAllBe(item => item.Bounds.Height == expectedRowHeight);
        list.Extent.Height.ShouldBe(20_000 * expectedRowHeight);
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
