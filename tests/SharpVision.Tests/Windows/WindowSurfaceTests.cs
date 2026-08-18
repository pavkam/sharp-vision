// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Windows;

using System.Text.Json;

using SharpVision.Tests.Styling;

/// <summary>Proves window focus and interaction through mounted terminal surfaces.</summary>
public sealed class WindowSurfaceTests
{
    /// <summary>Verifies focus and primary clicks switch Application activation without conflating keyboard focus.</summary>
    [Fact]
    public async Task Activation_WhenFocusAndWindowChromeChangeTargets_SwitchesWithoutStealingFocusAsync()
    {
        var firstAction = new Button { Text = "First" };
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

    /// <summary>Verifies a second Window attached to a live Overlay while already Visible becomes
    /// focused and activated on its own, instead of silently rendering behind the already-active
    /// first Window - the non-modal counterpart to the explicit Visibility fallback Window already
    /// runs for a re-shown Window, which never ran for one that starts life already Visible.</summary>
    [Fact]
    public async Task Activation_WhenWindowAttachesWhileAlreadyVisible_BecomesActiveAndRaisesAsync()
    {
        var firstAction = new Button { Text = "First" };
        var first = new Window
        {
            Header = "First",
            Content = firstAction,
            Width = Length.Cells(10),
            Height = Length.Cells(5)
        };
        var root = new Overlay { Children = { first } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 7),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(firstAction).ShouldBeTrue(),
            "focus the first Window action");

        surface.Application.ActiveWindow.ShouldBeSameAs(first);
        first.IsActive.ShouldBeTrue();

        var secondAction = new Button { Text = "Second" };
        var second = new Window
        {
            Header = "Second",
            Content = secondAction,
            Width = Length.Cells(10),
            Height = Length.Cells(5)
        };
        Overlay.SetLeft(second, Length.Cells(12));

        // Act: attach a second default-Visible Window to the live Overlay on a later UpdateAsync,
        // matching the failure scenario - a non-modal Window attached while already Visible never
        // got focused, so it never got activated or raised above the already-active first Window.
        await surface.UpdateAsync(() => root.Children.Add(second), "attach a second already-visible Window");

        // Assert: the deferred post-attach focus fallback picked up the newly attached Window's
        // first focusable descendant, which activated and raised it above its sibling.
        surface.ShouldHaveFocus(secondAction);
        surface.Application.ActiveWindow.ShouldBeSameAs(second);
        second.IsActive.ShouldBeTrue();
        first.IsActive.ShouldBeFalse();
        Overlay.GetZIndex(second).ShouldBeGreaterThan(Overlay.GetZIndex(first));
    }

