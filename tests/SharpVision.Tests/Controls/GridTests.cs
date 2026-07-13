namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;

using Shouldly;

using ControlText = SharpVision.Controls.Text;
using Wrapping = SharpVision.Text.Wrapping;

/// <summary>Verifies Grid track resolution, spacing, spans, resize, and semantic cells.</summary>
public sealed class GridTests
{
    /// <summary>Verifies empty definitions behave as one intrinsic automatic cell.</summary>
    [Fact]
    public void Layout_WhenDefinitionsAreEmpty_UsesOneImplicitAutoTrack()
    {
        var grid = new Grid();
        var child = new ProbeControl(new Size(3, 2));
        grid.Children.Add(child);

        new Engine().Layout(grid, new Size(8, 5));

        grid.DesiredSize.ShouldBe(new Size(3, 2));
        child.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
    }

    /// <summary>Verifies fixed, percent, auto, and star columns share final space exactly.</summary>
    [Fact]
    public void Layout_WhenColumnsMixKinds_ResolvesExactTracksAndSpacing()
    {
        var grid = new Grid { ColumnSpacing = 1 };
        grid.Columns.Add(Track.Cells(3));
        grid.Columns.Add(Track.Percent(25));
        grid.Columns.Add(Track.Auto());
        grid.Columns.Add(Track.Star(1));
        var fixedChild = Child(column: 0);
        var percentChild = Child(column: 1);
        var autoChild = new ProbeControl(new Size(2, 1));
        var starChild = Child(column: 3);
        Grid.SetColumn(autoChild, 2);
        grid.Children.Add(fixedChild);
        grid.Children.Add(percentChild);
        grid.Children.Add(autoChild);
        grid.Children.Add(starChild);

        new Engine().Layout(grid, new Size(20, 2));

        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        percentChild.Bounds.ShouldBe(new Rect(4, 0, 4, 1));
        autoChild.Bounds.ShouldBe(new Rect(9, 0, 2, 1));
        starChild.Bounds.ShouldBe(new Rect(12, 0, 8, 1));
    }

