// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Windows;

/// <summary>Proves window focus and interaction through mounted terminal surfaces.</summary>
public sealed class WindowSurfaceTests
{
    /// <summary>Verifies focus and primary clicks switch Application activation without conflating keyboard focus.</summary>
    [Fact]
    public async Task Activation_WhenFocusAndWindowChromeChangeTargets_SwitchesWithoutStealingFocusAsync()
    {
        var firstAction = new Button { Content = new ControlText("First") };
        var first = new Window
        {
            Header = "First",
            Content = firstAction,
            Width = Length.Cells(10),
            Height = Length.Cells(5)
        };
        var second = new Window
        {
            Header = "Second",
            Content = new ControlText("Passive"),
            Width = Length.Cells(10),
            Height = Length.Cells(5)
        };
        Overlay.SetLeft(second, Length.Cells(12));
        var root = new Overlay { Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 7),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(firstAction).ShouldBeTrue(),
            "focus the first Window action");

        surface.Application.ActiveWindow.ShouldBeSameAs(first);
        first.IsActive.ShouldBeTrue();
        second.IsActive.ShouldBeFalse();
        surface.ShouldHaveFocus(firstAction);
        var firstBorderPoint = new Point(first.Bounds.X, first.Bounds.Y);
        var secondBorderPoint = new Point(second.Bounds.X, second.Bounds.Y);
        var activeBorder = surface.Cell(firstBorderPoint).Style.Foreground;
        var normalBorder = surface.Cell(secondBorderPoint).Style.Foreground;
        activeBorder.ShouldNotBe(normalBorder);

        await surface.Pointer.MoveToAsync(second, new Point(1, 0));
        surface.Application.Capture.Hovered.ShouldBeSameAs(second);
        await surface.Pointer.PressAsync();

        surface.Application.ActiveWindow.ShouldBeSameAs(second);

        await surface.Pointer.ReleaseAsync();

