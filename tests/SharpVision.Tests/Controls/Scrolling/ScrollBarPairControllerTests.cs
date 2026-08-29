// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Scrolling;

/// <summary>Verifies the shared generated scrollbar-pair mechanics.</summary>
public sealed class ScrollBarPairControllerTests
{
    /// <summary>Verifies a vertical reservation can induce horizontal overflow during the bounded
    /// two-axis feedback loop.</summary>
    [Fact]
    public void Resolve_WhenOneRailInducesTheOther_ReservesBothRails()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var (_, viewport, horizontal, vertical) = controller.Resolve(
            new Size(5, 5),
            new Size(5, 6),
            ScrollBars.Both,
            ScrollBarVisibility.Auto,
            ScrollBarVisibility.Auto);

        // Assert
        horizontal.ShouldBeTrue();
        vertical.ShouldBeTrue();
        viewport.ShouldBe(new Size(4, 4));
    }

    /// <summary>Verifies synchronizing a smaller range pre-clamps rail values without forwarding
    /// controller-authored value changes back into the host.</summary>
    [Fact]
    public void Synchronize_WhenMaximumShrinks_PreclampsWithoutForwardingCallbacks()
    {
        // Arrange
        var controller = CreateController();
        controller.EnsureBars();
        var forwarded = 0;
        controller.HorizontalValueChanged += (_, _) => forwarded++;
        controller.Synchronize(10, 10, 3, 3, 10, 10, 1, 1, 3, 3);

        // Act
        controller.Synchronize(2, 2, 6, 6, 2, 2, 1, 1, 6, 6);

        // Assert
        controller.Horizontal!.Maximum.ShouldBe(2);
        controller.Horizontal.Value.ShouldBe(2);
        controller.Vertical!.Maximum.ShouldBe(2);
        controller.Vertical.Value.ShouldBe(2);
        forwarded.ShouldBe(0);
    }

    /// <summary>Verifies a newer synchronization requested from a rail property callback replaces
    /// the outer request after its current publication completes.</summary>
    [Fact]
    public void Synchronize_WhenRailObserverReenters_AppliesNewestCompleteConfiguration()
    {
        // Arrange
        var controller = CreateController();
        controller.EnsureBars();
        controller.Horizontal!.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ScrollBar.Maximum) && controller.Horizontal.Maximum == 5)
            {
                controller.Synchronize(8, 9, 4, 3, 7, 6, 2, 3, 4, 5);
            }
        };

        // Act
        controller.Synchronize(5, 5, 2, 2, 4, 4, 1, 1, 2, 2);

        // Assert
        controller.Horizontal.Maximum.ShouldBe(8);
        controller.Horizontal.Value.ShouldBe(7);
        controller.Horizontal.SmallChange.ShouldBe(2);
        controller.Vertical!.Maximum.ShouldBe(9);
        controller.Vertical.Value.ShouldBe(6);
        controller.Vertical.SmallChange.ShouldBe(3);
    }

    /// <summary>Verifies zero-sized hosts keep both generated rail rectangles bounded and empty.</summary>
    [Fact]
    public void Arrange_WhenBoundsAreEmpty_KeepsRailGeometryEmpty()
    {
        // Arrange
        var controller = CreateController();
        var (_, viewport, _, _) = controller.Resolve(
            default,
            new Size(10, 10),
            ScrollBars.Both,
            ScrollBarVisibility.Always,
            ScrollBarVisibility.Always);

        // Act
        controller.Arrange(default, new Rect(0, 0, viewport.Width, viewport.Height), true, true);

        // Assert
        controller.Horizontal!.Bounds.ShouldBe(default);
        controller.Vertical!.Bounds.ShouldBe(default);
    }

    private static ScrollBarPairController CreateController() => new(
        new ProbeControl(),
        "test-scroll-bars",
        InvalidationImpact.Arrange,
        isFocusable: false,
        horizontalIncludesCorner: false,
        static _ => { });
}
