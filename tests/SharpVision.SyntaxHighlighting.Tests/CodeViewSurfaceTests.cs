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

    /// <summary>Verifies a tab character contributes exactly one cell to the committed horizontal
    /// extent - the same one cell <c>DrawSlice</c> substitutes and draws for it - rather than the
    /// zero cells a raw, unsubstituted measurement would count it as.</summary>
    [Fact]
    public async Task Extent_WhenALineContainsATab_CountsItAsExactlyOneCellAsync()
    {
        var tabbed = new CodeView { Code = "a\tb\n" };
        await using var tabbedSurface = await ComponentSurface.MountAsync(
            tabbed,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        var spaced = new CodeView { Code = "a b\n" };
        await using var spacedSurface = await ComponentSurface.MountAsync(
            spaced,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        tabbed.Extent.Width.ShouldBe(spaced.Extent.Width);
    }

    /// <summary>Verifies a double-click selects the complete word under the pointer.</summary>
    [Fact]
    public async Task Pointer_WhenDoubleClicked_SelectsTheCompleteWordAsync()
    {
        var view = new CodeView { Code = "alpha beta gamma\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Column 2 is the gutter's width; "beta" starts right after "alpha ", so column 2 + 7
        // lands one character into "beta".
        await surface.Pointer.ClickAsync(view, new Point(2 + 7, 0));
        await surface.Pointer.ClickAsync(view, new Point(2 + 7, 0));

        view.SelectedText.ShouldBe("beta");
    }

    /// <summary>Verifies a triple-click selects the complete line under the pointer.</summary>
    [Fact]
    public async Task Pointer_WhenTripleClicked_SelectsTheCompleteLineAsync()
    {
        var view = new CodeView { Code = "alpha beta gamma\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(view, new Point(2 + 7, 0));
        await surface.Pointer.ClickAsync(view, new Point(2 + 7, 0));
        await surface.Pointer.ClickAsync(view, new Point(2 + 7, 0));

        view.SelectedText.ShouldBe("alpha beta gamma");
    }

    /// <summary>Verifies a wheel notch scrolls the view by <see cref="CodeView.LineSize"/> lines,
    /// using a non-default LineSize so the assertion cannot pass merely by coincidence with a
    /// hardcoded single-line delta.</summary>
    [Fact]
    public async Task Pointer_WhenWheelScrolled_MovesTheVerticalOffsetByLineSizeAsync()
    {
        var code = string.Join('\n', Enumerable.Range(0, 20).Select(index => $"line{index}")) + "\n";
        var view = new CodeView { Code = code, LineSize = 3 };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.Pointer.WheelAsync(view, new Point(0, 0), wheelY: -1);

        view.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies reassigning <see cref="CodeView.Code"/> to shorter text while a drag is
    /// still in progress cancels the drag instead of leaving a stale anchor that the next move
    /// event would use to build an out-of-range Selection.</summary>
    [Fact]
    public async Task Code_WhenReassignedDuringAnInProgressDrag_CancelsTheDragInsteadOfGoingOutOfRangeAsync()
    {
        var view = new CodeView { Code = "abcdefghijklmnopqrstuvwxyz\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Start a drag near the end of the long original line, so _pointerAnchor is an offset
        // that would exceed the length of the much shorter replacement text below.
        await surface.Pointer.MoveToAsync(view, new Point(2 + 15, 0));
        await surface.Pointer.PressAsync();

        await surface.UpdateAsync(() => view.Code = "ab\n", "replace Code mid-drag with shorter text");

        // The drag must be fully canceled, not merely tolerant of the next event: capture released
        // and selecting turned off, exactly as a real Release would leave it.
        view.HasPointerCapture.ShouldBeFalse();

        // A move event continuing the same physical drag must not resurrect selection extension
        // from the now-stale anchor.
        await surface.Pointer.MovePressedToAsync(new Point(2, 0));
        _ = view.SelectedText;
        await surface.Pointer.ReleaseAsync();
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

    /// <summary>Verifies a pointer drag held past the control's own right edge on a line wider
    /// than the viewport keeps auto-scrolling and extending the selection for as long as the
    /// button stays down, eventually reaching the true end of the line without any further
    /// pointer motion.</summary>
    [Fact]
    public async Task Pointer_WhenHeldPastTheRightEdge_AutoScrollsToTheEndOfAWideLineAsync()
    {
        const string Line = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN";
        var clock = new ManualTimeProvider();
        var view = new CodeView { Code = Line + "\n", Width = Length.Cells(20) };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(30, 3),
            clock,
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(view, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(view.Bounds.Right + 5, view.Bounds.Y));

        // Advancing the clock in increments matching the auto-scroll interval - rather than in
        // one large jump - lets the dispatcher actually drain and re-arm the timer between due
        // periods, the same pattern SpinnerSurfaceTests uses to observe every animation frame.
        for (var tick = 0; tick < 40; tick++)
        {
            await surface.AdvanceAsync(TimeSpan.FromMilliseconds(60), $"auto-scroll tick {tick}");
        }

        await surface.Pointer.ReleaseAsync();

        view.SelectedText.ShouldBe(Line);
    }

    /// <summary>Verifies a pointer drag held past the control's own bottom edge on a buffer taller
    /// than the viewport keeps scrolling down and extending the selection line by line for as long
    /// as the button stays down, eventually reaching the last line without any further pointer
    /// motion.</summary>
    [Fact]
    public async Task Pointer_WhenHeldPastTheBottomEdge_AutoScrollsToTheLastLineAsync()
    {
        var lines = Enumerable.Range(0, 20).Select(index => $"line{index}").ToArray();
        var code = string.Join('\n', lines) + "\n";
        var clock = new ManualTimeProvider();
        var view = new CodeView { Code = code, Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 10),
            clock,
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(view, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(view.Bounds.X + 2, view.Bounds.Bottom + 5));

        // Advancing the clock in increments matching the auto-scroll interval - rather than in
        // one large jump - lets the dispatcher actually drain and re-arm the timer between due
        // periods, the same pattern SpinnerSurfaceTests uses to observe every animation frame.
        for (var tick = 0; tick < 40; tick++)
        {
            await surface.AdvanceAsync(TimeSpan.FromMilliseconds(60), $"auto-scroll tick {tick}");
        }

        await surface.Pointer.ReleaseAsync();

        view.SelectedText.ShouldBe(code);
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

    /// <summary>Verifies Left arrow moves the caret back by one grapheme.</summary>
    [Fact]
    public async Task Keyboard_WhenLeftArrowIsPressed_MovesCaretBackByOneAsync()
    {
        var view = new CodeView { Code = "abcdef\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(view, new Point(2 + 3, 0));

        await surface.Keyboard.PressAsync(Code.Left);

        view.Selection.Caret.ShouldBe(2);
    }

    /// <summary>Verifies Home moves the caret to the start of the current line.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeIsPressed_MovesCaretToLineStartAsync()
    {
        var view = new CodeView { Code = "abcdef\n" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(view, new Point(2 + 4, 0));

        await surface.Keyboard.PressAsync(Code.Home);

        view.Selection.Caret.ShouldBe(0);
    }

    /// <summary>Verifies Page Down moves the caret forward by one viewport height minus PageOverlap.</summary>
    [Fact]
    public async Task Keyboard_WhenPageDownIsPressed_MovesCaretByOneViewportHeightAsync()
    {
        var code = string.Join('\n', Enumerable.Range(0, 20).Select(index => $"line{index}")) + "\n";
        var view = new CodeView { Code = code };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(view, new Point(2, 0));
        var pageSize = Math.Max(1, view.Viewport.Height - view.PageOverlap);

        await surface.Keyboard.PressAsync(Code.PageDown);

        // The caret must land at the start of the line exactly pageSize rows below where it
        // started, matching Down arrow's own "same column, next visible line" semantics repeated
        // pageSize times.
        var expectedLineStart = string.Join('\n', Enumerable.Range(0, pageSize).Select(index => $"line{index}")).Length + 1;
        view.Selection.Caret.ShouldBe(expectedLineStart);
    }

    /// <summary>Verifies Page Up moves the caret back by one viewport height minus PageOverlap,
    /// undoing an equivalent Page Down.</summary>
    [Fact]
    public async Task Keyboard_WhenPageUpIsPressed_MovesCaretBackByOneViewportHeightAsync()
    {
        var code = string.Join('\n', Enumerable.Range(0, 20).Select(index => $"line{index}")) + "\n";
        var view = new CodeView { Code = code };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(view, new Point(2, 0));
        await surface.Keyboard.PressAsync(Code.PageDown);
        var afterPageDown = view.Selection.Caret;

        await surface.Keyboard.PressAsync(Code.PageUp);

        view.Selection.Caret.ShouldBe(0);
        afterPageDown.ShouldBeGreaterThan(0);
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
