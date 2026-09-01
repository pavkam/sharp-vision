// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Windows;

using System.Text.Json;

/// <summary>Proves window focus and interaction through mounted terminal surfaces.</summary>
public sealed class WindowSurfaceTests
{
    /// <summary>Verifies a modal Window with no focusable descendant still becomes the active
    /// Window instead of clearing activation when modal entry commits null focus.</summary>
    [Fact]
    public async Task ShowModal_WhenWindowHasNoFocusableContent_ActivatesModalWindowAsync()
    {
        // Arrange
        var backgroundInput = new Button { Text = "Background" };
        var background = new Window { Content = backgroundInput };
        var modal = new Window
        {
            Content = new ControlText("Please wait"),
            Visibility = Visibility.Collapsed
        };
        var root = new Overlay { Children = { background, modal } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(32, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(backgroundInput).ShouldBeTrue(),
            "activate background Window");
        surface.Application.ActiveWindow.ShouldBeSameAs(background);
        ModalScope? scope = null;

        // Act
        await surface.UpdateAsync(() => scope = modal.ShowModal(), "show passive modal Window");

        // Assert
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
        surface.Application.ActiveWindow.ShouldBeSameAs(modal);
        modal.IsActive.ShouldBeTrue();
        background.IsActive.ShouldBeFalse();

        await surface.UpdateAsync(scope.Dispose, "close passive modal Window");
    }

    /// <summary>Verifies a modal Window whose only focusable content becomes ineligible while the
    /// deferred initial-focus commit is still queued still becomes the active Window, even though
    /// the target inspected at entry time was non-null.</summary>
    [Fact]
    public async Task ShowModal_WhenQueuedInitialFocusBecomesIneligible_ActivatesModalWindowAsync()
    {
        // Arrange
        var backgroundInput = new Button { Text = "Background" };
        var background = new Window { Content = backgroundInput };
        var requested = new Button { Text = "Requested" };
        var modalInput = new Button { Text = "Modal" };
        var modal = new Window
        {
            Content = modalInput,
            Visibility = Visibility.Collapsed
        };
        var root = new Overlay { Children = { background, requested, modal } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(32, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(backgroundInput).ShouldBeTrue(),
            "activate background Window");
        surface.Application.ActiveWindow.ShouldBeSameAs(background);
        ModalScope? scope = null;

        void OnChanging(object? _, FocusChangingEventArgs eventArgs)
        {
            if (scope is null && ReferenceEquals(eventArgs.Next, requested))
            {
                scope = modal.ShowModal();
                modalInput.IsEnabled = false;
            }
        }

        surface.Application.Focus.Changing += OnChanging;

        // Act
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(requested).ShouldBeFalse(),
            "request focus while the modal's initial target becomes ineligible before it settles");

        // Assert
        var active = scope.ShouldNotBeNull();
        active.IsActive.ShouldBeTrue();
        surface.Application.ActiveWindow.ShouldBeSameAs(modal);
        modal.IsActive.ShouldBeTrue();
        background.IsActive.ShouldBeFalse();
        surface.Application.Focus.Focused.ShouldBeNull();

        surface.Application.Focus.Changing -= OnChanging;
        await surface.UpdateAsync(scope.Dispose, "close modal Window");
    }

    /// <summary>Verifies ancestor availability and reparenting transitions clear retained close
    /// hover when the framework pointer path is retired without routing a raw Leave event.</summary>
    [Theory]
    [InlineData("hide")]
    [InlineData("disable")]
    [InlineData("detach")]
    [InlineData("reparent")]
    public async Task Hover_WhenAncestorTransitionClearsPointerPath_ClearsCloseHoverAsync(string transition)
    {
        var window = new Window
        {
            CanClose = true,
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };
        var source = new Overlay
        {
            Width = Length.Cells(14),
            Height = Length.Cells(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { window }
        };
        var destination = new Overlay
        {
            Width = Length.Cells(14),
            Height = Length.Cells(6),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var root = new Overlay { Children = { source, destination } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(34, 6),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(window, new Point(4, 0));
        window.HasClosePointerOver().ShouldBeTrue();

        await surface.UpdateAsync(
            () =>
            {
                switch (transition)
                {
                    case "hide":
                        source.Visibility = Visibility.Hidden;
                        break;
                    case "disable":
                        source.IsEnabled = false;
                        break;
                    case "detach":
                        root.Children.Remove(source).ShouldBeTrue();
                        break;
                    case "reparent":
                        source.Children.Remove(window).ShouldBeTrue();
                        destination.Children.Add(window);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown transition '{transition}'.");
                }
            },
            $"clear Window hover through ancestor {transition}");

        window.IsPointerOver.ShouldBeFalse();
        window.HasClosePointerOver().ShouldBeFalse();
    }

    /// <summary>Verifies a sibling modal plane clears close-chrome hover and repaints the
    /// still-visible background Window without requiring pointer movement.</summary>
    [Fact]
    public async Task Hover_WhenSiblingModalPlaneExcludesWindow_ClearsRenderedCloseHoverAsync()
    {
        var window = new Window
        {
            CanClose = true,
            Width = Length.Cells(12),
            Height = Length.Cells(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var modal = new Window
        {
            Visibility = Visibility.Collapsed,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var root = new Overlay { Children = { window, modal } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var closeCell = new Point(4, 0);
        var normalForeground = surface.Cell(closeCell).Style.Foreground;
        await surface.Pointer.MoveToAsync(window, closeCell);
        surface.Cell(closeCell).Style.Foreground.ShouldNotBe(normalForeground);
        ModalScope? scope = null;

        await surface.UpdateAsync(() => scope = modal.ShowModal(), "exclude Window with sibling modal Window");

        window.IsPointerOver.ShouldBeFalse();
        window.HasClosePointerOver().ShouldBeFalse();
        surface.Cell(closeCell).Style.Foreground.ShouldBe(normalForeground);
        await surface.UpdateAsync(scope.ShouldNotBeNull().Dispose, "end sibling modal Window");
    }

    /// <summary>Verifies active Window chrome consumes Turbo Vision's explicit
    /// "window.focusWithin.border.foreground" delta - a flat "activeBorder" color reaching every
    /// edge against WindowStyle's own Flat relief baseline, since turbo-vision's own window relief is
    /// also Flat (only Container gets Sunken there). IntrinsicBorderSurfaceTests.cs:58
    /// (turbo-vision's "container.normal.border.relief": "sunken") is the sole surviving non-Flat
    /// specimen in the whole test suite, which is why Flat is the correct default assumption
    /// everywhere else.</summary>
    /// <remarks>
    /// Before the border-relief-vs-authored-Foreground fix, this authored per-state color was
    /// silently discarded for every non-Flat relief, and every edge showed the same
    /// highlight/shade corner pattern active or not - exactly the defect
    /// <see cref="Activation_WhenFocusTargetSwitches_ChangesOwnershipWithoutStealingFocusAsync"/>
    /// also exercises.
    /// </remarks>
    [Fact]
    public async Task Render_WhenTurboVisionWindowIsMounted_ShowsFlatActiveFrameAsync()
    {
        var action = new Button { Text = "Focus" };
        var window = new Window
        {
            Header = "Turbo",
            Content = action,
            Width = Length.Cells(10),
            Height = Length.Cells(4)
        };
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(13, 6),
            options,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () =>
            {
                surface.Application.Theme = ThemeCatalog.Load("turbo-vision");
                surface.Application.Focus.Focus(action).ShouldBeTrue();
            },
            "apply Turbo Vision and activate the window");

        var activeBorder = surface.Application.Theme.ResolveColor(SemanticColor.ActiveBorder);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(activeBorder);
        surface.Cell(new Point(9, 0)).Style.Foreground.ShouldBe(activeBorder);
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(activeBorder);
        surface.Cell(new Point(9, 1)).Style.Foreground.ShouldBe(activeBorder);
        surface.Cell(new Point(0, 3)).Style.Foreground.ShouldBe(activeBorder);
        surface.Cell(new Point(9, 3)).Style.Foreground.ShouldBe(activeBorder);
    }

    /// <summary>Verifies focus and primary clicks switch Application activation without
    /// conflating keyboard focus, and that only the active Window's frame adopts its flat
    /// ActiveBorder color while the inactive one keeps its passive flat border color.</summary>
    [Fact]
    public async Task Activation_WhenFocusTargetSwitches_ChangesOwnershipWithoutStealingFocusAsync()
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
        var theme = surface.Application.Theme;
        var activeBorder = TerminalPalette.Project(theme.ResolveColor(SemanticColor.ActiveBorder), ColorDepth.Basic16);
        var passiveBorder = TerminalPalette.Project(
            theme.Resolve(second.GetActualBorder(VisualState.Normal).Foreground),
            ColorDepth.Basic16);

        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(firstAction).ShouldBeTrue(),
            "focus the first Window action");

        surface.Application.ActiveWindow.ShouldBeSameAs(first);
        first.IsActive.ShouldBeTrue();
        second.IsActive.ShouldBeFalse();
        surface.ShouldHaveFocus(firstAction);
        var firstBorderPoint = new Point(first.Bounds.X, first.Bounds.Y);
        var secondBorderPoint = new Point(second.Bounds.X, second.Bounds.Y);
        surface.Cell(firstBorderPoint).Style.Foreground.ShouldBe(activeBorder);
        surface.Cell(secondBorderPoint).Style.Foreground.ShouldBe(passiveBorder);

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
        surface.Cell(firstBorderPoint).Style.Foreground.ShouldBe(passiveBorder);
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

    /// <summary>Verifies inherited visibility and enabled changes immediately yield modeless activation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Activation_WhenActiveWindowBecomesUnavailableThroughAncestor_ActivatesFallbackAsync(
        bool disableAncestor)
    {
        var fallback = new Window
        {
            Header = "Fallback",
            Width = Length.Cells(10),
            Height = Length.Cells(5)
        };
        var active = new Window
        {
            Header = "Nested",
            Width = Length.Cells(10),
            Height = Length.Cells(5)
        };
        var host = new Overlay { Children = { active } };
        Overlay.SetLeft(host, Length.Cells(12));
        var root = new Overlay { Children = { fallback, host } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 7),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(fallback, new Point(1, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        await surface.Pointer.MoveToAsync(active, new Point(1, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        surface.Application.ActiveWindow.ShouldBeSameAs(active);

        await surface.UpdateAsync(
            () =>
            {
                if (disableAncestor)
                {
                    host.IsEnabled = false;
                }
                else
                {
                    host.Visibility = Visibility.Collapsed;
                }
            },
            "make the active Window unavailable through its ancestor");

        surface.Application.ActiveWindow.ShouldBeSameAs(fallback);
        fallback.IsActive.ShouldBeTrue();
        active.IsActive.ShouldBeFalse();
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
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            Coordinates = MouseCoordinates.Pixel
        };
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
    /// <remarks>
    /// Releases outside the close target to cancel activation, since a completed click now closes
    /// the Window by default.
    ///
    /// <para>The frame itself only reacts to genuine Window activation (FocusWithin), not to mere
    /// pointer-over of the close mark - "window" authors no "pointerOver" delta, so hovering alone
    /// leaves the frame at its Flat baseline exactly like Normal (WindowStyle's own relief default is
    /// Flat; IntrinsicBorderSurfaceTests.cs:58 is the sole surviving non-Flat specimen in the whole
    /// test suite, which is why Flat is the correct default assumption everywhere else), and only
    /// pressing (which also focuses/activates the Window) switches it to the flat ActiveBorder color
    /// Theme.BuildWindowStyleSet's code-owned default has always intended.</para>
    /// </remarks>
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

        // Act and assert hover - the frame does not react, since "window" authors no
        // "pointerOver" delta of its own.
        await surface.Pointer.MoveToAsync(window, new Point(4, 0));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(hovered);
        var hoveredFrame = surface.Cell(new Point(3, 0)).Style.Foreground;
        hoveredFrame.IsRgb.ShouldBeTrue();
        hoveredFrame.ShouldBe(frame);
        surface.Cell(new Point(4, 0)).Style.Background.ShouldBe(background);

        // Act and assert held press - pressing the close mark also focuses/activates the Window,
        // which switches the frame to its own flat ActiveBorder color, distinct from the passive
        // bezel hovering alone left untouched above.
        await surface.Pointer.PressAsync();
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(pressed);
        window.IsActive.ShouldBeTrue();
        var activeFrame = surface.Cell(new Point(3, 0)).Style.Foreground;
        activeFrame.ShouldNotBe(hoveredFrame);
        activeFrame.ShouldBe(TerminalPalette.Project(theme.ResolveColor(SemanticColor.ActiveBorder), ColorDepth.Basic16));
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

    /// <summary>Verifies leaving the terminal entirely while the close mark is pressed - with no
    /// drag or resize in flight - still cancels the press and releases capture, matching
    /// PressBehavior's ordinary Leave handling unchanged. This is the non-dragging counterpart to
    /// the drag/resize-in-flight Leave tests: Window must route a Leave to the close chrome
    /// normally whenever no gesture actually owns the capture, and only skip it while one does.</summary>
    [Fact]
    public async Task Close_WhenPointerLeavesTerminalWhilePressedWithoutDragging_CancelsPressAndReleasesCaptureAsync()
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
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        var closeCell = new Point(4, 0);
        var normalForeground = surface.Cell(closeCell).Style.Foreground;

        // Act - press and hold the close mark, then leave the terminal without ever dragging
        await surface.Pointer.MoveToAsync(window, closeCell);
        await surface.Pointer.PressAsync();
        surface.Cell(closeCell).Style.Foreground.ShouldNotBe(normalForeground);
        surface.ShouldHaveCapture(window);
        await surface.Pointer.LeaveAsync();

        // Assert - the press is cancelled and capture is released, exactly as before Window
        // learned to withhold routing to the close chrome while a drag or resize is in flight
        surface.ShouldHaveCapture(null);
        surface.Cell(closeCell).Style.Foreground.ShouldBe(normalForeground);
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

    /// <summary>Verifies moving a dual-anchored flexible Window preserves its resolved extent when
    /// the drag converts trailing-anchor placement into leading-anchor placement.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Drag_WhenFlexibleWindowHasBothAnchors_PreservesResolvedSizeAsync(bool isStar)
    {
        // Arrange
        var window = new Window
        {
            Header = "Move",
            Width = isStar ? Length.Star(1) : Length.Auto,
            Height = isStar ? Length.Star(1) : Length.Auto,
            Shadow = AppearanceTestValues.Shadow(visible: false),
        };
        Overlay.SetLeft(window, Length.Cells(5));
        Overlay.SetTop(window, Length.Cells(1));
        Overlay.SetRight(window, Length.Cells(5));
        Overlay.SetBottom(window, Length.Cells(2));
        var stage = new Overlay
        {
            Width = Length.Cells(50),
            Height = Length.Cells(15),
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(50, 15),
            TestContext.Current.CancellationToken);
        var boundsBeforeDrag = window.Bounds;

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(
            stage,
            new Point(boundsBeforeDrag.X + 5, boundsBeforeDrag.Y + 2));
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.Width.ShouldBe(boundsBeforeDrag.Width);
        window.Bounds.Height.ShouldBe(boundsBeforeDrag.Height);
        window.Bounds.X.ShouldBe(boundsBeforeDrag.X + 3);
        window.Bounds.Y.ShouldBe(boundsBeforeDrag.Y + 2);
        Overlay.GetRight(window).ShouldBeNull();
        Overlay.GetBottom(window).ShouldBeNull();
    }

    /// <summary>Verifies revoking move permission during a captured title drag prevents every
    /// later move in that gesture from changing the Window position.</summary>
    [Fact]
    public async Task Drag_WhenCanMoveBecomesFalseMidGesture_StopsMovingAsync()
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

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(8, 4));
        var boundsAfterFirstMove = window.Bounds;
        await surface.UpdateAsync(() => window.CanMove = false, "lock position mid-gesture");
        await surface.Pointer.MovePressedToAsync(stage, new Point(15, 8));
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.ShouldBe(boundsAfterFirstMove);
        surface.ShouldHaveCapture(null);
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
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);

        // A bubble handler registered on the stage (without handledEventsToo) only runs for a
        // route that reaches it still unhandled - see Router.RouteCore. Window defaults CanClose
        // to true, so its close-chrome PressBehavior gets first crack at every pointer event
        // ahead of HandlePointerDrag; this proves the in-flight drag's own Leave/Release branch -
        // not the close chrome, which never armed a press here - is the one that claims this
        // Leave, instead of the event silently escaping the Window unhandled.
        var leaveEscapedWindowUnhandled = false;
        using var unhandledLeaveProbe = stage.AddHandler(Events.Pointer, (_, args) =>
        {
            if (args.Phase == RoutingPhase.Bubble && args.Pointer.Action == PointerAction.Leave)
            {
                leaveEscapedWindowUnhandled = true;
            }
        });

        // Act — begin a title drag, then leave the terminal with the button still held
        await surface.Pointer.MoveToAsync(window, new Point(4, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(10, 6));
        await surface.Pointer.LeaveAsync();

        // Assert — the gesture ended and capture is released, and the Window itself claimed the
        // Leave that ended it (matching every other in-flight drag/resize termination).
        surface.ShouldHaveCapture(null);
        leaveEscapedWindowUnhandled.ShouldBeFalse();
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
            MinWidth = Length.Cells(6),
            MinHeight = Length.Cells(3),
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

    /// <summary>Verifies interactive resize resolves percentage floors from the parent content
    /// bounds rather than treating their numeric percentage values as cells.</summary>
    [Fact]
    public async Task Resize_WhenMinimumSizeIsRelative_ClampsAgainstParentExtentAsync()
    {
        var window = new Window
        {
            Header = "Resize",
            CanResize = true,
            Width = Length.Cells(20),
            Height = Length.Cells(6),
            MinWidth = Length.Percent(50),
            MinHeight = Length.Percent(20),
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

        await surface.Pointer.MoveToAsync(window, new Point(19, 5));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(0, 0));
        await surface.Pointer.ReleaseAsync();

        window.Bounds.Width.ShouldBe(15);
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
            MaxWidth = Length.Cells(2),
            MaxHeight = Length.Cells(2),
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

    /// <summary>Verifies revoking resize permission during a captured corner drag prevents every
    /// later move in that gesture from changing the Window extent.</summary>
    [Fact]
    public async Task Resize_WhenCanResizeBecomesFalseMidGesture_StopsGrowingAsync()
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

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(9, 3));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(15, 7));
        var boundsAfterFirstGrow = window.Bounds;
        await surface.UpdateAsync(() => window.CanResize = false, "lock size mid-gesture");
        await surface.Pointer.MovePressedToAsync(stage, new Point(22, 12));
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.ShouldBe(boundsAfterFirstGrow);
        surface.ShouldHaveCapture(null);
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
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);

        // See the analogous drag test above for why an unhandled-only stage handler proves the
        // Window's own gesture-ending Leave branch - not the close-chrome PressBehavior that
        // otherwise gets first crack at every pointer event - is the one that claims this Leave.
        var leaveEscapedWindowUnhandled = false;
        using var unhandledLeaveProbe = stage.AddHandler(Events.Pointer, (_, args) =>
        {
            if (args.Phase == RoutingPhase.Bubble && args.Pointer.Action == PointerAction.Leave)
            {
                leaveEscapedWindowUnhandled = true;
            }
        });

        // Act — begin a corner resize, then leave the terminal with the button still held
        await surface.Pointer.MoveToAsync(window, new Point(9, 3));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(13, 6));
        await surface.Pointer.LeaveAsync();

        // Assert — the gesture ended and capture is released, and the Window itself claimed the
        // Leave that ended it.
        surface.ShouldHaveCapture(null);
        leaveEscapedWindowUnhandled.ShouldBeFalse();
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
