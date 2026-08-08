// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Grid track resolution, spacing, spans, resize, and semantic cells.</summary>
public sealed class GridTests
{
    /// <summary>Verifies empty definitions behave as one intrinsic automatic cell.</summary>
    [ComponentUnitEvidence(typeof(Grid))]
    [Fact]
    public void Layout_WhenDefinitionsAreEmpty_UsesOneImplicitAutoTrack()
    {
        var grid = new Grid();
        var child = new ProbeControl(new Size(3, 2));
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(8, 5));

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

        new LayoutEngine().Layout(grid, new Size(20, 2));

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

        new LayoutEngine().Layout(grid, new Size(10, 4));

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

        new LayoutEngine().Layout(grid, new Size(10, 2));

        grid.DesiredSize.Width.ShouldBe(5);
        child.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
    }

    /// <summary>Verifies a child spanning a Cells track and an Auto track gets its full
    /// intrinsic width, instead of the Cells track's deposited cells being silently discarded
    /// because Tracks.ResolveCore only reads the automatic request back for Auto.</summary>
    [Fact]
    public void Measure_WhenChildSpansCellsAndAutoColumns_ReceivesFullIntrinsicWidth()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Cells(4));
        grid.Columns.Add(Track.Auto());
        var child = new ProbeControl(new Size(20, 1));
        Grid.SetColumnSpan(child, 2);
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(60, 5));

        grid.DesiredSize.Width.ShouldBe(20);
        child.Bounds.Width.ShouldBe(20);
    }

    /// <summary>Verifies the same Cells + Auto span receives its full intrinsic width from the
    /// unbounded intrinsic measure path too, since the unbounded branch's absorbing set (every
    /// kind except Cells) differs from the bounded branch's (Auto only).</summary>
    [Fact]
    public void Measure_WhenChildSpansCellsAndAutoColumnsUnbounded_ReceivesFullIntrinsicWidth()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Cells(4));
        grid.Columns.Add(Track.Auto());
        var child = new ProbeControl(new Size(20, 1));
        Grid.SetColumnSpan(child, 2);
        grid.Children.Add(child);

        grid.Measure(new Constraint(null, null));

        grid.DesiredSize.Width.ShouldBe(20);
    }

    /// <summary>Verifies a child spanning a Percent track and an Auto track gets its full
    /// intrinsic width.</summary>
    [Fact]
    public void Measure_WhenChildSpansPercentAndAutoColumns_ReceivesFullIntrinsicWidth()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Percent(10));
        grid.Columns.Add(Track.Auto());
        var child = new ProbeControl(new Size(20, 1));
        Grid.SetColumnSpan(child, 2);
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(60, 5));

        grid.DesiredSize.Width.ShouldBe(20);
        child.Bounds.Width.ShouldBe(20);
    }

    /// <summary>Verifies row spans over a Cells + Auto pair also receive the full intrinsic
    /// height, since the defect is not axis-specific.</summary>
    [Fact]
    public void Measure_WhenChildSpansCellsAndAutoRows_ReceivesFullIntrinsicHeight()
    {
        var grid = new Grid();
        grid.Rows.Add(Track.Cells(2));
        grid.Rows.Add(Track.Auto());
        var child = new ProbeControl(new Size(5, 10));
        Grid.SetRowSpan(child, 2);
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(60, 40));

        grid.DesiredSize.Height.ShouldBe(10);
        child.Bounds.Height.ShouldBe(10);
    }

    /// <summary>Verifies a three-track span over two Cells tracks and one Auto track receives
    /// its full intrinsic width, so a wider span does not degrade proportionally worse.</summary>
    [Fact]
    public void Measure_WhenChildSpansThreeTracksMixingCellsAndAuto_ReceivesFullIntrinsicWidth()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Cells(2));
        grid.Columns.Add(Track.Cells(2));
        grid.Columns.Add(Track.Auto());
        var child = new ProbeControl(new Size(30, 1));
        Grid.SetColumnSpan(child, 3);
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(80, 5));

        grid.DesiredSize.Width.ShouldBe(30);
        child.Bounds.Width.ShouldBe(30);
    }

    /// <summary>Verifies the fix is order-independent: an Auto track before a Cells track in
    /// the span behaves the same as a Cells track before an Auto track.</summary>
    [Fact]
    public void Measure_WhenChildSpansAutoThenCellsColumns_ReceivesFullIntrinsicWidth()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Auto());
        grid.Columns.Add(Track.Cells(4));
        var child = new ProbeControl(new Size(10, 1));
        Grid.SetColumnSpan(child, 2);
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(60, 5));

        grid.DesiredSize.Width.ShouldBe(10);
        child.Bounds.Width.ShouldBe(10);
    }

    /// <summary>Verifies a span over two Cells tracks still caps the child at their combined
    /// fixed width, since neither track is absorbing and Tracks' Satisfy contract is
    /// deliberately kind-blind for that case.</summary>
    [Fact]
    public void Measure_WhenChildSpansTwoCellsColumns_CapsAtCombinedFixedWidth()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Cells(4));
        grid.Columns.Add(Track.Cells(4));
        var child = new ProbeControl(new Size(20, 1));
        Grid.SetColumnSpan(child, 2);
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(60, 5));

        grid.DesiredSize.Width.ShouldBe(8);
        child.Bounds.Width.ShouldBe(8);
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

        new LayoutEngine().Layout(grid, new Size(10, 1));

        first.Bounds.Width.ShouldBe(2);
        second.Bounds.ShouldBe(new Rect(2, 0, 8, 1));
    }

    /// <summary>Verifies arrange skips its bounded re-measure entirely when the final viewport
    /// exactly matches the constraint MeasureOverride already resolved these same extents
    /// against, instead of unconditionally repeating it.</summary>
    [Fact]
    public void Layout_WhenArrangedViewportMatchesMeasureConstraint_SkipsRedundantRemeasure()
    {
        var grid = new Grid();
        var child = new ProbeControl(new Size(3, 2));
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(8, 5));

        child.MeasureConstraints.Count.ShouldBe(3);
        child.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
    }

    /// <summary>Verifies a resize that changes the final viewport still triggers the bounded
    /// re-measure, so wrapped content stays correct after the arranged size actually
    /// changes.</summary>
    [Fact]
    public void Layout_WhenArrangedViewportChangesAfterResize_StillRemeasures()
    {
        var grid = new Grid();
        var child = new ProbeControl(new Size(3, 2));
        grid.Children.Add(child);
        var engine = new LayoutEngine();
        engine.Layout(grid, new Size(8, 5));
        var initialCount = child.MeasureConstraints.Count;

        engine.Layout(grid, new Size(12, 6));

        child.MeasureConstraints.Count.ShouldBeGreaterThan(initialCount);
        child.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
    }

    /// <summary>Verifies a consumer that decouples measure from arrange - arranging a Grid at a
    /// slot different from the constraint it last measured with, repeatedly - always re-measures
    /// against the arranged bounds instead of comparing against a recorded constraint the arrange
    /// path itself failed to refresh, a regression of an earlier arrange-skip guard.</summary>
    [Fact]
    public void Layout_WhenArrangedAtDecoupledBoundsRepeatedly_NeverArrangesStaleContent()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Star(1));
        var child = new WrappingProbeControl(wideWidth: 20, narrowWidth: 3);
        grid.Children.Add(child);

        // pass1: an ordinary measure/arrange pair at the wide width establishes the baseline.
        grid.Measure(new Constraint(20, 1));
        grid.Arrange(new Rect(0, 0, 20, 1));

        // pass2: a decoupled arrange at the narrow width, with no intervening Measure call -
        // exactly the shape a splitter, pane, or accordion container produces through
        // MeasureChild/ArrangeChild. The viewport genuinely changed, so this re-measures.
        grid.Arrange(new Rect(0, 0, 3, 2));
        child.MeasureConstraints.Last().Width.ShouldBe(3);

        // pass3: another decoupled arrange, back at the wide width. The recorded constraint must
        // reflect pass2's re-measure (3), not pass1's original measure (20), so this compares
        // unequal and re-measures again instead of skipping and arranging pass2's stale 3-wide
        // wrap inside the 20-wide slot.
        grid.Arrange(new Rect(0, 0, 20, 1));
        child.MeasureConstraints.Last().Width.ShouldBe(20);
    }

    /// <summary>Verifies collapsed children contribute no intrinsic requirement.</summary>
    [Fact]
    public void Measure_WhenLargeChildIsCollapsed_IgnoresItsRequest()
    {
        var grid = new Grid();
        var visible = new ProbeControl(new Size(2, 1));
        var collapsed = new ProbeControl(new Size(20, 10)) { Visibility = Visibility.Collapsed };
        grid.Children.Add(visible);
        grid.Children.Add(collapsed);

        new LayoutEngine().Layout(grid, new Size(30, 20));

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

        new LayoutEngine().Layout(grid, new Size(12, 3));

        child.MeasureConstraints.ShouldContain(new Constraint(12, 1));
        child.Bounds.Width.ShouldBe(12);
    }

    /// <summary>Verifies finite arrange remeasurement does not re-invalidate an arranging ancestor chain.</summary>
    [Fact]
    public void Layout_WhenStarGridIsInsidePercentWindow_SettlesEveryLayoutPhase()
    {
        // Arrange
        var grid = new Grid { ColumnSpacing = 1 };
        grid.Columns.Add(Track.Auto());
        grid.Columns.Add(Track.Star(1, minimum: 12));
        grid.Columns.Add(Track.Auto());
        grid.Children.Add(new ProbeControl(new Size(8, 1)));
        var field = new ProbeControl(new Size(20, 1));
        Grid.SetColumn(field, 1);
        grid.Children.Add(field);
        var action = new ProbeControl(new Size(10, 1));
        Grid.SetColumn(action, 2);
        grid.Children.Add(action);
        var window = new Window
        {
            Content = grid,
            Width = Length.Percent(75),
            MinWidth = 48,
            MaxWidth = 72
        };
        var canvas = new Overlay { Children = { window } };
        var engine = new LayoutEngine();

        // Act
        engine.Layout(canvas, new Size(90, 20));

        // Assert
        (canvas.Pending & (Invalidation.Measure | Invalidation.Arrange)).ShouldBe(Invalidation.None);
        (window.Pending & (Invalidation.Measure | Invalidation.Arrange)).ShouldBe(Invalidation.None);
        (grid.Pending & (Invalidation.Measure | Invalidation.Arrange)).ShouldBe(Invalidation.None);

        // Act resize
        engine.Layout(canvas, new Size(110, 20));

        // Assert resized settlement
        (canvas.Pending & (Invalidation.Measure | Invalidation.Arrange)).ShouldBe(Invalidation.None);
        (window.Pending & (Invalidation.Measure | Invalidation.Arrange)).ShouldBe(Invalidation.None);
        (grid.Pending & (Invalidation.Measure | Invalidation.Arrange)).ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies automatic rows remeasure wrapped content with finite width and unbounded height.</summary>
    [Fact]
    public void Layout_WhenStarColumnNarrowsWrappedText_GrowsAutoRowForEveryLine()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Star(1));
        var text = new ControlText("One two three four five six") { Overflow = Overflow.Wrap };
        grid.Children.Add(text);

        new LayoutEngine().Layout(grid, new Size(10, 10));

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
        var engine = new LayoutEngine();

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

        new LayoutEngine().Layout(grid, new Size(3, 1));

        first.Bounds.Width.ShouldBe(0);
        second.Bounds.Width.ShouldBe(0);
        first.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
        second.Bounds.Right.ShouldBeLessThanOrEqualTo(3);
    }

    /// <summary>Verifies a Grid accepts a negative visual origin while its scrolling owner is offset.</summary>
    [Fact]
    public void Arrange_WhenGridIsScrolled_UsesNegativeVisualOrigin()
    {
        var grid = new Grid { Height = Length.Cells(20) };
        grid.Children.Add(new ProbeControl(new Size(3, 1)));
        var host = new Stack { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        host.Children.Add(grid);
        var engine = new LayoutEngine();

        engine.Layout(host, new Size(5, 5));
        _ = host.ScrollBy(0, 2);
        engine.Layout(host, new Size(5, 5));

        grid.Bounds.Y.ShouldBe(-2);
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
        new LayoutEngine().Layout(grid, new Size(2, 1));
        using Frame frame = new(new Size(2, 1));

        grid.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("B");
    }

    /// <summary>Verifies an empty grid with auto tracks measures to zero.</summary>
    [Fact]
    public void Layout_WhenEmptyWithAutoTracks_MeasuresToZero()
    {
        var grid = new Grid { ColumnSpacing = 5, RowSpacing = 5 };
        grid.Rows.Add(Track.Auto());
        grid.Columns.Add(Track.Auto());

        new LayoutEngine().Layout(grid, new Size(20, 20));

        grid.DesiredSize.ShouldBe(default);
    }

    /// <summary>Verifies equal-weight star columns divide width evenly.</summary>
    [Fact]
    public void Layout_WhenAllStarColumnsHaveEqualWeight_DividesEvenly()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Star(1));
        grid.Columns.Add(Track.Star(1));
        grid.Columns.Add(Track.Star(1));
        var a = Child(column: 0);
        var b = Child(column: 1);
        var c = Child(column: 2);
        grid.Children.Add(a);
        grid.Children.Add(b);
        grid.Children.Add(c);

        new LayoutEngine().Layout(grid, new Size(12, 1));

        a.Bounds.Width.ShouldBe(4);
        b.Bounds.Width.ShouldBe(4);
        c.Bounds.Width.ShouldBe(4);
        a.Bounds.X.ShouldBe(0);
        b.Bounds.X.ShouldBe(4);
        c.Bounds.X.ShouldBe(8);
    }

    /// <summary>Verifies percentage column tracks with min/max clamp correctly.</summary>
    [Fact]
    public void Layout_WhenPercentTrackHasMinMax_ClampsToConstraints()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Percent(80, minimum: 5, maximum: 10));
        grid.Columns.Add(Track.Star(1));
        var percent = Child(column: 0);
        var star = Child(column: 1);
        grid.Children.Add(percent);
        grid.Children.Add(star);

        new LayoutEngine().Layout(grid, new Size(20, 1));

        percent.Bounds.Width.ShouldBeGreaterThanOrEqualTo(5);
        percent.Bounds.Width.ShouldBeLessThanOrEqualTo(10);
    }

    /// <summary>Verifies children with margins offset inside their cell.</summary>
    [Fact]
    public void Layout_WhenChildHasMargin_OffsetsInsideCell()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Cells(10));
        grid.Rows.Add(Track.Cells(5));
        var child = new ProbeControl(new Size(3, 2)) { Margin = new Thickness(1) };
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(20, 10));

        child.Bounds.X.ShouldBe(1);
        child.Bounds.Y.ShouldBe(1);
        child.Bounds.Right.ShouldBeLessThanOrEqualTo(9);
        child.Bounds.Bottom.ShouldBeLessThanOrEqualTo(4);
    }

    /// <summary>Verifies a child with no explicit Width/Height and default alignment still fills
    /// its cell, matching the behavior every existing Grid consumer already relies on.</summary>
    [Fact]
    public void Layout_WhenChildHasNoExplicitSizeOrAlignment_FillsTheCell()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Star(1));
        grid.Rows.Add(Track.Cells(1));
        var child = new ProbeControl(new Size(2, 1));
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(20, 1));

        child.Bounds.ShouldBe(new Rect(0, 0, 20, 1));
    }

    /// <summary>Verifies an explicit Width paired with a non-default HorizontalAlignment
    /// participates instead of being silently overridden while MinWidth/MaxWidth kept working.</summary>
    [Fact]
    public void Layout_WhenChildHasExplicitWidthAndCenterAlignment_HonorsBoth()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Star(1));
        grid.Rows.Add(Track.Cells(1));
        var child = new ProbeControl(new Size(2, 1))
        {
            Width = Length.Cells(4),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(20, 1));

        child.Bounds.ShouldBe(new Rect(8, 0, 4, 1));
    }

    /// <summary>Verifies an explicit Height paired with a non-default VerticalAlignment
    /// participates the same way the width axis does.</summary>
    [Fact]
    public void Layout_WhenChildHasExplicitHeightAndBottomAlignment_HonorsBoth()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Cells(4));
        grid.Rows.Add(Track.Star(1));
        var child = new ProbeControl(new Size(4, 2))
        {
            Height = Length.Cells(2),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(4, 10));

        child.Bounds.ShouldBe(new Rect(0, 8, 4, 2));
    }

    /// <summary>Verifies a 2x2 grid with row and column spacing places cells correctly.</summary>
    [Fact]
    public void Layout_WhenTwoByTwoWithSpacing_PlacesCellsCorrectly()
    {
        var grid = new Grid { RowSpacing = 1, ColumnSpacing = 2 };
        grid.Rows.Add(Track.Cells(3));
        grid.Rows.Add(Track.Cells(3));
        grid.Columns.Add(Track.Cells(5));
        grid.Columns.Add(Track.Cells(5));
        var topLeft = Child(column: 0);
        var topRight = Child(column: 1);
        var bottomLeft = Child(column: 0);
        Grid.SetRow(bottomLeft, 1);
        var bottomRight = Child(column: 1);
        Grid.SetRow(bottomRight, 1);
        grid.Children.Add(topLeft);
        grid.Children.Add(topRight);
        grid.Children.Add(bottomLeft);
        grid.Children.Add(bottomRight);

        new LayoutEngine().Layout(grid, new Size(20, 10));

        topLeft.Bounds.ShouldBe(new Rect(0, 0, 5, 3));
        topRight.Bounds.ShouldBe(new Rect(7, 0, 5, 3));
        bottomLeft.Bounds.ShouldBe(new Rect(0, 4, 5, 3));
        bottomRight.Bounds.ShouldBe(new Rect(7, 4, 5, 3));
    }

    /// <summary>Verifies hidden children still contribute to track sizing.</summary>
    [Fact]
    public void Layout_WhenChildIsHidden_StillContributesToTrackSize()
    {
        var grid = new Grid();
        grid.Columns.Add(Track.Auto());
        var visible = new ProbeControl(new Size(3, 1));
        var hidden = new ProbeControl(new Size(8, 1)) { Visibility = Visibility.Hidden };
        grid.Children.Add(visible);
        grid.Children.Add(hidden);

        new LayoutEngine().Layout(grid, new Size(20, 5));

        grid.DesiredSize.Width.ShouldBe(8);
    }

    /// <summary>Verifies border and padding on grid reserve edges before track resolution.</summary>
    [Fact]
    public void Layout_WhenGridHasBorderAndPadding_TracksArrangeInsideContentBox()
    {
        var grid = new Grid
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(2, 1)
        };
        grid.Columns.Add(Track.Star(1));
        var child = new ProbeControl(new Size(3, 2));
        grid.Children.Add(child);

        new LayoutEngine().Layout(grid, new Size(20, 10));

        child.Bounds.X.ShouldBeGreaterThanOrEqualTo(3);
        child.Bounds.Y.ShouldBeGreaterThanOrEqualTo(2);
    }

    /// <summary>Verifies MaxWidth alone (no explicit Width) still caps the filled cell and hands
    /// Center alignment the resulting slack - the pre-existing, already-correct contract that
    /// resolving only an explicit Width/Height, established above, must not disturb.</summary>
    [Fact]
    public void Layout_WhenChildSetsOnlyMaxWidthWithCenterAlignment_CapsTheFilledCellAndCenters()
    {
        var child = new ProbeControl(new Size(2, 1))
        {
            MaxWidth = 4,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var grid = new Grid { Columns = { Track.Star(1) }, Children = { child } };

        new LayoutEngine().Layout(grid, new Size(20, 1));

        child.Bounds.ShouldBe(new Rect(8, 0, 4, 1));
    }

    /// <summary>Verifies a Percent row inside an AutoScroll Grid resolves against the viewport,
    /// not the scroll extent it itself contributes to - otherwise a full-sized Auto sibling row
    /// raises the extent and crushes the Percent row toward zero.</summary>
    [Fact]
    public void Layout_WhenAutoScrollGridHasPercentRow_ResolvesAgainstViewportNotExtent()
    {
        var grid = new Grid
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Rows = { Track.Percent(50), Track.Auto() }
        };
        var percentChild = new ProbeControl(new Size(4, 1));
        var tall = new ProbeControl(new Size(4, 30));
        Grid.SetRow(tall, 1);
        grid.Children.Add(percentChild);
        grid.Children.Add(tall);

        new LayoutEngine().Layout(grid, new Size(10, 10));

        grid.Viewport.ShouldBe(new Size(10, 10));
        percentChild.Bounds.Height.ShouldBe(5);
    }

    private static ProbeControl Child(int column, string content = "")
    {
        var child = new ProbeControl(new Size(1, 1)) { Content = content.AsMemory() };
        Grid.SetColumn(child, column);
        return child;
    }

    private const int _caseCount = 10_000;
    private const int _seed = 0x051A_475A;

    /// <summary>Verifies mixed tracks, spans, visibility, and tiny sizes remain exact.</summary>
    [Fact]
    public void Layout_WhenGridsAreRandomized_RemainsDeterministicAndContained()
    {
        var seeds = new Random(_seed);

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var caseSeed = seeds.Next();
            var first = CreateGrid(new Random(caseSeed), out var firstColumns, out var firstRows);
            var second = CreateGrid(new Random(caseSeed), out var secondColumns, out var secondRows);
            var size = new Size(caseSeed % 31, caseSeed / 31 % 17);
            var context = $"seed=0x{_seed:X8}, case={sample}, caseSeed={caseSeed}, size={size}";
            var engine = new LayoutEngine();

            engine.Layout(first, size);
            engine.Layout(second, size);

            AssertAxis(firstColumns, first.ColumnSpacing, size.Width, horizontal: true, context);
            AssertAxis(firstRows, first.RowSpacing, size.Height, horizontal: false, context);
            AssertContained(first, context);
            first.Bounds.ShouldBe(second.Bounds, context);
            first.DesiredSize.ShouldBe(second.DesiredSize, context);
            firstColumns.Select(static child => child.Bounds)
                .ShouldBe(secondColumns.Select(static child => child.Bounds), context);
            firstRows.Select(static child => child.Bounds)
                .ShouldBe(secondRows.Select(static child => child.Bounds), context);
        }
    }

    private static void AssertAxis(
        List<ProbeControl> controls,
        int spacing,
        int available,
        bool horizontal,
        string context)
    {
        var consumed = 0;
        var previousEnd = 0;

        for (var index = 0; index < controls.Count; index++)
        {
            var bounds = controls[index].Bounds;
            var origin = horizontal ? bounds.X : bounds.Y;
            var extent = horizontal ? bounds.Width : bounds.Height;
            origin.ShouldBeGreaterThanOrEqualTo(previousEnd, context);
            extent.ShouldBeGreaterThanOrEqualTo(0, context);

            if (index > 0)
            {
                var gap = origin - previousEnd;
                gap.ShouldBeLessThanOrEqualTo(spacing, context);
                consumed += gap;
            }

            consumed += extent;
            previousEnd = origin + extent;
        }

        consumed.ShouldBe(available, context);
    }

    private static void AssertContained(Grid grid, string context)
    {
        foreach (var child in grid.Children)
        {
            child.Bounds.Width.ShouldBeGreaterThanOrEqualTo(0, context);
            child.Bounds.Height.ShouldBeGreaterThanOrEqualTo(0, context);
            child.Bounds.X.ShouldBeGreaterThanOrEqualTo(grid.Bounds.X, context);
            child.Bounds.Y.ShouldBeGreaterThanOrEqualTo(grid.Bounds.Y, context);
            child.Bounds.Right.ShouldBeLessThanOrEqualTo(grid.Bounds.Right, context);
            child.Bounds.Bottom.ShouldBeLessThanOrEqualTo(grid.Bounds.Bottom, context);
        }
    }

    private static Grid CreateGrid(
        Random random,
        out List<ProbeControl> columnControls,
        out List<ProbeControl> rowControls)
    {
        var columnCount = random.Next(1, 6);
        var rowCount = random.Next(1, 6);
        var grid = new Grid { ColumnSpacing = random.Next(0, 4), RowSpacing = random.Next(0, 4) };

        AddDefinitions(random, grid.Columns, columnCount);
        AddDefinitions(random, grid.Rows, rowCount);
        columnControls = [];
        rowControls = [];

        for (var column = 0; column < columnCount; column++)
        {
            var child = new ProbeControl(new Size(random.Next(0, 9), random.Next(0, 5)));
            Grid.SetColumn(child, column);
            grid.Children.Add(child);
            columnControls.Add(child);
        }

        for (var row = 0; row < rowCount; row++)
        {
            var child = new ProbeControl(new Size(random.Next(0, 9), random.Next(0, 5)));
            Grid.SetRow(child, row);
            grid.Children.Add(child);
            rowControls.Add(child);
        }

        var extraCount = random.Next(0, 6);

        for (var index = 0; index < extraCount; index++)
        {
            var row = random.Next(rowCount);
            var column = random.Next(columnCount);
            var child = new ProbeControl(new Size(random.Next(0, 17), random.Next(0, 9)))
            {
                Visibility = random.Next(0, 5) == 0 ? Visibility.Collapsed : Visibility.Visible
            };
            Grid.SetRow(child, row);
            Grid.SetColumn(child, column);
            Grid.SetRowSpan(child, random.Next(1, rowCount - row + 1));
            Grid.SetColumnSpan(child, random.Next(1, columnCount - column + 1));
            grid.Children.Add(child);
        }

        return grid;
    }

    private static void AddDefinitions(Random random, TrackCollection collection, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (index == count - 1)
            {
                collection.Add(Track.Star(random.NextDouble() + 0.01));
                continue;
            }

            var minimum = random.Next(0, 4);
            var maximum = random.Next(minimum, minimum + 10);
            var track = random.Next(0, 4) switch
            {
                0 => Track.Auto(minimum, maximum),
                1 => Track.Cells(random.Next(0, 13), minimum, maximum),
                2 => Track.Percent(random.NextDouble() * 100, minimum, maximum),
                _ => Track.Star(random.NextDouble() + 0.01, minimum, maximum)
            };
            collection.Add(track);
        }
    }

    /// <summary>Verifies row and column collections are permanent and invalidate real mutations.</summary>
    [Fact]
    public void Definitions_WhenCollectionMutates_InvalidatesMeasureOncePerChange()
    {
        var grid = new Grid();

        _ = grid.Rows.ShouldNotBeNull();
        _ = grid.Columns.ShouldNotBeNull();
        grid.Rows.ShouldBeEmpty();
        grid.Columns.ShouldBeEmpty();
        grid.Clear(Invalidation.All);
        grid.Rows.Add(Track.Auto());
        grid.Pending.ShouldBe(Invalidation.All);
        grid.Clear(Invalidation.All);
        grid.Rows[0] = Track.Auto();
        grid.Pending.ShouldBe(Invalidation.None);
        grid.Rows.Remove(Track.Cells(4)).ShouldBeFalse();
        grid.Pending.ShouldBe(Invalidation.None);
        grid.Rows.Clear();
        grid.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies Grid and placement defaults are deterministic.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasDocumentedGridDefaults()
    {
        var grid = new Grid();
        var child = new ProbeControl();

        grid.RowSpacing.ShouldBe(0);
        grid.ColumnSpacing.ShouldBe(0);
        Grid.GetRow(child).ShouldBe(0);
        Grid.GetColumn(child).ShouldBe(0);
        Grid.GetRowSpan(child).ShouldBe(1);
        Grid.GetColumnSpan(child).ShouldBe(1);
    }

    /// <summary>Verifies origins, spans, and spacing reject invalid values before mutation.</summary>
    [Fact]
    public void Setter_WhenValueIsInvalid_ThrowsBeforeMutation()
    {
        var grid = new Grid();
        var child = new ProbeControl();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => grid.RowSpacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => grid.ColumnSpacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetRow(child, -1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetColumn(child, -1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetRowSpan(child, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetColumnSpan(child, 0));

        grid.RowSpacing.ShouldBe(0);
        grid.ColumnSpacing.ShouldBe(0);
        Grid.GetRow(child).ShouldBe(0);
        Grid.GetColumn(child).ShouldBe(0);
        Grid.GetRowSpan(child).ShouldBe(1);
        Grid.GetColumnSpan(child).ShouldBe(1);
    }

    /// <summary>Verifies owned placement must fit explicit or implicit definitions.</summary>
    [Fact]
    public void SetPlacement_WhenChildIsOwned_ValidatesCommittedTrackRange()
    {
        var grid = new Grid();
        var child = new ProbeControl();
        grid.Children.Add(child);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetRow(child, 1));
        grid.Rows.Add(Track.Auto());
        grid.Rows.Add(Track.Auto());
        Grid.SetRow(child, 1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetRowSpan(child, 2));

        Grid.GetRow(child).ShouldBe(1);
        Grid.GetRowSpan(child).ShouldBe(1);
    }

    /// <summary>Verifies attached placement mutation invalidates measure and requires affinity.</summary>
    [Fact]
    public async Task SetRow_WhenChildIsAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var grid = new Grid();
        grid.Rows.Add(Track.Auto());
        grid.Rows.Add(Track.Auto());
        var child = new ProbeControl();
        grid.Children.Add(child);
        grid.Clear(Invalidation.All);
        Grid.SetRow(child, 1);
        grid.Pending.ShouldBe(Invalidation.All);
        await dispatcher.InvokeAsync(
            () => grid.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => Grid.SetRow(child, 0));

        Grid.GetRow(child).ShouldBe(1);
    }

    /// <summary>Verifies off-thread definition mutation fails before changing the collection.</summary>
    [Fact]
    public async Task Add_WhenGridIsAttachedOffThread_ThrowsBeforeCollectionMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var grid = new Grid();
        await dispatcher.InvokeAsync(() =>
        {
            grid.Attach(dispatcher);
            grid.Clear(Invalidation.All);
        }, TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => grid.Rows.Add(Track.Auto()));

        grid.Rows.ShouldBeEmpty();
        grid.Pending.ShouldBe(Invalidation.None);
    }
}
