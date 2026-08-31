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
}
