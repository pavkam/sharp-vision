// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Windows;

/// <summary>Proves Window header placement, close chrome, gesture edge cases, modal keyboard
/// confinement, and activation ordering through mounted terminal surfaces.</summary>
public sealed class WindowInteractionTests
{
    #region Header placement

    /// <summary>Verifies a centered header beside right-placed close chrome stays entirely inside
    /// the title lane instead of being clipped under the chrome. Centering is computed against the
    /// whole interior, and with right-placed chrome the lane ends seven cells short of it, so the
    /// run must be pushed back into the lane rather than losing its trailing cells.</summary>
    [Fact]
    public async Task Render_WhenHeaderIsCenteredBesideRightCloseChrome_KeepsWholeHeaderInsideLaneAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Settings",
            CanClose = true,
            ClosePlacement = WindowClosePlacement.Right,
            HeaderPlacement = WindowTitlePlacement.Center,
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        // Assert - the lane is columns 1..11; the ten-cell run fits only at offset one.
        surface.Cell(new Point(2, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("S");
        surface.Cell(new Point(10, 0)).Text.ShouldBe("s");
        surface.Cell(new Point(11, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(14, 0)).Text.ShouldBe("[");
        surface.Cell(new Point(15, 0)).Text.ShouldBe("■");
        surface.Cell(new Point(16, 0)).Text.ShouldBe("]");
    }

    /// <summary>Verifies every header placement resolves its exact start column for each close
    /// chrome arrangement: no chrome, left chrome, and right chrome.</summary>
    [Theory]
    [InlineData("none", WindowTitlePlacement.Left, 2)]
    [InlineData("none", WindowTitlePlacement.Center, 9)]
    [InlineData("none", WindowTitlePlacement.Right, 16)]
    [InlineData("left", WindowTitlePlacement.Left, 9)]
    [InlineData("left", WindowTitlePlacement.Center, 9)]
    [InlineData("left", WindowTitlePlacement.Right, 16)]
    [InlineData("right", WindowTitlePlacement.Left, 2)]
    [InlineData("right", WindowTitlePlacement.Center, 9)]
    [InlineData("right", WindowTitlePlacement.Right, 9)]
    public async Task Render_WhenHeaderPlacementAndCloseChromeVary_StartsHeaderAtExpectedColumnAsync(
        string closeChrome,
        WindowTitlePlacement placement,
        int expectedColumn)
    {
        // Arrange
        var window = new Window
        {
            Header = "Hi",
            CanClose = closeChrome != "none",
            ClosePlacement = closeChrome == "right" ? WindowClosePlacement.Right : WindowClosePlacement.Left,
            HeaderPlacement = placement,
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(expectedColumn - 1, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(expectedColumn, 0)).Text.ShouldBe("H");
        surface.Cell(new Point(expectedColumn + 1, 0)).Text.ShouldBe("i");
        surface.Cell(new Point(expectedColumn + 2, 0)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies a wide-glyph header centers by cell width and never splits a glyph.</summary>
    [Fact]
    public async Task Render_WhenWideHeaderIsCentered_CentersByCellWidthWithoutSplittingGlyphsAsync()
    {
        // Arrange - "界界" measures four cells, plus the two padding spaces: offset (18 - 6) / 2 = 6.
        var window = new Window
        {
            Header = "界界",
            CanClose = false,
            HeaderPlacement = WindowTitlePlacement.Center,
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(7, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(8, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(9, 0)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(10, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(11, 0)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(12, 0)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies an overflowing header is ellipsized by cell width and the truncated run
    /// is then placed by HeaderPlacement inside the lane: a wide glyph that would straddle the cut
    /// is dropped whole, leaving one cell of slack that Right uses while Left and Center (which
    /// rounds the half cell down) do not, and both corners survive under every placement.</summary>
    [Theory]
    [InlineData(WindowTitlePlacement.Left, 0)]
    [InlineData(WindowTitlePlacement.Center, 0)]
    [InlineData(WindowTitlePlacement.Right, 1)]
    public async Task Render_WhenHeaderOverflowsLaneAtAWideGlyph_EllipsizesThenPlacesTheShorterRunAsync(
        WindowTitlePlacement placement,
        int expectedOffset)
    {
        // Arrange - 14 narrow glyphs plus three wide ones need 22 cells with padding; the lane is
        // 18 wide, so 15 cells remain for glyphs before the ellipsis, and the first wide glyph
        // would straddle the cut at cell 15.
        var window = new Window
        {
            Header = "ABCDEFGHIJKLMN界界界",
            CanClose = false,
            HeaderPlacement = placement,
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("╔");
        surface.Cell(new Point(1 + expectedOffset, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(2 + expectedOffset, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(15 + expectedOffset, 0)).Text.ShouldBe("N");
        surface.Cell(new Point(16 + expectedOffset, 0)).Text.ShouldBe("…");
        surface.Cell(new Point(17 + expectedOffset, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(19, 0)).Text.ShouldBe("╗");

        for (var x = 1; x < 19; x++)
        {
            surface.Cell(new Point(x, 0)).Text.ShouldNotBe("界");
        }
    }

    /// <summary>Verifies a header whose lane is narrower than four cells is clipped to the lane
    /// without an ellipsis and never overwrites the close chrome or the far corner.</summary>
    [Fact]
    public async Task Render_WhenLaneIsNarrowerThanFourCells_ClipsHeaderInsideLaneAsync()
    {
        // Arrange - width 11 with left chrome (columns 1..7) leaves a two-cell lane at 8..9.
        var window = new Window
        {
            Header = "Hello",
            CanClose = true,
            Width = Length.Cells(11),
            Height = Length.Cells(3),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(4, 0)).Text.ShouldBe("■");
        surface.Cell(new Point(8, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(9, 0)).Text.ShouldBe("H");
        surface.Cell(new Point(10, 0)).Text.ShouldBe("╗");
    }

    /// <summary>Verifies header text, header placement, close placement, and closability changed
    /// on a mounted window repaint the title row immediately, and that the close target follows
    /// the chrome to its new side.</summary>
    [Fact]
    public async Task Header_WhenChromePropertiesChangeWhileMounted_RepaintsTitleRowAsync()
    {
        // Arrange
        var closing = 0;
        var window = new Window
        {
            Header = "Hi",
            CanClose = false,
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        window.Closing += (_, _) => closing++;
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(2, 0)).Text.ShouldBe("H");

        // Act
        await surface.UpdateAsync(() => window.Header = "Yo", "change the header");

        // Assert
        surface.Cell(new Point(2, 0)).Text.ShouldBe("Y");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("o");

        // Act
        await surface.UpdateAsync(() => window.HeaderPlacement = WindowTitlePlacement.Right, "right-align the header");

        // Assert
        surface.Cell(new Point(2, 0)).Text.ShouldBe("═");
        surface.Cell(new Point(16, 0)).Text.ShouldBe("Y");

        // Act
        await surface.UpdateAsync(() => window.CanClose = true, "enable the close affordance");

        // Assert - left chrome appears and the right-aligned header keeps its column
        surface.Cell(new Point(4, 0)).Text.ShouldBe("■");
        surface.Cell(new Point(16, 0)).Text.ShouldBe("Y");

        // Act
        await surface.UpdateAsync(() => window.ClosePlacement = WindowClosePlacement.Right, "move the chrome right");

        // Assert - the header is pushed into the shorter lane and the old mark cell is frame again
        surface.Cell(new Point(4, 0)).Text.ShouldBe("═");
        surface.Cell(new Point(15, 0)).Text.ShouldBe("■");
        surface.Cell(new Point(9, 0)).Text.ShouldBe("Y");

        // Act - the close target moved with the chrome
        await surface.Pointer.ClickAsync(window, new Point(4, 0));
        closing.ShouldBe(0);
        window.Visibility.ShouldBe(Visibility.Visible);
        await surface.Pointer.ClickAsync(window, new Point(15, 0));

        // Assert
        closing.ShouldBe(1);
        window.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies a live host resize that shrinks the frame across the chrome thresholds
    /// degrades the affordance step by step (bracketed at nine, bare mark at eight and four, none
    /// at three) and re-resolves the close hit target each time, proven by the hover color and by
    /// the final click.</summary>
    [Fact]
    public async Task ResizeAsync_WhenFrameCrossesChromeThresholds_DegradesChromeAndReresolvesTargetAsync()
    {
        // Arrange
        var closing = 0;
        var window = new Window
        {
            CanClose = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        window.Closing += (_, _) => closing++;
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(9, 3),
            TestContext.Current.CancellationToken);
        var theme = window.Theme.ShouldNotBeNull();
        var style = theme.GetWindowStyleSet().Normal;
        var normal = TerminalPalette.Project(style.CloseMarkColor.Resolve(theme), ColorDepth.Basic16);
        var hovered = TerminalPalette.Project(style.CloseMarkActiveColor.Resolve(theme), ColorDepth.Basic16);
        normal.ShouldNotBe(hovered);
        window.Bounds.Width.ShouldBe(9);
        surface.Cell(new Point(3, 0)).Text.ShouldBe("[");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("■");
        surface.Cell(new Point(5, 0)).Text.ShouldBe("]");

        // Act
        await surface.Pointer.MoveToAsync(new Point(4, 0));

        // Assert
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(hovered);

        // Act - eight cells: bare mark at column one, the old mark column is plain frame
        await surface.ResizeAsync(new Size(8, 3));

        // Assert
        window.Bounds.Width.ShouldBe(8);
        surface.Cell(new Point(1, 0)).Text.ShouldBe("■");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("═");
        await surface.Pointer.MoveToAsync(new Point(4, 0));
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(normal);
        await surface.Pointer.MoveToAsync(new Point(1, 0));
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(hovered);

        // Act - four cells: still a bare mark
        await surface.ResizeAsync(new Size(4, 3));

        // Assert
        surface.Cell(new Point(1, 0)).Text.ShouldBe("■");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("╗");

        // Act - three cells: no mark at all, and a press on the old mark cell does not close
        await surface.ResizeAsync(new Size(3, 3));

        // Assert
        surface.Cell(new Point(1, 0)).Text.ShouldNotBe("■");
        await surface.Pointer.MoveToAsync(new Point(1, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        closing.ShouldBe(0);
        window.Visibility.ShouldBe(Visibility.Visible);

        // Act - growing back re-resolves the target, and the click now closes
        await surface.ResizeAsync(new Size(9, 3));
        surface.Cell(new Point(4, 0)).Text.ShouldBe("■");
        await surface.Pointer.MoveToAsync(new Point(4, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        closing.ShouldBe(1);
        window.Visibility.ShouldBe(Visibility.Collapsed);
    }

    #endregion

    #region Close chrome

    /// <summary>Verifies the close chrome degrades by frame width: full bracketed chrome from nine
    /// cells (at ten the two placements resolve to different columns), a single mark down to four
    /// cells, and nothing below that - for both placements.</summary>
    [Theory]
    [InlineData(9, WindowClosePlacement.Left, 4, true)]
    [InlineData(10, WindowClosePlacement.Left, 4, true)]
    [InlineData(10, WindowClosePlacement.Right, 5, true)]
    [InlineData(8, WindowClosePlacement.Left, 1, false)]
    [InlineData(8, WindowClosePlacement.Right, 6, false)]
    [InlineData(4, WindowClosePlacement.Left, 1, false)]
    [InlineData(4, WindowClosePlacement.Right, 2, false)]
    public async Task Render_WhenFrameWidthVaries_DegradesCloseChromeAtDocumentedThresholdsAsync(
        int width,
        WindowClosePlacement placement,
        int expectedMarkColumn,
        bool expectBrackets)
    {
        // Arrange
        var closing = 0;
        var window = new Window
        {
            CanClose = true,
            ClosePlacement = placement,
            Width = Length.Cells(width),
            Height = Length.Cells(3),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        window.Closing += (_, _) => closing++;

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(width, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(expectedMarkColumn, 0)).Text.ShouldBe("■");

        if (expectBrackets)
        {
            surface.Cell(new Point(expectedMarkColumn - 1, 0)).Text.ShouldBe("[");
            surface.Cell(new Point(expectedMarkColumn + 1, 0)).Text.ShouldBe("]");
        }
        else
        {
            surface.Cell(new Point(expectedMarkColumn - 1, 0)).Text.ShouldNotBe("[");
            surface.Cell(new Point(expectedMarkColumn + 1, 0)).Text.ShouldNotBe("]");
        }

        // Act - the rendered mark is the live hit target in every degraded shape.
        await surface.Pointer.ClickAsync(window, new Point(expectedMarkColumn, 0));

        // Assert
        closing.ShouldBe(1);
        window.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies a frame narrower than four cells draws no close mark and a press on its
    /// title row starts a move instead of closing.</summary>
    [Fact]
    public async Task Render_WhenFrameIsNarrowerThanFourCells_DrawsNoCloseMarkAndTitlePressMovesAsync()
    {
        // Arrange
        var closing = 0;
        var window = new Window
        {
            CanClose = true,
            Width = Length.Cells(3),
            Height = Length.Cells(3),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        window.Closing += (_, _) => closing++;
        var stage = new Overlay { Width = Length.Cells(12), Height = Length.Cells(6), Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(12, 6),
            TestContext.Current.CancellationToken);

        // Assert - no mark anywhere on the title row
        for (var x = 0; x < 3; x++)
        {
            surface.Cell(new Point(window.Bounds.X + x, window.Bounds.Y)).Text.ShouldNotBe("■");
        }

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(1, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(7, 3));
        await surface.Pointer.ReleaseAsync();

        // Assert
        closing.ShouldBe(0);
        window.Visibility.ShouldBe(Visibility.Visible);
        window.Bounds.X.ShouldBe(6);
        window.Bounds.Y.ShouldBe(3);
    }

    /// <summary>Verifies right-placed close chrome hovers, presses, and closes through its own
    /// three-cell target, and that the flanking chrome frame cells are not part of that target.</summary>
    [Fact]
    public async Task Close_WhenPlacementIsRight_ActivatesOnlyThroughTheMarkTargetAsync()
    {
        // Arrange - chrome occupies columns 6..12; its target is 8..10.
        var closing = 0;
        var window = new Window
        {
            CanClose = true,
            ClosePlacement = WindowClosePlacement.Right,
            Width = Length.Cells(14),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        window.Closing += (_, _) => closing++;
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(14, 4),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(9, 0)).Text.ShouldBe("■");
        var theme = window.Theme.ShouldNotBeNull();
        var style = theme.GetWindowStyleSet().Normal;
        var normal = TerminalPalette.Project(style.CloseMarkColor.Resolve(theme), ColorDepth.Basic16);
        var hovered = TerminalPalette.Project(style.CloseMarkActiveColor.Resolve(theme), ColorDepth.Basic16);
        normal.ShouldNotBe(hovered);
        surface.Cell(new Point(9, 0)).Style.Foreground.ShouldBe(normal);

        // Act - hover the frame cell just outside the target
        await surface.Pointer.MoveToAsync(window, new Point(7, 0));

        // Assert - the mark does not light up
        surface.Cell(new Point(9, 0)).Style.Foreground.ShouldBe(normal);

        // Act - hover the mark
        await surface.Pointer.MoveToAsync(window, new Point(9, 0));

        // Assert
        surface.Cell(new Point(9, 0)).Style.Foreground.ShouldBe(hovered);

        // Act
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        closing.ShouldBe(1);
        window.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies pressing one of the chrome's flanking frame cells starts a title drag
    /// rather than a close, because only the bracketed mark is the close target.</summary>
    [Fact]
    public async Task Drag_WhenPressLandsOnCloseChromeFrameCell_MovesWindowWithoutClosingAsync()
    {
        // Arrange - left chrome spans relative columns 1..7; its target is 3..5.
        var closing = 0;
        var window = new Window
        {
            CanClose = true,
            Width = Length.Cells(14),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        window.Closing += (_, _) => closing++;
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay { Width = Length.Cells(30), Height = Length.Cells(12), Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 12),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(1, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(13, 6));
        await surface.Pointer.ReleaseAsync();

        // Assert - pointer moved by (10, 5) from (3, 1)
        closing.ShouldBe(0);
        window.Visibility.ShouldBe(Visibility.Visible);
        Overlay.GetLeft(window).ShouldBe(Length.Cells(12));
        Overlay.GetTop(window).ShouldBe(Length.Cells(6));
        window.Bounds.ShouldBe(new Rect(12, 6, 14, 4));
    }

    #endregion

    #region Keyboard

    /// <summary>Verifies Escape leaves a window with CanClose false untouched even when
    /// CloseOnEscape is true, and lets the key bubble past the window unhandled.</summary>
    [Fact]
    public async Task Escape_WhenCloseOnEscapeIsTrueButCanCloseIsFalse_DoesNotCloseAndBubblesAsync()
    {
        // Arrange
        var requested = 0;
        var closing = 0;
        var escapedUnhandled = 0;
        var action = new Button { Text = "Action" };
        var window = new Window
        {
            CanClose = false,
            CloseOnEscape = true,
            Content = action,
            Width = Length.Cells(16),
            Height = Length.Cells(6)
        };
        window.CloseRequested += (_, _) => requested++;
        window.Closing += (_, _) => closing++;
        var root = new Overlay { Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(action).ShouldBeTrue(),
            "focus the window action");
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
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        requested.ShouldBe(0);
        closing.ShouldBe(0);
        window.IsOpen.ShouldBeTrue();
        window.Visibility.ShouldBe(Visibility.Visible);
        escapedUnhandled.ShouldBe(1);
        await surface.Application.Dispatcher.InvokeAsync(probe.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a CloseRequested veto of an Escape close keeps the window presented and
    /// focused, raises no Closing, and still consumes the key as the window's own command.</summary>
    [Fact]
    public async Task Escape_WhenCloseRequestedVetoes_KeepsWindowOpenAndConsumesKeyAsync()
    {
        // Arrange
        var requested = 0;
        var closing = 0;
        var escapedUnhandled = 0;
        var action = new Button { Text = "Action" };
        var window = new Window
        {
            CanClose = true,
            CloseOnEscape = true,
            Content = action,
            Width = Length.Cells(16),
            Height = Length.Cells(6)
        };
        window.CloseRequested += (_, args) =>
        {
            requested++;
            args.Cancel = true;
        };
        window.Closing += (_, _) => closing++;
        var root = new Overlay { Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(action).ShouldBeTrue(),
            "focus the window action");
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
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        requested.ShouldBe(1);
        closing.ShouldBe(0);
        window.IsOpen.ShouldBeTrue();
        window.Visibility.ShouldBe(Visibility.Visible);
        surface.ShouldHaveFocus(action);
        escapedUnhandled.ShouldBe(0);
        await surface.Application.Dispatcher.InvokeAsync(probe.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Tab and Shift+Tab inside a modal Window cycle only through that window's
    /// descendants, never reaching the background window, and that ending the scope restores the
    /// background focus.</summary>
    [Fact]
    public async Task Tab_WhenWindowIsModal_ConfinesTraversalToTheModalWindowAsync()
    {
        // Arrange
        var backgroundFirst = new Button { Text = "B1" };
        var backgroundSecond = new Button { Text = "B2" };
        var background = new Window
        {
            Header = "Back",
            Content = new Stack { Children = { backgroundFirst, backgroundSecond } },
            Width = Length.Cells(12),
            Height = Length.Cells(7)
        };
        var modalFirst = new Button { Text = "M1" };
        var modalSecond = new Button { Text = "M2" };
        var modal = new Window
        {
            Header = "Modal",
            Content = new Stack { Children = { modalFirst, modalSecond } },
            Width = Length.Cells(12),
            Height = Length.Cells(7),
            Visibility = Visibility.Collapsed
        };
        Overlay.SetLeft(modal, Length.Cells(14));
        var root = new Overlay { Children = { background, modal } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 9),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(backgroundFirst).ShouldBeTrue(),
            "focus the background window");
        ModalScope? scope = null;

        // Act
        await surface.UpdateAsync(() => scope = modal.ShowModal(), "show modal Window");

        // Assert
        surface.ShouldHaveFocus(modalFirst);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(modalSecond);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(modalFirst);

        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(modalSecond);

        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(modalFirst);
        backgroundFirst.IsFocused.ShouldBeFalse();
        backgroundSecond.IsFocused.ShouldBeFalse();

        // Act
        await surface.UpdateAsync(() => scope.ShouldNotBeNull().Dispose(), "end modal presentation");

        // Assert
        surface.ShouldHaveFocus(backgroundFirst);
        surface.Application.ActiveWindow.ShouldBeSameAs(background);
    }

    #endregion

    #region Modal close and activation

    /// <summary>Verifies closing a modal window through its close mark restores focus and window
    /// activation to the background window that owned them before the modal presentation.</summary>
    [Fact]
    public async Task ShowModal_WhenClosedThroughCloseMark_RestoresBackgroundFocusAndActivationAsync()
    {
        // Arrange
        var backgroundAction = new Button { Text = "Back" };
        var background = new Window
        {
            Header = "Back",
            Content = backgroundAction,
            Width = Length.Cells(12),
            Height = Length.Cells(5)
        };
        var modal = new Window
        {
            CanClose = true,
            Content = new Button { Text = "Modal" },
            Width = Length.Cells(14),
            Height = Length.Cells(5),
            Visibility = Visibility.Collapsed
        };
        Overlay.SetLeft(modal, Length.Cells(14));
        var root = new Overlay { Children = { background, modal } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 7),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(backgroundAction).ShouldBeTrue(),
            "focus the background window");
        ModalScope? scope = null;
        await surface.UpdateAsync(() => scope = modal.ShowModal(), "show closable modal Window");
        surface.Application.ActiveWindow.ShouldBeSameAs(modal);

        // Act
        await surface.Pointer.ClickAsync(modal, new Point(4, 0));

        // Assert
        scope.ShouldNotBeNull().IsActive.ShouldBeFalse();
        modal.Visibility.ShouldBe(Visibility.Collapsed);
        surface.ShouldHaveFocus(backgroundAction);
        surface.Application.ActiveWindow.ShouldBeSameAs(background);
        background.IsActive.ShouldBeTrue();
        modal.IsActive.ShouldBeFalse();
    }

    /// <summary>Verifies each activation raises only the activated window above its siblings and
    /// leaves the remaining windows in their previous relative order.</summary>
    [Fact]
    public async Task Activation_WhenWindowsAreClickedInTurn_RaisesOnlyTheActivatedWindowAsync()
    {
        // Arrange
        var first = new Window { Header = "1", Content = new Button { Text = "A" }, Width = Length.Cells(8), Height = Length.Cells(4) };
        var second = new Window { Header = "2", Content = new Button { Text = "B" }, Width = Length.Cells(8), Height = Length.Cells(4) };
        var third = new Window { Header = "3", Content = new Button { Text = "C" }, Width = Length.Cells(8), Height = Length.Cells(4) };
        Overlay.SetLeft(second, Length.Cells(9));
        Overlay.SetLeft(third, Length.Cells(18));
        var root = new Overlay { Children = { first, second, third } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(28, 6),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(first, new Point(2, 0));
        var afterFirst = (Overlay.GetZIndex(first), Overlay.GetZIndex(second), Overlay.GetZIndex(third));
        await surface.Pointer.ClickAsync(third, new Point(2, 0));
        var afterThird = (Overlay.GetZIndex(first), Overlay.GetZIndex(second), Overlay.GetZIndex(third));
        await surface.Pointer.ClickAsync(second, new Point(2, 0));
        var afterSecond = (Overlay.GetZIndex(first), Overlay.GetZIndex(second), Overlay.GetZIndex(third));

        // Assert
        surface.Application.ActiveWindow.ShouldBeSameAs(second);
        afterFirst.Item1.ShouldBeGreaterThan(afterFirst.Item2);
        afterFirst.Item1.ShouldBeGreaterThan(afterFirst.Item3);
        afterThird.Item3.ShouldBeGreaterThan(afterThird.Item1);
        afterThird.Item1.ShouldBe(afterFirst.Item1);
        afterThird.Item2.ShouldBe(afterFirst.Item2);
        afterSecond.Item2.ShouldBeGreaterThan(afterSecond.Item3);
        afterSecond.Item3.ShouldBeGreaterThan(afterSecond.Item1);
        afterSecond.Item1.ShouldBe(afterThird.Item1);
        afterSecond.Item3.ShouldBe(afterThird.Item3);
    }

    #endregion

    #region Gestures

    /// <summary>Verifies a drag started on the frame's own corner cell of the title row moves the
    /// window: the whole top row, corners included, is the title lane.</summary>
    [Fact]
    public async Task Drag_WhenPressLandsOnTopLeftCornerCell_MovesWindowAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Move",
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay { Width = Length.Cells(30), Height = Length.Cells(15), Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(10, 5));
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.ShouldBe(new Rect(10, 5, 10, 4));
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies only the bottom-right corner is a resize handle: the other corners and
    /// the edges never resize, and a press on the top-right corner moves the window instead.</summary>
    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(9, 2, false)]
    [InlineData(4, 3, false)]
    [InlineData(0, 2, false)]
    [InlineData(9, 0, true)]
    public async Task Resize_WhenPressLandsOffTheBottomRightCorner_NeverResizesAsync(
        int relativeX,
        int relativeY,
        bool expectMove)
    {
        // Arrange
        var window = new Window
        {
            Header = "Size",
            CanResize = true,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay { Width = Length.Cells(30), Height = Length.Cells(15), Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var origin = new Point(2 + relativeX, 1 + relativeY);

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(relativeX, relativeY));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(origin.X + 6, origin.Y + 4));
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.Width.ShouldBe(10);
        window.Bounds.Height.ShouldBe(4);
        window.Width.ShouldBe(Length.Cells(10));
        window.Height.ShouldBe(Length.Cells(4));

        if (expectMove)
        {
            window.Bounds.ShouldBe(new Rect(8, 5, 10, 4));
        }
        else
        {
            window.Bounds.ShouldBe(new Rect(2, 1, 10, 4));
        }
    }

    /// <summary>Verifies losing pointer capture mid-drag cancels the move, so later pressed
    /// motion no longer relocates the window.</summary>
    [Fact]
    public async Task Drag_WhenPointerCaptureIsLost_CancelsGestureAsync()
    {
        // Arrange - no close chrome, so every title-row cell is a drag handle.
        var window = new Window
        {
            Header = "Move",
            CanClose = false,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay { Width = Length.Cells(30), Height = Length.Cells(15), Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(window, new Point(3, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(8, 4));
        window.Bounds.ShouldBe(new Rect(5, 4, 10, 4));
        await surface.UpdateAsync(() => window.IsEnabled = false, "disable Window mid-drag");

        // Assert
        surface.ShouldHaveCapture(null);

        // Act
        await surface.UpdateAsync(() => window.IsEnabled = true, "re-enable Window");
        await surface.Pointer.MovePressedToAsync(stage, new Point(20, 10));
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.ShouldBe(new Rect(5, 4, 10, 4));
        Overlay.GetLeft(window).ShouldBe(Length.Cells(5));
        Overlay.GetTop(window).ShouldBe(Length.Cells(4));
    }

    /// <summary>Verifies closing the window programmatically mid-drag releases capture and ends
    /// the gesture, so the collapsed window is not moved by the still-held pointer.</summary>
    [Fact]
    public async Task Close_WhenCalledMidDrag_ReleasesCaptureAndEndsGestureAsync()
    {
        // Arrange - no close chrome, so every title-row cell is a drag handle.
        var window = new Window
        {
            Header = "Move",
            CanClose = false,
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay { Width = Length.Cells(30), Height = Length.Cells(15), Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(window, new Point(3, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(stage, new Point(8, 4));
        surface.ShouldHaveCapture(window);

        // Act
        await surface.UpdateAsync(window.Close, "close Window mid-drag");

        // Assert
        window.Visibility.ShouldBe(Visibility.Collapsed);
        surface.ShouldHaveCapture(null);

        // Act
        await surface.Pointer.MovePressedToAsync(stage, new Point(20, 10));
        await surface.Pointer.ReleaseAsync();

        // Assert
        Overlay.GetLeft(window).ShouldBe(Length.Cells(5));
        Overlay.GetTop(window).ShouldBe(Length.Cells(4));
    }

    /// <summary>Verifies a host resize during a title drag applies the new client bounds to the
    /// very next pressed move, clamping the window inside the shrunken host.</summary>
    [Fact]
    public async Task Drag_WhenHostShrinksMidGesture_ClampsNextMoveToNewClientBoundsAsync()
    {
        // Arrange
        var window = new Window
        {
            Header = "Move",
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false)
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        var stage = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { window }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stage,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(window, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(16, 9));
        window.Bounds.ShouldBe(new Rect(14, 9, 10, 4));

        // Act
        await surface.ResizeAsync(new Size(20, 10));
        surface.ShouldHaveCapture(window);
        await surface.Pointer.MovePressedToAsync(new Point(19, 9));
        await surface.Pointer.ReleaseAsync();

        // Assert - delta (15, 8) from the press origin, clamped to the 20x10 client
        Overlay.GetLeft(window).ShouldBe(Length.Cells(10));
        Overlay.GetTop(window).ShouldBe(Length.Cells(6));
        window.Bounds.ShouldBe(new Rect(10, 6, 10, 4));
        surface.ShouldHaveCapture(null);
    }

    #endregion
}
