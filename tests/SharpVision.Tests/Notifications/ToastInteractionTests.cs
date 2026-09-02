// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Notifications;

/// <summary>Proves Toast dissolve progression, stacking and re-slotting, host resizes, dismissal
/// mid-animation, presentation-time option locks, and owner validation on mounted surfaces.</summary>
public sealed class ToastInteractionTests
{
    #region Animation

    /// <summary>Verifies the fade reveals exactly floor(progress * area) cells at each step, so the
    /// dissolve advances uniformly with the clock instead of jumping ahead and stalling.</summary>
    [Theory]
    [InlineData(20, 1)]
    [InlineData(100, 7)]
    [InlineData(160, 12)]
    [InlineData(200, 15)]
    public async Task AdvanceAsync_WhenFadeIsPartlyElapsed_RevealsExactlyProportionalCellCountAsync(
        int elapsedMilliseconds,
        int expectedRevealed)
    {
        // Arrange - a 5x3 border box, so the area is 15 cells and every cell is non-blank once shown.
        var clock = new ManualTimeProvider();
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.BottomRight);
        toast.Animation = ToastAnimation.Fade;
        toast.AnimationDuration = TimeSpan.FromMilliseconds(200);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show fading Toast");
        CountRevealed(surface, toast.Bounds).ShouldBe(0);

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(elapsedMilliseconds), "advance the fade");

        // Assert
        toast.Bounds.ShouldBe(new Rect(15, 7, 5, 3));
        CountRevealed(surface, toast.Bounds).ShouldBe(expectedRevealed);
    }

    /// <summary>Verifies a cell revealed at an earlier fade step stays revealed at every later
    /// step, so the dissolve is monotonic rather than flickering.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenFadeProgresses_NeverHidesARevealedCellAgainAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        toast.Animation = ToastAnimation.Fade;
        toast.AnimationDuration = TimeSpan.FromMilliseconds(300);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show fading Toast");
        var revealed = new HashSet<Point>();

        // Act / Assert
        for (var step = 0; step < 15; step++)
        {
            await surface.AdvanceAsync(TimeSpan.FromMilliseconds(20), $"fade step {step}");
            var current = RevealedCells(surface, toast.Bounds);
            revealed.ShouldBeSubsetOf(current);
            current.Count.ShouldBe(step + 1);
            revealed = current;
        }

        revealed.Count.ShouldBe(15);
        toast.AnimationProgress.ShouldBe(1);
    }

    /// <summary>Verifies dismissing while the entrance animation is in flight removes the toast
    /// immediately, releases its timers, and leaves nothing to tick afterwards.</summary>
    [Fact]
    public async Task Dismiss_WhenEntranceIsInFlight_RemovesImmediatelyAndStopsAnimatingAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.BottomRight);
        toast.Animation = ToastAnimation.SlideLeft;
        toast.AnimationDuration = TimeSpan.FromMilliseconds(200);
        toast.DisplayDuration = TimeSpan.FromMilliseconds(50);
        var closed = 0;
        toast.Closed += (_, _) => closed++;
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show sliding Toast");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "slide halfway");
        toast.AnimationProgress.ShouldBe(0.5, tolerance: 0.001);
        var midBounds = toast.Bounds;

        // Act
        await surface.UpdateAsync(toast.Dismiss, "dismiss mid-entrance");

        // Assert
        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
        toast.AnimationProgress.ShouldBe(0);
        closed.ShouldBe(1);
        root.Children.ShouldNotContain(toast);
        surface.Cell(new Point(midBounds.X, midBounds.Y)).Text.ShouldBe(" ");

        // Act - the entrance and display timers must not fire after removal
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(500), "advance past every timer");

        // Assert
        toast.IsOpen.ShouldBeFalse();
        toast.AnimationProgress.ShouldBe(0);
        closed.ShouldBe(1);

        // Act - the toast remains reusable
        await surface.UpdateAsync(() => toast.Show(root), "show the same Toast again");

        // Assert
        toast.IsOpen.ShouldBeTrue();
        toast.AnimationProgress.ShouldBe(0);
    }

    #endregion

    #region Stacking

    /// <summary>Verifies each position keeps its own stack: a toast at another position never
    /// pushes this one inward.</summary>
    [Fact]
    public async Task Show_WhenPositionsDiffer_StacksIndependentlyPerPositionAsync()
    {
        // Arrange
        var root = new Overlay();
        using var left = CreateToast("one", ToastPosition.TopLeft);
        using var right = CreateToast("two", ToastPosition.TopRight);
        using var secondRight = CreateToast("thr", ToastPosition.TopRight);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(
            () =>
            {
                left.Show(root);
                right.Show(root);
                secondRight.Show(root);
            },
            "show Toasts at two positions");

        // Assert
        left.Bounds.ShouldBe(new Rect(0, 0, 5, 3));
        secondRight.Bounds.ShouldBe(new Rect(15, 0, 5, 3));
        right.Bounds.ShouldBe(new Rect(15, 4, 5, 3));
    }

    /// <summary>Verifies dismissing one of several stacked toasts moves the survivors back toward
    /// the edge, whichever slot was vacated.</summary>
    [Fact]
    public async Task Dismiss_WhenOneOfSeveralStackedToastsCloses_ReslotsSurvivorsTowardTheEdgeAsync()
    {
        // Arrange
        var root = new Overlay();
        using var first = CreateToast("one", ToastPosition.TopRight);
        using var second = CreateToast("two", ToastPosition.TopRight);
        using var third = CreateToast("thr", ToastPosition.TopRight);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 14),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                first.Show(root);
                second.Show(root);
                third.Show(root);
            },
            "show three stacked Toasts");
        third.Bounds.ShouldBe(new Rect(15, 0, 5, 3));
        second.Bounds.ShouldBe(new Rect(15, 4, 5, 3));
        first.Bounds.ShouldBe(new Rect(15, 8, 5, 3));

        // Act - vacate the middle slot
        await surface.UpdateAsync(second.Dismiss, "dismiss the middle Toast");

        // Assert
        third.Bounds.ShouldBe(new Rect(15, 0, 5, 3));
        first.Bounds.ShouldBe(new Rect(15, 4, 5, 3));
        surface.Cell(new Point(16, 9)).Text.ShouldBe(" ");
        surface.Cell(new Point(16, 5)).Text.ShouldBe("o");

        // Act - vacate the edge slot
        await surface.UpdateAsync(third.Dismiss, "dismiss the edge Toast");

        // Assert
        first.Bounds.ShouldBe(new Rect(15, 0, 5, 3));
        surface.Cell(new Point(16, 1)).Text.ShouldBe("o");
        surface.Cell(new Point(16, 5)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies a stack that outgrows the host clamps its innermost toast to the far
    /// edge instead of arranging it outside the host.</summary>
    [Theory]
    [InlineData(ToastPosition.TopLeft, 7)]
    [InlineData(ToastPosition.BottomLeft, 0)]
    public async Task Show_WhenStackOutgrowsHost_ClampsInnermostToastInsideHostAsync(
        ToastPosition position,
        int expectedInnermostY)
    {
        // Arrange - four 3-row toasts with one-cell spacing need 15 rows; the host has 10.
        var root = new Overlay();
        var toasts = Enumerable.Range(0, 4).Select(index => CreateToast($"t{index:00}", position)).ToArray();
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(
            () =>
            {
                foreach (var toast in toasts)
                {
                    toast.Show(root);
                }
            },
            "show four stacked Toasts");

        // Assert - the surface disposes the presented toasts with the tree
        toasts[0].Bounds.Y.ShouldBe(expectedInnermostY);
        toasts[0].Bounds.Height.ShouldBe(3);

        foreach (var toast in toasts)
        {
            toast.Bounds.Y.ShouldBeGreaterThanOrEqualTo(0);
            toast.Bounds.Bottom.ShouldBeLessThanOrEqualTo(10);
        }
    }

    #endregion

    #region Host resize

    /// <summary>Verifies an edge-anchored toast follows the host edge when the host grows and is
    /// clamped to the host when it shrinks below the toast's own size.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHostChanges_ReslotsAndClampsShownToastAsync()
    {
        // Arrange
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.BottomRight);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast");
        toast.Bounds.ShouldBe(new Rect(15, 7, 5, 3));

        // Act
        await surface.ResizeAsync(new Size(30, 12));

        // Assert
        toast.Bounds.ShouldBe(new Rect(25, 9, 5, 3));
        toast.SurfaceBounds.ShouldBe(toast.Bounds);
        surface.Cell(new Point(26, 10)).Text.ShouldBe("o");

        // Act
        await surface.ResizeAsync(new Size(4, 4));

        // Assert
        toast.IsOpen.ShouldBeTrue();
        toast.Bounds.ShouldBe(new Rect(0, 1, 4, 3));
        surface.Cell(new Point(1, 2)).Text.ShouldBe("o");
    }

    #endregion

    #region Presentation locks

    /// <summary>Verifies the presentation options are locked while a toast is open: each setter
    /// throws, leaves its value unchanged, and the toast stays presented.</summary>
    [Theory]
    [InlineData("Position")]
    [InlineData("Animation")]
    [InlineData("AnimationDuration")]
    [InlineData("DisplayDuration")]
    [InlineData("Show")]
    public async Task PresentationOptions_WhenChangedWhileOpen_ThrowAndLeaveToastUnchangedAsync(string member)
    {
        // Arrange
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast");

        // Act
        var failure = await surface.Application.Dispatcher.InvokeAsync(
            () => Record.Exception(() =>
            {
                switch (member)
                {
                    case "Position":
                        toast.Position = ToastPosition.BottomRight;
                        break;
                    case "Animation":
                        toast.Animation = ToastAnimation.SlideLeft;
                        break;
                    case "AnimationDuration":
                        toast.AnimationDuration = TimeSpan.FromMilliseconds(50);
                        break;
                    case "DisplayDuration":
                        toast.DisplayDuration = TimeSpan.FromMilliseconds(50);
                        break;
                    default:
                        toast.Show(root);
                        break;
                }
            }),
            TestContext.Current.CancellationToken);

        // Assert
        _ = failure.ShouldBeOfType<InvalidOperationException>();
        toast.Position.ShouldBe(ToastPosition.TopLeft);
        toast.Animation.ShouldBe(ToastAnimation.Fade);
        toast.AnimationDuration.ShouldBe(TimeSpan.Zero);
        toast.DisplayDuration.ShouldBe(Timeout.InfiniteTimeSpan);
        toast.IsOpen.ShouldBeTrue();
        toast.Parent.ShouldBeSameAs(root);
        toast.Bounds.ShouldBe(new Rect(0, 0, 5, 3));
    }

    /// <summary>Verifies IsDismissible is the one presentation member that stays mutable while
    /// open: clearing it removes the rendered close glyph and Escape no longer dismisses.</summary>
    [Fact]
    public async Task IsDismissible_WhenClearedWhileOpen_RemovesCloseGlyphAndIgnoresEscapeAsync()
    {
        // Arrange
        var root = new Overlay();
        using var toast = new Toast
        {
            Position = ToastPosition.TopLeft,
            AnimationDuration = TimeSpan.Zero,
            DisplayDuration = Timeout.InfiniteTimeSpan,
            Title = "Notice",
            Style = ToastStyle.Info with { Padding = default, ContentGap = 0, AdornmentGap = 0 },
            Content = new Button { Text = "Act" }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show dismissible Toast");
        var inner = toast.ActualStyle.Padding.Deflate(toast.ContentBounds);
        surface.Cell(new Point(inner.Right - 1, inner.Y)).Text.ShouldBe("■");

        // Act
        await surface.UpdateAsync(() => toast.IsDismissible = false, "clear IsDismissible while open");

        // Assert
        toast.IsOpen.ShouldBeTrue();
        inner = toast.ActualStyle.Padding.Deflate(toast.ContentBounds);
        surface.Cell(new Point(inner.Right - 1, inner.Y)).Text.ShouldNotBe("■");

        // Act
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(OwnedTree.Find<Button>(toast).ShouldNotBeNull()).ShouldBeTrue(),
            "focus the Toast content");
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        toast.IsOpen.ShouldBeTrue();
    }

    #endregion

    #region Owner validation

    /// <summary>Verifies showing through an owner with no presentation host is rejected before
    /// any presentation state changes, and the toast remains reusable with a hosted owner.</summary>
    [Fact]
    public async Task Show_WhenOwnerHasNoPresentationHost_ThrowsAndLeavesToastClosedAsync()
    {
        // Arrange - a detached non-Overlay control resolves neither a Screen nor an Overlay host.
        var root = new Overlay();
        var detached = new Button { Text = "Alone" };
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);

        // Act
        var failure = await surface.Application.Dispatcher.InvokeAsync(
            () => Record.Exception(() => toast.Show(detached)),
            TestContext.Current.CancellationToken);

        // Assert
        var rejection = failure.ShouldBeOfType<ArgumentException>();
        rejection.ParamName.ShouldBe("owner");
        rejection.Message.ShouldContain("presentation host");
        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();

        // Act
        await surface.UpdateAsync(() => toast.Show(root), "show through the attached owner");

        // Assert
        toast.IsOpen.ShouldBeTrue();
        toast.Bounds.ShouldBe(new Rect(0, 0, 5, 3));
    }

    #endregion

    private static Toast CreateToast(string content, ToastPosition position) => new()
    {
        Position = position,
        AnimationDuration = TimeSpan.Zero,
        DisplayDuration = Timeout.InfiniteTimeSpan,
        IsDismissible = false,
        Style = ToastStyle.Info with { Padding = default, ContentGap = 0, AdornmentGap = 0 },
        Content = new ControlText(content)
    };

    private static int CountRevealed(ComponentSurface surface, Rect bounds) => RevealedCells(surface, bounds).Count;

    private static HashSet<Point> RevealedCells(ComponentSurface surface, Rect bounds)
    {
        var revealed = new HashSet<Point>();

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (surface.Cell(new Point(x, y)).Text != " ")
                {
                    _ = revealed.Add(new Point(x, y));
                }
            }
        }

        return revealed;
    }
}
