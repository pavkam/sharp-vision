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
                Visibility = random.Next(0, 4) == 0 ? Visibility.Collapsed : Visibility.Visible
            };
            wrap.Children.Add(child);
            children.Add(child);
        }

        return (wrap, children);
    }
}
