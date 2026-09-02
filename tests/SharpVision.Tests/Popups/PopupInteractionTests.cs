// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

/// <summary>Proves Popup placement resolution, lifecycle event snapshots, keyboard dismissal
/// policies, and modal keyboard confinement through mounted terminal surfaces.</summary>
public sealed class PopupInteractionTests
{
    #region Placement

    /// <summary>Verifies every placement resolves its surface origin against the matching anchor
    /// edge when the preferred side fits, and that the surface stretches to the anchor width.</summary>
    [Theory]
    [InlineData(PopupPlacement.Below, 10, 7)]
    [InlineData(PopupPlacement.Above, 10, 1)]
    [InlineData(PopupPlacement.Right, 18, 4)]
    [InlineData(PopupPlacement.Left, 2, 4)]
    public async Task Placement_WhenPreferredSideFits_ArrangesSurfaceAgainstAnchorEdgeAsync(
        PopupPlacement placement,
        int expectedX,
        int expectedY)
    {
        // Arrange - the anchor sits at (10, 4) with an 8x3 footprint inside a 30x12 root.
        var anchor = CreateAnchor(left: 10, top: 4);
        var popup = new Popup
        {
            Anchor = anchor,
            Placement = placement,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        anchor.Bounds.ShouldBe(new Rect(10, 4, 8, 3));

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, $"open {placement} Popup");

        // Assert
        popup.ResolvedPlacement.ShouldBe(placement);
        popup.SurfaceBounds.ShouldBe(new Rect(expectedX, expectedY, 8, 3));
        surface.Cell(new Point(expectedX, expectedY)).Text.ShouldBe("╭");
        surface.Cell(new Point(expectedX + 7, expectedY + 2)).Text.ShouldBe("╯");
        surface.Cell(new Point(expectedX + 1, expectedY + 1)).Text.ShouldBe("M");
    }

    /// <summary>Verifies each placement flips to its opposite side when the anchor touches the
    /// matching host edge and the opposite side fits.</summary>
    [Theory]
    [InlineData(PopupPlacement.Below, 10, 9, PopupPlacement.Above, 10, 6)]
    [InlineData(PopupPlacement.Above, 10, 0, PopupPlacement.Below, 10, 3)]
    [InlineData(PopupPlacement.Right, 22, 4, PopupPlacement.Left, 14, 4)]
    [InlineData(PopupPlacement.Left, 0, 4, PopupPlacement.Right, 8, 4)]
    public async Task Placement_WhenAnchorTouchesHostEdge_FlipsToOppositeSideAsync(
        PopupPlacement placement,
        int anchorLeft,
        int anchorTop,
        PopupPlacement expectedPlacement,
        int expectedX,
        int expectedY)
    {
        // Arrange
        var anchor = CreateAnchor(anchorLeft, anchorTop);
        var popup = new Popup
        {
            Anchor = anchor,
            Placement = placement,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, $"open edge-anchored {placement} Popup");

        // Assert
        popup.ResolvedPlacement.ShouldBe(expectedPlacement);
        popup.SurfaceBounds.ShouldBe(new Rect(expectedX, expectedY, 8, 3));
    }

