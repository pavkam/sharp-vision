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

    /// <summary>Verifies a chorded press that includes the primary button dismisses, because the
    /// policy mask matches any pressed button in the set.</summary>
    [Fact]
    public async Task Pointer_WhenChordedPressIncludesPrimary_DismissesAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var anchor = new ProbeControl(new Size(6, 1));
            var flyout = new Flyout { Anchor = anchor, Content = new ProbeControl(new Size(4, 2)) };
            var root = new Overlay();
            root.Children.Add(new ProbeControl(new Size(20, 10)));
            root.Children.Add(anchor);
            root.Children.Add(flyout);
            root.Attach(dispatcher);
            flyout.IsOpen = true;

            // Act
            var pointer = new Pointer(
                new Point(19, 9),
                pixels: null,
                Buttons.Primary | Buttons.Secondary,
                PointerAction.Press,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false);
            var eventArgs = new PointerEventArgs(pointer);
            _ = Router.Route(root, Events.Pointer, eventArgs);

            // Assert
            flyout.IsOpen.ShouldBeFalse();
            eventArgs.IsHandled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    private static Button CreateAnchor() => new()
    {
        Text = "Anchor",
        Width = Length.Cells(8),
        Height = Length.Cells(1),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };
}
