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

    private static Stack CreateViewport(ControlBase projection) => new()
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Both,
        ShowScrollBars = ShowScrollBars.WhenNeeded,
        Children = { projection },
    };

    private static void Layout(ControlBase control)
    {
        control.Measure(new Constraint(10, 5));
        control.Arrange(new Rect(0, 0, 10, 5), widthResolved: true, heightResolved: true);
    }
}
