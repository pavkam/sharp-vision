// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

/// <summary>Proves popup promotion and dismissal through mounted terminal surfaces.</summary>
public sealed class PopupSurfaceTests
{
    /// <summary>Verifies a mounted Popup remodalizes once after nested disabled ancestors recover.</summary>
    [Fact]
    public async Task IsEnabled_WhenParentAndGrandparentRecover_RestoresOneMountedAutomaticScopeAsync()
    {
        var popup = new Popup { Content = new Button { Text = "Action" } };
        var parent = new Overlay { Children = { popup } };
        var grandparent = new Overlay { Children = { parent } };
        await using var surface = await ComponentSurface.MountAsync(
            grandparent,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open nested Popup");
        var first = surface.Application.Modality.Active.ShouldNotBeNull();

        await surface.UpdateAsync(() => parent.IsEnabled = false, "disable Popup parent");

        popup.IsOpen.ShouldBeTrue();
        first.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        await surface.UpdateAsync(
            () =>
            {
                grandparent.IsEnabled = false;
                parent.IsEnabled = true;
            },
            "retarget disabled Popup ancestor");

        popup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeNull();

        await surface.UpdateAsync(() => grandparent.IsEnabled = true, "restore Popup ancestors");

        var restored = surface.Application.Modality.Active.ShouldNotBeNull();
        restored.ShouldNotBeSameAs(first);
        restored.Root.ShouldBeSameAs(popup);
        restored.IsActive.ShouldBeTrue();
    }

    /// <summary>Verifies a mounted Popup remodalizes once after nested hidden ancestors recover.</summary>
    [Fact]
    public async Task Visibility_WhenParentAndGrandparentRecover_RestoresOneMountedAutomaticScopeAsync()
    {
        var popup = new Popup { Content = new Button { Text = "Action" } };
        var parent = new Overlay { Children = { popup } };
        var grandparent = new Overlay { Children = { parent } };
        await using var surface = await ComponentSurface.MountAsync(
            grandparent,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open nested Popup");
        var first = surface.Application.Modality.Active.ShouldNotBeNull();

        await surface.UpdateAsync(() => parent.Visibility = Visibility.Hidden, "hide Popup parent");

        popup.IsOpen.ShouldBeTrue();
        first.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        await surface.UpdateAsync(
            () =>
            {
                grandparent.Visibility = Visibility.Hidden;
                parent.Visibility = Visibility.Visible;
            },
            "retarget hidden Popup ancestor");

        popup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeNull();

        await surface.UpdateAsync(() => grandparent.Visibility = Visibility.Visible, "restore Popup ancestors");

        var restored = surface.Application.Modality.Active.ShouldNotBeNull();
        restored.ShouldNotBeSameAs(first);
        restored.Root.ShouldBeSameAs(popup);
        restored.IsActive.ShouldBeTrue();
    }

    /// <summary>Verifies a Popup opened during detached construction enters default modality on attachment.</summary>
    [Fact]
    public async Task MountAsync_WhenPopupIsAlreadyOpen_EntersDefaultDismissPresentationAsync()
    {
        // Arrange
        var popup = new Popup
        {
            Content = new Button
            {
                Text = "Action",
                Width = Length.Cells(8),
                Height = Length.Cells(3)
            },
            IsOpen = true
        };
        var root = new Overlay { Children = { popup } };

        var size = new Size(18, 7);
        var terminal = new ComponentTerminal(size);
        _ = terminal.QueueResize(new Dimensions(size));
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        // Act
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        await application.Dispatcher.InvokeAsync(() =>
        {
            var scope = application.Modality.Active.ShouldNotBeNull();
            scope.Root.ShouldBeSameAs(popup);
            scope.OutsideInteraction.ShouldBe(OutsideInteraction.Dismiss);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies inherited shadow chrome uses the promoted popup surface and remains non-interactive.</summary>
    [Fact]
    public async Task Render_WhenPopupHasBlockShadow_DrawsOutsideSurfaceWithoutExpandingHitTargetAsync()
    {
        // Arrange
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: true, mode: ShadowMode.BlockGlyph, offset: new Point(1, 1), glyph: new Rune('▓')),
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open shadowed Popup");
        var rightShadow = new Point(popup.SurfaceBounds.Right, popup.SurfaceBounds.Y + 1);
        var bottomShadow = new Point(popup.SurfaceBounds.X + 1, popup.SurfaceBounds.Bottom);

        // Assert
        surface.Cell(rightShadow).Text.ShouldBe("▓");
        surface.Cell(bottomShadow).Text.ShouldBe("▓");
        popup.HitTest(rightShadow).ShouldBeNull();
        popup.HitTest(bottomShadow).ShouldBeNull();
    }

    /// <summary>Verifies open popup composition, descendant hover, focus transfer, and Escape closure.</summary>
    [ComponentBehaviorEvidence(
        typeof(Popup),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
    [ComponentBehaviorEvidence(
        typeof(Flyout),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
    [ComponentBehaviorEvidence(
        typeof(Tooltip),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenPopupOpensAndEscapes_TracksTransientCompositionAsync()
    {
        // Arrange
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        var action = new Button
        {
            Text = "Action",
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        var popup = new Popup { Anchor = anchor, Content = action };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 7),
            TestContext.Current.CancellationToken);

        // Act open and hover composed content
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");
        await surface.Pointer.MoveToAsync(action);

        // Assert transient ancestry and focus transfer
        popup.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.Width.ShouldBeGreaterThan(0);
        popup.IsPointerOver.ShouldBeTrue();
        popup.IsPointerDirectlyOver.ShouldBeFalse();
        popup.IsFocused.ShouldBeFalse();
        popup.IsPressed.ShouldBeFalse();
        action.Parent.ShouldBeSameAs(popup);
        surface.ShouldHaveFocus(action);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        popup.IsOpen.ShouldBeFalse();
        popup.SurfaceBounds.ShouldBe(default);
        action.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies modal outside press closes, restores focus, and never activates the background.</summary>
    [Fact]
    public async Task IsOpen_WhenOutsidePressArrives_ClosesAndRestoresWithoutBackgroundActivationAsync()
    {
        // Arrange
        var activations = 0;
        var background = new Button
        {
            Text = "Background",
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        background.Click += (_, _) => activations++;
        var action = new Button
        {
            Text = "Action",
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var popup = new Popup { Anchor = background, Content = action };
        var root = new Overlay { Children = { background, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(background).ShouldBeTrue(),
            "focus Popup background");
        await surface.UpdateAsync(
            () => popup.IsOpen = true,
            "open default modal Popup");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(background);

        // Assert
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(background);
        activations.ShouldBe(0);
    }

    /// <summary>Verifies outside wheel dismisses a modal popup without routing into background content.</summary>
    [Fact]
    public async Task IsOpen_WhenOutsideWheelArrives_ClosesWithoutBackgroundRouteAsync()
    {
        // Arrange
        var wheelRoutes = 0;
        var background = new Button
        {
            Text = "Background",
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble && eventArgs.Pointer.Action == PointerAction.Wheel)
            {
                wheelRoutes++;
            }
        });
        var action = new Button
        {
            Text = "Action",
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var popup = new Popup { Anchor = background, Content = action };
        var root = new Overlay { Children = { background, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(background).ShouldBeTrue(),
            "focus Popup wheel background");
        await surface.UpdateAsync(
            () => popup.IsOpen = true,
            "open default wheel-dismiss Popup");

        // Act
        await surface.Pointer.WheelAsync(background, default, wheelY: 1);

        // Assert
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(background);
        wheelRoutes.ShouldBe(0);
    }

    /// <summary>Verifies Escape closes the visual and modal lifetimes together.</summary>
    [Fact]
    public async Task IsOpen_WhenEscapeArrives_ClosesScopeAndRestoresBackgroundFocusAsync()
    {
        // Arrange
        var background = new Button
        {
            Text = "Background",
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        var action = new Button
        {
            Text = "Action",
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var popup = new Popup { Anchor = background, Content = action };
        var root = new Overlay { Children = { background, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(background).ShouldBeTrue(),
            "focus Popup Escape background");
        await surface.UpdateAsync(
            () => popup.IsOpen = true,
            "open default Escape Popup");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(background);
    }

    /// <summary>Verifies an opened Popup renders its border frame and content on the surface.</summary>
    [Fact]
    public async Task Render_WhenPopupIsOpenedWithContent_DrawsFramedContentBelowAnchorAsync()
    {
        // Arrange
        var anchor = new Button
        {
            Text = "Open",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(1)
        };
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup with text content");

        // Assert — Popup appears below the anchor with a bordered frame.
        popup.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.Y.ShouldBeGreaterThanOrEqualTo(anchor.Bounds.Bottom);
        surface.Cell(new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Y)).Text.ShouldBe("╭");
        surface.Cell(new Point(popup.SurfaceBounds.Right - 1, popup.SurfaceBounds.Y)).Text.ShouldBe("╮");
        surface.Cell(new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Bottom - 1)).Text.ShouldBe("╰");
        surface.Cell(new Point(popup.SurfaceBounds.Right - 1, popup.SurfaceBounds.Bottom - 1)).Text.ShouldBe("╯");
    }

    /// <summary>Verifies the Popup flips from Below to Above when the anchor is near the bottom edge.</summary>
    [Fact]
    public async Task Render_WhenNoSpaceBelowAnchor_FlipsPopupToAboveAsync()
    {
        // Arrange — anchor sits near the bottom, leaving no room for content below.
        var anchor = new Button
        {
            Text = "Bottom",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        Overlay.SetTop(anchor, Length.Cells(7));
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Flipped"),
            Placement = PopupPlacement.Below,
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        var root = new Overlay
        {
            Width = Length.Cells(12),
            Height = Length.Cells(8),
            Children = { anchor, popup }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup near bottom");

        // Assert — the popup should flip above the anchor.
        popup.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(anchor.Bounds.Y);
    }

    /// <summary>Verifies default Popup dismissal consumes the press without activating its outside target.</summary>
    [Fact]
    public async Task IsOpen_WhenOutsidePressArrives_ClosesWithoutLegacyBackgroundActivationAsync()
    {
        // Arrange
        var activations = 0;
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var background = new Button
        {
            Text = "Outside",
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        Overlay.SetLeft(background, Length.Cells(12));
        background.Click += (_, _) => activations++;
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new Button
            {
                Text = "Action",
                Width = Length.Cells(8),
                Height = Length.Cells(3),
            },
            FocusOnOpen = false,
        };
        var root = new Overlay { Children = { anchor, background, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open default modal Popup");

        // Act
        await surface.Pointer.ClickAsync(background);

        // Assert
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        activations.ShouldBe(0);
    }

    /// <summary>Verifies opening a popup as a context-menu modal enters scope and dismisses on outside click.</summary>
    [Fact]
    public async Task Pointer_WhenContextMenuIsOpenedModally_EntersScopeAndDismissesOnOutsideClickAsync()
    {
        // Arrange
        var target = new Button { Text = "Target", Width = Length.Cells(10), Height = Length.Cells(3) };
        var menuContent = new ProbeControl { Focusable = true, Width = Length.Cells(8), Height = Length.Cells(2) };
        var contextPopup = new Popup { Content = menuContent, Anchor = target, FocusOnOpen = true };
        var root = new Overlay { Children = { target, contextPopup } };
        await using var surface = await ComponentSurface.MountAsync(
            root, new Size(30, 10), TestContext.Current.CancellationToken);

        // Act — open the popup as a dismissing modal (simulating a context-menu handler)
        ModalScope? contextScope = null;
        await surface.UpdateAsync(() =>
        {
            contextScope = contextPopup.OpenModal(OutsideInteraction.Dismiss, menuContent);
        }, "open context popup modally");

        // Assert — popup is open with an active modal scope
        contextPopup.IsOpen.ShouldBeTrue();
        _ = contextScope.ShouldNotBeNull();
        contextScope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(contextScope);
        surface.ShouldHaveFocus(menuContent);

        // Act — click outside the popup to trigger dismiss
        await surface.Pointer.ClickAsync(root, new Point(25, 8));

        // Assert — popup dismissed and scope exited
        contextPopup.IsOpen.ShouldBeFalse();
        contextScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }
}
