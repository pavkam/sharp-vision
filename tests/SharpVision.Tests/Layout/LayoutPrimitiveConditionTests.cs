// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

using SharpVision.Controls.Scrolling;

/// <summary>Verifies small layout and scrolling primitives: saturating enumeration, constraint
/// and length formatting, attached-property invalidation mapping, scroll event argument
/// validation, track-resolution input validation, and viewport coordinator reentrancy.</summary>
public sealed class LayoutPrimitiveConditionTests
{
    /// <summary>Verifies the saturating sum rejects a null sequence and saturates at the integer
    /// maximum instead of wrapping.</summary>
    [Fact]
    public void SaturatingSum_WhenSequenceIsNullOrOverflows_ThrowsOrSaturates()
    {
        // Arrange
        IEnumerable<int> missing = null!;
        IEnumerable<int> overflowing = [int.MaxValue, 1, -3];

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => missing.SaturatingSum());
        overflowing.SaturatingSum().ShouldBe(int.MaxValue - 3);
        Enumerable.Empty<int>().SaturatingSum().ShouldBe(0);
    }

    /// <summary>Verifies constraint formatting prints bounded axes as numbers and unbounded axes
    /// as the infinity symbol.</summary>
    [Theory]
    [InlineData(3, 4, "3×4")]
    [InlineData(0, null, "0×∞")]
    [InlineData(null, 7, "∞×7")]
    [InlineData(null, null, "∞×∞")]
    public void Constraint_ToString_WhenAxesAreBoundedOrUnbounded_FormatsEachAxis(int? width, int? height, string expected)
    {
        // Act
        var text = new Constraint(width, height).ToString();

        // Assert
        text.ShouldBe(expected);
    }

    /// <summary>Verifies every length kind formats with its own unit suffix.</summary>
    [Fact]
    public void Length_ToString_WhenKindVaries_UsesTheKindSuffix()
    {
        // Assert
        Length.Auto.ToString().ShouldBe("Auto");
        Length.Cells(3).ToString().ShouldBe("3cells");
        Length.Percent(12.5).ToString().ShouldBe("12.5%");
        Length.Percent(50).ToString().ShouldBe("50%");
        Length.Star(2).ToString().ShouldBe("2*");
        Length.Star(1.25).ToString().ShouldBe("1.25*");
    }

    /// <summary>Verifies an attached layout property maps None, Render, and Arrange impacts onto
    /// the owner invalidation it publishes (an arrange invalidation also implies render).</summary>
    [Theory]
    [InlineData(InvalidationImpact.None, 0)]
    [InlineData(InvalidationImpact.Arrange, 3)]
    [InlineData(InvalidationImpact.Render, 1)]
    public void AttachedLayoutProperty_WhenImpactIsSet_InvalidatesTheOwningParentAccordingly(
        InvalidationImpact impact,
        int expectedInvalidation)
    {
        var expected = (Invalidation) expectedInvalidation;
        // Arrange
        AttachedLayoutProperty<Dock, int> property = new(0, impact);
        var dock = new Dock();
        var child = new ProbeControl(new Size(1, 1));
        dock.Children.Add(child);
        new LayoutEngine().Layout(dock, new Size(4, 4));
        dock.Clear(Invalidation.All);

        // Act
        property.Set(child, 5);

        // Assert
        property.Get(child).ShouldBe(5);
        dock.Pending.ShouldBe(expected);
    }

    /// <summary>Verifies an undefined invalidation impact is rejected at construction.</summary>
    [Fact]
    public void AttachedLayoutProperty_WhenImpactIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AttachedLayoutProperty<Dock, int>(0, (InvalidationImpact) 99));

        // Assert
        exception.ParamName.ShouldBe("impact");
    }

    /// <summary>Verifies scroll change arguments reject a negative previous or committed offset,
    /// naming the offending parameter.</summary>
    [Theory]
    [InlineData(-1, 0, 0, 0, "previousOffset")]
    [InlineData(0, -1, 0, 0, "previousOffset")]
    [InlineData(0, 0, -1, 0, "offset")]
    [InlineData(0, 0, 0, -1, "offset")]
    public void ScrollChangedEventArgs_WhenAnOffsetIsNegative_ThrowsNamingTheParameter(
        int previousX,
        int previousY,
        int x,
        int y,
        string parameter)
    {
        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ScrollChangedEventArgs(
            new Point(previousX, previousY),
            new Point(x, y),
            new Size(4, 4),
            new Size(2, 2),
            ScrollCause.Programmatic));

        // Assert
        exception.ParamName.ShouldBe(parameter);
    }

    /// <summary>Verifies track resolution rejects input spans of unequal length and a comparable
    /// maximum below its minimum before writing any output.</summary>
    [Fact]
    public void Tracks_Resolve_WhenInputsDisagree_ThrowsBeforeWritingDestination()
    {
        // Arrange
        Length[] lengths = [Length.Cells(2), Length.Star(1)];
        int[] automatic = [0, 0];
        var destination = new int[] { -1, -1 };

        // Act and assert mismatched lengths
        _ = Should.Throw<ArgumentException>(() => Tracks.Resolve(10, lengths, automatic, [0], [int.MaxValue, int.MaxValue], destination));
        _ = Should.Throw<ArgumentException>(() => Tracks.Resolve(10, lengths, [0], [0, 0], [int.MaxValue, int.MaxValue], destination));
        destination.ShouldBe([-1, -1]);

        // Act and assert a maximum below its comparable minimum
        var exception = Should.Throw<ArgumentException>(() => Tracks.Resolve(
            10,
            lengths,
            automatic,
            [Length.Cells(5), Length.Cells(0)],
            [Length.Cells(3), null],
            destination));
        exception.ParamName.ShouldBe("maximum");
        destination.ShouldBe([-1, -1]);

        // Act a percent maximum against a cells minimum is not comparable and resolves
        Tracks.Resolve(10, lengths, automatic, [Length.Cells(5), Length.Cells(0)], [Length.Percent(10), null], destination);

        // Assert
        destination[0].ShouldBe(5);
        destination[1].ShouldBe(5);
    }

    /// <summary>Verifies a projection callback that re-enters Arrange is rejected, and the
    /// transaction state is cleared so the next Arrange proceeds normally.</summary>
    [Fact]
    public void WidthDependentViewportCoordinator_WhenArrangeReenters_ThrowsThenRecovers()
    {
        // Arrange
        var projection = new ProbeControl(new Size(20, 20));
        var viewport = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Children = { projection }
        };
        int? projectionWidth = null;
        WidthDependentViewportCoordinator? coordinator = null;
        var reenter = true;
        coordinator = new WidthDependentViewportCoordinator(
            viewport,
            viewport,
            projection,
            static () => true,
            () => projectionWidth,
            width =>
            {
                projectionWidth = width;

                if (reenter)
                {
                    reenter = false;
                    coordinator!.Arrange(new Rect(0, 0, 10, 5), () => Layout(viewport));
                }
            });
        coordinator.CaptureMeasureConstraint(new Constraint(10, 5));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => coordinator.Arrange(
            new Rect(0, 0, 10, 5),
            () => Layout(viewport)));

        // Assert
        exception.Message.ShouldContain("reentered");

        // Act a following arrange settles normally
        projectionWidth = null;
        coordinator.Arrange(new Rect(0, 0, 10, 5), () => Layout(viewport));

        // Assert
        projectionWidth.ShouldBe(viewport.Viewport.Width);
    }

    private static void Layout(ControlBase control)
    {
        control.Measure(new Constraint(10, 5));
        control.Arrange(new Rect(0, 0, 10, 5), widthResolved: true, heightResolved: true);
    }
}
