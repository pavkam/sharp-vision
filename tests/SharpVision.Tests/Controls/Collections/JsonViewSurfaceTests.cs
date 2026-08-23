// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies JsonView rendering and interaction through a mounted application surface.</summary>
public sealed class JsonViewSurfaceTests
{
    /// <summary>Verifies a long string wraps at words within the viewport instead of creating horizontal overflow.</summary>
    [Fact]
    public async Task Render_WhenStringExceedsViewport_WrapsValueWithoutHorizontalOverflowAsync()
    {
        // Arrange
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"text\":\"alpha beta gamma\"}",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(18, 6),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(/*lang=json*/ """
                             {
                               "text": "alpha
                                       beta
                                       gamma"
                             }

                             """);
        view.Extent.Width.ShouldBeLessThanOrEqualTo(view.Viewport.Width);
        view.HorizontalOffset.ShouldBe(0);

        // Act
        await surface.ResizeAsync(new Size(24, 6));

        // Assert resized projection
        view.Bounds.Width.ShouldBe(24);
        surface.ShouldRender(/*lang=json*/ """
                             {
                               "text": "alpha beta
                                       gamma"
                             }


                             """);
        view.Extent.Width.ShouldBeLessThanOrEqualTo(view.Viewport.Width);
    }

    /// <summary>Verifies reserving a vertical scrollbar reflows strings to the narrowed viewport.</summary>
    [Fact]
    public async Task Layout_WhenWrappedStringNeedsVerticalBar_ReflowsWithoutHorizontalBarAsync()
    {
        // Arrange
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"text\":\"abcdefghijklmnopqrstuvwxyz\"}",
            Height = Length.Cells(4),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(18, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert
        view.Extent.Width.ShouldBeLessThanOrEqualTo(view.Viewport.Width);
        view.Viewport.Width.ShouldBe(17);
        view.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies fallback wrapping keeps extended grapheme clusters intact.</summary>
    [Fact]
    public async Task Render_WhenStringContainsEmojiCluster_WrapsOnlyBetweenGraphemesAsync()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"text\":\"👩‍💻👩‍💻\"}" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(13, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(/*lang=json*/ """
                             {
                               "text": "👩‍💻
                                       👩‍💻"
                             }
                             """);
        view.Extent.Width.ShouldBeLessThanOrEqualTo(view.Viewport.Width);
    }

    /// <summary>Verifies a scalar root keeps its JSON-kind color without becoming a synthetic selection target.</summary>
    [Fact]
    public async Task Render_WhenRootIsString_UsesStringColorWithoutSelectionAsync()
    {
        // Arrange
        var view = new JsonView { Json = "\"root\"" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(8, 1),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("\"root\"  ");
        view.SelectedPath.ShouldBeNull();
        surface.Cell(default).Style.Foreground.ShouldBe(TerminalPalette.Project(
            TestThemes.BorderlessContainer.ResolveColor(SemanticColor.Success),
            ColorDepth.Basic16));
    }

    /// <summary>Verifies syntax lines, keyboard traversal, selection, and collapse through decoded terminal input.</summary>
    [Fact]
    public async Task Input_WhenNestedDocumentIsMounted_NavigatesAndCollapsesAsync()
    {
        // Arrange
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"title\":\"Sharp\",\"year\":2026,\"funny\":true,\"nothing\":null,\"author\":{\"name\":\"Alex\"}}"
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(30, 9),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert initial tree
        surface.ShouldRender("""
                             {
                               "title": "Sharp",
                               "year": 2026,
                               "funny": true,
                               "nothing": null,
                             ▼ "author": {
                                 "name": "Alex"
                               }
                             }
                             """);
        var theme = TestThemes.BorderlessContainer;
        var depth = ColorDepth.Basic16;
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.SelectedText), depth));
        surface.Cell(new Point(2, 1)).Style.Background.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.SelectedControl), depth));
        surface.Cell(new Point(11, 1)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.Success), depth));
        surface.Cell(new Point(2, 2)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.Accent), depth));
        surface.Cell(new Point(10, 2)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.Info), depth));
        surface.Cell(new Point(11, 3)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.Warning), depth));
        surface.Cell(new Point(13, 4)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.Muted), depth));

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        view.SelectedPath.ShouldBe("/author");
        surface.ShouldHaveFocus(view);

        // Act
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert collapsed tree
        surface.ShouldRender("""
                             {
                               "title": "Sharp",
                               "year": 2026,
                               "funny": true,
                               "nothing": null,
                             ▶ "author": {…}
                             }


                             """);

        // Act and assert directional expansion
        await surface.Keyboard.PressAsync(Code.Right);
        surface.Cell(new Point(4, 6)).Text.ShouldBe("\"");
        view.SelectedPath.ShouldBe("/author");

        // Act and assert keyboard activation through both supported keys
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Enter);
        surface.Cell(new Point(4, 6)).Text.ShouldBe("\"");
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        surface.Cell(new Point(0, 5)).Text.ShouldBe("▶");
    }

    /// <summary>Verifies an incidental Control modifier on Enter does not toggle the selected
    /// container's disclosure, and leaves the stroke unhandled.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterHasControlModifier_DoesNotToggleAndLeavesUnhandledAsync()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"branch\":{\"child\":2}}", Indent = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(24, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.Cell(new Point(0, 1)).Text.ShouldBe("▼");

        // Act
        var enter = new KeyEventArgs(new Stroke(
            Code.Enter, default, nativeCode: 0, Modifiers.Control, KeyAction.Press));
        await surface.UpdateAsync(() => _ = Router.Route(view, Events.Key, enter), "press Ctrl+Enter");

        // Assert
        enter.IsHandled.ShouldBeFalse();
        surface.Cell(new Point(0, 1)).Text.ShouldBe("▼");
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) still toggles the selected
    /// container's disclosure.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterHasShiftModifier_StillTogglesAsync()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"branch\":{\"child\":2}}", Indent = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(24, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.Cell(new Point(0, 1)).Text.ShouldBe("▼");

        // Act
        var enter = new KeyEventArgs(new Stroke(
            Code.Enter, default, nativeCode: 0, Modifiers.Shift, KeyAction.Press));
        await surface.UpdateAsync(() => _ = Router.Route(view, Events.Key, enter), "press Shift+Enter");

        // Assert
        enter.IsHandled.ShouldBeTrue();
        surface.Cell(new Point(0, 1)).Text.ShouldBe("▶");
    }

    /// <summary>Verifies PageDown and PageUp move the selection by one viewport's worth of lines
    /// instead of by one entry, mirroring TreeView's selection-paging precedent rather than
    /// Table's viewport-offset paging.</summary>
    [Fact]
    public async Task Keyboard_WhenPageDownThenPageUpIsPressed_MovesSelectionByOnePageAsync()
    {
        // Arrange
        var properties = string.Join(',', Enumerable.Range(0, 20).Select(index => $"\"key{index}\":{index}"));
        var view = new JsonView
        {
            Json = $"{{{properties}}}",
            Height = Length.Cells(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ShowScrollBars = ShowScrollBars.Never
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        view.SelectedPath.ShouldBe("/key0");
        var expectedIndex = view.Viewport.Height;

        // Act
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert - each top-level property is one line, so the landing index equals the page step.
        view.SelectedPath.ShouldBe($"/key{expectedIndex}");

        // Act
        await surface.Keyboard.PressAsync(Code.PageUp);

        // Assert
        view.SelectedPath.ShouldBe("/key0");
    }

    /// <summary>Verifies a configured PageOverlap retains that much context on PageDown instead of
    /// jumping the full viewport height, matching Table, ListView, TreeView, and NavigationView's
    /// overlap-aware paging.</summary>
    [Fact]
    public async Task Keyboard_WhenPageDownWithConfiguredPageOverlap_LandsOverlapAwareAsync()
    {
        // Arrange
        var properties = string.Join(',', Enumerable.Range(0, 20).Select(index => $"\"key{index}\":{index}"));
        var view = new JsonView
        {
            Json = $"{{{properties}}}",
            Height = Length.Cells(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ShowScrollBars = ShowScrollBars.Never,
            PageOverlap = 2
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        view.SelectedPath.ShouldBe("/key0");
        var expectedIndex = view.Viewport.Height - view.PageOverlap;

        // Act
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert - the overlap-reduced step lands earlier than the overlap=0 case above would.
        view.SelectedPath.ShouldBe($"/key{expectedIndex}");
    }

    /// <summary>
    /// Verifies a click on the disclosure glyph's first cell toggles expansion under
    /// <see cref="Ambiguous.Wide"/>, where the glyph ('▶'/'▼', both East Asian Ambiguous-width)
    /// occupies two cells instead of one. The hit test computes the glyph's column from a
    /// hard-coded "leadingWidth - 2" literal that only ever lands on the glyph's second cell
    /// under the wide policy, silently ignoring a click on its first cell.
    /// </summary>
    [Fact]
    public async Task Pointer_WhenDisclosureGlyphIsWideAndFirstCellIsClicked_TogglesExpansionAsync()
    {
        // Arrange
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"branch\":{\"child\":2}}",
            Indent = 0
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(24, 4),
            TerminalOptions.Minimal with
            {
                Capabilities = TerminalCapabilities.Conservative with { AmbiguousWidth = Ambiguous.Wide }
            },
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert: expanded by default, so the disclosure glyph starts as '▼'.
        surface.Cell(new Point(0, 1)).Text.ShouldBe("▼");

        // Act: column 0 is the disclosure glyph's first (leading) wide cell.
        await surface.Pointer.ClickAsync(view, new Point(0, 1));

        // Assert: collapsing swaps the glyph to '▶'.
        surface.Cell(new Point(0, 1)).Text.ShouldBe("▶");
    }

    /// <summary>Verifies primary pointer input selects a key and toggles a disclosure glyph, and the
    /// full disabled contract: direct and ancestor-inherited disabled state, stable geometry across
    /// a genuine resize, and re-enable recovery.</summary>
    [Fact]
    public async Task Pointer_WhenKeyAndDisclosureAreClicked_SelectsAndCollapsesAsync()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"first\":1,\"branch\":{\"child\":2}}" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(24, 6),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(view, new Point(2, 2));

        // Assert
        view.SelectedPath.ShouldBe("/branch");
        surface.ShouldHaveFocus(view);
        view.IsPointerOver.ShouldBeTrue();
        view.IsPressed.ShouldBeFalse();

        // Act
        await surface.Pointer.ClickAsync(view, new Point(0, 2));

        // Assert
        surface.ShouldRender("""
                             {
                               "first": 1,
                             ▶ "branch": {…}
                             }


                             """);

        // Act unavailable cleanup
        await surface.UpdateAsync(() => view.IsEnabled = false, "disable JsonView");

        // Assert cleanup and disabled appearance
        view.IsPointerOver.ShouldBeFalse();
        view.IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(view, VisualState.Disabled);

        // Act re-enable
        await surface.UpdateAsync(() => view.IsEnabled = true, "re-enable JsonView");

        // Assert re-enable recovery: pointer selection resumes
        surface.ShouldHaveState(view, VisualState.Normal);
        await surface.Pointer.ClickAsync(view, new Point(2, 1));
        view.SelectedPath.ShouldBe("/first");
        surface.ShouldHaveFocus(view);

        // Arrange a disabled JsonView and an independently-mounted enabled twin at the same size
        var disabledTwin = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"first\":1,\"branch\":{\"child\":2}}",
            IsEnabled = false
        };
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabledTwin,
            new Size(24, 6),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var enabledTwin = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"first\":1,\"branch\":{\"child\":2}}"
        };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledTwin,
            new Size(24, 6),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Act genuine resize of both twins to a different shared size
        await disabledSurface.ResizeAsync(new Size(18, 8));
        await enabledSurface.ResizeAsync(new Size(18, 8));

        // Assert stable geometry: disabling never perturbs layout
        disabledTwin.Bounds.ShouldBe(enabledTwin.Bounds);
        disabledTwin.DesiredSize.ShouldBe(enabledTwin.DesiredSize);

        // Arrange an ancestor container that owns a JsonView
        var ancestorView = new JsonView { Json = /*lang=json,strict*/ "{\"first\":1}" };
        var ancestor = new Overlay { Children = { ancestorView } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            ancestor,
            new Size(24, 6),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor container
        await ancestorSurface.UpdateAsync(() => ancestor.IsEnabled = false, "disable ancestor container");

        // Assert the owned JsonView inherits Disabled without being disabled itself
        ancestorView.IsEnabled.ShouldBeTrue();
        ancestorSurface.ShouldHaveState(ancestorView, VisualState.Disabled);
    }

    /// <summary>Verifies a keyboard collapse reveals the selection like every other navigation
    /// path. Collapsing removes every projected line below the selected container, so a viewport
    /// scrolled into the removed range kept an offset past the selected row - the selection
    /// stayed live but nowhere on screen until the next arrow key snapped it back.</summary>
    [Fact]
    public async Task Keyboard_WhenScrolledAwayAndSelectionCollapses_RevealsTheSelectedRowAsync()
    {
        // Arrange: a collapsible first section plus a long tail, so the document still overflows
        // the viewport after the collapse and the scroll container cannot mask a stale offset by
        // clamping it away.
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ """
                   {
                     "metrics": { "a": 1, "b": 2, "c": 3, "d": 4, "e": 5, "f": 6 },
                     "tail": { "p": 1, "q": 2, "r": 3, "s": 4, "t": 5, "u": 6 }
                   }
                   """,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Home);
        view.SelectedPath.ShouldBe("/metrics");

        // Act: scroll the selected container out of the viewport, then collapse it.
        await surface.UpdateAsync(() => view.VerticalOffset = 8, "scroll away from the selection");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert: still selected, still collapsed, and back on screen.
        view.SelectedPath.ShouldBe("/metrics");
        view.VerticalOffset.ShouldBeLessThanOrEqualTo(1);

        // Act: the same guarantee for the Left-arrow collapse.
        await surface.Keyboard.PressAsync(Code.Enter);
        view.SelectedPath.ShouldBe("/metrics");
        await surface.UpdateAsync(() => view.VerticalOffset = 8, "scroll away again");
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert
        view.SelectedPath.ShouldBe("/metrics");
        view.VerticalOffset.ShouldBeLessThanOrEqualTo(1);
    }

    /// <summary>Verifies keyboard focus on the JsonView itself recolors its own bordered frame.
    /// JsonViewStyle previously fell back to the bare passive "container" key, which no bundled
    /// theme authors a focus delta for, so a user tabbing onto a JsonView had no cue it had
    /// happened despite the JsonView already drawing an all-sides border to recolor.</summary>
    [Fact]
    public async Task Keyboard_WhenJsonViewReceivesFocus_RecolorsItsOwnBorderAsync()
    {
        // Arrange
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"key\":\"value\"}",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var theme = view.Theme.ShouldNotBeNull();
        view.ActualBorder.Foreground.Literal.ShouldBe(ThemeColorHelper.Border(theme));

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        view.IsFocused.ShouldBeTrue();
        view.ActualBorder.Foreground.Literal.ShouldBe(ThemeColorHelper.FocusedBorder(theme));
    }
}