    /// <summary>Verifies Window chrome ignores hover and activates only when focus enters its subtree.</summary>
    [Fact]
    public async Task Theme_WhenWindowHoveredAndActivated_RespondsOnlyToActivationAsync()
    {
        // Arrange
        var inside = new Button { Text = "Inside" };
        var window = new Window
        {
            Content = inside,
            Width = Length.Cells(10),
            Height = Length.Cells(5)
        };
        var outside = new Button
        {
            Text = "Outside",
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        Overlay.SetLeft(outside, Length.Cells(12));
        var root = new Overlay { Children = { window, outside } };
        var capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };
        var options = TerminalOptions.Minimal with { Capabilities = capabilities };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 7),
            options,
            TestContext.Current.CancellationToken);
        var theme = ThemeCatalog.Load("catppuccin-mocha");
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
        surface.Cell(borderPoint).Style.Foreground.ShouldBe(theme.ResolveColor(SemanticColor.ActiveBorder));
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
        var expected = TerminalPalette.Project(
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
        surface.Cell(new Point(4, 0)).Text.ShouldBe("■");
        surface.Cell(new Point(5, 0)).Text.ShouldBe("]");
        surface.Cell(new Point(6, 0)).Text.ShouldBe("═");
        surface.Cell(new Point(7, 0)).Text.ShouldBe("═");
    }

    /// <summary>Verifies a theme-authored close chrome reaches the rendered cells.
    ///
    /// <para>The mark and its two brackets used to come from the internal <c>ControlGlyphs</c>
    /// registry, which nothing in the theme pipeline parses. The mark had a per-instance control
    /// property; the brackets had no override at all, so a theme targeting a terminal without
    /// dependable box-drawing coverage could not produce a coherent ASCII frame by any means.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsCloseChrome_DrawsTheAuthoredGlyphsAsync()
    {
        // Arrange
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(
                windowExtra: """, "closeGlyph": "x", "closeLeftBracket": "(", "closeRightBracket": ")" """));
        var window = new Window
        {
            Header = "Window",
            CanClose = true,
            Width = Length.Cells(20),
            Height = Length.Cells(6)
        };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = theme, "author close chrome");

        // Assert
        surface.Cell(new Point(3, 0)).Text.ShouldBe("(");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("x");
        surface.Cell(new Point(5, 0)).Text.ShouldBe(")");
    }

    /// <summary>Verifies a theme-authored close-mark color reaches the rendered cell, proving
    /// <see cref="WindowStyle.CloseMarkColor"/> is genuinely theme-authorable end-to-end rather
    /// than merely structurally present on the style type.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsCloseMarkColor_DrawsTheAuthoredColorAsync()
    {
        // Arrange
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(windowExtra: """, "closeMarkColor": "error" """));
        var window = new Window
        {
            Header = "Window",
            CanClose = true,
            Width = Length.Cells(20),
            Height = Length.Cells(6)
        };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = theme, "author close-mark color");

        // Assert
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.Error, ColorDepth.Basic16));
    }

    /// <summary>Verifies close-mark states remain local while its primary press activates the Window frame.</summary>
    /// <remarks>Releases outside the close target to cancel activation, since a completed click now closes the Window by default.</remarks>
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
        var hovered = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(theme), ColorDepth.Basic16);
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

        // Act and assert releasing outside the target cancels activation, leaving the Window
        // presented so the remaining visual states below still apply.
        await surface.Pointer.MovePressedToAsync(window, new Point(9, 0));
        await surface.Pointer.ReleaseAsync();
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(activeFrame);
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(normal);

        // Act and assert re-hovering the close mark still works normally afterward
        await surface.Pointer.MoveToAsync(window, new Point(4, 0));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(hovered);

        // Act and assert pointer departure
        await surface.Pointer.MoveToAsync(window, new Point(9, 0));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(normal);
    }

    /// <summary>Verifies a theme swap confined to the directly-resolved Accent role still
    /// repaints the close-mark glyph, instead of the base role-profile comparison alone
    /// under-invalidating it.</summary>
    [Fact]
    public async Task Surface_WhenThemeSwapChangesOnlyAccent_RepaintsCloseMarkAsync()
    {
        // Arrange
        var themeA = WithColor(SemanticColor.Accent, Color.Rgb(10, 20, 30));
        var themeB = WithColor(SemanticColor.Accent, Color.Rgb(200, 210, 220));
        var window = new Window
        {
            CanClose = true,
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(14, 6),
            themeA,
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(TerminalPalette.Project(Color.Rgb(10, 20, 30), ColorDepth.Basic16));

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap Accent-only theme");

        // Assert
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(TerminalPalette.Project(Color.Rgb(200, 210, 220), ColorDepth.Basic16));
    }

    private static Theme WithColor(SemanticColor role, Color value)
    {
        var source = ThemeCatalog.Dark;
        var theme = new Theme(
            source.Palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);

        foreach (var color in Enum.GetValues<SemanticColor>())
        {
            theme.SetColor(color, color == role ? value : source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<SemanticDecoration>())
        {
            theme.SetAttributes(decoration, source.ResolveAttributes(decoration));
        }

        theme.SetStyleSections(new Dictionary<string, JsonElement>(source.StyleSections));
        theme.Freeze();
        return theme;
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
    /// so a later bare-cursor move does not keep dragging the Window.</summary>
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
    /// extent every arrange.</summary>
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
    /// the far edge as the extent grows.</summary>
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

        // Assert: resize neutralizes the stale trailing anchor by fixing Width/Height up front,
        // unlike drag, so it never needs to - and must not - clear the original Right/Bottom offsets.
        Overlay.GetRight(window).ShouldBe(Length.Cells(2));
        Overlay.GetBottom(window).ShouldBe(Length.Cells(1));
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
    /// to 0x0, and the resize corner remains hit-testable afterward.</summary>
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

    /// <summary>
    /// Verifies a resize drag never throws when MaxWidth/MaxHeight is configured below the
    /// chrome-aware resize floor (only validated against MinWidth/MinHeight, which default to
    /// zero, so this is a legally reachable configuration). The resize gesture's Math.Clamp call
    /// requires its low and high bounds to stay ordered; without also raising the effective upper
    /// bound to at least the floor, the very first resize-drag move throws ArgumentException.
    /// </summary>
    [Fact]
    public async Task Resize_WhenMaxSizeIsBelowChromeFloor_ClampsWithoutThrowingAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Resize",
            CanResize = true,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            MaxWidth = 2,
            MaxHeight = 2,
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

        // Act: MaxWidth/MaxHeight already constrained the arranged size below the requested
        // 10x4, so grab whatever corner the window actually arranged at.
        var corner = new Point(window.Bounds.Width - 1, window.Bounds.Height - 1);
        await surface.Pointer.MoveToAsync(window, corner);
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(15, 8));
        await surface.Pointer.ReleaseAsync();

        // Assert: no exception, and the window still respects the chrome-aware resize floor even
        // though MaxWidth/MaxHeight asked for something smaller than that floor.
        window.Bounds.Width.ShouldBeGreaterThan(0);
        window.Bounds.Height.ShouldBeGreaterThan(0);
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
    /// so a later bare-cursor move does not keep resizing the Window.</summary>
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
        rightShadow.Style.Attributes.ShouldBe(TerminalAttributes.Dim);

        // Assert: bottom row shadow uses half-block with transparent background
        var bottomShadow = surface.Cell(new Point(1, 3));
        bottomShadow.Text.ShouldBe("▀");
        bottomShadow.Style.Background.ShouldBe(ReferenceColors.Get(0));
        bottomShadow.Style.Attributes.ShouldBe(TerminalAttributes.Dim);

        // Assert: corner cell uses full block with transparent background
        var cornerShadow = surface.Cell(new Point(10, 3));
        cornerShadow.Text.ShouldBe("▀");
        cornerShadow.Style.Background.ShouldBe(ReferenceColors.Get(0));
        cornerShadow.Style.Attributes.ShouldBe(TerminalAttributes.Dim);
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
        ComponentBehavior.Disabled |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenWindowHostsButtons_RoutesFallbacksAndCleansFocusAsync()
    {
        // Arrange
        var accepted = 0;
        var cancelled = 0;
        var accept = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel", IsCancel = true };
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

        // Assert cleanup and direct disable
        accept.IsFocused.ShouldBeFalse();
        window.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveState(window, VisualState.Disabled);

        // Act a genuine resize while disabled and assert geometry stability against an
        // independently mounted, otherwise-identical enabled window at the same new size.
        await surface.ResizeAsync(new Size(22, 12));
        var disabledBounds = window.Bounds;
        var disabledDesiredSize = window.DesiredSize;

        var referenceAccept = new Button { Text = "OK", IsDefault = true };
        var referenceCancel = new Button { Text = "Cancel", IsCancel = true };
        var referenceContent = new Stack { Children = { referenceAccept, referenceCancel } };
        var referenceWindow = new Window
        {
            Header = "Dialog",
            Content = referenceContent,
            Width = Length.Cells(14),
            Height = Length.Cells(8),
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Rounded),
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceWindow,
            new Size(22, 12),
            TestContext.Current.CancellationToken);

        referenceWindow.Bounds.ShouldBe(disabledBounds);
        referenceWindow.DesiredSize.ShouldBe(disabledDesiredSize);

        // Act re-enable recovery
        await surface.UpdateAsync(() => window.IsEnabled = true, "re-enable Window");

        // Assert Normal state and resumed interaction
        surface.ShouldHaveState(window, VisualState.Normal);
        await surface.Pointer.MoveToAsync(accept);
        accept.IsPointerOver.ShouldBeTrue();
    }

    /// <summary>Verifies a mounted Window inherits Disabled from a disabled ancestor rather than
    /// only from its own IsEnabled flag, and resumes Normal once re-enabled.</summary>
    [ComponentBehaviorEvidence(typeof(Window), ComponentBehavior.Disabled)]
    [Fact]
    public async Task IsEnabled_WhenAncestorIsDisabled_InheritsDisabledAndRecoversAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Dialog",
            Content = new Button { Text = "OK" },
            Width = Length.Cells(14),
            Height = Length.Cells(8)
        };
        var host = new Overlay { Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(18, 10),
            TestContext.Current.CancellationToken);

        // Act ancestor disable
        await surface.UpdateAsync(() => host.IsEnabled = false, "disable ancestor Overlay");

        // Assert Window inherits Disabled without its own IsEnabled flag changing
        window.IsEnabled.ShouldBeTrue();
        window.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(window, VisualState.Disabled);

        // Act re-enable recovery
        await surface.UpdateAsync(() => host.IsEnabled = true, "re-enable ancestor Overlay");

        // Assert Normal state resumes
        surface.ShouldHaveState(window, VisualState.Normal);
    }

    /// <summary>Verifies the default modal policy consumes mounted background activation without requesting closure.</summary>
    [Fact]
    public async Task ShowModal_WhenOutsideInteractionDefaultsToIgnore_ConsumesBackgroundClickAsync()
    {
        // Arrange
        var activations = 0;
        var background = new Button
        {
            Text = "Background",
            Width = Length.Cells(10),
            Height = Length.Cells(3),
        };
        Overlay.SetLeft(background, Length.Cells(20));
        Overlay.SetTop(background, Length.Cells(1));
        background.Click += (_, _) => activations++;
        var action = new Button { Text = "Action" };
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
        surface.Cell(shadowPoint).Style.Attributes.ShouldBe(TerminalAttributes.Dim);

        // Assert — shadow cell to the right has Dim attributes
        var rightShadow = new Point(window.Bounds.Right, window.Bounds.Y + 1);
        surface.Cell(rightShadow).Style.Attributes.ShouldBe(TerminalAttributes.Dim);

        // Assert — body cell does NOT have Dim attributes
        var bodyCell = new Point(window.Bounds.X + 1, window.Bounds.Y + 1);
        surface.Cell(bodyCell).Style.Attributes.ShouldNotBe(TerminalAttributes.Dim);
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
        surface.Cell(shadowPoint).Style.Attributes.ShouldBe(TerminalAttributes.Dim);

        // Assert — body cell does NOT contain the shadow glyph
        var bodyCell = new Point(window.Bounds.X + 1, window.Bounds.Y + 2);
        surface.Cell(bodyCell).Text.ShouldNotBe("░");
    }

    /// <summary>Verifies an outside press under Dismiss actually closes the Window by default, without ever activating the background control it swallowed the press from.</summary>
    [Fact]
    public async Task ShowModal_WhenDismissRequestsClosing_ClosesByDefaultWithoutBackgroundActivationAsync()
    {
        // Arrange
        var activations = 0;
        var closing = 0;
        var closed = 0;
        var background = new Button
        {
            Text = "Background",
            Width = Length.Cells(10),
            Height = Length.Cells(3),
        };
        Overlay.SetLeft(background, Length.Cells(20));
        Overlay.SetTop(background, Length.Cells(1));
        background.Click += (_, _) => activations++;
        var window = new Window
        {
            Content = new Button { Text = "Action" },
            Width = Length.Cells(14),
            Height = Length.Cells(6),
        };
        Overlay.SetLeft(window, Length.Cells(1));
        Overlay.SetTop(window, Length.Cells(1));
        window.Closing += (_, _) => closing++;
        window.Closed += (_, _) => closed++;
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

        // Assert
        closing.ShouldBe(1);
        closed.ShouldBe(1);
        activations.ShouldBe(0);
        scope.ShouldNotBeNull().IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        window.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies a CloseRequested handler can veto an outside-press Dismiss the same way it
    /// vetoes the close affordance, Escape, and programmatic Close().</summary>
    [Fact]
    public async Task ShowModal_WhenDismissIsCancelledByCloseRequested_LeavesWindowPresentedAsync()
    {
        // Arrange
        var closing = 0;
        var window = new Window
        {
            Content = new Button { Text = "Action" },
            Width = Length.Cells(14),
            Height = Length.Cells(6),
        };
        Overlay.SetLeft(window, Length.Cells(1));
        Overlay.SetTop(window, Length.Cells(1));
        window.CloseRequested += (_, args) => args.Cancel = true;
        window.Closing += (_, _) => closing++;
        var root = new Overlay { Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(32, 10),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        // Act
        await surface.UpdateAsync(
            () => scope = window.ShowModal(OutsideInteraction.Dismiss),
            "show dismissing modal Window");
        await surface.Pointer.ClickAsync(root, new Point(30, 8));

        // Assert
        closing.ShouldBe(0);
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
        _ = surface.Application.Modality.Active.ShouldNotBeNull();
        window.Visibility.ShouldBe(Visibility.Visible);
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
        surface.Cell(new Point(10, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        surface.Cell(new Point(11, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        surface.Cell(new Point(2, 4)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        surface.Cell(new Point(9, 4)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        // Body inside the frame should not carry shadow Dim.
        surface.Cell(new Point(1, 1)).Style.Attributes.ShouldNotBe(TerminalAttributes.Dim);
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
    public async Task ShowModal_WhenCloseGlyphIsActivated_ClosesModalWindowByDefaultAsync()
    {
        // Arrange
        var closing = 0;
        var window = new Window
        {
            CanClose = true,
            Content = new Button { Text = "Action" },
            Width = Length.Cells(14),
            Height = Length.Cells(6),
        };
        window.Closing += (_, _) => closing++;
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(24, 10),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        // Act
        await surface.UpdateAsync(() => scope = window.ShowModal(), "show closable modal Window");
        await surface.Pointer.ClickAsync(window, new Point(4, 0));

        // Assert
        closing.ShouldBe(1);
        scope.ShouldNotBeNull().IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        window.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies a Closing handler that hides the Window itself is respected instead of being force-collapsed.</summary>
    [ComponentBehaviorEvidence(typeof(Window), ComponentBehavior.PointerActivation)]
    [Fact]
    public async Task ShowModal_WhenClosingHandlerHidesTheWindowItself_RespectsThatOutcomeAsync()
    {
        // Arrange
        var closing = 0;
        var window = new Window
        {
            CanClose = true,
            Content = new Button { Text = "Action" },
            Width = Length.Cells(14),
            Height = Length.Cells(6),
        };
        window.Closing += (_, _) =>
        {
            closing++;
            window.Visibility = Visibility.Hidden;
        };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(24, 10),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        // Act
        await surface.UpdateAsync(() => scope = window.ShowModal(), "show closable modal Window");
        await surface.Pointer.ClickAsync(window, new Point(4, 0));

        // Assert
        closing.ShouldBe(1);
        scope.ShouldNotBeNull().IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        window.Visibility.ShouldBe(Visibility.Hidden);
    }

    /// <summary>Verifies dragging a modal window preserves the modal scope and pointer capture.</summary>
    [Fact]
    public async Task Drag_WhenModalWindowIsDragged_PreservesModalScopeAsync()
    {
        // Arrange
        var button = new Button { Text = "OK" };
        var window = new Window
        {
            Header = "Drag",
            Width = Length.Cells(12),
            Height = Length.Cells(5),
            Shadow = AppearanceTestValues.Shadow(visible: false),
            Content = button,
            CanClose = false,
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