        surface.Application.ActiveWindow.ShouldBeSameAs(second);
        first.IsActive.ShouldBeFalse();
        second.IsActive.ShouldBeTrue();
        first.ContainsFocus.ShouldBeTrue();
        second.ContainsFocus.ShouldBeFalse();
        surface.ShouldHaveFocus(firstAction);
        surface.Cell(firstBorderPoint).Style.Foreground.ShouldBe(normalBorder);
        surface.Cell(secondBorderPoint).Style.Foreground.ShouldBe(activeBorder);
    }

    /// <summary>Verifies Window chrome ignores hover and activates only when focus enters its subtree.</summary>
    [Fact]
    public async Task Theme_WhenWindowHoveredAndActivated_RespondsOnlyToActivationAsync()
    {
        // Arrange
        var inside = new Button { Content = new ControlText("Inside") };
        var window = new Window
        {
            Content = inside,
            Width = Length.Cells(10),
            Height = Length.Cells(5)
        };
        var outside = new Button
        {
            Content = new ControlText("Outside"),
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        Overlay.SetLeft(outside, Length.Cells(12));
        var root = new Overlay { Children = { window, outside } };
        var capabilities = Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };
        var options = TerminalOptions.Minimal with { Capabilities = capabilities };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 7),
            options,
            TestContext.Current.CancellationToken);
        var theme = Themes.Load("catppuccin-mocha");
        await surface.UpdateAsync(
            () =>
            {
                surface.Application.Theme = theme;
                surface.Application.Focus.Focus(outside).ShouldBeTrue();
            },
            "apply theme and deactivate Window");
        var borderPoint = new Point(window.Bounds.X, window.Bounds.Y);
        var normalBorder = surface.Cell(borderPoint).Style.Foreground;
        var normalFace = window.ActualFace;

        // Act and assert hover is inert
        await surface.Pointer.MoveToAsync(window, new Point(1, 1));
        window.IsPointerOver.ShouldBeTrue();
        window.ContainsFocus.ShouldBeFalse();
        surface.Cell(borderPoint).Style.Foreground.ShouldBe(normalBorder);

        // Act and assert focus ancestry activates only the border
        await surface.Pointer.LeaveAsync();
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(inside).ShouldBeTrue(),
            "activate Window through retained content focus");

        window.ContainsFocus.ShouldBeTrue();
        surface.Cell(borderPoint).Style.Foreground.ShouldBe(theme.ResolveColor(ThemeColor.ActiveBorder));
        window.ActualFace.ShouldBe(normalFace);
    }

    /// <summary>Verifies specialized Windows use the same unmistakable paired frame as ordinary Windows.</summary>
    [Fact]
    public async Task Render_WhenDialogWindowIsMounted_DrawsPairedFrameAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Dialog",
            CanMove = false,
            CanClose = true,
            HeaderPlacement = WindowTitlePlacement.Center,
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(14, 6),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(default).Text.ShouldBe("╔");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("║");
        surface.Cell(new Point(0, 3)).Text.ShouldBe("╚");
        surface.Cell(new Point(1, 3)).Text.ShouldBe("═");
        surface.Cell(new Point(11, 3)).Text.ShouldBe("╝");
    }

    /// <summary>Verifies the default Window body paints its semantic background over untouched cells.</summary>
    [Fact]
    public async Task Render_WhenDefaultWindowIsMounted_FillsSemanticWindowBackgroundAsync()
    {
        // Arrange
        var window = new Window { Header = "Solid", Width = Length.Cells(8), Height = Length.Cells(4) };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(10, 6),
            TestContext.Current.CancellationToken);

        // Assert
        var expected = Palette.Project(
            ThemeColorHelper.WindowBackground(window.Theme.ShouldNotBeNull()),
            ColorDepth.Basic16);
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(expected);
        surface.Cell(new Point(1, 1)).Style.Background.ShouldNotBe(Color.Default);
    }

    /// <summary>Verifies a closable Window renders a compact Turbo Vision-style close affordance.</summary>
    [Fact]
    public async Task Render_WhenWindowIsClosable_UsesFramedCloseChromeAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "MessageBox",
            CanClose = true,
            Width = Length.Cells(20),
            Height = Length.Cells(6)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(1, 0)).Text.ShouldBe("═");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("═");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("[");
        surface.Cell(new Point(4, 0)).Text.ShouldBe(window.CloseGlyph.ToString());
        surface.Cell(new Point(5, 0)).Text.ShouldBe("]");
        surface.Cell(new Point(6, 0)).Text.ShouldBe("═");
        surface.Cell(new Point(7, 0)).Text.ShouldBe("═");
    }

    /// <summary>Verifies close-mark states remain local while its primary press activates the Window frame.</summary>
    [Fact]
    public async Task Pointer_WhenCloseAffordanceChangesState_ChangesOnlyMarkForegroundAsync()
    {
        // Arrange
        var window = new Window
        {
            CanClose = true,
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(14, 6),
            TestContext.Current.CancellationToken);
        var theme = window.Theme.ShouldNotBeNull();
        var normal = ThemeColorHelper.Accent(theme);
        var hovered = ThemeColorHelper.HoveredForeground(theme);
        var pressed = ThemeColorHelper.PressedForeground(theme);
        var frame = surface.Cell(new Point(3, 0)).Style.Foreground;
        var background = surface.Cell(new Point(4, 0)).Style.Background;

        // Assert normal
        normal.ShouldNotBe(frame);
        hovered.ShouldNotBe(pressed);
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(normal);

        // Act and assert hover
        await surface.Pointer.MoveToAsync(window, new Point(4, 0));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(hovered);
        var hoveredFrame = surface.Cell(new Point(3, 0)).Style.Foreground;
        hoveredFrame.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(4, 0)).Style.Background.ShouldBe(background);

        // Act and assert held press
        await surface.Pointer.PressAsync();
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(pressed);
        window.IsActive.ShouldBeTrue();
        var activeFrame = surface.Cell(new Point(3, 0)).Style.Foreground;
        activeFrame.ShouldNotBe(hoveredFrame);
        surface.Cell(new Point(4, 0)).Style.Background.ShouldBe(background);

        // Act and assert release
        await surface.Pointer.ReleaseAsync();
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(hovered);
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(activeFrame);

        // Act and assert pointer departure
        await surface.Pointer.MoveToAsync(window, new Point(9, 0));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(normal);
    }

    /// <summary>Verifies a dragged Window remains in the client area without repainting its parent's frame.</summary>
    [Fact]
    public async Task Drag_WhenMovedToClientEdges_PreservesParentBorderAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Move",
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay
        {
            Width = Length.Cells(20),
            Height = Length.Cells(8),
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Light),
            Padding = new Thickness(1, 0),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(20, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, default);
        await surface.Pointer.ReleaseAsync();

        // Assert top-left edge
        window.Bounds.X.ShouldBe(stage.ContentBounds.X);
        window.Bounds.Y.ShouldBe(stage.ContentBounds.Y);
        surface.Cell(default).Text.ShouldBe("┌");
        surface.Cell(new Point(0, 3)).Text.ShouldBe("│");

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(19, 7));
        await surface.Pointer.ReleaseAsync();

        // Assert bottom-right edge
        window.Bounds.Right.ShouldBe(stage.ContentBounds.Right);
        window.Bounds.Bottom.ShouldBe(stage.ContentBounds.Bottom);
        surface.Cell(new Point(19, 0)).Text.ShouldBe("┐");
        surface.Cell(new Point(19, 7)).Text.ShouldBe("┘");
        surface.Cell(new Point(19, 4)).Text.ShouldBe("│");
    }

    /// <summary>Verifies a terminal pointer-leave mid-drag ends the gesture and releases capture,
    /// so a later bare-cursor move does not keep dragging the Window (see #218).</summary>
    [Fact]
    public async Task Drag_WhenPointerLeavesTerminalMidDrag_EndsGestureAndReleasesCaptureAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Move",
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);

        // Act — begin a title drag, then leave the terminal with the button still held
        await surface.Pointer.MoveToAsync(window, new Point(4, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(10, 6));
        await surface.Pointer.LeaveAsync();

        // Assert — the gesture ended and capture is released
        surface.ShouldHaveCapture(null);
        var boundsAfterLeave = window.Bounds;

        // Act — a plain move with no button held must not drag the window
        await surface.Pointer.MoveToAsync(stage, new Point(25, 12));

        // Assert — the window did not follow the bare cursor
        window.Bounds.ShouldBe(boundsAfterLeave);
    }

    /// <summary>Verifies dragging the bottom-right corner grows the Window without moving its origin.</summary>
    [Fact]
    public async Task Resize_WhenCornerIsDraggedOutward_GrowsInPlaceAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            CanResize = true,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var originX = window.Bounds.X;
        var originY = window.Bounds.Y;

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(9, 3));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(15, 7));
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.X.ShouldBe(originX);
        window.Bounds.Y.ShouldBe(originY);
        window.Bounds.Width.ShouldBe(14);
        window.Bounds.Height.ShouldBe(7);
    }

    /// <summary>Verifies dragging the bottom-right corner of a centered Window grows it in place
    /// under the cursor instead of drifting away as ConstrainOverlaySlot re-centers the growing
    /// extent every arrange (see #174).</summary>
    [Fact]
    public async Task Resize_WhenWindowIsCentered_GrowsInPlaceUnderCursorAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            CanResize = true,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        var stage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var originX = window.Bounds.X;
        var originY = window.Bounds.Y;
        var grip = new Point(window.Bounds.Right - 1, window.Bounds.Bottom - 1);

        // Act: one diagonal move, the grip must move to exactly follow the cursor
        await surface.Pointer.MoveToAsync(stage, grip);
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(grip.X + 4, grip.Y + 4));
        await surface.Pointer.ReleaseAsync();

        // Assert: the top-left origin stayed fixed, the whole grow happened on the far edges
        window.Bounds.X.ShouldBe(originX);
        window.Bounds.Y.ShouldBe(originY);
        window.Bounds.Width.ShouldBe(14);
        window.Bounds.Height.ShouldBe(8);
    }

    /// <summary>Verifies dragging the bottom-right corner of a Right/Bottom-anchored Window grows
    /// it under the cursor instead of stalling immediately because TrailingOrigin keeps pinning
    /// the far edge as the extent grows (see #174).</summary>
    [Fact]
    public async Task Resize_WhenWindowIsAnchoredTrailing_GrowsUnderCursorInsteadOfStallingAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            CanResize = true,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetRight(window, Length.Cells(2));
        Overlay.SetBottom(window, Length.Cells(1));
        var stage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var originX = window.Bounds.X;
        var originY = window.Bounds.Y;
        var grip = new Point(window.Bounds.Right - 1, window.Bounds.Bottom - 1);

        // Act
        await surface.Pointer.MoveToAsync(stage, grip);
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(grip.X + 2, grip.Y + 1));
        await surface.Pointer.ReleaseAsync();

        // Assert: the top-left origin stayed fixed and the corner actually followed the cursor,
        // instead of the far Right/Bottom anchor keeping the window pinned near the stage edge.
        window.Bounds.X.ShouldBe(originX);
        window.Bounds.Y.ShouldBe(originY);
        window.Bounds.Width.ShouldBe(12);
        window.Bounds.Height.ShouldBe(5);
    }

    /// <summary>Verifies the resize gesture clamps to MinWidth/MinHeight instead of shrinking further.</summary>
    [Fact]
    public async Task Resize_WhenDraggedBelowMinimumSize_ClampsToMinimumAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            CanResize = true,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            MinWidth = 6,
            MinHeight = 3,
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(9, 3));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(0, 0));
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.Width.ShouldBe(6);
        window.Bounds.Height.ShouldBe(3);
    }

    /// <summary>Verifies the resize gesture clamps to a chrome-aware floor even at the documented
    /// default MinWidth/MinHeight of zero, so an ordinary inward drag never collapses the window
    /// to 0x0, and the resize corner remains hit-testable afterward (see #170).</summary>
    [Fact]
    public async Task Resize_WhenDraggedFarInwardAtDefaultMinimums_ClampsToChromeFloorAndStaysGrabbableAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            CanResize = true,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);

        // Act: drag the resize corner far past where an unclamped gesture would reach 0x0.
        await surface.Pointer.MoveToAsync(window, new Point(9, 3));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(0, 0));
        await surface.Pointer.ReleaseAsync();

        // Assert: the window stayed visible and its dimensions never touched zero.
        window.Bounds.Width.ShouldBeGreaterThan(0);
        window.Bounds.Height.ShouldBeGreaterThan(0);

        // Assert: the resize corner is still exactly where the clamped bounds say it is, so a
        // second grab succeeds instead of hitting an unreachable window.
        var corner = new Point(window.Bounds.Width - 1, window.Bounds.Height - 1);
        await surface.Pointer.MoveToAsync(window, corner);
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(20, 10));
        await surface.Pointer.ReleaseAsync();

        window.Bounds.Width.ShouldBeGreaterThan(1);
        window.Bounds.Height.ShouldBeGreaterThan(1);
    }

    /// <summary>Verifies a Window with CanResize false ignores a corner drag entirely.</summary>
    [Fact]
    public async Task Resize_WhenCanResizeIsFalse_LeavesSizeUnchangedAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            CanResize = false,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(9, 3));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(15, 7));
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.Width.ShouldBe(10);
        window.Bounds.Height.ShouldBe(4);
    }

    /// <summary>Verifies losing pointer capture mid-resize cancels the gesture cleanly.</summary>
    [Fact]
    public async Task Resize_WhenPointerCaptureIsLost_CancelsGestureAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            CanResize = true,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);

        // Act — begin a resize, then lose capture mid-gesture
        await surface.Pointer.MoveToAsync(window, new Point(9, 3));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(13, 6));
        await surface.UpdateAsync(() => window.IsEnabled = false, "disable Window mid-resize");

        // Assert — capture released, and a further move under the stale press does nothing
        surface.ShouldHaveCapture(null);
        var sizeAfterCancellation = window.Bounds;
        await surface.UpdateAsync(() => window.IsEnabled = true, "re-enable Window");
        window.Bounds.ShouldBe(sizeAfterCancellation);
    }

    /// <summary>Verifies a terminal pointer-leave mid-resize ends the gesture and releases capture,
    /// so a later bare-cursor move does not keep resizing the Window (see #218).</summary>
    [Fact]
    public async Task Resize_WhenPointerLeavesTerminalMidResize_EndsGestureAndReleasesCaptureAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            CanResize = true,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);

        // Act — begin a corner resize, then leave the terminal with the button still held
        await surface.Pointer.MoveToAsync(window, new Point(9, 3));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(13, 6));
        await surface.Pointer.LeaveAsync();

        // Assert — the gesture ended and capture is released
        surface.ShouldHaveCapture(null);
        var boundsAfterLeave = window.Bounds;

        // Act — a plain move with no button held must not resize the window
        await surface.Pointer.MoveToAsync(stage, new Point(25, 12));

        // Assert — the window did not keep resizing under the bare cursor
        window.Bounds.ShouldBe(boundsAfterLeave);
    }

    /// <summary>Verifies terminal resize pushes an Overlay-hosted Window inside the new client bounds.</summary>
    [Fact]
    public async Task ResizeAsync_WhenWindowWouldLeaveOverlay_KeepsBorderBoxInsideClientAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(18));
        Overlay.SetTop(window, Length.Cells(7));
        var canvas = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            canvas,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        window.Bounds.ShouldBe(new Rect(18, 7, 10, 4));

        // Act
        await surface.ResizeAsync(new Size(20, 8));

        // Assert
        window.Bounds.ShouldBe(new Rect(10, 4, 10, 4));
        Overlay.GetLeft(window).ShouldBe(Length.Cells(18));
        Overlay.GetTop(window).ShouldBe(Length.Cells(7));
        surface.Cell(new Point(10, 4)).Text.ShouldBe(window.Border.GlyphStyle.TopLeft.ToString());
    }

    /// <summary>Verifies a Window with FractionalBlock shadow renders half-block glyphs with transparent background.</summary>
    [Fact]
    public async Task Render_WhenWindowHasFractionalBlockShadow_UsesTransparentBackgroundAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Frac",
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            Shadow = AppearanceTestValues.Shadow(mode: ShadowMode.FractionalBlock, offset: new Point(1, 1)),
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        // Assert: window renders its body
        surface.Cell(default).Text.ShouldBe("╔");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("╚");

        // Assert: right column shadow uses half-block with transparent background
        var rightShadow = surface.Cell(new Point(10, 0));
        rightShadow.Text.ShouldBe("▄");
        rightShadow.Style.Background.ShouldBe(ReferenceColors.Get(0));
        rightShadow.Style.Attributes.ShouldBe(Attributes.Dim);

        // Assert: bottom row shadow uses half-block with transparent background
        var bottomShadow = surface.Cell(new Point(1, 3));
        bottomShadow.Text.ShouldBe("▀");
        bottomShadow.Style.Background.ShouldBe(ReferenceColors.Get(0));
        bottomShadow.Style.Attributes.ShouldBe(Attributes.Dim);

        // Assert: corner cell uses full block with transparent background
        var cornerShadow = surface.Cell(new Point(10, 3));
        cornerShadow.Text.ShouldBe("▀");
        cornerShadow.Style.Background.ShouldBe(ReferenceColors.Get(0));
        cornerShadow.Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies retained content focus, fallback activation, hover ancestry, and unavailable cleanup.</summary>
    [ComponentBehaviorEvidence(
        typeof(Window),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenWindowHostsButtons_RoutesFallbacksAndCleansFocusAsync()
    {
        // Arrange
        var accepted = 0;
        var cancelled = 0;
        var accept = new Button { Content = new ControlText("OK"), IsDefault = true };
        var cancel = new Button { Content = new ControlText("Cancel"), IsCancel = true };
        accept.Click += (_, _) => accepted++;
        cancel.Click += (_, _) => cancelled++;
        var content = new Stack { Children = { accept, cancel } };
        var window = new Window
        {
            Header = "Dialog",
            Content = content,
            Width = Length.Cells(14),
            Height = Length.Cells(8),
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Rounded),
        };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(18, 10),
            TestContext.Current.CancellationToken);

        // Act focus and hover nested content
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Pointer.MoveToAsync(accept);

        // Assert composition and focus ancestry
        content.Parent.ShouldBeSameAs(window);
        accept.Parent.ShouldBeSameAs(content);
        window.IsPointerOver.ShouldBeTrue();
        window.IsPointerDirectlyOver.ShouldBeFalse();
        window.IsFocused.ShouldBeFalse();
        window.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(accept);

        // Act fallback activation through bubbling
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        accepted.ShouldBe(1);
        cancelled.ShouldBe(1);
        window.IsPressed.ShouldBeFalse();

        // Act unavailable
        await surface.UpdateAsync(() => window.IsEnabled = false, "disable focused Window");

        // Assert cleanup
        accept.IsFocused.ShouldBeFalse();
        window.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies the default modal policy consumes mounted background activation without requesting closure.</summary>
    [Fact]
    public async Task ShowModal_WhenOutsideInteractionDefaultsToIgnore_ConsumesBackgroundClickAsync()
    {
        // Arrange
        var activations = 0;
        var background = new Button
        {
            Content = new ControlText("Background"),
            Width = Length.Cells(10),
            Height = Length.Cells(3),
        };
        Overlay.SetLeft(background, Length.Cells(20));
        Overlay.SetTop(background, Length.Cells(1));
        background.Click += (_, _) => activations++;
        var action = new Button { Content = new ControlText("Action") };
        var window = new Window
        {
            Content = action,
            Width = Length.Cells(14),
            Height = Length.Cells(6),
            Visibility = Visibility.Collapsed,
        };
        Overlay.SetLeft(window, Length.Cells(1));
        Overlay.SetTop(window, Length.Cells(1));
        var root = new Overlay { Children = { background, window } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(32, 10),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        // Act
        await surface.UpdateAsync(() => scope = window.ShowModal(), "show modal Window");
        await surface.Pointer.ClickAsync(background);

        // Assert
        activations.ShouldBe(0);
        scope.ShouldNotBeNull().OutsideInteraction.ShouldBe(OutsideInteraction.Ignore);
        scope.IsActive.ShouldBeTrue();
        window.Visibility.ShouldBe(Visibility.Visible);
        surface.ShouldHaveFocus(action);
        surface.Application.ActiveWindow.ShouldBeSameAs(window);
        window.IsActive.ShouldBeTrue();

        await surface.UpdateAsync(scope.Dispose, "end modal Window presentation");
    }

    /// <summary>Verifies the default Composite shadow renders Dim attributes below and right of the body.</summary>
    [Fact]
    public async Task Render_WhenWindowHasDefaultShadow_DrawsCompositeShadowFootprintAsync()
    {
        // Arrange — the Window theme profile supplies a visible Composite shadow at (2,1).
        var window = new Window
        {
            Header = "Shadow",
            Width = Length.Cells(14),
            Height = Length.Cells(5),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(18, 8),
            TestContext.Current.CancellationToken);

        // Assert — shadow is enabled with default config
        window.Shadow.IsVisible.ShouldBeTrue();
        window.Shadow.Mode.ShouldBe(ShadowMode.Composite);
        window.Shadow.Offset.ShouldBe(new Point(2, 1));

        // Assert — shadow cell below the window body has Dim attributes
        var shadowPoint = new Point(window.Bounds.X + 2, window.Bounds.Bottom);
        surface.Cell(shadowPoint).Style.Attributes.ShouldBe(Attributes.Dim);

        // Assert — shadow cell to the right has Dim attributes
        var rightShadow = new Point(window.Bounds.Right, window.Bounds.Y + 1);
        surface.Cell(rightShadow).Style.Attributes.ShouldBe(Attributes.Dim);

        // Assert — body cell does NOT have Dim attributes
        var bodyCell = new Point(window.Bounds.X + 1, window.Bounds.Y + 1);
        surface.Cell(bodyCell).Style.Attributes.ShouldNotBe(Attributes.Dim);
    }

    /// <summary>Verifies BlockGlyph shadow draws the configured glyph outside the Window body.</summary>
    [Fact]
    public async Task Render_WhenWindowUsesBlockShadow_DrawsGlyphOutsideBodyAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Block",
            Width = Length.Cells(12),
            Height = Length.Cells(5),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Shadow = AppearanceTestValues.Shadow(mode: ShadowMode.BlockGlyph, offset: new Point(2, 1), glyph: new Rune('░')),
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(16, 8),
            TestContext.Current.CancellationToken);

        // Assert — shadow cell below has the configured glyph
        var shadowPoint = new Point(window.Bounds.X + 2, window.Bounds.Bottom);
        surface.Cell(shadowPoint).Text.ShouldBe("░");
        surface.Cell(shadowPoint).Style.Attributes.ShouldBe(Attributes.Dim);

        // Assert — body cell does NOT contain the shadow glyph
        var bodyCell = new Point(window.Bounds.X + 1, window.Bounds.Y + 2);
        surface.Cell(bodyCell).Text.ShouldNotBe("░");
    }

    /// <summary>Verifies every mounted outside press requests dismissal again while Closing retains the Window.</summary>
    [Fact]
    public async Task ShowModal_WhenDismissClosingRetainsWindow_RequestsAgainWithoutBackgroundActivationAsync()
    {
        // Arrange
        var activations = 0;
        var closing = 0;
        var background = new Button
        {
            Content = new ControlText("Background"),
            Width = Length.Cells(10),
            Height = Length.Cells(3),
        };
        Overlay.SetLeft(background, Length.Cells(20));
        Overlay.SetTop(background, Length.Cells(1));
        background.Click += (_, _) => activations++;
        var window = new Window
        {
            Content = new Button { Content = new ControlText("Action") },
            Width = Length.Cells(14),
            Height = Length.Cells(6),
        };
        Overlay.SetLeft(window, Length.Cells(1));
        Overlay.SetTop(window, Length.Cells(1));
        window.Closing += (_, _) => closing++;
        var root = new Overlay { Children = { background, window } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(32, 10),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        // Act
        await surface.UpdateAsync(
            () => scope = window.ShowModal(OutsideInteraction.Dismiss),
            "show dismissing modal Window");
        await surface.Pointer.ClickAsync(background);
        await surface.Pointer.ClickAsync(background);

        // Assert
        closing.ShouldBe(2);
        activations.ShouldBe(0);
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
        window.Visibility.ShouldBe(Visibility.Visible);

        await surface.UpdateAsync(scope.Dispose, "end retained modal Window presentation");
    }

    /// <summary>Verifies a closable Window renders its complete paired frame, left-placed close chrome, and title.</summary>
    [Fact]
    public async Task Render_WhenClosableWindowIsMounted_DrawsCompleteFrameWithHeaderAndCloseAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Test",
            CanClose = true,
            Width = Length.Cells(20),
            Height = Length.Cells(5),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(20, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ╔══[■]══ Test ═════╗
                             ║                  ║
                             ║                  ║
                             ║                  ║
                             ╚══════════════════╝
                             """);
        surface.Cell(new Point(3, 0)).Text.ShouldBe("[");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("■");
        surface.Cell(new Point(5, 0)).Text.ShouldBe("]");
        surface.Cell(new Point(9, 0)).Text.ShouldBe("T");
    }

    /// <summary>Verifies the default Composite shadow applies dimmed style to cells beneath the window.</summary>
    [Fact]
    public async Task Render_WhenWindowHasCompositeShadow_DrawsDimmedOverlayBeneathFrameAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Win",
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act — shadow offset defaults to (2, 1), needing extra columns and rows.
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Assert — Composite shadow preserves underlying glyphs but applies Dim.
        window.Shadow.IsVisible.ShouldBeTrue();
        window.Shadow.Offset.ShouldBe(new Point(2, 1));
        surface.Cell(new Point(10, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
        surface.Cell(new Point(11, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
        surface.Cell(new Point(2, 4)).Style.Attributes.ShouldBe(Attributes.Dim);
        surface.Cell(new Point(9, 4)).Style.Attributes.ShouldBe(Attributes.Dim);
        // Body inside the frame should not carry shadow Dim.
        surface.Cell(new Point(1, 1)).Style.Attributes.ShouldNotBe(Attributes.Dim);
    }

    /// <summary>Verifies the close glyph renders with the Accent foreground in its normal unpressed state.</summary>
    [Fact]
    public async Task Render_WhenClosableWindowIsMounted_DrawsCloseGlyphWithAccentForegroundAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Close",
            CanClose = true,
            Width = Length.Cells(20),
            Height = Length.Cells(5),
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(20, 5),
            TestContext.Current.CancellationToken);

        // Assert — the close mark uses Accent, the bracket uses the border style.
        var theme = window.Theme.ShouldNotBeNull();
        var accent = ThemeColorHelper.Accent(theme);
        var borderForeground = surface.Cell(new Point(3, 0)).Style.Foreground;
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(accent);
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(borderForeground);
        surface.Cell(new Point(5, 0)).Style.Foreground.ShouldBe(borderForeground);
    }

    /// <summary>Verifies mounted close-glyph requests retain modality until the Closing owner hides the Window.</summary>
    [ComponentBehaviorEvidence(typeof(Window), ComponentBehavior.PointerActivation)]
    [Fact]
    public async Task ShowModal_WhenCloseGlyphIsActivated_ClosingOwnerDecidesModalLifetimeAsync()
    {
        // Arrange
        var closing = 0;
        var window = new Window
        {
            CanClose = true,
            Content = new Button { Content = new ControlText("Action") },
            Width = Length.Cells(14),
            Height = Length.Cells(6),
        };
        window.Closing += (_, _) =>
        {
            closing++;

            if (closing == 2)
            {
                window.Visibility = Visibility.Hidden;
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(24, 10),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        // Act retain
        await surface.UpdateAsync(() => scope = window.ShowModal(), "show closable modal Window");
        await surface.Pointer.ClickAsync(window, new Point(4, 0));

        // Assert retain
        closing.ShouldBe(1);
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
        window.Visibility.ShouldBe(Visibility.Visible);

        // Act close
        await surface.Pointer.ClickAsync(window, new Point(4, 0));

        // Assert close
        closing.ShouldBe(2);
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        window.Visibility.ShouldBe(Visibility.Hidden);
    }

    /// <summary>Verifies dragging a modal window preserves the modal scope and pointer capture.</summary>
    [Fact]
    public async Task Drag_WhenModalWindowIsDragged_PreservesModalScopeAsync()
    {
        // Arrange
        var button = new Button { Content = new ControlText("OK") };
        var window = new Window
        {
            Header = "Drag",
            Width = Length.Cells(12),
            Height = Length.Cells(5),
            Shadow = AppearanceTestValues.Shadow(visible: false),
            Content = button,
        };
        Overlay.SetLeft(window, Length.Cells(3));
        Overlay.SetTop(window, Length.Cells(2));
        var stage = new Overlay
        {
            Width = Length.Cells(24),
            Height = Length.Cells(10),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(24, 10),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        // Act — show modal
        await surface.UpdateAsync(() => scope = window.ShowModal(), "show modal");
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        var startX = window.Bounds.X;
        var startY = window.Bounds.Y;

        // Act — drag the modal window
        await surface.Pointer.MoveToAsync(window, new Point(3, 0));
        await surface.Pointer.PressAsync();
        scope.IsActive.ShouldBeTrue();
        await surface.Pointer.MovePressedToAsync(stage, new Point(10, 5));
        await surface.Pointer.ReleaseAsync();

        // Assert — modal scope preserved and position changed
        scope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
        (window.Bounds.X != startX || window.Bounds.Y != startY).ShouldBeTrue();

        // Cleanup
        await surface.UpdateAsync(scope.Dispose, "end modal");
    }
}
