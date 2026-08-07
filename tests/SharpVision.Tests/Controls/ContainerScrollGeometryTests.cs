// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves fixed-seed automatic scrollbar convergence and containment for an armed Container.</summary>
public sealed class ContainerScrollGeometryTests
{
    private const int _caseCount = 10_000;
    private const int _seed = 0x005C_701E;

    /// <summary>Verifies viewport and both framework bars remain inside border and padding.</summary>
    [Fact]
    public void Layout_WhenBorderPaddingAndBothBarsArePresent_ContainsViewportAndBars()
    {
        var child = new ProbeControl(new Size(20, 10));
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        container.Children.Add(child);

        new LayoutEngine().Layout(container, new Size(10, 6));

        container.Viewport.ShouldBe(new Size(5, 1));
        child.Bounds.X.ShouldBe(2);
        child.Bounds.Y.ShouldBe(2);
        container.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Horizontal);
        container.HitTest(new Point(7, 2)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies shadow overflow neither changes scroll geometry nor escapes the committed viewport.</summary>
    [Fact]
    public void Render_WhenChildShadowIsVisible_KeepsExtentNeutralAndClipsToViewport()
    {
        var child = new LayoutProbe
        {
            Shadow = AppearanceTestValues.Shadow(visible: true, mode: ShadowMode.BlockGlyph, offset: new Point(1, 1), glyph: new Rune('▓')),
            Children = { new ProbeControl(new Size(3, 2)) }
        };
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
            Children = { child }
        };
        new LayoutEngine().Layout(container, new Size(3, 2));
        using Frame frame = new(new Size(4, 3));

        container.Render(frame.Canvas);

        container.Extent.ShouldBe(new Size(3, 2));
        container.Viewport.ShouldBe(new Size(3, 2));
        frame.GetCell(new Point(3, 1)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(2, 2)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies randomized viewports and policies stabilize in one repeated layout.</summary>
    [Fact]
    public void Layout_WhenCasesAreRandomized_PreservesStableContainedGeometry()
    {
        var random = new Random(_seed);
        var engine = new LayoutEngine();
        var container = new LayoutProbe { AutoScroll = true, ScrollBars = ScrollBars.Both };
        container.Children.Add(new ProbeControl(new Size(50, 30)));

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var size = new Size(random.Next(0, 80), random.Next(0, 50));
            container.HorizontalBarVisibility = UnicodePolicy(random);
            container.VerticalBarVisibility = UnicodePolicy(random);
            engine.Layout(container, size);
            var first = container.Viewport;
            engine.Layout(container, size);
            var context = $"seed=0x{_seed:X8}, case={sample}, size={size}";

            container.Viewport.ShouldBe(first, context);
            container.Viewport.Width.ShouldBeInRange(0, size.Width, context);
            container.Viewport.Height.ShouldBeInRange(0, size.Height, context);
            container.HorizontalOffset.ShouldBeInRange(
                0,
                Math.Max(0, container.Extent.Width - container.Viewport.Width),
                context);
            container.VerticalOffset.ShouldBeInRange(
                0,
                Math.Max(0, container.Extent.Height - container.Viewport.Height),
                context);
        }
    }

    /// <summary>Verifies an unpadded armed container reserves a vertical bar cell in DesiredSize
    /// when content overflows the border-box height.</summary>
    [Fact]
    public void Measure_WhenUnpaddedContentOverflowsHeight_ReservesVerticalBarCellInDesiredSize()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Height = Length.Cells(6)
        };
        container.Children.Add(new ProbeControl(new Size(4, 7)));

        new LayoutEngine().Layout(container, new Size(20, 20));

