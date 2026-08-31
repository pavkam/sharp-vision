// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Wrap's validated public panel contract.</summary>
public sealed class WrapTests
{
    /// <summary>Verifies defaults and invalid setters preserve prior state.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasValidatedDefaults()
    {
        var wrap = new Wrap();

        wrap.Orientation.ShouldBe(Orientation.Horizontal);
        wrap.Spacing.ShouldBe(0);
        wrap.LineSpacing.ShouldBe(0);

        wrap.Orientation = Orientation.Vertical;
        wrap.Spacing = 1;
        wrap.LineSpacing = 2;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => wrap.Orientation = (Orientation) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => wrap.Spacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => wrap.LineSpacing = -1);

        wrap.Orientation.ShouldBe(Orientation.Vertical);
        wrap.Spacing.ShouldBe(1);
        wrap.LineSpacing.ShouldBe(2);
    }

    /// <summary>Verifies a child that cannot fit after the preceding item begins the next row.</summary>
    [Fact]
    public void Layout_WhenNextChildExceedsFiniteLane_StartsANewRow()
    {
        // Arrange
        var wrap = new Wrap { Spacing = 1 };
        var first = new ProbeControl(new Size(3, 1));
        var second = new ProbeControl(new Size(3, 2));
        wrap.Children.Add(first);
        wrap.Children.Add(second);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 8));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        second.Bounds.ShouldBe(new Rect(0, 1, 3, 2));
    }

    /// <summary>Verifies vertical packing starts the next column when the finite height is full.</summary>
    [Fact]
    public void Layout_WhenVerticalChildExceedsFiniteLane_StartsANewColumn()
    {
        // Arrange
        var wrap = new Wrap { Orientation = Orientation.Vertical, Spacing = 1 };
        var first = new ProbeControl(new Size(1, 3));
        var second = new ProbeControl(new Size(2, 3));
        wrap.Children.Add(first);
        wrap.Children.Add(second);

        // Act
        new LayoutEngine().Layout(wrap, new Size(8, 5));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 1, 3));
        second.Bounds.ShouldBe(new Rect(1, 0, 2, 3));
    }

    /// <summary>Verifies line gaps are inserted only between completed packed rows.</summary>
    [Fact]
    public void Layout_WhenRowsWrap_AppliesLineSpacingBetweenRows()
    {
        // Arrange
        var wrap = new Wrap { LineSpacing = 2 };
        var first = new ProbeControl(new Size(3, 1));
        var second = new ProbeControl(new Size(3, 1));
        wrap.Children.Add(first);
        wrap.Children.Add(second);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 8));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        second.Bounds.ShouldBe(new Rect(0, 3, 3, 1));
    }

    /// <summary>Verifies child margins consume the same primary lane as visible border boxes.</summary>
    [Fact]
    public void Layout_WhenMarginsExhaustFiniteLane_WrapsUsingOuterChildExtents()
    {
        // Arrange
        var wrap = new Wrap { Spacing = 1 };
        var first = new ProbeControl(new Size(2, 1)) { Margin = new Thickness(left: 1, top: 0, right: 0, bottom: 0) };
        var second = new ProbeControl(new Size(2, 1));
        wrap.Children.Add(first);
        wrap.Children.Add(second);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 8));

        // Assert
        first.Bounds.ShouldBe(new Rect(1, 0, 2, 1));
        second.Bounds.ShouldBe(new Rect(0, 1, 2, 1));
    }

    /// <summary>Verifies an oversized item occupies a contained row before later children pack.</summary>
    [Fact]
    public void Layout_WhenChildExceedsFiniteLane_ContainsItInItsOwnRow()
    {
        // Arrange
        var wrap = new Wrap();
        var oversized = new ProbeControl(new Size(8, 2));
        var next = new ProbeControl(new Size(2, 1));
        wrap.Children.Add(oversized);
        wrap.Children.Add(next);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 8));

        // Assert
        oversized.Bounds.ShouldBe(new Rect(0, 0, 5, 2));
        next.Bounds.ShouldBe(new Rect(0, 2, 2, 1));
    }

    /// <summary>Verifies a lane-relative percentage child consumes the complete lane and wraps.</summary>
    [Fact]
    public void Layout_WhenPercentageChildUsesFullLane_PacksItOnItsOwnRow()
    {
        // Arrange
        var wrap = new Wrap { Spacing = 1 };
        var first = new ProbeControl(new Size(3, 1));
        var fullLane = new ProbeControl(new Size(1, 1)) { Width = Length.Percent(100) };
        wrap.Children.Add(first);
        wrap.Children.Add(fullLane);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 8));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        fullLane.Bounds.ShouldBe(new Rect(0, 1, 5, 1));
    }

    /// <summary>Verifies a proportional child requests the complete finite lane without sharing it.</summary>
    [Fact]
    public void Layout_WhenProportionalChildUsesFullLane_PacksItOnItsOwnRow()
    {
        // Arrange
        var wrap = new Wrap { Spacing = 1 };
        var first = new ProbeControl(new Size(3, 1));
        var fullLane = new ProbeControl(new Size(1, 1)) { Width = Length.Star(1) };
        wrap.Children.Add(first);
        wrap.Children.Add(fullLane);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 8));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        fullLane.Bounds.ShouldBe(new Rect(0, 1, 5, 1));
    }

    /// <summary>Verifies saturated candidate arithmetic cannot place a later child past a finite lane.</summary>
    [Fact]
    public void Layout_WhenFiniteLaneIsAtMaximumExtent_StartsNewRowBeforeSaturatingPastIt()
    {
        // Arrange
        var wrap = new Wrap();
        var first = new ProbeControl(new Size(int.MaxValue, 1));
        var second = new ProbeControl(new Size(1, 1));
        wrap.Children.Add(first);
        wrap.Children.Add(second);

        // Act
        new LayoutEngine().Layout(wrap, new Size(int.MaxValue, 3));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, int.MaxValue, 1));
        second.Bounds.ShouldBe(new Rect(0, 1, 1, 1));
    }

    /// <summary>Verifies an unbounded primary measure keeps all participants on one line.</summary>
    [Fact]
    public void Measure_WhenPrimaryAxisIsUnbounded_ReportsOneNaturalLine()
    {
        // Arrange
        var wrap = new Wrap { Spacing = 1 };
        wrap.Children.Add(new ProbeControl(new Size(3, 1)));
        wrap.Children.Add(new ProbeControl(new Size(3, 2)));

        // Act
        wrap.Measure(new Constraint(width: null, height: 8));

        // Assert
        wrap.DesiredSize.ShouldBe(new Size(7, 2));
    }

    /// <summary>Verifies a child collapsed after an initial layout releases its former arranged slot.</summary>
    [Fact]
    public void Layout_WhenPreviouslyVisibleChildBecomesCollapsed_ClearsItsArrangedBounds()
    {
        // Arrange
        var wrap = new Wrap { Spacing = 1 };
        var first = new ProbeControl(new Size(2, 1));
        var second = new ProbeControl(new Size(2, 1));
        wrap.Children.Add(first);
        wrap.Children.Add(second);
        var layout = new LayoutEngine();
        layout.Layout(wrap, new Size(5, 2));

        // Act
        first.Visibility = Visibility.Collapsed;
        layout.Layout(wrap, new Size(5, 2));

        // Assert
        first.Bounds.ShouldBe(default);
        second.Bounds.ShouldBe(new Rect(0, 0, 2, 1));
    }

    /// <summary>Verifies primary-axis scrolling preserves one unbounded packed row rather than
    /// treating the viewport as a wrap lane.</summary>
    [Fact]
    public void Layout_WhenHorizontalWrapAutoScrolls_UsesAnUnboundedPrimaryLane()
    {
        // Arrange
        var wrap = new Wrap
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var first = new ProbeControl(new Size(4, 1));
        var second = new ProbeControl(new Size(4, 1));
        wrap.Children.Add(first);
        wrap.Children.Add(second);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 1));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 4, 1));
        second.Bounds.ShouldBe(new Rect(4, 0, 4, 1));
        wrap.Extent.ShouldBe(new Size(8, 1));
    }

    /// <summary>Verifies primary-axis scrolling retains its unbounded pack while percentage
    /// requests resolve against the visible viewport and contribute their full extent.</summary>
    [Fact]
    public void Layout_WhenHorizontalWrapAutoScrollsWithPercentChild_UsesViewportForPercentAndExtent()
    {
        // Arrange
        var wrap = new Wrap
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var fixedChild = new ProbeControl(new Size(5, 1)) { Width = Length.Cells(5) };
        var percentChild = new ProbeControl(new Size(1, 1)) { Width = Length.Percent(100) };
        wrap.Children.Add(fixedChild);
        wrap.Children.Add(percentChild);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 1));

        // Assert
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
        percentChild.Bounds.ShouldBe(new Rect(5, 0, 5, 1));
        wrap.Extent.ShouldBe(new Size(10, 1));
    }

    /// <summary>Verifies an always-visible opposite scrollbar reserves its rail before
    /// viewport-relative primary percentage requests are measured.</summary>
    [Fact]
    public void Layout_WhenHorizontalWrapHasOppositeAlwaysVisibleRail_UsesTheReducedViewportForPercent()
    {
        // Arrange
        var wrap = new Wrap
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden
        };
        var fixedChild = new ProbeControl(new Size(4, 1)) { Width = Length.Cells(4) };
        var percentChild = new ProbeControl(new Size(1, 1)) { Width = Length.Percent(100) };
        wrap.Children.Add(fixedChild);
        wrap.Children.Add(percentChild);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 2));

        // Assert
        wrap.Viewport.ShouldBe(new Size(4, 2));
        percentChild.Bounds.ShouldBe(new Rect(4, 0, 4, 1));
        wrap.Extent.ShouldBe(new Size(8, 1));
    }

    /// <summary>Verifies automatic opposite-rail feedback remeasures a primary percentage request
    /// against the candidate viewport after cross-axis overflow reserves that rail.</summary>
    [Fact]
    public void Layout_WhenHorizontalWrapNeedsAutomaticOppositeRail_UsesTheCandidateViewportForPercent()
    {
        // Arrange
        var wrap = new Wrap
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Auto
        };
        var fixedChild = new ProbeControl(new Size(4, 2)) { Width = Length.Cells(4) };
        var percentChild = new ProbeControl(new Size(1, 1)) { Width = Length.Percent(100) };
        wrap.Children.Add(fixedChild);
        wrap.Children.Add(percentChild);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 2));

        // Assert
        wrap.Viewport.Width.ShouldBe(4);
        percentChild.Bounds.ShouldBe(new Rect(4, 0, 4, 2));
        wrap.Extent.Width.ShouldBe(8);
    }

    /// <summary>Verifies a zero scrolling primary viewport resolves percentage requests to zero without negative geometry.</summary>
    [Fact]
    public void Layout_WhenHorizontalWrapHasZeroScrollViewport_ResolvesPercentToZero()
    {
        // Arrange
        var wrap = new Wrap { AutoScroll = true, ScrollBars = ScrollBars.Horizontal, ShowScrollBars = ShowScrollBars.Never };
        var child = new ProbeControl(new Size(1, 1)) { Width = Length.Percent(100) };
        wrap.Children.Add(child);

        // Act
        new LayoutEngine().Layout(wrap, new Size(0, 1));

        // Assert
        wrap.Viewport.ShouldBe(new Size(0, 1));
        child.Bounds.ShouldBe(new Rect(0, 0, 0, 1));
        wrap.Extent.ShouldBe(new Size(0, 1));
    }

    /// <summary>Verifies a scrolling viewport resize remeasures and repacks percentage children.</summary>
    [Fact]
    public void Layout_WhenHorizontalScrollViewportResizes_ReflowsPercentageChild()
    {
        // Arrange
        var wrap = new Wrap { AutoScroll = true, ScrollBars = ScrollBars.Horizontal, ShowScrollBars = ShowScrollBars.Never };
        var child = new ProbeControl(new Size(1, 1)) { Width = Length.Percent(100) };
        wrap.Children.Add(child);
        var layout = new LayoutEngine();
        layout.Layout(wrap, new Size(5, 1));

        // Act
        layout.Layout(wrap, new Size(7, 1));

        // Assert
        child.Bounds.ShouldBe(new Rect(0, 0, 7, 1));
        wrap.Extent.ShouldBe(new Size(7, 1));
    }

    /// <summary>Verifies a parent-resolved relative-limit measure cannot satisfy a later ordinary measure cache lookup.</summary>
    [Fact]
    public void Measure_WhenResolvedLimitBaseChangesToOrdinary_RemeasuresTheChild()
    {
        // Arrange
        var child = new ProbeControl(new Size(1, 1)) { Width = Length.Star(1) };
        child.Measure(new Constraint(3, 1), widthLimitBase: 5, heightLimitBase: null);

        // Act
        child.Measure(new Constraint(3, 1));

        // Assert
        child.MeasureConstraints.Count.ShouldBe(2);
        child.MeasureConstraints[0].ShouldBe(new Constraint(3, 1));
        child.MeasureConstraints[1].ShouldBe(new Constraint(3, 1));
    }

    /// <summary>Verifies a proportional primary-axis child retains its intrinsic request while
    /// a scrolling Wrap supplies a viewport base only to percentage semantics.</summary>
    [Fact]
    public void Layout_WhenHorizontalWrapAutoScrollsWithStarChild_PreservesItsIntrinsicExtent()
    {
        // Arrange
        var wrap = new Wrap
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var fixedChild = new ProbeControl(new Size(5, 1)) { Width = Length.Cells(5) };
        var starChild = new ProbeControl(new Size(1, 1)) { Width = Length.Star(1) };
        wrap.Children.Add(fixedChild);
        wrap.Children.Add(starChild);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 1));

        // Assert
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
        starChild.Bounds.ShouldBe(new Rect(5, 0, 1, 1));
        wrap.Extent.ShouldBe(new Size(6, 1));
    }

    /// <summary>Verifies viewport-relative limits constrain an intrinsic Star request without
    /// converting the scrolling primary lane into a finite Star allocation pool.</summary>
    [Fact]
    public void Layout_WhenScrollingStarHasPercentageMaximum_PreservesItsIntrinsicExtentBelowTheLimit()
    {
        // Arrange
        var wrap = new Wrap
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never
        };
        var child = new ProbeControl(new Size(1, 1))
        {
            Width = Length.Star(1),
            MaxWidth = Length.Percent(50)
        };
        wrap.Children.Add(child);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 1));

        // Assert
        child.Bounds.ShouldBe(new Rect(0, 0, 1, 1));
        wrap.Extent.ShouldBe(new Size(1, 1));
    }

    /// <summary>Verifies an intrinsic Star request remeasures wrapped content at a narrower
    /// viewport-relative maximum before Wrap commits its packed cross extent.</summary>
    [Fact]
    public void Layout_WhenScrollingStarHasPercentageMaximum_ReflowsItsCrossAxisAtTheCappedWidth()
    {
        // Arrange
        var wrap = new Wrap
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never
        };
        var fixedChild = new ProbeControl(new Size(5, 1)) { Width = Length.Cells(5) };
        var text = new ControlText("abcdef")
        {
            Width = Length.Star(1),
            MaxWidth = Length.Percent(50),
            Overflow = Overflow.WrapAnywhere
        };
        wrap.Children.Add(fixedChild);
        wrap.Children.Add(text);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 3));

        // Assert
        text.Bounds.ShouldBe(new Rect(5, 0, 3, 2));
        wrap.Extent.ShouldBe(new Size(8, 2));
    }

    /// <summary>Verifies fixed and automatic overflowing children retain their unbounded
    /// authored and intrinsic primary extents under horizontal scrolling.</summary>
    [Fact]
    public void Layout_WhenHorizontalWrapAutoScrollsWithFixedAndAutomaticOverflow_PreservesTheirExtents()
    {
        // Arrange
        var wrap = new Wrap
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never
        };
        var fixedChild = new ProbeControl(new Size(1, 1)) { Width = Length.Cells(7) };
        var automaticChild = new ProbeControl(new Size(6, 1));
        wrap.Children.Add(fixedChild);
        wrap.Children.Add(automaticChild);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 1));

        // Assert
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 7, 1));
        automaticChild.Bounds.ShouldBe(new Rect(7, 0, 6, 1));
        wrap.Extent.ShouldBe(new Size(13, 1));
    }

    /// <summary>Verifies the vertical primary scroll axis uses its viewport as the percentage base.</summary>
    [Fact]
    public void Layout_WhenVerticalWrapAutoScrollsWithPercentChild_UsesViewportForPercentAndExtent()
    {
        // Arrange
        var wrap = new Wrap
        {
            Orientation = Orientation.Vertical,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var fixedChild = new ProbeControl(new Size(1, 5)) { Height = Length.Cells(5) };
        var percentChild = new ProbeControl(new Size(1, 1)) { Height = Length.Percent(100) };
        wrap.Children.Add(fixedChild);
        wrap.Children.Add(percentChild);

        // Act
        new LayoutEngine().Layout(wrap, new Size(1, 5));

        // Assert
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 1, 5));
        percentChild.Bounds.ShouldBe(new Rect(0, 5, 1, 5));
        wrap.Extent.ShouldBe(new Size(1, 10));
    }

    /// <summary>Verifies AutoSize discovers the single natural unbounded row before committing size.</summary>
    [Fact]
    public void Measure_WhenWrapAutoSizes_ReportsItsNaturalPackedExtent()
    {
        // Arrange
        var wrap = new Wrap { AutoSize = true, Spacing = 1 };
        wrap.Children.Add(new ProbeControl(new Size(3, 1)));
        wrap.Children.Add(new ProbeControl(new Size(3, 2)));

        // Act
        wrap.Measure(new Constraint(5, 8));

        // Assert
        wrap.DesiredSize.ShouldBe(new Size(7, 2));
    }

    /// <summary>Verifies child minimum and maximum lengths constrain their packed requests.</summary>
    [Fact]
    public void Layout_WhenChildrenHaveMinimumAndMaximumLengths_UsesTheirConstrainedExtents()
    {
        // Arrange
        var wrap = new Wrap { Spacing = 1 };
        var minimum = new ProbeControl(new Size(1, 1)) { MinWidth = Length.Cells(3) };
        var maximum = new ProbeControl(new Size(6, 1)) { MaxWidth = Length.Cells(3) };
        wrap.Children.Add(minimum);
        wrap.Children.Add(maximum);

        // Act
        new LayoutEngine().Layout(wrap, new Size(10, 2));

        // Assert
        minimum.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        maximum.Bounds.ShouldBe(new Rect(4, 0, 3, 1));
    }

    /// <summary>Verifies panel padding and border reserve the child packing content box.</summary>
    [Fact]
    public void Layout_WhenWrapHasBorderAndPadding_ArrangesChildrenInsideItsContentBox()
    {
        // Arrange
        var wrap = new Wrap
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1)
        };
        var child = new ProbeControl(new Size(2, 1));
        wrap.Children.Add(child);

        // Act
        new LayoutEngine().Layout(wrap, new Size(8, 5));

        // Assert
        child.Bounds.ShouldBe(new Rect(2, 2, 2, 1));
    }

    /// <summary>Verifies percentage limits use the complete finite primary lane.</summary>
    [Fact]
    public void Layout_WhenChildMaximumIsPercentage_UsesTheCompletePrimaryLane()
    {
        // Arrange
        var wrap = new Wrap();
        var child = new ProbeControl(new Size(1, 1))
        {
            Width = Length.Percent(100),
            MaxWidth = Length.Percent(50)
        };
        wrap.Children.Add(child);

        // Act
        new LayoutEngine().Layout(wrap, new Size(6, 1));

        // Assert
        child.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
    }

    /// <summary>Verifies a finite lane resolves a relative maximum once against the full lane,
    /// including when the child's outer slot also carries a primary margin.</summary>
    [Fact]
    public void Layout_WhenFiniteChildHasPercentageMaximumAndMargin_DoesNotResolveItsLimitTwice()
    {
        // Arrange
        var wrap = new Wrap();
        var child = new ProbeControl(new Size(1, 1))
        {
            Width = Length.Percent(100),
            MaxWidth = Length.Percent(50),
            Margin = new Thickness(left: 1, top: 0, right: 0, bottom: 0)
        };
        wrap.Children.Add(child);

        // Act
        new LayoutEngine().Layout(wrap, new Size(5, 1));

        // Assert
        child.Bounds.ShouldBe(new Rect(1, 0, 3, 1));
    }

    /// <summary>Verifies zero and tiny constraints produce contained non-negative child bounds.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void Layout_WhenConstraintIsZeroOrTiny_ContainsEveryChild(int width, int height)
    {
        // Arrange
        var wrap = new Wrap { Spacing = 1, LineSpacing = 1 };
        var first = new ProbeControl(new Size(2, 1));
        var second = new ProbeControl(new Size(2, 1));
        wrap.Children.Add(first);
        wrap.Children.Add(second);

        // Act
        new LayoutEngine().Layout(wrap, new Size(width, height));

        // Assert
        foreach (var child in wrap.Children)
        {
            child.Bounds.Width.ShouldBeGreaterThanOrEqualTo(0);
            child.Bounds.Height.ShouldBeGreaterThanOrEqualTo(0);
            child.Bounds.Right.ShouldBeLessThanOrEqualTo(width);
            child.Bounds.Bottom.ShouldBeLessThanOrEqualTo(height);
        }
    }

    /// <summary>Verifies saturated line gaps cannot wrap a later row back into negative coordinates.</summary>
    [Fact]
    public void Layout_WhenLineSpacingSaturates_PreservesNonNegativeContainedOrigins()
    {
        // Arrange
        var wrap = new Wrap { LineSpacing = int.MaxValue };
        var first = new ProbeControl(new Size(1, 1));
        var second = new ProbeControl(new Size(1, 1));
        wrap.Children.Add(first);
        wrap.Children.Add(second);

        // Act
        new LayoutEngine().Layout(wrap, new Size(1, int.MaxValue));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 1, 1));
        second.Bounds.ShouldBe(new Rect(0, int.MaxValue, 1, 0));
    }

    /// <summary>Verifies fixed-seed mixed participants produce deterministic, contained, ordered
    /// rectangles without overlap.</summary>
    [Fact]
    public void Layout_WhenRandomizedParticipantsArePacked_ProducesDeterministicContainedRows()
    {
        // Arrange
        const int seed = 8128;
        var first = CreateRandomWrap(seed);
        var second = CreateRandomWrap(seed);
        var layout = new LayoutEngine();

        // Act
        layout.Layout(first.Wrap, new Size(12, 40));
        layout.Layout(second.Wrap, new Size(12, 40));

        // Assert
        for (var index = 0; index < first.Children.Count; index++)
        {
            var bounds = first.Children[index].Bounds;
            bounds.ShouldBe(second.Children[index].Bounds);
            bounds.Width.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Height.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Right.ShouldBeLessThanOrEqualTo(12);
            bounds.Bottom.ShouldBeLessThanOrEqualTo(40);

            if (first.Children[index].Visibility == Visibility.Collapsed)
            {
                continue;
            }

            for (var other = index + 1; other < first.Children.Count; other++)
            {
                if (first.Children[other].Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                var intersection = bounds.Intersect(first.Children[other].Bounds);
                (intersection.Width == 0 || intersection.Height == 0).ShouldBeTrue();
            }
        }

        var participants = first.Children.Where(child => child.Visibility != Visibility.Collapsed).ToArray();
        for (var index = 1; index < participants.Length; index++)
        {
            participants[index].Bounds.Y.ShouldBeGreaterThanOrEqualTo(participants[index - 1].Bounds.Y);
            if (participants[index].Bounds.Y == participants[index - 1].Bounds.Y)
            {
                participants[index].Bounds.X.ShouldBeGreaterThan(participants[index - 1].Bounds.X);
            }
        }
    }

    /// <summary>Verifies the fixed-seed packing invariants also hold on the vertical primary axis.</summary>
    [Fact]
    public void Layout_WhenVerticalRandomizedParticipantsArePacked_ProducesDeterministicContainedColumns()
    {
        // Arrange
        const int seed = 8128;
        var first = CreateRandomWrap(seed);
        var second = CreateRandomWrap(seed);
        first.Wrap.Orientation = Orientation.Vertical;
        second.Wrap.Orientation = Orientation.Vertical;
        var layout = new LayoutEngine();

        // Act
        layout.Layout(first.Wrap, new Size(40, 12));
        layout.Layout(second.Wrap, new Size(40, 12));

        // Assert
        for (var index = 0; index < first.Children.Count; index++)
        {
            var bounds = first.Children[index].Bounds;
            bounds.ShouldBe(second.Children[index].Bounds);
            bounds.Width.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Height.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Right.ShouldBeLessThanOrEqualTo(40);
            bounds.Bottom.ShouldBeLessThanOrEqualTo(12);
        }
    }

    private static (Wrap Wrap, List<ProbeControl> Children) CreateRandomWrap(int seed)
    {
        var random = new Random(seed);
        var wrap = new Wrap { Spacing = random.Next(0, 3), LineSpacing = random.Next(0, 3) };
        var children = new List<ProbeControl>();

        for (var index = 0; index < 12; index++)
        {
            var child = new ProbeControl(new Size(random.Next(1, 5), random.Next(1, 3)))
            {
                Margin = new Thickness(random.Next(0, 2), 0, random.Next(0, 2), 0),
                Visibility = random.Next(0, 4) == 0 ? Visibility.Collapsed : Visibility.Visible,
                Width = CreateRandomWidth(random, index)
            };
            wrap.Children.Add(child);
            children.Add(child);
        }

        return (wrap, children);
    }

    private static Length CreateRandomWidth(Random random, int index) => (index % 4) switch
    {
        0 => Length.Auto,
        1 => Length.Cells(random.Next(1, 5)),
        2 => Length.Percent(random.Next(10, 51)),
        _ => Length.Star(random.Next(1, 3))
    };
}
