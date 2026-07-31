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
            container.HorizontalBarVisibility = Policy(random);
            container.VerticalBarVisibility = Policy(random);
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

    private static ScrollBarVisibility Policy(Random random) =>
        (ScrollBarVisibility) random.Next(0, 3);
}
