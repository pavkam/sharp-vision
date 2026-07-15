// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;


/// <summary>Proves fixed-seed automatic scrollbar convergence and containment for an armed Container.</summary>
public sealed class ContainerScrollGeometryTests
{
    private const int _caseCount = 10_000;
    private const int _seed = 0x005C_701E;

    /// <summary>Verifies randomized viewports and policies stabilize in one repeated layout.</summary>
    [Fact]
    public void Layout_WhenCasesAreRandomized_PreservesStableContainedGeometry()
    {
        var random = new Random(_seed);
        var engine = new Engine();
        var container = new LayoutProbe() { AutoScroll = true, ScrollBars = ScrollBars.Both };
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
