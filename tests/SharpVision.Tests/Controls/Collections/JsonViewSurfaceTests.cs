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
    [ComponentBehaviorEvidence(
        typeof(JsonView),
        ComponentBehavior.Mounted |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.Activation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.Composition)]
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

    /// <summary>Verifies primary pointer input selects a key and toggles a disclosure glyph.</summary>
    [ComponentBehaviorEvidence(
        typeof(JsonView),
        ComponentBehavior.Hover |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.UnavailableCleanup)]
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
        view.PointerOver.ShouldBeTrue();
        view.Pressed.ShouldBeFalse();

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
        await surface.UpdateAsync(() => view.Enabled = false, "disable JsonView");

        // Assert
        view.PointerOver.ShouldBeFalse();
        view.Focused.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
    }
}