    /// <summary>Verifies automatic tracks use the maximum non-spanning intrinsic request.</summary>
    [Fact]
    public void Measure_WhenChildrenShareAutoTrack_UsesMaximumRequest()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Auto());
        var narrow = new ProbeControl(new Size(2, 1));
        var wide = new ProbeControl(new Size(5, 2));
        grid.Children.Add(narrow);
        grid.Children.Add(wide);

        new Engine().Layout(grid, new Size(10, 4));

        grid.DesiredSize.ShouldBe(new Size(5, 2));
        narrow.Bounds.Width.ShouldBe(5);
        wide.Bounds.Width.ShouldBe(5);
    }

    /// <summary>Verifies a spanning intrinsic request distributes only track cells.</summary>
    [Fact]
    public void Measure_WhenChildSpansAutoTracks_ExcludesInternalSpacingFromTracks()
    {
        var grid = new Grid { ColumnSpacing = 1 };
        grid.Columns.Add(Track.Auto());
        grid.Columns.Add(Track.Auto());
        var child = new ProbeControl(new Size(5, 1));
        Grid.SetColumnSpan(child, 2);
        grid.Children.Add(child);

        new Engine().Layout(grid, new Size(10, 2));

        grid.DesiredSize.Width.ShouldBe(5);
        child.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
    }

    /// <summary>Verifies min/max clipping redistributes remaining star cells.</summary>
    [Fact]
    public void Layout_WhenStarTrackHasMaximum_RedistributesRemainder()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Star(1, maximum: 2));
        grid.Columns.Add(Track.Star(1));
        var first = Child(column: 0);
        var second = Child(column: 1);
        grid.Children.Add(first);
        grid.Children.Add(second);

        new Engine().Layout(grid, new Size(10, 1));

        first.Bounds.Width.ShouldBe(2);
        second.Bounds.ShouldBe(new Rect(2, 0, 8, 1));
    }

    /// <summary>Verifies collapsed children contribute no intrinsic requirement.</summary>
    [Fact]
    public void Measure_WhenLargeChildIsCollapsed_IgnoresItsRequest()
    {
        var grid = new Grid();
        var visible = new ProbeControl(new Size(2, 1));
        var collapsed = new ProbeControl(new Size(20, 10))
        {
            Visibility = Visibility.Collapsed,
        };
        grid.Children.Add(visible);
        grid.Children.Add(collapsed);

        new Engine().Layout(grid, new Size(30, 20));

        grid.DesiredSize.ShouldBe(new Size(2, 1));
        collapsed.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies final span constraints remeasure content before arrangement.</summary>
    [Fact]
    public void Layout_WhenFinalTrackConstraintChanges_RemeasuresChildForSpan()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Percent(50));
        grid.Columns.Add(Track.Star(1));
        var child = new ProbeControl(new Size(8, 1));
        Grid.SetColumnSpan(child, 2);
        grid.Children.Add(child);

        new Engine().Layout(grid, new Size(12, 3));

        child.MeasureConstraints.ShouldContain(new Constraint(12, 1));
        child.Bounds.Width.ShouldBe(12);
    }

    /// <summary>Verifies automatic rows remeasure wrapped content with finite width and unbounded height.</summary>
    [Fact]
    public void Layout_WhenStarColumnNarrowsWrappedText_GrowsAutoRowForEveryLine()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Star(1));
        var text = new ControlText("One two three four five six") { Wrapping = Wrapping.Word };
        grid.Children.Add(text);

        new Engine().Layout(grid, new Size(10, 10));

        text.Bounds.Height.ShouldBeGreaterThan(1);
        grid.DesiredSize.Height.ShouldBeGreaterThan(1);
    }

    /// <summary>Verifies resize recomputes percentage edges and star remainder.</summary>
    [Fact]
    public void Layout_WhenViewportResizes_RecomputesDeferredTrackGeometry()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Percent(50));
        grid.Columns.Add(Track.Star(1));
        var percent = Child(column: 0);
        var star = Child(column: 1);
        grid.Children.Add(percent);
        grid.Children.Add(star);
        var engine = new Engine();

        engine.Layout(grid, new Size(9, 1));
        percent.Bounds.Width.ShouldBe(5);
        star.Bounds.Width.ShouldBe(4);
        engine.Layout(grid, new Size(13, 1));
        percent.Bounds.Width.ShouldBe(7);
        star.Bounds.Width.ShouldBe(6);
    }

    /// <summary>Verifies spacing and tracks saturate within a tiny viewport.</summary>
    [Fact]
    public void Layout_WhenSpacingExceedsViewport_ContainsEveryChild()
    {
        var grid = new Grid { ColumnSpacing = 9 };
        grid.Columns.Add(Track.Cells(4));
        grid.Columns.Add(Track.Cells(4));
        var first = Child(column: 0);
        var second = Child(column: 1);
        grid.Children.Add(first);
        grid.Children.Add(second);

        new Engine().Layout(grid, new Size(3, 1));

        first.Bounds.Width.ShouldBe(0);
        second.Bounds.Width.ShouldBe(0);
        first.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
        second.Bounds.Right.ShouldBeLessThanOrEqualTo(3);
    }

    /// <summary>Verifies shrinking definitions cannot strand an owned child out of range.</summary>
    [Fact]
    public void RemoveAt_WhenChildWouldBecomeOutOfRange_ThrowsBeforeMutation()
    {
        var grid = new Grid();
        grid.Rows.Add(Track.Auto());
        grid.Rows.Add(Track.Auto());
        var child = new ProbeControl();
        grid.Children.Add(child);
        Grid.SetRow(child, 1);

        _ = Should.Throw<InvalidOperationException>(() => grid.Rows.RemoveAt(1));

        grid.Rows.Count.ShouldBe(2);
        Grid.GetRow(child).ShouldBe(1);
    }

    /// <summary>Verifies arranged children render in collection order through semantic cells.</summary>
    [Fact]
    public void Render_WhenCellsAreArranged_WritesExpectedGridPositions()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Cells(1));
        grid.Columns.Add(Track.Cells(1));
        var first = Child(column: 0, content: "A");
        var second = Child(column: 1, content: "B");
        grid.Children.Add(first);
        grid.Children.Add(second);
        new Engine().Layout(grid, new Size(2, 1));
        using var frame = new Frame(new Size(2, 1));

        grid.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("B");
    }

    private static ProbeControl Child(int column, string content = "")
    {
        var child = new ProbeControl(new Size(1, 1)) { Content = content.AsMemory() };
        Grid.SetColumn(child, column);
        return child;
    }
}
