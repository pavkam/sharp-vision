// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies mounted Overlay behavior: a child-supplied position constraint clamping its
/// slot, absolute offsets changed or cleared after layout, and z-order under a one-cell host.</summary>
public sealed class OverlayInteractionTests
{
    /// <summary>Verifies a positioned child that implements the overlay position constraint has
    /// its resolved slot clamped inside the content bounds, while an unconstrained sibling with
    /// the same offset is allowed to overflow and clip.</summary>
    [Fact]
    public async Task Layout_WhenChildImplementsAPositionConstraint_ArrangesTheConstrainedSlotAsync()
    {
        // Arrange
        var constrained = new ClampedProbe();
        var unconstrained = new ProbeControl(new Size(4, 1));
        Overlay.SetLeft(constrained, Length.Cells(6));
        Overlay.SetTop(constrained, Length.Cells(0));
        Overlay.SetLeft(unconstrained, Length.Cells(6));
        Overlay.SetTop(unconstrained, Length.Cells(1));
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { constrained, unconstrained }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Assert
        constrained.Bounds.ShouldBe(new Rect(4, 0, 4, 1));
        constrained.LastContentBounds.ShouldBe(new Rect(0, 0, 8, 2));
        unconstrained.Bounds.ShouldBe(new Rect(6, 1, 4, 1));
        await surface.Pointer.MoveToAsync(overlay, new Point(4, 0));
        surface.ShouldHaveState(constrained, VisualState.IsPointerOver);

        // Act resize so the clamp moves with the content bounds
        await surface.ResizeAsync(new Size(6, 2));

        // Assert
        constrained.Bounds.ShouldBe(new Rect(2, 0, 4, 1));
        constrained.LastContentBounds.ShouldBe(new Rect(0, 0, 6, 2));
    }

    /// <summary>Verifies changing and clearing absolute offsets after layout moves the child's
    /// rendered cells and hit target, and clearing returns it to the shared slot.</summary>
    [Fact]
    public async Task Offsets_WhenChangedOrClearedAfterLayout_MoveTheChildAndItsHitTargetAsync()
    {
        // Arrange
        var child = new ControlText("X");
        Overlay.SetLeft(child, Length.Cells(1));
        Overlay.SetTop(child, Length.Cells(1));
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(4, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("    \n X  \n    ");

        // Act move to the trailing corner
        await surface.UpdateAsync(
            () =>
            {
                Overlay.SetLeft(child, null);
                Overlay.SetTop(child, null);
                Overlay.SetRight(child, Length.Cells(0));
                Overlay.SetBottom(child, Length.Cells(0));
            },
            "anchor to the trailing corner");

        // Assert
        surface.ShouldRender("    \n    \n   X");
        await surface.Pointer.MoveToAsync(overlay, new Point(3, 2));
        surface.ShouldHaveState(child, VisualState.IsPointerOver);

        // Act clear every offset
        await surface.UpdateAsync(
            () =>
            {
                Overlay.SetRight(child, null);
                Overlay.SetBottom(child, null);
            },
            "clear the offsets");

        // Assert the child returns to the shared slot origin
        surface.ShouldRender("X   \n    \n    ");
        child.Bounds.X.ShouldBe(0);
        child.Bounds.Y.ShouldBe(0);
        child.Bounds.Height.ShouldBe(3);
    }

    /// <summary>Verifies a one-cell host renders the highest z-order child and hit-tests it, and
    /// swapping z-order after layout swaps both the rendered cell and the target.</summary>
    [Fact]
    public async Task ZIndex_WhenHostIsOneCell_RendersAndTargetsTheTopLayerAsync()
    {
        // Arrange
        var lower = new ControlText("L");
        var upper = new ControlText("U");
        Overlay.SetZIndex(upper, 1);
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { upper, lower }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("U");
        await surface.Pointer.MoveToAsync(overlay, new Point(0, 0));
        surface.ShouldHaveState(upper, VisualState.IsPointerOver);
        surface.ShouldHaveState(lower, VisualState.Normal);

        // Act
        await surface.UpdateAsync(() => Overlay.SetZIndex(lower, 2), "raise the lower child");

        // Assert
        surface.ShouldRender("L");
        await surface.Pointer.MoveToAsync(overlay, new Point(0, 0));
        surface.ShouldHaveState(lower, VisualState.IsPointerOver);
        surface.ShouldHaveState(upper, VisualState.Normal);
    }

    private sealed class ClampedProbe: ControlBase, IOverlayPositionConstraint
    {
        public Rect LastContentBounds { get; private set; }

        public Rect ConstrainOverlaySlot(Rect slot, Rect contentBounds)
        {
            LastContentBounds = contentBounds;
            var x = Math.Min(slot.X, contentBounds.Right - slot.Width);
            return new Rect(Math.Max(contentBounds.X, x), slot.Y, slot.Width, slot.Height);
        }

        protected override Size MeasureOverride(Constraint constraint) => new(4, 1);
    }
}
