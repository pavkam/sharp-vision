// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies CodeView's mounted rendering, keyboard, and pointer behavior.</summary>
public sealed class CodeViewSurfaceTests
{
    /// <summary>Verifies a keyword token renders with a different foreground than plain identifier text.</summary>
    [Fact]
    public async Task Render_WhenLanguageHighlightsAKeyword_ColorsItDifferentlyFromPlainTextAsync()
    {
        var view = new CodeView { Code = "fn zzzzzz() {}\n", Language = "Rust" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // "fn" (column 2, the gutter's width) is the Keyword-styled token; "zzzzzz" (column 5) is
        // an unrecognized identifier that falls back to plain Normal-Text styling.
        surface.Cell(new Point(2, 0)).Text.ShouldBe("f");
        surface.Cell(new Point(5, 0)).Text.ShouldBe("z");
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldNotBe(surface.Cell(new Point(5, 0)).Style.Foreground);
    }

    /// <summary>Verifies a primary click sets an empty selection at the clicked column and focuses the view.</summary>
    [Fact]
    public async Task Pointer_WhenPrimaryClickOccurs_SetsCaretAndFocusesAsync()
    {
        var view = new CodeView { Code = "abcdef\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(view, new Point(2 + 3, 0));

        surface.ShouldHaveFocus(view);
        view.Selection.IsEmpty.ShouldBeTrue();
        view.Selection.Caret.ShouldBe(3);
    }

    /// <summary>Verifies a pointer drag across two columns selects the intervening text.</summary>
    [Fact]
    public async Task Pointer_WhenDragged_SelectsTheSpannedTextAsync()
    {
        var view = new CodeView { Code = "abcdef\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.Pointer.DragAsync(view, new Point(2, 0), new Point(2 + 3, 0));

        view.SelectedText.ShouldBe("abc");
    }

    /// <summary>Verifies Right arrow advances the caret by one grapheme.</summary>
    [Fact]
    public async Task Keyboard_WhenRightArrowIsPressed_AdvancesCaretByOneAsync()
    {
        var view = new CodeView { Code = "abcdef\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(view, new Point(2, 0));

        await surface.Keyboard.PressAsync(Code.Right);

        view.Selection.Caret.ShouldBe(1);
    }

    /// <summary>Verifies Shift+Right extends the selection instead of collapsing it.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftRightIsPressed_ExtendsSelectionAsync()
    {
        var view = new CodeView { Code = "abcdef\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(view, new Point(2, 0));

        var shiftRight = new KeyEventArgs(new Stroke(Code.Right, null, nativeCode: 0, Modifiers.Shift, KeyAction.Press));
        await surface.UpdateAsync(() => _ = Router.Route(view, Events.Key, shiftRight), "press Shift+Right (1)");
        var shiftRightAgain = new KeyEventArgs(new Stroke(Code.Right, null, nativeCode: 0, Modifiers.Shift, KeyAction.Press));
        await surface.UpdateAsync(() => _ = Router.Route(view, Events.Key, shiftRightAgain), "press Shift+Right (2)");

        view.SelectedText.ShouldBe("ab");
    }

    /// <summary>Verifies Ctrl+A selects the entire document.</summary>
    [Fact]
    public async Task Keyboard_WhenControlAIsPressed_SelectsAllAsync()
    {
        var view = new CodeView { Code = "abc\ndef" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(view, new Point(2, 0));

        var controlA = new KeyEventArgs(new Stroke(
            Code.Character, new Rune('a'), nativeCode: 0, Modifiers.Control, KeyAction.Press));
        await surface.UpdateAsync(() => _ = Router.Route(view, Events.Key, controlA), "press Ctrl+A");

        view.SelectedText.ShouldBe("abc\ndef");
    }

    /// <summary>Verifies Down arrow moves the caret to the same column on the next visible line.</summary>
    [Fact]
    public async Task Keyboard_WhenDownArrowIsPressed_MovesToTheNextLineSameColumnAsync()
    {
        var view = new CodeView { Code = "abcdef\nuvwxyz\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(view, new Point(2 + 2, 0));

        await surface.Keyboard.PressAsync(Code.Down);

        view.Selection.Caret.ShouldBe("abcdef\n".Length + 2);
    }

    /// <summary>Verifies pressing End on a long line scrolls exactly far enough that the caret
    /// column lands within the text-drawing cells of the viewport (or, when the caret sits one
    /// past the very last character of the widest line, exactly at that boundary - there is no
    /// character to draw there either way) - not merely within the whole viewport, which also
    /// counts the fold gutter's own non-scrolling cells and would let the caret sit up to the
    /// gutter's width past the true visible right edge.</summary>
    [Fact]
    public async Task Keyboard_WhenEndIsPressedOnALongLine_RevealsTheCaretWithinTheTextColumnsAsync()
    {
        var longLine = new string('x', 40);
        var view = new CodeView { Code = longLine + "\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(10, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(view, new Point(2, 0));

        await surface.Keyboard.PressAsync(Code.End);

        var caretColumn = view.Selection.Caret;
        var textViewportWidth = view.Viewport.Width - 2;
        caretColumn.ShouldBeGreaterThanOrEqualTo(view.HorizontalOffset);
        caretColumn.ShouldBeLessThanOrEqualTo(view.HorizontalOffset + textViewportWidth);
    }

    /// <summary>Verifies collapsing a fold hides its interior lines from the rendered surface.</summary>
    [Fact]
    public async Task Fold_WhenCollapsed_HidesInteriorLinesFromRenderingAsync()
    {
        var view = new CodeView
        {
            Code = "fn main() {\n    let x = 1;\n}\nafter()\n",
            Language = "Rust",
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 4).First(view.IsFoldStart);

        await surface.UpdateAsync(() => view.SetFolded(foldStart, true), "collapse the fold");

        view.Extent.Height.ShouldBe(3);
    }

    /// <summary>Verifies clicking a fold-start line's gutter arrow toggles that fold, matching the
    /// same effect as calling ToggleFold directly.</summary>
    [Fact]
    public async Task Pointer_WhenGutterArrowIsClicked_TogglesTheFoldAsync()
    {
        var view = new CodeView
        {
            Code = "fn main() {\n    let x = 1;\n}\nafter()\n",
            Language = "Rust",
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 4).First(view.IsFoldStart);
        view.IsFolded(foldStart).ShouldBeFalse();

        await surface.Pointer.ClickAsync(view, new Point(0, foldStart));

        view.IsFolded(foldStart).ShouldBeTrue();
        view.Extent.Height.ShouldBe(3);

        await surface.Pointer.ClickAsync(view, new Point(0, foldStart));

        view.IsFolded(foldStart).ShouldBeFalse();
    }

    /// <summary>Verifies clicking within the gutter does not also move the text caret.</summary>
    [Fact]
    public async Task Pointer_WhenGutterArrowIsClicked_DoesNotMoveTheCaretAsync()
    {
        var view = new CodeView { Code = "fn f() {\n    g();\n}\n", Language = "Rust" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 3).First(view.IsFoldStart);
        var before = view.Selection;

        await surface.Pointer.ClickAsync(view, new Point(0, foldStart));

        view.Selection.ShouldBe(before);
    }

    /// <summary>Verifies disabling folding stops rendering the gutter column and stops hiding
    /// collapsed lines, without discarding the collapsed bookkeeping.</summary>
    [Fact]
    public async Task IsFoldingEnabled_WhenDisabledWhileCollapsed_ShowsEveryLineAgainAsync()
    {
        var view = new CodeView
        {
            Code = "fn main() {\n    let x = 1;\n}\nafter()\n",
            Language = "Rust",
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 4).First(view.IsFoldStart);
        await surface.UpdateAsync(() => view.SetFolded(foldStart, true), "collapse the fold");
        view.Extent.Height.ShouldBe(3);

        await surface.UpdateAsync(() => view.IsFoldingEnabled = false, "disable folding");

        view.Extent.Height.ShouldBe(5);
        view.IsFolded(foldStart).ShouldBeTrue();
    }

    /// <summary>Verifies right-click opens the default CodeViewContextMenu.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryPressOccurs_OpensDefaultContextMenuAsync()
    {
        var view = new CodeView { Code = "abcdef\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var menu = view.ContextMenu.ShouldBeOfType<CodeViewContextMenu>();

        await surface.Pointer.RightClickAsync(view, new Point(2, 0));

        menu.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies Ctrl+C forwards the current selection to ClipboardWriter.</summary>
    [Fact]
    public async Task Keyboard_WhenControlCIsPressed_ForwardsSelectionToClipboardWriterAsync()
    {
        var view = new CodeView { Code = "abcdef\n" };
        string? written = null;
        view.ClipboardWriter = value => written = value;
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.DragAsync(view, new Point(2, 0), new Point(2 + 3, 0));

        var controlC = new KeyEventArgs(new Stroke(
            Code.Character, new Rune('c'), nativeCode: 0, Modifiers.Control, KeyAction.Press));
        await surface.UpdateAsync(() => _ = Router.Route(view, Events.Key, controlC), "press Ctrl+C");

        written.ShouldBe("abc");
    }

    /// <summary>Verifies swapping the live Theme repaints a syntax color role even when the
    /// unresolved CodeViewStyle stays declaratively identical - the common case for every symbolic
    /// SemanticColor role CodeViewStyle.Complete assigns - by changing only SemanticColor.Info,
    /// which the "container" style section this control falls back to never authors.</summary>
    [Fact]
    public async Task Theme_WhenOnlyASyntaxSemanticColorChanges_RepaintsTheTokenAsync()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-view-theme-info-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "info-role.xml"),
                """
                <language name="InfoRoleTest" section="Sources" extensions="*.ext" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                        <DetectChar attribute="BuiltIn" context="#stay" char="!"/>
                      </context>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                      <itemData name="BuiltIn" defStyleNum="dsBuiltIn"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);

            // CodeViewStyle falls back to ContainerStyle, and ThemeCatalog.Dark's own "container"
            // section authors an all-sides border - unlike TestThemes.BorderlessContainer, cloning
            // ThemeCatalog.Dark here keeps every other semantic role resolving exactly as the
            // shipped default theme does, so the token cell sits one row and one column past the
            // control's own top-left corner instead of at (2, 0).
            var themeA = WithSemanticColor(SemanticColor.Info, Color.Rgb(10, 20, 30));
            var themeB = WithSemanticColor(SemanticColor.Info, Color.Rgb(240, 230, 220));
            var view = new CodeView
            {
                Catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName),
                Language = "InfoRoleTest",
                Code = "!\n",
            };
            await using var surface = await ComponentSurface.MountAsync(
                view,
                new Size(10, 4),
                themeA,
                TestContext.Current.CancellationToken);
            var before = surface.Cell(new Point(3, 1)).Style.Foreground;

            await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap the Info semantic color");

            surface.Cell(new Point(3, 1)).Style.Foreground.ShouldNotBe(before);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies assigning a local Style that differs only in CollapsedGlyph repaints the
    /// fold gutter with the new glyph.</summary>
    [Fact]
    public async Task Style_WhenOnlyCollapsedGlyphChanges_RepaintsTheGutterArrowAsync()
    {
        var view = new CodeView { Code = "fn f() {\n    g();\n}\n", Language = "Rust" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 3).First(view.IsFoldStart);
        await surface.UpdateAsync(() => view.SetFolded(foldStart, true), "collapse the fold");
        surface.Cell(new Point(0, foldStart)).Text.ShouldBe("▶");

        await surface.UpdateAsync(
            () => view.Style = view.ActualStyle with { CollapsedGlyph = new Rune('X') },
            "swap the collapsed glyph");

        surface.Cell(new Point(0, foldStart)).Text.ShouldBe("X");
    }

    private static Theme WithSemanticColor(SemanticColor role, Color value)
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

        theme.SetStyleSections(source.StyleSections.ToDictionary(static pair => pair.Key, static pair => pair.Value));
        theme.Freeze();
        return theme;
    }
}
