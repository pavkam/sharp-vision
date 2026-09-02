// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

/// <summary>Proves Tooltip show and hide triggers - keyboard focus, focus loss, anchor presses,
/// hover re-entry, and anchor unavailability - through mounted surfaces with a deterministic clock.</summary>
public sealed class TooltipInteractionTests
{
    /// <summary>Verifies keyboard focus alone, with no pointer movement, shows the tooltip after
    /// the show delay.</summary>
    [Fact]
    public async Task Focus_WhenAnchorGainsKeyboardFocus_ShowsAfterShowDelayAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(anchor).ShouldBeTrue(), "focus the anchor");

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
        tooltip.IsShowTimerRunning.ShouldBeTrue();

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(9), "remain inside the show delay");
        tooltip.IsOpen.ShouldBeFalse();
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "reach the show delay");

        // Assert
        tooltip.IsOpen.ShouldBeTrue();
        tooltip.SurfaceBounds.Y.ShouldBe(anchor.Bounds.Bottom);
        surface.Cell(new Point(tooltip.SurfaceBounds.X + 1, tooltip.SurfaceBounds.Y + 1)).Text.ShouldBe("S");
        surface.ShouldHaveFocus(anchor);
        tooltip.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies losing keyboard focus hides a shown tooltip immediately, bypassing the
    /// hide delay entirely.</summary>
    [Fact]
    public async Task Focus_WhenAnchorLosesFocusWhileShown_HidesImmediatelyAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        tooltip.HideDelay = TimeSpan.FromSeconds(10);
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(anchor).ShouldBeTrue(), "focus the anchor");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show the tooltip");
        tooltip.IsOpen.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(other);
        tooltip.IsOpen.ShouldBeFalse();
        tooltip.SurfaceBounds.ShouldBe(default);
        tooltip.IsShowTimerRunning.ShouldBeFalse();
        tooltip.HasSurfaceRelayoutSubscription.ShouldBeFalse();
    }

    /// <summary>Verifies losing focus during the show delay cancels the pending show.</summary>
    [Fact]
    public async Task Focus_WhenAnchorLosesFocusDuringShowDelay_CancelsPendingShowAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(anchor).ShouldBeTrue(), "focus the anchor");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(5), "wait inside the show delay");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(20), "let the cancelled delay elapse");

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
        tooltip.IsShowTimerRunning.ShouldBeFalse();
    }

    /// <summary>Verifies a primary press on the anchor hides a shown tooltip immediately and
    /// leaves no timer running.</summary>
    [Fact]
    public async Task Pointer_WhenAnchorIsPressedWhileShown_HidesImmediatelyAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        tooltip.HideDelay = TimeSpan.FromSeconds(10);
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show the tooltip by hover");
        tooltip.IsOpen.ShouldBeTrue();

        // Act
        await surface.Pointer.PressAsync();

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
        tooltip.SurfaceBounds.ShouldBe(default);
        tooltip.IsShowTimerRunning.ShouldBeFalse();

        // Act - releasing and staying on the anchor does not bring the tooltip back on its own
        await surface.Pointer.ReleaseAsync();
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "linger on the anchor after the click");

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies re-entering the anchor during the hide delay cancels the pending hide
    /// rather than restarting it, so the tooltip stays shown past the original deadline.</summary>
    [Fact]
    public async Task Pointer_WhenAnchorIsReenteredDuringHideDelay_CancelsPendingHideAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        tooltip.HideDelay = TimeSpan.FromMilliseconds(10);
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show the tooltip by hover");
        tooltip.IsOpen.ShouldBeTrue();

        // Act
        await surface.Pointer.LeaveAsync();
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(5), "wait inside the hide delay");
        tooltip.IsOpen.ShouldBeTrue();
        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(30), "pass the original hide deadline");

        // Assert
        tooltip.IsOpen.ShouldBeTrue();
        tooltip.IsShowTimerRunning.ShouldBeFalse();

        // Act
        await surface.Pointer.LeaveAsync();
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "let a fresh hide delay elapse");

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies the tooltip is passive: moving the pointer from the anchor onto the
    /// tooltip's own cells counts as leaving the anchor and hides it after the hide delay.</summary>
    [Fact]
    public async Task Pointer_WhenMovedFromAnchorOntoTooltipSurface_HidesAfterHideDelayAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        tooltip.HideDelay = TimeSpan.FromMilliseconds(10);
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show the tooltip by hover");
        var inside = new Point(tooltip.SurfaceBounds.X + 1, tooltip.SurfaceBounds.Y + 1);

        // Act
        await surface.Pointer.MoveToAsync(inside);

        // Assert - the tooltip is never a hover target of its own
        tooltip.IsPointerOver.ShouldBeFalse();
        anchor.IsPointerOver.ShouldBeFalse();
        tooltip.IsOpen.ShouldBeTrue();

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "let the hide delay elapse");

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies Escape never closes a tooltip and never consumes the key.</summary>
    [Fact]
    public async Task Escape_WhenTooltipIsShown_LeavesItOpenAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(anchor).ShouldBeTrue(), "focus the anchor");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show the tooltip");
        tooltip.IsOpen.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        tooltip.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(anchor);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies hiding the anchor while its tooltip is shown closes the tooltip and
    /// releases its timers and relayout subscription.</summary>
    [Fact]
    public async Task Visibility_WhenAnchorIsHiddenWhileShown_ClosesTooltipAndReleasesTimersAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show the tooltip by hover");
        tooltip.IsOpen.ShouldBeTrue();
        var bounds = tooltip.SurfaceBounds;

        // Act
        await surface.UpdateAsync(() => anchor.Visibility = Visibility.Hidden, "hide the anchor");

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
        tooltip.SurfaceBounds.ShouldBe(default);
        tooltip.IsShowTimerRunning.ShouldBeFalse();
        tooltip.HasSurfaceRelayoutSubscription.ShouldBeFalse();
        surface.Cell(new Point(bounds.X, bounds.Y)).Text.ShouldBe(" ");

        // Act - showing the anchor again must not resurrect a passive hint nobody is hovering
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "advance past every delay");
        await surface.UpdateAsync(() => anchor.Visibility = Visibility.Visible, "show the anchor again");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "advance past every delay again");

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
        tooltip.SurfaceBounds.ShouldBe(default);
        surface.Cell(new Point(bounds.X, bounds.Y)).Text.ShouldBe(" ");

        // Act - a fresh hover still works
        await surface.Pointer.MoveToAsync(other);
        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show the tooltip by a fresh hover");

        // Assert
        tooltip.IsOpen.ShouldBeTrue();
        tooltip.SurfaceBounds.ShouldBe(bounds);
    }

    /// <summary>Verifies a disabled anchor never shows its tooltip through hover, because a
    /// disabled control receives no pointer enter.</summary>
    [Fact]
    public async Task Pointer_WhenAnchorIsDisabled_NeverShowsTooltipAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        anchor.IsEnabled = false;
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "advance well past the show delay");

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
        tooltip.IsShowTimerRunning.ShouldBeFalse();
    }

    /// <summary>Verifies clearing the tooltip while its show delay is pending stops the timer,
    /// so a later tick can never present a tooltip that no longer belongs to the anchor.</summary>
    [Fact]
    public async Task ClearTooltip_WhenShowDelayIsPending_StopsTimerAndNeverShowsAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(anchor);
        tooltip.IsShowTimerRunning.ShouldBeTrue();

        // Act
        await surface.UpdateAsync(() => Tooltip.ClearTooltip(anchor), "clear the tooltip mid-delay");

        // Assert
        Tooltip.GetTooltip(anchor).ShouldBeNull();
        tooltip.IsShowTimerRunning.ShouldBeFalse();
        tooltip.HasShowTimer.ShouldBeFalse();

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "advance past the show delay");

        // Assert
        tooltip.IsOpen.ShouldBeFalse();
        OwnedTree.FindAll<Popup>(anchor).ShouldBeEmpty();
    }

    /// <summary>Verifies a tooltip never participates in Tab traversal: focus moves from the
    /// anchor straight to the next control while the tooltip is shown.</summary>
    [Fact]
    public async Task Tab_WhenTooltipIsShown_SkipsTooltipInTraversalAsync()
    {
        // Arrange
        var (anchor, tooltip, other) = CreateAnchoredTooltip();
        var clock = new ManualTimeProvider();
        var root = new Overlay { Children = { anchor, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            clock,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(anchor);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(anchor).ShouldBeTrue(), "focus the anchor");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show the tooltip");
        tooltip.IsOpen.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(other);
        tooltip.IsFocused.ShouldBeFalse();
        tooltip.ContainsFocus.ShouldBeFalse();
    }

    /// <summary>Verifies replacing text content with rich content and then assigning Text again
    /// creates a fresh text body rather than resurrecting the original one.</summary>
    [Fact]
    public async Task Text_WhenAssignedAfterRichContent_CreatesFreshTextBodyAsync()
    {
        // Arrange
        var anchor = new Button { Text = "Anchor", Width = Length.Cells(8), Height = Length.Cells(1) };
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        var originalBody = tooltip.Content.ShouldNotBeNull();
        var rich = new Button { Text = "Rich" };

        // Act
        Tooltip.SetContent(anchor, rich);

        // Assert
        tooltip.Content.ShouldBeSameAs(rich);
        tooltip.Text = "Ignored";
        tooltip.Text.ShouldBe("Ignored");
        tooltip.Content.ShouldBeSameAs(rich);

        // Act
        Tooltip.SetContent(anchor, new ControlText("Placeholder"));
        tooltip.Content = null;
        tooltip.Text = "Again";

        // Assert
        var replacement = tooltip.Content.ShouldNotBeNull();
        replacement.ShouldNotBeSameAs(originalBody);
        replacement.ShouldBeOfType<ControlText>().Content.ShouldBe("Again");
        await Task.CompletedTask;
    }

    private static (Button Anchor, Tooltip Tooltip, Button Other) CreateAnchoredTooltip()
    {
        var anchor = new Button
        {
            Text = "Anchor",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(1)
        };
        Overlay.SetLeft(anchor, Length.Cells(2));
        Overlay.SetTop(anchor, Length.Cells(1));
        var other = new Button
        {
            Text = "Other",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(1)
        };
        Overlay.SetLeft(other, Length.Cells(12));
        Overlay.SetTop(other, Length.Cells(7));
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        tooltip.ShowDelay = TimeSpan.FromMilliseconds(10);
        tooltip.HideDelay = TimeSpan.FromMilliseconds(10);
        return (anchor, tooltip, other);
    }
}
