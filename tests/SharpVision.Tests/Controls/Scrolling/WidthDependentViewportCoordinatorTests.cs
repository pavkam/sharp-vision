// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Scrolling;

/// <summary>Verifies bounded and exception-safe width-dependent viewport transactions.</summary>
public sealed class WidthDependentViewportCoordinatorTests
{
    /// <summary>Verifies an oscillating projection fails after the documented fixed attempt count
    /// rather than returning a transitional width as if it had settled.</summary>
    [Fact]
    public void Arrange_WhenProjectionNeverConverges_ThrowsAfterBoundedAttempts()
    {
        // Arrange
        var projection = new ProbeControl(new Size(20, 20));
        var viewport = CreateViewport(projection);
        var attempts = 0;
        var coordinator = new WidthDependentViewportCoordinator(
            viewport,
            viewport,
            projection,
            static () => true,
            static () => null,
            _ => attempts++);
        coordinator.CaptureMeasureConstraint(new Constraint(10, 5));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => coordinator.Arrange(
            new Rect(0, 0, 10, 5),
            () => Layout(viewport)));

        // Assert
        attempts.ShouldBe(WidthDependentViewportCoordinator.MaximumReconciliationAttempts);
        exception.Message.ShouldContain("did not converge");
    }

    /// <summary>Verifies a projection exception clears transaction state so a following layout can
    /// reconcile and publish normally instead of being rejected as reentrant.</summary>
    [Fact]
    public void Arrange_WhenProjectionThrows_ClearsTransactionForNextArrange()
    {
        // Arrange
        var projection = new ProbeControl(new Size(20, 20));
        var viewport = CreateViewport(projection);
        int? projectionWidth = null;
        var throwOnProjection = true;
        var coordinator = new WidthDependentViewportCoordinator(
            viewport,
            viewport,
            projection,
            static () => true,
            () => projectionWidth,
            width =>
            {
                if (throwOnProjection)
                {
                    throw new InvalidOperationException("projection failed");
                }

                projectionWidth = width;
            });
        coordinator.CaptureMeasureConstraint(new Constraint(10, 5));
        _ = Should.Throw<InvalidOperationException>(() => coordinator.Arrange(
            new Rect(0, 0, 10, 5),
            () => Layout(viewport)));

        // Act
        throwOnProjection = false;
        coordinator.Arrange(new Rect(0, 0, 10, 5), () => Layout(viewport));

        // Assert
        projectionWidth.ShouldBe(viewport.Viewport.Width);
    }

    /// <summary>Verifies a ScrollChanged subscriber that throws does not prevent delivery to later
    /// subscribers, and that the original exception is what ultimately propagates.</summary>
    [Fact]
    public void ScrollChanged_WhenSubscriberThrows_StillNotifiesLaterSubscribersAndRethrows()
    {
        // Arrange
        var projection = new ProbeControl(new Size(20, 20));
        var viewport = CreateViewport(projection);
        var coordinator = new WidthDependentViewportCoordinator(
            viewport,
            viewport,
            projection,
            static () => false,
            static () => null,
            static _ => { });
        coordinator.CaptureMeasureConstraint(new Constraint(10, 5));
        coordinator.Arrange(new Rect(0, 0, 10, 5), () => Layout(viewport));
        var expected = new InvalidOperationException("first subscriber failed");
        var secondRan = false;
        coordinator.ScrollChanged += (_, _) => throw expected;
        coordinator.ScrollChanged += (_, _) => secondRan = true;

        // Act
        var exception = Should.Throw<InvalidOperationException>(
            () => viewport.ScrollBy(0, 1, ScrollCause.Keyboard));

        // Assert
        exception.ShouldBeSameAs(expected);
        secondRan.ShouldBeTrue();
    }

    /// <summary>Verifies a projection whose height is not monotone in width - a responsive
    /// breakpoint reflow that oscillates a vertical automatic bar under the old, non-transactional
    /// resolution - now settles within the bounded attempt budget instead of throwing.</summary>
    /// <remarks>
    /// Reproduces the documented numeric 2-cycle exactly: at width 21 the probe reports the
    /// two-column height 10, which overflows the viewport height 9 and reserves a vertical bar,
    /// narrowing the viewport to width 20; at width 20 the probe reports the one-column height 8,
    /// which fits without a bar and would widen the viewport straight back to 21 under the old
    /// per-attempt resolution. Keeping the bar reservation monotone for the whole transaction stops
    /// that reservation from ever being dropped once made, so the viewport settles at width 20 and
    /// the projection converges to it.
    /// </remarks>
    [Fact]
    public void Arrange_WhenProjectionHeightIsNonMonotoneInWidth_ConvergesWithoutThrowing()
    {
        // Arrange
        const int breakpointWidth = 21;
        var projection = new WidthDependentHeightProjectionProbe(
            width => width >= breakpointWidth ? 10 : 8,
            initialWidth: breakpointWidth);
        var viewport = CreateViewport(projection, ScrollBars.Vertical);
        var coordinator = new WidthDependentViewportCoordinator(
            viewport,
            viewport,
            projection,
            static () => true,
            () => projection.ProjectedWidth,
            projection.SetProjectedWidth);
        coordinator.CaptureMeasureConstraint(new Constraint(breakpointWidth, 9));

        // Act
        Should.NotThrow(() => coordinator.Arrange(
            new Rect(0, 0, breakpointWidth, 9),
            () => Layout(viewport, new Constraint(breakpointWidth, 9), new Rect(0, 0, breakpointWidth, 9))));

        // Assert
        projection.ProjectedWidth.ShouldBe(viewport.Viewport.Width);
        viewport.Viewport.Width.ShouldBe(breakpointWidth - 1);
        VerticalBar(viewport).Visibility.ShouldBe(Visibility.Visible);
    }

    /// <summary>Verifies a monotone projection - the already-supported shape where narrowing the
    /// viewport can only ever grow or preserve overflow, never remove it - still converges with the
    /// same bar-shown-iff-needed outcome the resolution produced before reservations became
    /// transaction-scoped.</summary>
    [Theory]
    [InlineData(60, true)]
    [InlineData(20, false)]
    public void Arrange_WhenProjectionHeightIsMonotoneInWidth_ConvergesWithBarShownExactlyWhenContentOverflows(
        int totalCells,
        bool expectBarVisible)
    {
        // Arrange
        var projection = new WidthDependentHeightProjectionProbe(
            width => Math.Max(1, totalCells / width),
            initialWidth: 10);
        var viewport = CreateViewport(projection, ScrollBars.Vertical);
        var coordinator = new WidthDependentViewportCoordinator(
            viewport,
            viewport,
            projection,
            static () => true,
            () => projection.ProjectedWidth,
            projection.SetProjectedWidth);
        coordinator.CaptureMeasureConstraint(new Constraint(10, 5));

        // Act
        Should.NotThrow(() => coordinator.Arrange(
            new Rect(0, 0, 10, 5),
            () => Layout(viewport, new Constraint(10, 5), new Rect(0, 0, 10, 5))));

        // Assert
        projection.ProjectedWidth.ShouldBe(viewport.Viewport.Width);
        VerticalBar(viewport).Visibility.ShouldBe(expectBarVisible ? Visibility.Visible : Visibility.Collapsed);
    }

    /// <summary>Verifies an automatic bar reserved by one reconciliation transaction is not carried
    /// into a later, independent transaction: once the projection's content shrinks enough to fit
    /// without it, a following layout pass drops the bar instead of keeping it stuck reserved.
    /// </summary>
    [Fact]
    public void Arrange_WhenContentShrinksOnALaterArrange_DropsThePreviouslyReservedBar()
    {
        // Arrange
        var projection = new WidthDependentHeightProjectionProbe(_ => 60, initialWidth: 10);
        var viewport = CreateViewport(projection, ScrollBars.Vertical);
        var coordinator = new WidthDependentViewportCoordinator(
            viewport,
            viewport,
            projection,
            static () => true,
            () => projection.ProjectedWidth,
            projection.SetProjectedWidth);
        coordinator.CaptureMeasureConstraint(new Constraint(10, 5));
        coordinator.Arrange(
            new Rect(0, 0, 10, 5),
            () => Layout(viewport, new Constraint(10, 5), new Rect(0, 0, 10, 5)));
        VerticalBar(viewport).Visibility.ShouldBe(Visibility.Visible);

        // Act - the content shrinks well below the viewport height regardless of width, so the
        // automatic bar this reserved on the previous, independent transaction should drop rather
        // than stay stuck reserved.
        projection.HeightForWidth = _ => 2;
        viewport.InvalidateSelf(Invalidation.Measure);
        projection.InvalidateSelf(Invalidation.Measure);
        coordinator.Arrange(
            new Rect(0, 0, 10, 5),
            () => Layout(viewport, new Constraint(10, 5), new Rect(0, 0, 10, 5)));

        // Assert
        VerticalBar(viewport).Visibility.ShouldBe(Visibility.Collapsed);
        viewport.Viewport.Width.ShouldBe(10);
    }

    private static ScrollBar VerticalBar(ControlBase viewport) =>
        OwnedTree.FindAll<ScrollBar>(viewport).Single(bar => bar.Orientation == Orientation.Vertical);

    private static Stack CreateViewport(ControlBase projection, ScrollBars axes = ScrollBars.Both) => new()
    {
        AutoScroll = true,
        ScrollBars = axes,
        ShowScrollBars = ShowScrollBars.WhenNeeded,
        Children = { projection },
    };

    private static void Layout(ControlBase control)
    {
        control.Measure(new Constraint(10, 5));
        control.Arrange(new Rect(0, 0, 10, 5), widthResolved: true, heightResolved: true);
    }

    private static void Layout(ControlBase control, Constraint constraint, Rect bounds)
    {
        control.Measure(constraint);
        control.Arrange(bounds, widthResolved: true, heightResolved: true);
    }
}
