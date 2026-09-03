// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Notifications;

/// <summary>Verifies Toast's detached defaults, ownership, mutation, validation, style, and layout contract.</summary>
public sealed class ToastTests
{
    /// <summary>Verifies a new Toast carries the documented presentation and timing defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasExpectedDefaults()
    {
        using var toast = new Toast();

        toast.Content.ShouldBeNull();
        toast.Title.ShouldBeNull();
        toast.Adornment.ShouldBeNull();
        toast.Position.ShouldBe(ToastPosition.TopRight);
        toast.Animation.ShouldBe(ToastAnimation.Fade);
        toast.AnimationDuration.ShouldBe(TimeSpan.FromMilliseconds(200));
        toast.DisplayDuration.ShouldBe(TimeSpan.FromSeconds(5));
        toast.IsDismissible.ShouldBeTrue();
        toast.IsOpen.ShouldBeFalse();
        toast.AnimationProgress.ShouldBe(0);
        toast.FadeInDuration.ShouldBe(TimeSpan.Zero);
        toast.FadeOutDuration.ShouldBe(TimeSpan.Zero);
        toast.FadeProgress.ShouldBe(0);
        toast.IsFocusable.ShouldBeTrue();
    }

    /// <summary>Verifies arbitrary content is the retained caller-replaceable child and style remains open-ended.</summary>
    [Fact]
    public void ContentAndStyle_WhenAssigned_RoundTripThroughPublicContract()
    {
        using var toast = new Toast();
        var content = new ControlText("Deployment complete");
        var custom = ToastStyle.Trace with { Padding = new Thickness(horizontal: 2, vertical: 1) };

        toast.Content = content;
        toast.Title = "Build";
        toast.Adornment = new Affix("✓", "v", SemanticColor.Success);
        toast.Style = custom;

        toast.Content.ShouldBeSameAs(content);
        content.Parent.ShouldBeSameAs(toast);
        toast.Title.ShouldBe("Build");
        toast.Adornment.ShouldBe(new Affix("✓", "v", SemanticColor.Success));
        toast.Style.ShouldBe(custom);
        toast.ActualStyle.ShouldBe(custom);
    }

    /// <summary>Verifies undefined enum values fail without replacing the previous values.</summary>
    [Fact]
    public void PositionAndAnimation_WhenUndefined_ThrowBeforeMutation()
    {
        using var toast = new Toast();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => toast.Position = (ToastPosition) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => toast.Animation = (ToastAnimation) 99);

        toast.Position.ShouldBe(ToastPosition.TopRight);
        toast.Animation.ShouldBe(ToastAnimation.Fade);
    }

    /// <summary>Verifies a title remains a single printable terminal line and invalid text does not replace it.</summary>
    [Fact]
    public void Title_WhenControlClusterIsAssigned_ThrowsBeforeMutation()
    {
        using var toast = new Toast { Title = "Original" };

        _ = Should.Throw<ArgumentException>(() => toast.Title = "First\nSecond");

        toast.Title.ShouldBe("Original");
    }

    /// <summary>Verifies animation permits an immediate transition while display timing is positive or persistent.</summary>
    [Fact]
    public void Durations_WhenInvalid_ThrowBeforeMutation()
    {
        using var toast = new Toast();

        toast.AnimationDuration = TimeSpan.Zero;
        toast.DisplayDuration = Timeout.InfiniteTimeSpan;
        _ = Should.Throw<ArgumentOutOfRangeException>(() => toast.AnimationDuration = TimeSpan.FromMilliseconds(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => toast.DisplayDuration = TimeSpan.Zero);

        toast.AnimationDuration.ShouldBe(TimeSpan.Zero);
        toast.DisplayDuration.ShouldBe(Timeout.InfiniteTimeSpan);
    }

    /// <summary>Verifies title, adornment, dismissibility, and padding participate in intrinsic measurement.</summary>
    [Fact]
    public void Measure_WhenTitleAdornmentAndContentExist_ReservesCompleteGeometry()
    {
        using var toast = new Toast
        {
            Title = "Build",
            Adornment = new Affix("!"),
            Content = new ProbeControl(new Size(10, 2)),
            Style = ToastStyle.Info with
            {
                Padding = new Thickness(1),
                ContentGap = 1,
                AdornmentGap = 1
            }
        };

        new LayoutEngine().Layout(toast, new Size(40, 10));

        toast.DesiredSize.Width.ShouldBe(14);
        toast.DesiredSize.Height.ShouldBe(8);
    }

    /// <summary>Verifies dismissing a detached or already-closed Toast is harmless.</summary>
    [Fact]
    public void Dismiss_WhenToastIsClosed_IsIdempotent()
    {
        using var toast = new Toast();

        toast.Dismiss();
        toast.Dismiss();

        toast.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a Right-anchored Toast's constrained X offset stays exact at
    /// int.MinValue instead of wrapping when the presentation content area's own bounds already
    /// sit at the integer coordinate limit, mirroring ButtonTests's End-alignment boundary pin
    /// for the identical Right-edge-minus-width arithmetic shape.</summary>
    [Fact]
    public void Constrain_WhenContentBoundsXIsIntMinValueAndPositionIsTopRight_StaysExactInsteadOfWrapping()
    {
        var overlay = new Overlay();
        using var toast = new Toast
        {
            Position = ToastPosition.TopRight,
            Content = new ProbeControl(new Size(5, 3)),
            Style = ToastStyle.Info with { Padding = default, ContentGap = 0, AdornmentGap = 0 }
        };
        new LayoutEngine().Layout(toast, new Size(40, 10));
        var coordinator = ToastCoordinator.Present(overlay, toast);
        var contentBounds = new Rect(int.MinValue, 0, toast.DesiredSize.Width, toast.DesiredSize.Height);

        var slot = coordinator.Constrain(toast, contentBounds);

        slot.X.ShouldBe(int.MinValue);
        slot.Width.ShouldBe(toast.DesiredSize.Width);
    }

    /// <summary>Verifies a Bottom-anchored Toast's constrained Y offset stays exact at
    /// int.MinValue instead of wrapping when the presentation content area's own bounds already
    /// sit at the integer coordinate limit, mirroring ButtonTests's End-alignment boundary pin
    /// for the analogous Bottom-edge-minus-inward-minus-height arithmetic shape.</summary>
    [Fact]
    public void Constrain_WhenContentBoundsYIsIntMinValueAndPositionIsBottomRight_StaysExactInsteadOfWrapping()
    {
        var overlay = new Overlay();
        using var toast = new Toast
        {
            Position = ToastPosition.BottomRight,
            Content = new ProbeControl(new Size(5, 3)),
            Style = ToastStyle.Info with { Padding = default, ContentGap = 0, AdornmentGap = 0 }
        };
        new LayoutEngine().Layout(toast, new Size(40, 10));
        var coordinator = ToastCoordinator.Present(overlay, toast);
        var contentBounds = new Rect(0, int.MinValue, toast.DesiredSize.Width, toast.DesiredSize.Height);

        var slot = coordinator.Constrain(toast, contentBounds);

        slot.Y.ShouldBe(int.MinValue);
        slot.Height.ShouldBe(toast.DesiredSize.Height);
    }
}