    /// <summary>Verifies a popup that fits on neither side keeps its preferred placement and is
    /// clamped inside the host instead of flipping.</summary>
    [Fact]
    public async Task Placement_WhenNeitherSideFits_KeepsPreferredPlacementAndClampsInsideHostAsync()
    {
        // Arrange - a five-row host leaves three rows below the anchor's top and one above it.
        var anchor = CreateAnchor(left: 10, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Placement = PopupPlacement.Below,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup with no room on either side");

        // Assert
        popup.ResolvedPlacement.ShouldBe(PopupPlacement.Below);
        popup.SurfaceBounds.ShouldBe(new Rect(10, 2, 8, 3));
        popup.SurfaceBounds.Bottom.ShouldBe(5);
    }

    /// <summary>Verifies content wider than the host is clamped to the host width and pinned at
    /// the host origin rather than overflowing past the right edge.</summary>
    [Fact]
    public async Task Placement_WhenContentIsWiderThanHost_ClampsSurfaceToHostWidthAsync()
    {
        // Arrange
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("MenuMenuMenu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup wider than its host");

        // Assert
        popup.SurfaceBounds.X.ShouldBe(0);
        popup.SurfaceBounds.Width.ShouldBe(12);
        popup.SurfaceBounds.Y.ShouldBe(4);
        surface.Cell(new Point(0, 4)).Text.ShouldBe("╭");
        surface.Cell(new Point(11, 4)).Text.ShouldBe("╮");
    }

    /// <summary>Verifies a popup anchored near the right edge with content narrower than the
    /// anchor is pushed left so its anchor-width surface stays inside the host.</summary>
    [Fact]
    public async Task Placement_WhenAnchorWidthSurfaceOverhangsRightEdge_ShiftsSurfaceLeftAsync()
    {
        // Arrange - the anchor spans columns 20..27 on a 25-column host, so its border box overhangs.
        var anchor = CreateAnchor(left: 20, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Hi"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(25, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup near the right edge");

        // Assert - the anchor-width surface (8) ends exactly at the host edge.
        popup.SurfaceBounds.ShouldBe(new Rect(17, 4, 8, 3));
    }

    /// <summary>Verifies the surface width is the larger of the anchor width and the framed
    /// content width, for both a wide anchor and a wide content.</summary>
    [Theory]
    [InlineData("Hi", 8)]
    [InlineData("A very wide item", 18)]
    public async Task Placement_WhenAnchorAndContentWidthsDiffer_UsesTheWiderOfTheTwoAsync(
        string content,
        int expectedWidth)
    {
        // Arrange
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText(content),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");

        // Assert
        popup.SurfaceBounds.Width.ShouldBe(expectedWidth);
        popup.SurfaceBounds.X.ShouldBe(2);
    }

    /// <summary>Verifies changing Placement while the popup is open re-arranges the surface against
    /// the new anchor edge without closing or reopening it.</summary>
    [Fact]
    public async Task Placement_WhenChangedWhileOpen_RearrangesWithoutReopeningAsync()
    {
        // Arrange
        var opened = 0;
        var closed = 0;
        var anchor = CreateAnchor(left: 10, top: 4);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        popup.Opened += (_, _) => opened++;
        popup.Closed += (_, _) => closed++;
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup below");
        popup.SurfaceBounds.ShouldBe(new Rect(10, 7, 8, 3));

        // Act
        await surface.UpdateAsync(() => popup.Placement = PopupPlacement.Right, "move Popup to the right");

        // Assert
        popup.IsOpen.ShouldBeTrue();
        popup.ResolvedPlacement.ShouldBe(PopupPlacement.Right);
        popup.SurfaceBounds.ShouldBe(new Rect(18, 4, 8, 3));
        surface.Cell(new Point(10, 7)).Text.ShouldNotBe("╭");
        surface.Cell(new Point(18, 4)).Text.ShouldBe("╭");
        opened.ShouldBe(1);
        closed.ShouldBe(0);
    }

    /// <summary>Verifies moving the anchor while the popup is open drags the surface with it and
    /// re-resolves a flip when the anchor reaches the host edge.</summary>
    [Fact]
    public async Task Anchor_WhenMovedWhileOpen_FollowsAndReflipsAsync()
    {
        // Arrange
        var anchor = CreateAnchor(left: 10, top: 2);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");
        popup.SurfaceBounds.ShouldBe(new Rect(10, 5, 8, 3));

        // Act
        await surface.UpdateAsync(() => Overlay.SetLeft(anchor, Length.Cells(4)), "move anchor left");

        // Assert
        popup.SurfaceBounds.ShouldBe(new Rect(4, 5, 8, 3));

        // Act
        await surface.UpdateAsync(() => Overlay.SetTop(anchor, Length.Cells(9)), "move anchor to the bottom");

        // Assert
        popup.ResolvedPlacement.ShouldBe(PopupPlacement.Above);
        popup.SurfaceBounds.ShouldBe(new Rect(4, 6, 8, 3));
        popup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies shrinking the host while the popup is open re-resolves the flip and keeps
    /// the surface inside the new bounds.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHostShrinksBelowOpenPopup_FlipsAndStaysInsideAsync()
    {
        // Arrange
        var anchor = CreateAnchor(left: 10, top: 4);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");
        popup.SurfaceBounds.ShouldBe(new Rect(10, 7, 8, 3));

        // Act
        await surface.ResizeAsync(new Size(30, 8));

        // Assert
        popup.IsOpen.ShouldBeTrue();
        popup.ResolvedPlacement.ShouldBe(PopupPlacement.Above);
        popup.SurfaceBounds.ShouldBe(new Rect(10, 1, 8, 3));
        surface.Cell(new Point(10, 1)).Text.ShouldBe("╭");
    }

    #endregion

    #region Lifecycle events

    /// <summary>Verifies Opened fires exactly once per opening, observes the committed open state
    /// before any surface bounds exist, and is not re-raised by a redundant open.</summary>
    [Fact]
    public async Task Opened_WhenPopupOpens_FiresOnceWithOpenStateBeforeBoundsCommitAsync()
    {
        // Arrange
        var opened = 0;
        var isOpenAtOpened = false;
        var boundsAtOpened = new Rect(1, 1, 1, 1);
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        popup.Opened += (_, _) =>
        {
            opened++;
            isOpenAtOpened = popup.IsOpen;
            boundsAtOpened = popup.SurfaceBounds;
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");

        // Assert
        opened.ShouldBe(1);
        isOpenAtOpened.ShouldBeTrue();
        boundsAtOpened.ShouldBe(default);
        popup.SurfaceBounds.ShouldNotBe(default);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "redundantly open Popup");

        // Assert
        opened.ShouldBe(1);
    }

    /// <summary>Verifies the state visible from Closing and Closed: at Closing the popup already
    /// reports closed but its last bounds and visible content are still readable; at Closed the
    /// bounds are cleared and the content is collapsed.</summary>
    [Fact]
    public async Task Closing_WhenPopupCloses_ExposesLastBoundsThenClearsThemAtClosedAsync()
    {
        // Arrange
        var content = new ControlText("Menu");
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = content,
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");
        var openBounds = popup.SurfaceBounds;
        openBounds.ShouldNotBe(default);
        var order = new List<string>();
        popup.Closing += (_, _) =>
        {
            order.Add("Closing");
            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(openBounds);
            content.Visibility.ShouldBe(Visibility.Visible);
        };
        popup.Closed += (_, _) =>
        {
            order.Add("Closed");
            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            content.Visibility.ShouldBe(Visibility.Collapsed);
        };

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = false, "close Popup");

        // Assert
        order.ShouldBe(["Closing", "Closed"]);
        surface.Cell(new Point(openBounds.X, openBounds.Y)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies a second CloseRequested subscriber can veto after the first one accepted,
    /// and that a later close request receives a fresh, uncancelled args instance.</summary>
    [Fact]
    public async Task CloseRequested_WhenLaterSubscriberVetoes_KeepsPopupOpenUntilNextRequestAsync()
    {
        // Arrange
        var firstSaw = new List<bool>();
        var veto = true;
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        popup.CloseRequested += (_, args) => firstSaw.Add(args.Cancel);
        popup.CloseRequested += (_, args) => args.Cancel = veto;
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = false, "request a vetoed close");

        // Assert
        popup.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.ShouldNotBe(default);

        // Act
        veto = false;
        await surface.UpdateAsync(() => popup.IsOpen = false, "request an accepted close");

        // Assert
        popup.IsOpen.ShouldBeFalse();
        firstSaw.ShouldBe([false, false]);
    }

    /// <summary>Verifies reopening a presented popup from its own Closing handler is rejected
    /// with the documented message while the outer close still completes to Closed.</summary>
    [Fact]
    public async Task Closing_WhenHandlerReopensPresentedPopup_ThrowsAndCompletesCloseAsync()
    {
        // Arrange
        var closed = 0;
        Exception? failure = null;
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("Menu"),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        popup.Closing += (_, _) =>
        {
            try
            {
                popup.IsOpen = true;
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }
        };
        popup.Closed += (_, _) => closed++;
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = false, "close Popup with a reopening handler");

        // Assert
        failure.ShouldNotBeNull().Message.ShouldContain("cannot open while it is closing");
        closed.ShouldBe(1);
        popup.IsOpen.ShouldBeFalse();
        popup.SurfaceBounds.ShouldBe(default);
    }

    #endregion

    #region Keyboard

    /// <summary>Verifies Escape closes the popup only for activation-eligible chords: plain and
    /// Shift-modified Escape close it, while command-modified Escape leaves it open and bubbles.</summary>
    [Theory]
    [InlineData(Modifiers.None, true)]
    [InlineData(Modifiers.Shift, true)]
    [InlineData(Modifiers.Control, false)]
    [InlineData(Modifiers.Alt, false)]
    public async Task Escape_WhenModifiersVary_ClosesOnlyForActivationEligibleChordsAsync(
        Modifiers modifiers,
        bool expectClose)
    {
        // Arrange
        var escapedUnhandled = 0;
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new Button { Text = "Action" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");
        var probe = await surface.Application.Dispatcher.InvokeAsync(
            () => root.AddHandler(Events.Key, (_, args) =>
            {
                if (args.Phase == RoutingPhase.Bubble && args.Stroke.Code == Code.Escape && !args.IsHandled)
                {
                    escapedUnhandled++;
                }
            }),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape, modifiers);

        // Assert
        popup.IsOpen.ShouldBe(!expectClose);
        await surface.Application.Dispatcher.InvokeAsync(probe.Dispose, TestContext.Current.CancellationToken);

        if (expectClose)
        {
            surface.Application.Modality.Active.ShouldBeNull();
        }
        else
        {
            surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(popup);
        }
    }

    /// <summary>Verifies CloseOnEscape false leaves the popup open and passes Escape along the
    /// route so an ancestor can observe it.</summary>
    [Fact]
    public async Task Escape_WhenCloseOnEscapeIsFalse_LeavesPopupOpenAsync()
    {
        // Arrange
        var anchor = CreateAnchor(left: 2, top: 1);
        var action = new Button { Text = "Action" };
        var popup = new Popup
        {
            Anchor = anchor,
            Content = action,
            CloseOnEscape = false,
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");
        surface.ShouldHaveFocus(action);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        popup.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(action);
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(popup);
    }

    /// <summary>Verifies Tab and Shift+Tab inside an automatically modal popup cycle only through
    /// the popup's own focusable content and never reach the background.</summary>
    [Fact]
    public async Task Tab_WhenPopupIsModal_ConfinesTraversalToPopupContentAsync()
    {
        // Arrange
        var background = new Button { Text = "Background" };
        var first = new Button { Text = "First" };
        var second = new Button { Text = "Second" };
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new Stack { Children = { first, second } },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        Overlay.SetTop(background, Length.Cells(10));
        var root = new Overlay { Children = { background, anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(background).ShouldBeTrue(),
            "focus the background");

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open modal Popup");

        // Assert
        surface.ShouldHaveFocus(first);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(second);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);

        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(second);
        background.IsFocused.ShouldBeFalse();

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        popup.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(background);
    }

    /// <summary>Verifies Escape with a nested popup open closes only the innermost popup and
    /// leaves the outer presentation and its scope active.</summary>
    [Fact]
    public async Task Escape_WhenNestedPopupIsOpen_ClosesOnlyInnermostAsync()
    {
        // Arrange
        var innerAnchor = new Button { Text = "Inner anchor", Width = Length.Cells(12), Height = Length.Cells(1) };
        var inner = new Popup
        {
            Anchor = innerAnchor,
            Content = new Button { Text = "Inner" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var outerAnchor = CreateAnchor(left: 2, top: 0);
        var outer = new Popup
        {
            Anchor = outerAnchor,
            Content = new Overlay { Children = { innerAnchor, inner } },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { outerAnchor, outer } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 14),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => outer.IsOpen = true, "open outer Popup");
        var outerScope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.UpdateAsync(() => inner.IsOpen = true, "open inner Popup");
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(inner);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        inner.IsOpen.ShouldBeFalse();
        outer.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(outerScope);
        outerScope.IsActive.ShouldBeTrue();
        surface.ShouldHaveFocus(innerAnchor);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        outer.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    #endregion

    #region Modality policy

    /// <summary>Verifies changing ModalBehavior while the popup is open neither exits nor enters a
    /// scope, and that the new policy applies on the next presentation.</summary>
    [Fact]
    public async Task ModalBehavior_WhenChangedWhileOpen_AppliesOnlyToTheNextPresentationAsync()
    {
        // Arrange
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new Button { Text = "Action" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open auto-modal Popup");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(() => popup.ModalBehavior = PopupModalBehavior.None, "switch policy while open");

        // Assert
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
        scope.IsActive.ShouldBeTrue();

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = false, "close Popup");
        await surface.UpdateAsync(() => popup.IsOpen = true, "reopen under the new policy");

        // Assert
        scope.IsActive.ShouldBeFalse();
        popup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeNull();

        // Act
        await surface.UpdateAsync(() => popup.ModalBehavior = PopupModalBehavior.Auto, "switch back while open");

        // Assert
        surface.Application.Modality.Active.ShouldBeNull();

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = false, "close Popup again");
        await surface.UpdateAsync(() => popup.IsOpen = true, "reopen under automatic modality");

        // Assert
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(popup);
    }

    /// <summary>Verifies an explicit OpenModal on an already-open modeless popup enters a scope on
    /// the existing presentation without re-raising Opened.</summary>
    [Fact]
    public async Task OpenModal_WhenPopupIsAlreadyOpenAndModeless_EntersScopeWithoutReopeningAsync()
    {
        // Arrange
        var opened = 0;
        var anchor = CreateAnchor(left: 2, top: 1);
        var popup = new Popup
        {
            Anchor = anchor,
            ModalBehavior = PopupModalBehavior.None,
            Content = new Button { Text = "Action" },
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        popup.Opened += (_, _) => opened++;
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open modeless Popup");
        surface.Application.Modality.Active.ShouldBeNull();
        var bounds = popup.SurfaceBounds;
        ModalScope? scope = null;

        // Act
        await surface.UpdateAsync(() => scope = popup.OpenModal(OutsideInteraction.Ignore), "enter modality explicitly");

        // Assert
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
        scope.OutsideInteraction.ShouldBe(OutsideInteraction.Ignore);
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
        opened.ShouldBe(1);
        popup.SurfaceBounds.ShouldBe(bounds);

        // Act
        await surface.UpdateAsync(scope.Dispose, "exit the explicit scope");

        // Assert - external exit closes the popup family presentation
        popup.IsOpen.ShouldBeFalse();
    }

    #endregion

    private static Button CreateAnchor(int left, int top)
    {
        var anchor = new Button
        {
            Text = "Anchor",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        Overlay.SetLeft(anchor, Length.Cells(left));
        Overlay.SetTop(anchor, Length.Cells(top));
        return anchor;
    }
}
