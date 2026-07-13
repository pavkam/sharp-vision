namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Tests.Support;

using Shouldly;

/// <summary>Proves fixed-seed automatic scrollbar convergence and containment.</summary>
public sealed class RandomizedScrollViewTests
{
    private const int _caseCount = 10_000;
    private const int _seed = 0x005C_701E;

    /// <summary>Verifies randomized viewports and policies stabilize in one repeated layout.</summary>
    [Fact]
    public void Layout_WhenCasesAreRandomized_PreservesStableContainedGeometry()
    {
        var random = new Random(_seed);
        var engine = new Engine();
        var view = new ScrollView { Content = new ProbeControl(new Size(50, 30)) };

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var size = new Size(random.Next(0, 80), random.Next(0, 50));
            view.HorizontalBarVisibility = Policy(random);
            view.VerticalBarVisibility = Policy(random);
            engine.Layout(view, size);
            var first = view.Viewport;
            engine.Layout(view, size);
            var context = $"seed=0x{_seed:X8}, case={sample}, size={size}";

            view.Viewport.ShouldBe(first, context);
            view.Viewport.Width.ShouldBeInRange(0, size.Width, context);
            view.Viewport.Height.ShouldBeInRange(0, size.Height, context);
            view.HorizontalOffset.ShouldBeInRange(
                0,
                Math.Max(0, view.Extent.Width - view.Viewport.Width),
                context);
            view.VerticalOffset.ShouldBeInRange(
                0,
                Math.Max(0, view.Extent.Height - view.Viewport.Height),
                context);
        }
    }

    private static ScrollBarVisibility Policy(Random random) =>
        (ScrollBarVisibility) random.Next(0, 3);
}
