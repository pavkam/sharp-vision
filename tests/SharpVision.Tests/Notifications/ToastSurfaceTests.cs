// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Notifications;

using System.ComponentModel;

/// <summary>Proves Toast presentation, positioning, stacking, lifecycle, and input-plane behavior on mounted surfaces.</summary>
public sealed class ToastSurfaceTests
{
    /// <summary>Verifies the header renders its adornment, title, dismiss affordance, and arbitrary retained content.</summary>
    [Fact]
    public async Task Show_WhenHeaderAndContentExist_RendersCompleteToastAsync()
    {
        var root = new Overlay();
        using var toast = new Toast
        {
            Position = ToastPosition.TopLeft,
            AnimationDuration = TimeSpan.Zero,
            DisplayDuration = Timeout.InfiniteTimeSpan,
            Title = "Alert",
            Adornment = new Affix("!"),
            Style = ToastStyle.Info with
            {
                Padding = new Thickness(1),
                ContentGap = 1,
                AdornmentGap = 1
            },
            Content = new ControlText("Body")
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 10),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(root), "show complete Toast");

        var inner = toast.ActualStyle.Padding.Deflate(toast.ContentBounds);
        surface.Cell(new Point(inner.X, inner.Y)).Text.ShouldBe("!");
        surface.Cell(new Point(inner.X + 2, inner.Y)).Text.ShouldBe("A");
        surface.Cell(new Point(inner.Right - 1, inner.Y)).Text.ShouldBe("■");
        surface.Cell(new Point(inner.X, inner.Y + 2)).Text.ShouldBe("B");
    }

    /// <summary>Verifies tiny bounds preserve a complete wide adornment and drop clipped title cells before overlap.</summary>
    [Fact]
    public async Task Show_WhenWideAdornmentIsClampedToTinyHost_PreservesClusterOwnershipAsync()
    {
        var root = new Overlay();
        using var toast = new Toast
        {
            Position = ToastPosition.TopLeft,
            AnimationDuration = TimeSpan.Zero,
            DisplayDuration = Timeout.InfiniteTimeSpan,
            Title = "A",
            Adornment = new Affix("界", "#"),
            Style = ToastStyle.Info,
            Content = new ControlText("B")
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(7, 7),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(root), "show tiny wide-adornment Toast");

        var inner = toast.ActualStyle.Padding.Deflate(toast.ContentBounds);
        surface.Cell(new Point(inner.X, inner.Y)).Text.ShouldBe("界");
        surface.Cell(new Point(inner.X + 1, inner.Y)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(inner.Right - 1, inner.Y)).Continuation.ShouldBeFalse();
        surface.Cell(new Point(inner.Right - 1, inner.Y)).Text.ShouldBe("■");
    }

    /// <summary>Verifies semantic presets paint an opaque body and color the title, adornment, close affordance, and border.</summary>
    [Theory]
    [InlineData(true, SemanticColor.Error)]
    [InlineData(false, SemanticColor.Info)]
    public async Task Show_WhenSemanticStyleIsSelected_RendersAccentAsync(bool isError, SemanticColor accent)
    {
        var root = new Overlay();
        using var toast = new Toast
        {
            Position = ToastPosition.TopLeft,
            AnimationDuration = TimeSpan.Zero,
            DisplayDuration = Timeout.InfiniteTimeSpan,
            Title = "Status",
            Adornment = new Affix("!"),
            Style = isError ? ToastStyle.Error : ToastStyle.Info,
            Content = new ControlText("Body")
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 10),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(root), "show semantic Toast");

        var expected = ThemeCatalog.Dark.ResolveColor(accent);
        var expectedBackground = ThemeCatalog.Dark.ResolveColor(SemanticColor.Window);
        var inner = toast.ActualStyle.Padding.Deflate(toast.ContentBounds);
        surface.Cell(new Point(toast.ContentBounds.X, toast.ContentBounds.Y)).Style.Background.ShouldBe(expectedBackground);
        surface.Cell(new Point(toast.Bounds.X, toast.Bounds.Y)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(inner.X, inner.Y)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(inner.X + 2, inner.Y)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(inner.Right - 1, inner.Y)).Style.Foreground.ShouldBe(expected);
    }

    /// <summary>Verifies Escape and activation keys dismiss a focused dismissible Toast.</summary>
    [Theory]
    [InlineData(Code.Escape)]
    [InlineData(Code.Enter)]
    [InlineData(Code.Character)]
    public async Task Keyboard_WhenDismissibleToastIsFocused_DismissesAsync(Code code)
    {
        var root = new Overlay();
        using var toast = CreateDismissibleToast();
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                toast.Show(root);
                _ = toast.Focus();
            },
            "show and focus dismissible Toast");

        if (code == Code.Character)
        {
            await surface.Keyboard.TypeAsync(" ");
        }
        else
        {
            await surface.Keyboard.PressAsync(code);
        }

        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
    }

    /// <summary>Verifies application-modified Escape does not dismiss a focused Toast.</summary>
    [Fact]
    public async Task Keyboard_WhenEscapeIsCommandModified_LeavesToastOpenAsync()
    {
        var root = new Overlay();
        using var toast = CreateDismissibleToast();
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                toast.Show(root);
                _ = toast.Focus();
            },
            "show and focus dismissible Toast");

        await surface.Keyboard.PressAsync(Code.Escape, Modifiers.Control);

        toast.IsOpen.ShouldBeTrue();
        toast.Parent.ShouldBeSameAs(root);
    }

    /// <summary>Verifies the close glyph uses shared capture-aware pointer activation.</summary>
    [Fact]
    public async Task Pointer_WhenCloseAffordanceIsClicked_DismissesAsync()
    {
        var root = new Overlay();
        using var toast = CreateDismissibleToast();
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show dismissible Toast");
        var inner = toast.ActualStyle.Padding.Deflate(toast.ContentBounds);

        await surface.Pointer.ClickAsync(toast, new Point(inner.Right - toast.Bounds.X - 1, inner.Y - toast.Bounds.Y));

        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
    }

    /// <summary>Verifies disabling during a held close press releases capture and prevents completion.</summary>
    [Fact]
    public async Task IsEnabled_WhenDisabledDuringClosePress_CancelsInteractionAsync()
    {
        var root = new Overlay();
        using var toast = CreateDismissibleToast();
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show dismissible Toast");
        var inner = toast.ActualStyle.Padding.Deflate(toast.ContentBounds);

        await surface.Pointer.MoveToAsync(
            toast,
            new Point(inner.Right - toast.Bounds.X - 1, inner.Y - toast.Bounds.Y));
        await surface.Pointer.PressAsync();
        toast.IsPressed.ShouldBeTrue();
        surface.ShouldHaveCapture(toast);

        await surface.UpdateAsync(() => toast.IsEnabled = false, "disable held Toast");

        toast.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();
        toast.IsOpen.ShouldBeTrue();
        surface.ShouldHaveState(toast, VisualState.Disabled);
    }

    /// <summary>Verifies disabling dismissal during a held close press releases capture without closing.</summary>
    [Fact]
    public async Task IsDismissible_WhenDisabledDuringClosePress_CancelsInteractionAsync()
    {
        var root = new Overlay();
        using var toast = CreateDismissibleToast();
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show dismissible Toast");
        var inner = toast.ActualStyle.Padding.Deflate(toast.ContentBounds);

        await surface.Pointer.MoveToAsync(
            toast,
            new Point(inner.Right - toast.Bounds.X - 1, inner.Y - toast.Bounds.Y));
        await surface.Pointer.PressAsync();
        toast.IsPressed.ShouldBeTrue();
        surface.ShouldHaveCapture(toast);

        await surface.UpdateAsync(() => toast.IsDismissible = false, "disable Toast dismissal");

        toast.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();
        toast.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a failing property observer cannot strand a held close interaction.</summary>
    [Fact]
    public async Task IsDismissible_WhenObserverThrowsDuringClosePress_StillCancelsInteractionAsync()
    {
        var root = new Overlay();
        using var toast = CreateDismissibleToast();
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show dismissible Toast");
        var inner = toast.ActualStyle.Padding.Deflate(toast.ContentBounds);

        await surface.Pointer.MoveToAsync(
            toast,
            new Point(inner.Right - toast.Bounds.X - 1, inner.Y - toast.Bounds.Y));
        await surface.Pointer.PressAsync();
        toast.PropertyChanged += ThrowWhenDismissibilityChanges;

        await surface.UpdateAsync(
            () => _ = Should.Throw<InvalidOperationException>(() => toast.IsDismissible = false),
            "disable Toast dismissal with a failing observer");

        toast.IsDismissible.ShouldBeFalse();
        toast.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();
        toast.IsOpen.ShouldBeTrue();

        static void ThrowWhenDismissibilityChanges(object? sender, PropertyChangedEventArgs eventArgs)
        {
            _ = sender;

            if (eventArgs.PropertyName == nameof(Toast.IsDismissible))
            {
                throw new InvalidOperationException("Observer failure.");
            }
        }
    }

    /// <summary>Verifies a cancelled close request preserves presentation for every dismissal source.</summary>
    [Fact]
    public async Task Dismiss_WhenCloseRequestIsCancelled_LeavesToastPresentedAsync()
    {
        var root = new Overlay();
        using var toast = CreateDismissibleToast();
        toast.CloseRequested += (_, eventArgs) => eventArgs.Cancel = true;
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show vetoed Toast");

        await surface.Keyboard.PressAsync(Code.Escape);

        toast.IsOpen.ShouldBeTrue();
        toast.Parent.ShouldBeSameAs(root);
    }

    /// <summary>Verifies every movement animation projects a distinct deterministic midpoint and exact final slot.</summary>
    [Theory]
    [InlineData(ToastAnimation.SlideTop, 15, -3, 15, 2)]
    [InlineData(ToastAnimation.SlideDown, 15, 4, 15, 6)]
    [InlineData(ToastAnimation.SlideLeft, 10, 7, 13, 7)]
    [InlineData(ToastAnimation.SlideRight, 20, 7, 18, 7)]
    public async Task AdvanceAsync_WhenToastSlides_ProjectsElapsedGeometryAsync(
        ToastAnimation animation,
        int startX,
        int startY,
        int middleX,
        int middleY)
    {
        var clock = new ManualTimeProvider();
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.BottomRight);
        toast.Animation = animation;
        toast.AnimationDuration = TimeSpan.FromMilliseconds(200);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            clock,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(root), $"show {animation} Toast");
        toast.Bounds.ShouldBe(new Rect(startX, startY, 5, 3));
        toast.AnimationProgress.ShouldBe(0);

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), $"advance {animation} halfway");
        toast.Bounds.ShouldBe(new Rect(middleX, middleY, 5, 3));
        toast.AnimationProgress.ShouldBe(0.5, tolerance: 0.001);

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), $"complete {animation}");
        toast.Bounds.ShouldBe(new Rect(15, 7, 5, 3));
        toast.AnimationProgress.ShouldBe(1);
    }

    /// <summary>Verifies Expand grows around the final slot center and reaches exact final geometry.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenToastExpands_GrowsFromCenterAsync()
    {
        var clock = new ManualTimeProvider();
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.BottomRight);
        toast.Animation = ToastAnimation.Expand;
        toast.AnimationDuration = TimeSpan.FromMilliseconds(200);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            clock,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(root), "show expanding Toast");
        new Size(toast.Bounds.Width, toast.Bounds.Height).ShouldBe(default);

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "expand Toast halfway");
        toast.Bounds.ShouldBe(new Rect(16, 8, 3, 2));

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "complete Toast expansion");
        toast.Bounds.ShouldBe(new Rect(15, 7, 5, 3));
    }

    /// <summary>Verifies Fade dissolves arbitrary retained content without moving its final slot.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenToastFades_RevealsFinalCellsInPlaceAsync()
    {
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
        toast.Bounds.ShouldBe(new Rect(15, 7, 5, 3));
        surface.Cell(new Point(16, 8)).Text.ShouldBe(" ");

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "complete Toast fade");
        toast.Bounds.ShouldBe(new Rect(15, 7, 5, 3));
        surface.Cell(new Point(16, 8)).Text.ShouldBe("o");
    }

    /// <summary>Verifies automatic lifetime begins after animation instead of consuming visible time during entrance.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenDisplayDurationExpiresAfterEntrance_DismissesThenAsync()
    {
        var clock = new ManualTimeProvider();
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopRight);
        toast.AnimationDuration = TimeSpan.FromMilliseconds(200);
        toast.DisplayDuration = TimeSpan.FromMilliseconds(50);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            clock,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(root), "show timed Toast");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "complete entrance");
        toast.IsOpen.ShouldBeTrue();

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(49), "remain inside display duration");
        toast.IsOpen.ShouldBeTrue();

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "reach display timeout");
        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
    }

    /// <summary>Verifies a descendant owner resolves the Screen's private presentation Overlay.</summary>
    [Fact]
    public async Task Show_WhenOwnerBelongsToScreen_UsesPrivatePresentationPlaneAsync()
    {
        var screen = new ProbeScreen();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        await using var surface = await ComponentSurface.MountAsync(
            screen,
            new Size(20, 10),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(screen.ContentRoot), "show Toast through Screen owner");

        screen.OwnsPresentation(toast).ShouldBeTrue();
        toast.Bounds.ShouldBe(new Rect(0, 0, 5, 3));
    }

    /// <summary>Verifies each requested position resolves the Toast border box against the live host.</summary>
    [Theory]
    [InlineData(ToastPosition.TopLeft, 0, 0)]
    [InlineData(ToastPosition.TopCenter, 7, 0)]
    [InlineData(ToastPosition.TopRight, 15, 0)]
    [InlineData(ToastPosition.BottomLeft, 0, 7)]
    [InlineData(ToastPosition.BottomCenter, 7, 7)]
    [InlineData(ToastPosition.BottomRight, 15, 7)]
    public async Task Show_WhenPositionIsSelected_ArrangesAtRequestedEdgeAsync(
        ToastPosition position,
        int expectedX,
        int expectedY)
    {
        var root = new Overlay();
        using var toast = CreateToast("one", position);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(root), $"show {position} Toast");

        toast.Bounds.ShouldBe(new Rect(expectedX, expectedY, 5, 3));
        toast.SurfaceBounds.ShouldBe(toast.Bounds);
        toast.IsOpen.ShouldBeTrue();
        toast.Parent.ShouldBeSameAs(root);
    }

    /// <summary>Verifies the newest Toast remains nearest its edge and older siblings move inward with one-cell spacing.</summary>
    [Fact]
    public async Task Show_WhenPositionAlreadyHasToast_StacksNewestNearestEdgeAsync()
    {
        var root = new Overlay();
        using var older = CreateToast("old", ToastPosition.TopRight);
        using var newer = CreateToast("new", ToastPosition.TopRight);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () =>
            {
                older.Show(root);
                newer.Show(root);
            },
            "show stacked Toasts");

        newer.Bounds.ShouldBe(new Rect(15, 0, 5, 3));
        older.Bounds.ShouldBe(new Rect(15, 4, 5, 3));
    }

    /// <summary>Verifies dismissal publishes the common lifecycle and removes the identical surface from its host.</summary>
    [Fact]
    public async Task Dismiss_WhenOpen_PublishesLifecycleAndRemovesSurfaceAsync()
    {
        var order = new List<string>();
        var closeRequests = 0;
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        toast.CloseRequested += (_, _) => closeRequests++;
        toast.Closing += (_, _) => order.Add("Closing");
        toast.Closed += (_, _) => order.Add("Closed");
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast");

        await surface.UpdateAsync(toast.Dismiss, "dismiss Toast");

        order.ShouldBe(["Closing", "Closed"]);
        closeRequests.ShouldBe(1);
        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
        root.Children.ShouldNotContain(toast);
    }

    /// <summary>Verifies Closed observes final host ownership and can immediately begin a distinct
    /// presentation of the same Toast without colliding with the completed close transaction.</summary>
    [Fact]
    public async Task Dismiss_WhenClosedObserverReshowsToast_ObservesRemovalAndStartsNewPresentationAsync()
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        ControlBase? parentAtClosed = root;
        var closed = 0;
        toast.Closed += (_, _) =>
        {
            closed++;
            parentAtClosed = toast.Parent;
            toast.Show(root);
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast");

        await surface.UpdateAsync(toast.Dismiss, "dismiss and reshow Toast from Closed");

        closed.ShouldBe(1);
        parentAtClosed.ShouldBeNull();
        toast.IsOpen.ShouldBeTrue();
        toast.Parent.ShouldBeSameAs(root);
        root.Children.Count(child => ReferenceEquals(child, toast)).ShouldBe(1);
    }

    /// <summary>Verifies a CloseRequested observer can repeat the same dismissal request without
    /// recursion or duplicate lifecycle publication.</summary>
    [Fact]
    public async Task Dismiss_WhenCloseRequestedObserverReenters_CompletesOnceAsync()
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        var requested = 0;
        var closed = 0;
        toast.CloseRequested += (_, _) =>
        {
            requested++;
            toast.Dismiss();
        };
        toast.Closed += (_, _) => closed++;
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast");

        await surface.UpdateAsync(toast.Dismiss, "dismiss Toast reentrantly");

        requested.ShouldBe(1);
        closed.ShouldBe(1);
        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
    }

    /// <summary>Verifies a failing request observer releases the shared request guard so the same
    /// Toast can be dismissed by a later request.</summary>
    [Fact]
    public async Task Dismiss_WhenCloseRequestedObserverFails_AllowsLaterRetryAsync()
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        var fail = true;
        var expected = new InvalidOperationException("close request failed");
        toast.CloseRequested += (_, _) =>
        {
            if (fail)
            {
                fail = false;
                throw expected;
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            surface.UpdateAsync(toast.Dismiss, "fail Toast close request"));
        thrown.ShouldBeSameAs(expected);
        toast.IsOpen.ShouldBeTrue();
        await surface.UpdateAsync(toast.Dismiss, "retry Toast dismissal");

        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
    }

    /// <summary>Verifies Toast rolls back both common and public open state when Opened fails and
    /// can be shown again afterward.</summary>
    [Fact]
    public async Task Show_WhenOpenedObserverFails_RollsBackAndRemainsReusableAsync()
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        var expected = new InvalidOperationException("opened failed");
        void Failing(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            throw expected;
        }

        toast.Opened += Failing;
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            surface.UpdateAsync(() => toast.Show(root), "fail Toast Opened observer"));

        thrown.ShouldBeSameAs(expected);
        toast.IsOpen.ShouldBeFalse();
        toast.SurfaceBounds.ShouldBe(default);
        toast.Parent.ShouldBeNull();
        toast.Opened -= Failing;
        await surface.UpdateAsync(() => toast.Show(root), "show Toast after Opened failure");
        toast.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies an Opened subscriber may synchronously dismiss without Show restarting timers.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    public async Task Show_WhenOpenedSubscriberDismisses_CompletesClosedWithoutTimersAsync(int animationMilliseconds)
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        toast.AnimationDuration = TimeSpan.FromMilliseconds(animationMilliseconds);
        toast.Opened += (_, _) => toast.Dismiss();
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(root), "dismiss Toast from Opened");

        toast.IsOpen.ShouldBeFalse();
        toast.AnimationProgress.ShouldBe(0);
        toast.Parent.ShouldBeNull();
    }

    /// <summary>Verifies a header-only Toast publishes its arranged surface bounds without content.</summary>
    [Fact]
    public async Task Show_WhenToastHasOnlyHeader_CommitsSurfaceBoundsAsync()
    {
        var root = new Overlay();
        using var toast = new Toast
        {
            Title = "Notice",
            Position = ToastPosition.TopLeft,
            AnimationDuration = TimeSpan.Zero,
            DisplayDuration = Timeout.InfiniteTimeSpan,
            IsDismissible = false
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => toast.Show(root), "show header-only Toast");

        toast.Bounds.ShouldNotBe(default);
        toast.SurfaceBounds.ShouldBe(toast.Bounds);
    }

    /// <summary>Verifies external detachment cancels presentation state and leaves the same Toast reusable.</summary>
    [Fact]
    public async Task Remove_WhenToastIsExternallyDetached_CleansUpAndAllowsReshowAsync()
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        toast.AnimationDuration = TimeSpan.FromSeconds(1);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show externally removed Toast");

        await surface.UpdateAsync(() => _ = root.Children.Remove(toast), "externally remove Toast");

        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
        toast.AnimationProgress.ShouldBe(0);

        await surface.UpdateAsync(() => toast.Show(root), "show detached Toast again");

        toast.IsOpen.ShouldBeTrue();
        toast.Parent.ShouldBeSameAs(root);
    }

    /// <summary>Verifies hiding closes and removes the Toast so visibility restoration can present it again.</summary>
    [Fact]
    public async Task Visibility_WhenOpenToastIsHidden_RemovesAndAllowsReshowAsync()
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast before hiding");

        await surface.UpdateAsync(() => toast.Visibility = Visibility.Hidden, "hide open Toast");

        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
        root.Children.ShouldNotContain(toast);

        await surface.UpdateAsync(
            () =>
            {
                toast.Visibility = Visibility.Visible;
                toast.Show(root);
            },
            "show formerly hidden Toast");

        toast.IsOpen.ShouldBeTrue();
        toast.Parent.ShouldBeSameAs(root);
    }

    /// <summary>Verifies a failing close-state observer cannot retain a hidden Toast in its host.</summary>
    [Fact]
    public async Task Visibility_WhenCloseStateObserverThrows_RemovesAndAllowsReshowAsync()
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast before failing hide");
        toast.PropertyChanged += ThrowWhenOpenChanges;

        await surface.UpdateAsync(
            () => _ = Should.Throw<InvalidOperationException>(() => toast.Visibility = Visibility.Hidden),
            "hide Toast with a failing close-state observer");

        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
        root.Children.ShouldNotContain(toast);
        toast.PropertyChanged -= ThrowWhenOpenChanges;

        await surface.UpdateAsync(
            () =>
            {
                toast.Visibility = Visibility.Visible;
                toast.Show(root);
            },
            "show formerly hidden Toast after observer failure");

        toast.IsOpen.ShouldBeTrue();
        toast.Parent.ShouldBeSameAs(root);

        static void ThrowWhenOpenChanges(object? sender, PropertyChangedEventArgs eventArgs)
        {
            _ = sender;

            if (eventArgs.PropertyName == nameof(Toast.IsOpen))
            {
                throw new InvalidOperationException("Observer failure.");
            }
        }
    }

    /// <summary>Verifies hiding from a host ownership callback defers unlinking until publication completes.</summary>
    [Fact]
    public async Task Visibility_WhenHiddenDuringHostPublication_RemovesAndAllowsReshowAsync()
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        using var trigger = new ControlText("trigger");
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast before host publication");
        trigger.ParentChanged += HideToastWhenAttached;

        await surface.UpdateAsync(() => root.Children.Add(trigger), "hide Toast during host publication");

        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
        root.Children.ShouldNotContain(toast);

        await surface.UpdateAsync(
            () =>
            {
                toast.Visibility = Visibility.Visible;
                toast.Show(root);
            },
            "show Toast hidden during host publication");

        toast.IsOpen.ShouldBeTrue();
        toast.Parent.ShouldBeSameAs(root);

        void HideToastWhenAttached(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;

            if (trigger.Parent is not null)
            {
                toast.Visibility = Visibility.Hidden;
            }
        }
    }

    /// <summary>Verifies hiding from an idle callback explicitly schedules a later removal turn.</summary>
    [Fact]
    public async Task Visibility_WhenHiddenDuringIdle_RemovesWithoutFurtherInputAsync()
    {
        var root = new Overlay();
        using var toast = CreateToast("one", ToastPosition.TopLeft);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => toast.Show(root), "show Toast before idle callback");
        var removalIdle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? hideOnIdle = null;
        EventHandler? observeRemovalIdle = null;
        hideOnIdle = (_, _) =>
        {
            surface.Application.Idle -= hideOnIdle;
            toast.Visibility = Visibility.Hidden;
            observeRemovalIdle = (_, _) =>
            {
                surface.Application.Idle -= observeRemovalIdle;
                _ = removalIdle.TrySetResult();
            };
            surface.Application.Idle += observeRemovalIdle;
        };
        surface.Application.Idle += hideOnIdle;

        await surface.UpdateAsync(() => root.Invalidate(Invalidation.Render), "enter Toast-hiding idle callback");
        await removalIdle.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        toast.IsOpen.ShouldBeFalse();
        toast.Parent.ShouldBeNull();
        root.Children.ShouldNotContain(toast);
    }

    /// <summary>Verifies showing a Toast changes neither the active modality plane nor existing focus.</summary>
    [Fact]
    public async Task Show_WhenOwnerHasFocus_DoesNotTakeFocusOrEnterModalityAsync()
    {
        var button = new Button { Text = "Work" };
        var root = new Overlay { Children = { button } };
        using var toast = CreateToast("one", ToastPosition.TopRight);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => _ = button.Focus(), "focus workspace button");

        await surface.UpdateAsync(() => toast.Show(button), "show owned Toast");

        surface.Application.Focus.Focused.ShouldBeSameAs(button);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    private static Toast CreateToast(string content, ToastPosition position) => new()
    {
        Position = position,
        AnimationDuration = TimeSpan.Zero,
        DisplayDuration = Timeout.InfiniteTimeSpan,
        IsDismissible = false,
        Style = ToastStyle.Info with { Padding = default, ContentGap = 0, AdornmentGap = 0 },
        Content = new ControlText(content)
    };

    private static Toast CreateDismissibleToast() => new()
    {
        Position = ToastPosition.TopLeft,
        AnimationDuration = TimeSpan.Zero,
        DisplayDuration = Timeout.InfiniteTimeSpan,
        Title = "Notice",
        Style = ToastStyle.Info,
        Content = new ControlText("Body")
    };
}
