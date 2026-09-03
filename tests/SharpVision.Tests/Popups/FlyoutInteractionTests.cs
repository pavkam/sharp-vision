// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

/// <summary>Proves Flyout light-dismiss boundaries, vetoed dismissal, focus restoration guards,
/// and anchor changes while open through mounted terminal surfaces.</summary>
public sealed class FlyoutInteractionTests
{
    /// <summary>Verifies a press on the anchor of an open Flyout is not a light dismiss: the
    /// flyout stays open and the anchor receives the click normally.</summary>
    [Fact]
    public async Task Pointer_WhenAnchorIsClickedWhileOpen_LeavesFlyoutOpenAndClicksAnchorAsync()
    {
        // Arrange
        var clicks = 0;
        var anchor = CreateAnchor();
        anchor.Click += (_, _) => clicks++;
        var flyout = new Flyout { Anchor = anchor, Content = new Button { Text = "Action" } };
        var root = new Overlay { Children = { anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");

        // Act
        await surface.Pointer.ClickAsync(anchor);

        // Assert
        flyout.IsOpen.ShouldBeTrue();
        clicks.ShouldBe(1);
    }

    /// <summary>Verifies a press on the flyout's own frame cell (not its content) is inside the
    /// surface and never dismisses.</summary>
    [Fact]
    public async Task Pointer_WhenFrameCellIsPressed_LeavesFlyoutOpenAsync()
    {
        // Arrange
        var anchor = CreateAnchor();
        var flyout = new Flyout
        {
            Anchor = anchor,
            Content = new Button { Text = "Action" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");
        var corner = new Point(flyout.SurfaceBounds.X, flyout.SurfaceBounds.Y);

        // Act
        await surface.Pointer.MoveToAsync(corner);
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        flyout.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies an outside wheel record is not a light dismiss for a Flyout and routes to
    /// the background normally.</summary>
    [Fact]
    public async Task Pointer_WhenWheelArrivesOutside_LeavesFlyoutOpenAsync()
    {
        // Arrange
        var wheels = 0;
        var background = new Button { Text = "Background", Width = Length.Cells(12), Height = Length.Cells(1) };
        Overlay.SetTop(background, Length.Cells(6));
        var anchor = CreateAnchor();
        var flyout = new Flyout { Anchor = anchor, Content = new Button { Text = "Action" } };
        var root = new Overlay { Children = { background, anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        var probe = await surface.Application.Dispatcher.InvokeAsync(
            () => background.AddHandler(Events.Pointer, (_, args) =>
            {
                if (args.Phase == RoutingPhase.Bubble && args.Pointer.Action == PointerAction.Wheel)
                {
                    wheels++;
                }
            }),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");

        // Act
        await surface.Pointer.WheelAsync(background, new Point(1, 0), wheelY: 1);

        // Assert
        flyout.IsOpen.ShouldBeTrue();
        wheels.ShouldBe(1);
        await surface.Application.Dispatcher.InvokeAsync(probe.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a vetoed light dismiss keeps the flyout open, keeps focus inside it, and
    /// still consumes the outside press instead of activating the background. Restoring the
    /// pre-open focus after a refused close used to pull focus out of the still-open flyout.</summary>
    [Fact]
    public async Task Pointer_WhenCloseRequestedVetoesLightDismiss_KeepsFlyoutOpenAndFocusedAsync()
    {
        // Arrange
        var backgroundClicks = 0;
        var background = new Button { Text = "Background", Width = Length.Cells(12), Height = Length.Cells(1) };
        background.Click += (_, _) => backgroundClicks++;
        Overlay.SetTop(background, Length.Cells(6));
        var anchor = CreateAnchor();
        var action = new Button { Text = "Action" };
        var flyout = new Flyout { Anchor = anchor, Content = action };
        var veto = true;
        flyout.CloseRequested += (_, args) => args.Cancel = veto;
        var root = new Overlay { Children = { background, anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(anchor).ShouldBeTrue(),
            "focus the anchor before opening");
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");
        surface.ShouldHaveFocus(action);

        // Act
        await surface.Pointer.ClickAsync(background);

        // Assert
        flyout.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(action);
        backgroundClicks.ShouldBe(0);
        flyout.HasLightDismissRegistration.ShouldBeTrue();

        // Act
        veto = false;
        await surface.Pointer.ClickAsync(background);

        // Assert
        flyout.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(anchor);
        backgroundClicks.ShouldBe(0);
    }

    /// <summary>Verifies a light dismiss whose pre-open focus owner became disabled while the
    /// flyout was open closes the flyout without focusing that owner or throwing.</summary>
    [Fact]
    public async Task Pointer_WhenPreOpenFocusOwnerIsDisabled_DismissesWithoutRestoringToItAsync()
    {
        // Arrange
        var anchor = CreateAnchor();
        var other = new Button { Text = "Other", Width = Length.Cells(8), Height = Length.Cells(1) };
        Overlay.SetTop(other, Length.Cells(6));
        var flyout = new Flyout { Anchor = anchor, Content = new Button { Text = "Action" } };
        var root = new Overlay { Children = { anchor, other, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(anchor).ShouldBeTrue(),
            "focus the anchor before opening");
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");
        await surface.UpdateAsync(() => anchor.IsEnabled = false, "disable the pre-open focus owner");

        // Act
        await surface.Pointer.MoveToAsync(new Point(20, 7));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        flyout.IsOpen.ShouldBeFalse();
        anchor.IsFocused.ShouldBeFalse();
        surface.Application.Focus.Focused.ShouldNotBeSameAs(anchor);
    }

    /// <summary>Verifies ShowAt on an already-open flyout retargets the anchor and repositions the
    /// surface without raising a second Opened.</summary>
    [Fact]
    public async Task ShowAt_WhenAlreadyOpenWithAnotherAnchor_RetargetsWithoutReopeningAsync()
    {
        // Arrange
        var opened = 0;
        var first = CreateAnchor();
        var second = new Button { Text = "Second", Width = Length.Cells(8), Height = Length.Cells(1) };
        Overlay.SetTop(second, Length.Cells(4));
        Overlay.SetLeft(second, Length.Cells(10));
        var flyout = new Flyout
        {
            Content = new Button { Text = "Action" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        flyout.Opened += (_, _) => opened++;
        var root = new Overlay { Children = { first, second, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => flyout.ShowAt(first), "show at the first anchor");
        flyout.SurfaceBounds.Y.ShouldBe(first.Bounds.Bottom);

        // Act
        await surface.UpdateAsync(() => flyout.ShowAt(second), "show at the second anchor while open");

        // Assert
        opened.ShouldBe(1);
        flyout.IsOpen.ShouldBeTrue();
        flyout.Anchor.ShouldBeSameAs(second);
        flyout.SurfaceBounds.Y.ShouldBe(second.Bounds.Bottom);
        flyout.SurfaceBounds.X.ShouldBe(second.Bounds.X);
        flyout.HasLightDismissRegistration.ShouldBeTrue();

        // Act - a press on the old anchor is now outside and dismisses
        await surface.Pointer.ClickAsync(first);

        // Assert
        flyout.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a chorded press that includes the primary button dismisses only when it
    /// lands outside the arranged surface: the same chord inside the surface is not a dismissal,
    /// and a secondary-only press outside is not in the policy mask.</summary>
    [Fact]
    public async Task Pointer_WhenChordedPressIncludesPrimary_DismissesOnlyOutsideTheSurfaceAsync()
    {
        // Arrange
        var anchor = CreateAnchor();
        var flyout = new Flyout
        {
            Anchor = anchor,
            Content = new Button { Text = "Action" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");
        var inside = new Point(flyout.SurfaceBounds.X + 1, flyout.SurfaceBounds.Y + 1);
        flyout.SurfaceBounds.Contains(new Point(22, 7)).ShouldBeFalse();

        // Act - secondary alone, outside: not in the mask
        await surface.UpdateAsync(
            () => _ = surface.Application.Capture.Dispatch(Chord(new Point(22, 7), Buttons.Secondary)),
            "secondary press outside");

        // Assert
        flyout.IsOpen.ShouldBeTrue();

        // Act - primary+secondary chord inside the surface: not outside
        await surface.UpdateAsync(
            () => _ = surface.Application.Capture.Dispatch(Chord(inside, Buttons.Primary | Buttons.Secondary)),
            "chorded press inside");

        // Assert
        flyout.IsOpen.ShouldBeTrue();

        // Act - the same chord outside dismisses
        await surface.UpdateAsync(
            () => _ = surface.Application.Capture.Dispatch(Chord(new Point(22, 7), Buttons.Primary | Buttons.Secondary)),
            "chorded press outside");

        // Assert
        flyout.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies the surface bounds are committed and the frame is painted by the time
    /// Opened is raised for a Flyout, the same contract the base Popup documents.</summary>
    [Fact]
    public async Task Opened_WhenFlyoutOpens_ObservesCommittedBoundsAsync()
    {
        // Arrange
        var boundsAtOpened = default(Rect);
        var anchor = CreateAnchor();
        var flyout = new Flyout
        {
            Anchor = anchor,
            Content = new Button { Text = "Action" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        flyout.Opened += (_, _) => boundsAtOpened = flyout.SurfaceBounds;
        var root = new Overlay { Children = { anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => flyout.ShowAt(anchor), "show Flyout");

        // Assert
        boundsAtOpened.ShouldNotBe(default);
        boundsAtOpened.ShouldBe(flyout.SurfaceBounds);
        boundsAtOpened.Y.ShouldBe(anchor.Bounds.Bottom);
        surface.Cell(new Point(boundsAtOpened.X, boundsAtOpened.Y)).Text.ShouldBe("╭");
    }

    /// <summary>Verifies nested flyouts dismiss innermost-first: a press on the outer flyout's own
    /// content closes only the inner one and still reaches that content, while a background press
    /// closes both.</summary>
    [Fact]
    public async Task Pointer_WhenNestedFlyoutsAreOpen_DismissesInnermostFirstAsync()
    {
        // Arrange
        var outerClicks = 0;
        var anchor = CreateAnchor();
        var innerAnchor = new Button { Text = "Inner", Width = Length.Cells(8), Height = Length.Cells(1) };
        var outerAction = new Button { Text = "Outer", Width = Length.Cells(8), Height = Length.Cells(1) };
        outerAction.Click += (_, _) => outerClicks++;
        Overlay.SetTop(outerAction, Length.Cells(6));
        var inner = new Flyout
        {
            Anchor = innerAnchor,
            Placement = PopupPlacement.Right,
            Content = new Button { Text = "Leaf" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var outer = new Flyout
        {
            Anchor = anchor,
            // Wide and tall enough that the inner surface (placed to the right of its anchor and
            // clamped inside this content host) never covers the outer action row.
            Content = new Overlay { Width = Length.Cells(26), Height = Length.Cells(8), Children = { innerAnchor, outerAction, inner } },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, outer } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 14),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => outer.IsOpen = true, "open the outer Flyout");
        await surface.UpdateAsync(() => inner.IsOpen = true, "open the inner Flyout");
        inner.IsOpen.ShouldBeTrue();
        outer.IsOpen.ShouldBeTrue();
        inner.SurfaceBounds.Contains(new Point(outerAction.Bounds.X, outerAction.Bounds.Y)).ShouldBeFalse();
        outer.SurfaceBounds.Contains(new Point(outerAction.Bounds.X, outerAction.Bounds.Y)).ShouldBeTrue();
        var order = new List<string>();
        inner.Closed += (_, _) => order.Add("inner");
        outer.Closed += (_, _) => order.Add("outer");

        // Act - press on the outer flyout's own content, outside the inner surface
        await surface.Pointer.ClickAsync(outerAction);

        // Assert - only the inner flyout closes; the press is consumed, not replayed to the button
        inner.IsOpen.ShouldBeFalse();
        outer.IsOpen.ShouldBeTrue();
        outerClicks.ShouldBe(0);
        order.ShouldBe(["inner"]);

        // Arrange - reopen the inner flyout for the background press
        await surface.UpdateAsync(() => inner.IsOpen = true, "reopen the inner Flyout");
        order.Clear();

        // Act
        await surface.Pointer.MoveToAsync(new Point(38, 13));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        inner.IsOpen.ShouldBeFalse();
        outer.IsOpen.ShouldBeFalse();
        order.ShouldBe(["inner", "outer"]);
    }

    /// <summary>Verifies pressing another flyout's anchor while one flyout is open only dismisses
    /// the open one: the press is consumed without replay, so the second flyout does not open and
    /// its anchor sees no click until the next press.</summary>
    [Fact]
    public async Task Pointer_WhenAnotherFlyoutAnchorIsPressedWhileOpen_DismissesWithoutOpeningTheOtherAsync()
    {
        // Arrange
        var secondClicks = 0;
        var first = CreateAnchor();
        var second = new Button { Text = "Second", Width = Length.Cells(8), Height = Length.Cells(1) };
        Overlay.SetLeft(second, Length.Cells(14));
        var firstFlyout = new Flyout { Anchor = first, Content = new Button { Text = "One" } };
        var secondFlyout = new Flyout { Anchor = second, Content = new Button { Text = "Two" } };
        second.Click += (_, _) =>
        {
            secondClicks++;
            secondFlyout.IsOpen = true;
        };
        var root = new Overlay { Children = { first, second, firstFlyout, secondFlyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => firstFlyout.IsOpen = true, "open the first Flyout");

        // Act
        await surface.Pointer.ClickAsync(second);

        // Assert
        firstFlyout.IsOpen.ShouldBeFalse();
        secondFlyout.IsOpen.ShouldBeFalse();
        secondClicks.ShouldBe(0);

        // Act - the next press is an ordinary click
        await surface.Pointer.ClickAsync(second);

        // Assert
        secondClicks.ShouldBe(1);
        secondFlyout.IsOpen.ShouldBeTrue();
        firstFlyout.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies an Escape whose close request is vetoed leaves the flyout open with focus
    /// still inside it, and consumes the key.</summary>
    [Fact]
    public async Task Escape_WhenCloseRequestedVetoes_KeepsFlyoutOpenAndFocusedAsync()
    {
        // Arrange
        var anchor = CreateAnchor();
        var action = new Button { Text = "Action" };
        var flyout = new Flyout { Anchor = anchor, Content = action };
        var veto = true;
        flyout.CloseRequested += (_, args) => args.Cancel = veto;
        var root = new Overlay { Children = { anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(anchor).ShouldBeTrue(), "focus the anchor");
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");
        surface.ShouldHaveFocus(action);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        flyout.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(action);

        // Act
        veto = false;
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        flyout.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(anchor);
    }

    /// <summary>Verifies an anchor that becomes hidden or collapsed while the flyout is open leaves
    /// the flyout presented in place - a collapse publishes no anchor reflow, so the dismiss-on-
    /// reflow rule does not fire - and the next outside press still dismisses it.</summary>
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public async Task Anchor_WhenHiddenWhileOpen_KeepsFlyoutPresentedUntilTheNextOutsidePressAsync(Visibility hidden)
    {
        // Arrange
        var anchor = CreateAnchor();
        var flyout = new Flyout
        {
            Anchor = anchor,
            Content = new Button { Text = "Action" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");
        var bounds = flyout.SurfaceBounds;

        // Act
        await surface.UpdateAsync(() => anchor.Visibility = hidden, $"make the anchor {hidden}");

        // Assert
        flyout.IsOpen.ShouldBeTrue();
        flyout.SurfaceBounds.ShouldBe(bounds);
        surface.Cell(new Point(bounds.X, bounds.Y)).Text.ShouldBe("╭");

        // Act
        await surface.Pointer.MoveToAsync(new Point(22, 7));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        flyout.IsOpen.ShouldBeFalse();
        surface.Cell(new Point(bounds.X, bounds.Y)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies disposing the anchor while the flyout is open leaves the flyout open and
    /// dismissable by the next outside press, which restores no focus to the dead anchor.</summary>
    [Fact]
    public async Task Anchor_WhenDisposedWhileOpen_StaysOpenAndDismissesWithoutRestoringFocusAsync()
    {
        // Arrange
        var anchor = CreateAnchor();
        var other = new Button { Text = "Other", Width = Length.Cells(8), Height = Length.Cells(1) };
        Overlay.SetTop(other, Length.Cells(6));
        var flyout = new Flyout { Anchor = anchor, Content = new Button { Text = "Action" } };
        var root = new Overlay { Children = { anchor, other, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(anchor).ShouldBeTrue(), "focus the anchor");
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");
        var bounds = flyout.SurfaceBounds;

        // Act
        await surface.UpdateAsync(anchor.Dispose, "dispose the anchor while open");

        // Assert
        flyout.IsOpen.ShouldBeTrue();
        flyout.SurfaceBounds.ShouldBe(bounds);
        surface.Cell(new Point(bounds.X, bounds.Y)).Text.ShouldNotBe(" ");

        // Act
        await surface.Pointer.MoveToAsync(new Point(22, 7));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        flyout.IsOpen.ShouldBeFalse();
        surface.Application.Focus.Focused.ShouldNotBeSameAs(anchor);
    }

    private static Pointer Chord(Point cells, Buttons buttons) => new(
        cells,
        pixels: null,
        buttons,
        PointerAction.Press,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);

    private static Button CreateAnchor() => new()
    {
        Text = "Anchor",
        Width = Length.Cells(8),
        Height = Length.Cells(1),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };
}
