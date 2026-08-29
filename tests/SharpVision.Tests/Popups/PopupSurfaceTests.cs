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

    /// <summary>Verifies a Button hosted as popup content shows its own hovered appearance -
    /// both IsPointerOver and the rendered face/border - once the pointer moves over it while the
    /// popup is open, matching an ordinary page-hosted Button.</summary>
    [Fact]
    public async Task Pointer_WhenMovedOverPopupHostedButton_ShowsHoveredAppearanceAsync()
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
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save"
        };
        var popup = new Popup { Anchor = anchor, Content = action };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");

        // Act - opening the Popup already transferred focus to its sole eligible descendant, so
        // hovering compounds onto that IsFocused state exactly as it does for a page-hosted focused
        // Button.
        await surface.Pointer.MoveToAsync(action);

        // Assert
        action.IsPointerOver.ShouldBeTrue();
        surface.ShouldHaveState(action, VisualState.IsPointerOver | VisualState.Focused);
        var origin = new Point(action.Bounds.X, action.Bounds.Y);
        surface.Cell(origin).Style.Foreground.ShouldBe(TerminalPalette.Project(
            ThemeCatalog.Dark.ResolveColor(SemanticColor.ReliefHighlight),
            ColorDepth.Basic16));
    }

    /// <summary>Verifies open popup composition, descendant hover, focus transfer, and Escape closure.</summary>
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

    /// <summary>Verifies a mounted Popup proves direct and ancestor-inherited disabled state,
    /// keeps a stable surface across a genuine resize while disabled, and resumes Normal once
    /// re-enabled. The nested modality-recovery test above separately proves that a disabled
    /// ancestor also deactivates and later restores this Popup's modal scope.</summary>
    [Fact]
    public async Task IsEnabled_WhenPopupIsDisabledDirectlyOrByAncestor_ReflectsDisabledAndRecoversAsync()
    {
        // Arrange
        var anchor = new Button { Text = "Anchor", Width = Length.Cells(8), Height = Length.Cells(3) };
        var action = new Button { Text = "Action", Width = Length.Cells(8), Height = Length.Cells(3) };
        var popup = new Popup { Anchor = anchor, Content = action };
        var host = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(18, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");

        // Act direct disable
        await surface.UpdateAsync(() => popup.IsEnabled = false, "disable open Popup");

        // Assert direct disable
        popup.IsOpen.ShouldBeTrue();
        surface.ShouldHaveState(popup, VisualState.Disabled);

        // Act re-enable before proving ancestor inheritance in isolation
        await surface.UpdateAsync(() => popup.IsEnabled = true, "re-enable Popup directly");
        surface.ShouldHaveState(popup, VisualState.Normal);

        // Act ancestor disable
        await surface.UpdateAsync(() => host.IsEnabled = false, "disable ancestor Overlay");

        // Assert the popup inherits Disabled without its own IsEnabled flag changing
        popup.IsOpen.ShouldBeTrue();
        popup.IsEnabled.ShouldBeTrue();
        popup.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(popup, VisualState.Disabled);

        // Act a genuine resize while disabled and assert surface stability against an
        // independently mounted, otherwise-identical enabled popup at the same new size.
        await surface.ResizeAsync(new Size(24, 10));
        var disabledSurfaceBounds = popup.SurfaceBounds;

        var referenceAnchor = new Button { Text = "Anchor", Width = Length.Cells(8), Height = Length.Cells(3) };
        var referenceAction = new Button { Text = "Action", Width = Length.Cells(8), Height = Length.Cells(3) };
        var referencePopup = new Popup { Anchor = referenceAnchor, Content = referenceAction };
        var referenceHost = new Overlay { Children = { referenceAnchor, referencePopup } };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceHost,
            new Size(24, 10),
            TestContext.Current.CancellationToken);
        await referenceSurface.UpdateAsync(() => referencePopup.IsOpen = true, "open reference Popup");

        referencePopup.SurfaceBounds.ShouldBe(disabledSurfaceBounds);

        // Act re-enable recovery
        await surface.UpdateAsync(() => host.IsEnabled = true, "re-enable ancestor Overlay");

        // Assert Normal state resumes
        surface.ShouldHaveState(popup, VisualState.Normal);
    }

    /// <summary>Verifies a mounted Flyout proves direct and ancestor-inherited disabled state,
    /// keeps a stable surface across a genuine resize while disabled, and resumes Normal once
    /// re-enabled.</summary>
    [Fact]
    public async Task IsEnabled_WhenFlyoutIsDisabledDirectlyOrByAncestor_ReflectsDisabledAndRecoversAsync()
    {
        // Arrange
        var anchor = new Button { Text = "Anchor", Width = Length.Cells(8), Height = Length.Cells(3) };
        var action = new Button { Text = "Action", Width = Length.Cells(8), Height = Length.Cells(3) };
        var flyout = new Flyout { Anchor = anchor, Content = action, FocusOnOpen = false };
        var host = new Overlay { Children = { anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(18, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");

        // Act direct disable
        await surface.UpdateAsync(() => flyout.IsEnabled = false, "disable open Flyout");

        // Assert direct disable
        surface.ShouldHaveState(flyout, VisualState.Disabled);

        // Act re-enable before proving ancestor inheritance in isolation
        await surface.UpdateAsync(() => flyout.IsEnabled = true, "re-enable Flyout directly");
        surface.ShouldHaveState(flyout, VisualState.Normal);

        // Act ancestor disable
        await surface.UpdateAsync(() => host.IsEnabled = false, "disable ancestor Overlay");

        // Assert the flyout inherits Disabled without its own IsEnabled flag changing
        flyout.IsEnabled.ShouldBeTrue();
        flyout.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(flyout, VisualState.Disabled);

        // Act a genuine resize while disabled and assert surface stability against an
        // independently mounted, otherwise-identical enabled flyout at the same new size.
        await surface.ResizeAsync(new Size(24, 10));
        var disabledSurfaceBounds = flyout.SurfaceBounds;

        var referenceAnchor = new Button { Text = "Anchor", Width = Length.Cells(8), Height = Length.Cells(3) };
        var referenceAction = new Button { Text = "Action", Width = Length.Cells(8), Height = Length.Cells(3) };
        var referenceFlyout = new Flyout { Anchor = referenceAnchor, Content = referenceAction, FocusOnOpen = false };
        var referenceHost = new Overlay { Children = { referenceAnchor, referenceFlyout } };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceHost,
            new Size(24, 10),
            TestContext.Current.CancellationToken);
        await referenceSurface.UpdateAsync(() => referenceFlyout.IsOpen = true, "open reference Flyout");

        referenceFlyout.SurfaceBounds.ShouldBe(disabledSurfaceBounds);

        // Act re-enable recovery
        await surface.UpdateAsync(() => host.IsEnabled = true, "re-enable ancestor Overlay");

        // Assert Normal state resumes
        surface.ShouldHaveState(flyout, VisualState.Normal);
    }

    /// <summary>Verifies a mounted Tooltip proves direct and ancestor-inherited disabled state,
    /// keeps a stable surface across a genuine resize while disabled, and resumes Normal once
    /// re-enabled.</summary>
    [Fact]
    public async Task IsEnabled_WhenTooltipIsDisabledDirectlyOrByAncestor_ReflectsDisabledAndRecoversAsync()
    {
        // Arrange
        var anchor = new Button { Text = "Anchor", Width = Length.Cells(8), Height = Length.Cells(3) };
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        var host = new Overlay { Children = { anchor } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => tooltip.IsOpen = true, "open direct Tooltip surface");

        // Act direct disable
        await surface.UpdateAsync(() => tooltip.IsEnabled = false, "disable open Tooltip");

        // Assert direct disable
        surface.ShouldHaveState(tooltip, VisualState.Disabled);

        // Act re-enable before proving ancestor inheritance in isolation
        await surface.UpdateAsync(() => tooltip.IsEnabled = true, "re-enable Tooltip directly");
        surface.ShouldHaveState(tooltip, VisualState.Normal);

        // Act ancestor disable
        await surface.UpdateAsync(() => host.IsEnabled = false, "disable ancestor Overlay");

        // Assert the tooltip inherits Disabled without its own IsEnabled flag changing
        tooltip.IsEnabled.ShouldBeTrue();
        tooltip.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(tooltip, VisualState.Disabled);

        // Act a genuine resize while disabled and assert surface stability against an
        // independently mounted, otherwise-identical enabled tooltip at the same new size.
        await surface.ResizeAsync(new Size(24, 9));
        var disabledSurfaceBounds = tooltip.SurfaceBounds;

        var referenceAnchor = new Button { Text = "Anchor", Width = Length.Cells(8), Height = Length.Cells(3) };
        Tooltip.SetText(referenceAnchor, "Save");
        var referenceTooltip = Tooltip.GetTooltip(referenceAnchor).ShouldNotBeNull();
        var referenceHost = new Overlay { Children = { referenceAnchor } };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceHost,
            new Size(24, 9),
            TestContext.Current.CancellationToken);
        await referenceSurface.UpdateAsync(() => referenceTooltip.IsOpen = true, "open reference Tooltip");

        referenceTooltip.SurfaceBounds.ShouldBe(disabledSurfaceBounds);

        // Act re-enable recovery
        await surface.UpdateAsync(() => host.IsEnabled = true, "re-enable ancestor Overlay");

        // Assert Normal state resumes
        surface.ShouldHaveState(tooltip, VisualState.Normal);
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
        var menuContent = new ProbeControl { IsFocusable = true, Width = Length.Cells(8), Height = Length.Cells(2) };
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

    /// <summary>Verifies a bare Popup anchored to a foreign sibling re-resolves its placement
    /// after that sibling reflows while open, rather than continuing to render at the anchor's
    /// old position. A popup anchored to a foreign sibling must follow it when layout moves it -
    /// this pins the tracking Popup itself now owns rather than relying on a family override.
    /// A Stack (flow layout) is required to move the anchor without resizing the root: an Overlay
    /// alone would not reproduce a reflow.</summary>
    [Fact]
    public async Task IsOpen_WhenAnchorReflowsWhileOpen_FollowsAnchorAsync()
    {
        var spacer = new Button
        {
            Text = string.Empty,
            Width = Length.Cells(1),
            Height = Length.Cells(4)
        };
        var anchor = new Button
        {
            Text = "Bottom",
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        var stack = new Stack
        {
            Orientation = Orientation.Vertical,
            Width = Length.Cells(12),
            Height = Length.Cells(8),
            Children = { spacer, anchor }
        };
        var popup = new Popup { Anchor = anchor, Content = new ControlText("Menu") };
        var root = new Overlay { Children = { stack, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => popup.IsOpen = true, "open popup that fits below at initial anchor position");
        var initialY = popup.SurfaceBounds.Y;

        await surface.UpdateAsync(() => spacer.Height = Length.Cells(7), "reflow the anchor toward the bottom while open");

        popup.SurfaceBounds.Y.ShouldNotBe(initialY);
        popup.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(anchor.Bounds.Y);
    }

    /// <summary>Verifies TracksAnchorReflow gates the anchor-reflow response: false produces no
    /// calls for a foreign anchor reflowing while open, and true (the default) produces exactly
    /// one call per reflow. Pins both sides of the opt-out self-anchored composites rely on.</summary>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task IsOpen_WhenAnchorReflowsWhileOpen_TracksAnchorReflowGatesCallCountAsync(
        bool tracksAnchorReflow,
        int expectedCalls)
    {
        var spacer = new Button
        {
            Text = string.Empty,
            Width = Length.Cells(1),
            Height = Length.Cells(4)
        };
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        var stack = new Stack
        {
            Orientation = Orientation.Vertical,
            Width = Length.Cells(12),
            Height = Length.Cells(8),
            Children = { spacer, anchor }
        };
        var popup = new PopupAnchorReflowProbe
        {
            Anchor = anchor,
            Content = new ControlText("Menu"),
            TracksAnchorReflow = tracksAnchorReflow
        };
        var root = new Overlay { Children = { stack, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => popup.IsOpen = true, "open popup that fits below at initial anchor position");

        await surface.UpdateAsync(() => spacer.Height = Length.Cells(7), "reflow the anchor while open");

        popup.AnchorReflowCalls.ShouldBe(expectedCalls);
    }

    /// <summary>Verifies clearing Anchor while a popup is open drops its reflow subscription
    /// instead of leaking it - the old anchor reflowing afterward must not still trigger a
    /// response, since this popup no longer considers that control its anchor.</summary>
    [Fact]
    public async Task Anchor_WhenClearedWhileOpen_StopsRespondingToThePreviousAnchorsReflowAsync()
    {
        var spacer = new Button
        {
            Text = string.Empty,
            Width = Length.Cells(1),
            Height = Length.Cells(4)
        };
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        var stack = new Stack
        {
            Orientation = Orientation.Vertical,
            Width = Length.Cells(12),
            Height = Length.Cells(8),
            Children = { spacer, anchor }
        };
        var popup = new PopupAnchorReflowProbe { Anchor = anchor, Content = new ControlText("Menu") };
        var root = new Overlay { Children = { stack, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => popup.IsOpen = true, "open popup anchored to the reflowing sibling");
        await surface.UpdateAsync(() => popup.Anchor = null, "clear the anchor while still open");

        await surface.UpdateAsync(() => spacer.Height = Length.Cells(7), "reflow the former anchor while open");

        popup.AnchorReflowCalls.ShouldBe(0);
    }

    /// <summary>Verifies a menu item invocation that opens a popup transitions scopes cleanly.</summary>
    [Fact]
    public async Task OpenModal_WhenMenuItemOpensPopup_TransitionsFromMenuScopeToPopupScopeAsync()
    {
        // Arrange
        var background = new ProbeControl { IsFocusable = true };
        var dialogContent = new ProbeControl { IsFocusable = true };
        var dialog = new Popup { Content = dialogContent };
        var actionItem = new MenuItem { Text = "Action" };
        ModalScope? dialogScope = null;
        actionItem.Invoked += (_, _) =>
        {
            dialogScope = dialog.OpenModal(OutsideInteraction.Dismiss, dialogContent);
        };
        var menu = new Menu { Items = { actionItem } };
        var root = new Overlay { Children = { background, menu, dialog } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 10),
            TestContext.Current.CancellationToken);

        // Pre-condition: focus the background
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(background).ShouldBeTrue(),
            "focus background");

        // Act — click the menu item to invoke it and open the popup
        await surface.Pointer.ClickAsync(actionItem);

        // Assert — popup modal is active with focus on dialog content
        _ = dialogScope.ShouldNotBeNull();
        dialogScope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(dialogScope);
        surface.Application.Focus.Focused.ShouldBeSameAs(dialogContent);
        dialog.IsOpen.ShouldBeTrue();

        // Act — close the popup
        await surface.UpdateAsync(() => dialog.IsOpen = false, "close popup");

        // Assert — modal scope ended, focus returned to menu (which had focus when dialog opened)
        dialogScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.Application.Focus.Focused.ShouldBeSameAs(menu);
    }
}