        container.DesiredSize.ShouldBe(new Size(5, 6));
    }

    /// <summary>Verifies a padded armed container with an identical content-box height still
    /// reserves the vertical bar cell in DesiredSize: comparing the content-box extent against a
    /// border-box result under-detects overflow by exactly the padding and border inset.</summary>
    [Fact]
    public void Measure_WhenPaddedContentOverflowsContentBoxHeight_ReservesVerticalBarCellInDesiredSize()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Height = Length.Cells(10),
            Padding = new Thickness(0, 2, 0, 2)
        };
        container.Children.Add(new ProbeControl(new Size(4, 7)));

        new LayoutEngine().Layout(container, new Size(20, 20));

        // The content box is 10 - 4 = 6 cells tall, identical to the unpadded case above, so the
        // bar cell must be reserved here too. Before the fix, DesiredSize.Width stayed 4 because
        // ContentExtent.Height (7) was compared against the border-box result.Height (10).
        container.DesiredSize.ShouldBe(new Size(5, 10));
    }

    /// <summary>Verifies an <see cref="Container.AutoSize"/> container with an explicit
    /// <see cref="LengthKind.Cells"/> Width - overridden to content size under
    /// <see cref="AutoSizeMode.GrowAndShrink"/> - still reserves the vertical bar cell.</summary>
    [Fact]
    public void Measure_WhenAutoSizeOverridesAnExplicitWidth_ReservesVerticalBarCellInDesiredSize()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            AutoSize = true,
            ScrollBars = ScrollBars.Vertical,
            Width = Length.Cells(99),
            MaxHeight = 5
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new LayoutEngine().Layout(container, new Size(20, 20));

        // GrowAndShrink ignores the explicit Cells(99) and content-sizes to width 4, but its mere
        // presence must not suppress bar-cell reservation just because Width.Kind != Auto.
        container.DesiredSize.ShouldBe(new Size(5, 5));
    }

    /// <summary>Verifies a padded and bordered armed container with Auto-visibility bars (not
    /// Always, the only policy the existing padded+bordered test above exercises) still contains
    /// the discovered viewport and both automatically-added bars entirely inside the
    /// border-and-padding-deflated content box.</summary>
    [Fact]
    public void Layout_WhenBorderPaddingAndAutoBarsArePresent_ContainsViewportAndBars()
    {
        var child = new ProbeControl(new Size(20, 10));
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        container.Children.Add(child);

        new LayoutEngine().Layout(container, new Size(10, 6));

        // Content (20x10) overflows the 6x2 content box (10 - 2 border - 2 padding on each axis)
        // on both axes, so both Auto bars are added - identical geometry to the Always case
        // above, proving Auto-triggered addition reserves the same cells Always does.
        container.Viewport.ShouldBe(new Size(5, 1));
        child.Bounds.X.ShouldBe(2);
        child.Bounds.Y.ShouldBe(2);
        container.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Horizontal);
        container.HitTest(new Point(7, 2)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies content that shrinks while scrolled near the end of its previous, larger
    /// extent re-clamps the offset to the new, smaller maximum instead of leaving it stranded
    /// past the current extent - an out-of-range offset that a naive re-layout could otherwise
    /// commit verbatim.</summary>
    [Fact]
    public void Layout_WhenContentShrinksWhileScrolledNearTheEnd_ReClampsOffsetToNewMaximum()
    {
        var child = new ProbeControl(new Size(4, 40));
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(child);
        var engine = new LayoutEngine();
        engine.Layout(container, new Size(4, 10));

        _ = container.ScrollBy(0, 1000);
        container.VerticalOffset.ShouldBe(30);

        _ = container.Children.Remove(child);
        var shrunkChild = new ProbeControl(new Size(4, 12));
        container.Children.Add(shrunkChild);
        engine.Layout(container, new Size(4, 10));

        // The new extent (12) less the unchanged viewport (10) leaves a maximum of 2 - the stale
        // offset (30) must be clamped down to that, not left pointing past the new extent.
        container.Extent.Height.ShouldBe(12);
        container.VerticalOffset.ShouldBe(2);
    }

    private static ScrollBarVisibility UnicodePolicy(Random random) =>
        (ScrollBarVisibility) random.Next(0, 3);
}
