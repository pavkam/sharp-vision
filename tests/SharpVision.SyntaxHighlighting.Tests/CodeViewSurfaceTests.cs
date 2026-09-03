// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

using SharpVision.Controls.Documents;
using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;
using SharpVision.Layout;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Unicode;

/// <summary>Verifies CodeView's mounted rendering, keyboard, and pointer behavior.</summary>
public sealed class CodeViewSurfaceTests
{
    /// <summary>Verifies the selectable projection maps only complete visible code graphemes.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenMountedWithUnicodeAndTabs_MapsVisibleCodeWithoutGutterAsync()
    {
        var view = new CodeView { Code = "a🙂\te\u0301\nclipped", Height = Length.Cells(1) };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(12, 1),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(
            () => snapshot = GetSelectableSnapshot(view),
            "project selectable code text");

        snapshot!.Text.ShouldBe("a🙂\te\u0301\nclipped");
        snapshot.Glyphs.Select(static glyph => glyph.Range).ShouldBe(
            [new Selection(0, 1), new Selection(1, 3), new Selection(3, 4), new Selection(4, 6)]);
        snapshot.Glyphs.Select(static glyph => glyph.Bounds).ShouldBe(
            [new Rect(2, 0, 1, 1), new Rect(3, 0, 2, 1), new Rect(5, 0, 1, 1), new Rect(6, 0, 1, 1)]);
    }

    /// <summary>Verifies folded and offscreen code remains semantic while exposing no stale geometry.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenFoldedAndScrolled_PreservesCompleteTextAndOnlyVisibleGeometryAsync()
    {
        var view = new CodeView
        {
            Code = "fn main() {\n    hidden();\n}\nafter()\n",
            Language = "Rust",
            Height = Length.Cells(2),
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(12, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 4).First(view.IsFoldStart);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(
            () =>
            {
                _ = view.SetFolded(foldStart, true);
                snapshot = GetSelectableSnapshot(view);
            },
            "fold and project selectable code text");

        snapshot!.Text.ShouldBe("fn main() {\n    hidden();\n}\nafter()\n");
        snapshot.Glyphs.ShouldNotContain(static glyph => glyph.Range.Start >= "fn main() {\n".Length && glyph.Range.Start < "fn main() {\n    hidden();\n".Length);
        snapshot.Glyphs.ShouldAllBe(static glyph => glyph.Bounds.Y >= 0 && glyph.Bounds.Y < 2);
    }

    /// <summary>Verifies selectable viewport movement is bounded and reports only real changes.</summary>
    [Fact]
    public async Task SelectableTextViewport_WhenScrolledAndRevealed_ReportsActualBoundedMovementAsync()
    {
        var view = new CodeView
        {
            Code = "01234567890123456789\nsecond\nthird\nfourth",
            Width = Length.Cells(8),
            Height = Length.Cells(2),
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(8, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => view.SelectableTextViewport.ShouldBe(new Rect(0, 0, view.Viewport.Width, view.Viewport.Height)),
            "read selectable code viewport");

        await surface.UpdateAsync(
            () => view.ScrollSelectableTextViewport(100, 100).ShouldBeTrue(),
            "scroll selectable viewport to its end");
        var saturated = new Point(view.HorizontalOffset, view.VerticalOffset);
        await surface.UpdateAsync(
            () => view.ScrollSelectableTextViewport(100, 100).ShouldBeFalse(),
            "attempt saturated selectable viewport scroll");
        new Point(view.HorizontalOffset, view.VerticalOffset).ShouldBe(saturated);

        await surface.UpdateAsync(
            () => view.RevealSelectableTextOffset(0).ShouldBeTrue(),
            "reveal first selectable code offset");
        view.HorizontalOffset.ShouldBe(0);
        view.VerticalOffset.ShouldBe(0);
        await surface.UpdateAsync(
            () => view.RevealSelectableTextOffset(0).ShouldBeFalse(),
            "reveal already visible selectable code offset");
    }

    /// <summary>Verifies a leading tab contributes the same single display cell to extent,
    /// scrolling, rendering, and selectable glyph geometry.</summary>
    [Fact]
    public async Task SelectableTextViewport_WhenLineStartsWithTab_SaturatesAtTheRenderedFinalCellAsync()
    {
        var view = new CodeView
        {
            Code = "\t1234567",
            Width = Length.Cells(8),
            Height = Length.Cells(2),
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(8, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(
            () => view.ScrollSelectableTextViewport(100, 0).ShouldBeTrue(),
            "scroll tab-prefixed code to its horizontal end");
        await surface.UpdateAsync(
            () =>
            {
                view.ScrollSelectableTextViewport(1, 0).ShouldBeFalse();
                snapshot = GetSelectableSnapshot(view);
            },
            "prove the tab-prefixed viewport is saturated");

        view.HorizontalOffset.ShouldBe(2);
        surface.Cell(new Point(7, 0)).Text.ShouldBe("7");
        snapshot!.Glyphs[^1].Range.ShouldBe(new Selection(7, 8));
        snapshot.Glyphs[^1].Bounds.ShouldBe(new Rect(7, 0, 1, 1));
    }

    /// <summary>Verifies revealing a semantic offset hidden by folding expands its containing fold
    /// and then performs the minimum intrinsic viewport scroll.</summary>
    [Fact]
    public async Task RevealSelectableTextOffset_WhenOffsetIsInsideCollapsedFold_ExpandsAndRevealsItAsync()
    {
        var view = new CodeView
        {
            Code = "fn main() {\nhidden();\n}\nafter()\n",
            Language = "Rust",
            Height = Length.Cells(1),
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 1),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 4).First(view.IsFoldStart);
        var hiddenOffset = "fn main() {\n".Length;
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(
            () =>
            {
                _ = view.SetFolded(foldStart, true);
                view.SetSelection(new Selection(hiddenOffset, hiddenOffset + 1));
            },
            "collapse the code fold with a semantic selection inside it");
        await surface.UpdateAsync(
            () => view.RevealSelectableTextOffset(hiddenOffset).ShouldBeTrue(),
            "reveal semantic code hidden by the fold");
        await surface.UpdateAsync(
            () => snapshot = GetSelectableSnapshot(view),
            "project the revealed folded code");

        view.IsFolded(foldStart).ShouldBeFalse();
        view.VerticalOffset.ShouldBe(1);
        view.SelectedText.ShouldBe("h");
        view.CopySelection().ShouldBe("h");
        snapshot!.Glyphs.ShouldContain(static glyph => glyph.Range == new Selection(12, 13));
        surface.Cell(new Point(2, 0)).Text.ShouldBe("h");
    }

    /// <summary>Verifies revealing a wide line deep inside a collapsed fold waits for the expanded
    /// extent before committing bounded offsets and paints the requested glyph.</summary>
    [Fact]
    public async Task RevealSelectableTextOffset_WhenCollapsedTargetIsDeepAndWide_UsesExpandedExtentAsync()
    {
        var hiddenLines = Enumerable.Range(0, 300).Select(static index => $"line{index}").ToList();
        hiddenLines[275] = new string('x', 80) + "Z";
        var code = "fn main() {\n" + string.Join('\n', hiddenLines) + "\n}\nafter()\n";
        var view = new CodeView
        {
            Code = code,
            Language = "Rust",
            Height = Length.Cells(3),
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 302).First(view.IsFoldStart);
        var targetOffset = code.IndexOf('Z');
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(() => view.SetFolded(foldStart, true), "collapse the deep code fold");
        await surface.UpdateAsync(
            () => view.RevealSelectableTextOffset(targetOffset).ShouldBeTrue(),
            "reveal the deep wide folded glyph");
        await surface.UpdateAsync(
            () => snapshot = GetSelectableSnapshot(view),
            "project the deep wide revealed glyph");

        view.IsFolded(foldStart).ShouldBeFalse();
        view.VerticalOffset.ShouldBeGreaterThan(250);
        view.HorizontalOffset.ShouldBeGreaterThan(60);
        var targetGlyph = snapshot!.Glyphs.Single(glyph => glyph.Range == new Selection(targetOffset, targetOffset + 1));
        surface.Cell(new Point(targetGlyph.Bounds.X, targetGlyph.Bounds.Y)).Text.ShouldBe("Z");
    }

    /// <summary>Verifies a synchronous scroll callback that replaces the semantic projection
    /// cannot make reveal continue through stale line indexes.</summary>
    [Fact]
    public async Task RevealSelectableTextOffset_WhenScrollCallbackReplacesCode_StopsSafelyAsync()
    {
        var original = string.Join('\n', Enumerable.Range(0, 100).Select(static index => $"line{index}"));
        var view = new CodeView { Code = original, Height = Length.Cells(2) };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var callbackCount = 0;
        var replacementCount = 0;
        var revealed = true;

        await surface.UpdateAsync(
            () =>
            {
                view.ScrollChanged += (_, _) =>
                {
                    callbackCount++;

                    if (replacementCount == 0)
                    {
                        replacementCount++;
                        view.Code = "short";
                    }
                };
                revealed = view.RevealSelectableTextOffset(original.Length - 1);
            },
            "replace code during reveal scrolling");

        callbackCount.ShouldBeGreaterThanOrEqualTo(1);
        replacementCount.ShouldBe(1);
        revealed.ShouldBeFalse();
        view.Code.ShouldBe("short");
        view.HorizontalOffset.ShouldBe(0);
        view.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies a synchronous scroll callback that hides the control terminates reveal
    /// without attempting a later horizontal commit against invisible geometry.</summary>
    [Fact]
    public async Task RevealSelectableTextOffset_WhenScrollCallbackHidesControl_StopsSafelyAsync()
    {
        var text = string.Join('\n', Enumerable.Range(0, 20).Select(static _ => new string('x', 80)));
        var view = new CodeView { Code = text, Height = Length.Cells(2) };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var revealed = true;

        await surface.UpdateAsync(
            () =>
            {
                view.ScrollChanged += (_, _) => view.Visibility = Visibility.Hidden;
                revealed = view.RevealSelectableTextOffset(text.Length - 1);
            },
            "hide code during reveal scrolling");

        revealed.ShouldBeFalse();
        view.Visibility.ShouldBe(Visibility.Hidden);
        view.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies selectable snapshot projection inspects only clipped viewport rows even
    /// when the authoritative semantic stream contains a very large number of lines.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenSourceHasManyLines_InspectsOnlyViewportRowsAsync()
    {
        var view = new CodeView
        {
            Code = string.Join('\n', Enumerable.Repeat("x", 100_000)),
            Height = Length.Cells(3),
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(12, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(
            () => snapshot = GetSelectableSnapshot(view),
            "project a huge selectable code source");

        snapshot!.Text.ShouldBe(view.Code);
        view.LastSelectableTextSnapshotInspectedLineCount.ShouldBeLessThanOrEqualTo(view.Viewport.Height);
        view.LastSelectableTextSnapshotInspectedLineCount.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies a narrow viewport bounds snapshot and render work before a huge line tail.</summary>
    [Fact]
    public async Task Render_WhenLineHasHugeOffscreenTail_InspectsOnlyViewportGraphemesAsync()
    {
        var view = new CodeView { Code = new string('x', 1_000_000), IsFoldingEnabled = false };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(8, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () =>
            {
                view.ScrollSelectableTextViewport(3, 0).ShouldBeTrue();
                _ = GetSelectableSnapshot(view);
            },
            "scroll and snapshot narrow long line");

        view.LastSelectableTextSnapshotInspectedGraphemeCount.ShouldBeLessThanOrEqualTo(12);
        view.LastRenderInspectedGraphemeCount.ShouldBeLessThanOrEqualTo(12);
        surface.Cell(new Point(5, 0)).Text.ShouldBe("x");
    }

    /// <summary>Verifies horizontal viewport movement keeps rendered and selectable geometry aligned.</summary>
    [Fact]
    public async Task SelectableTextViewport_WhenScrolledHorizontally_AlignsRenderedAndProjectedCodeAsync()
    {
        var view = new CodeView
        {
            Code = "0123456789",
            Width = Length.Cells(8),
            Height = Length.Cells(2),
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(8, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(
            () =>
            {
                view.ScrollSelectableTextViewport(3, 0).ShouldBeTrue();
                snapshot = GetSelectableSnapshot(view);
            },
            "scroll and project horizontal code viewport");

        surface.Cell(new Point(2, 0)).Text.ShouldBe("3");
        snapshot!.Glyphs[0].Range.ShouldBe(new Selection(3, 4));
        snapshot.Glyphs[0].Bounds.ShouldBe(new Rect(2, 0, 1, 1));
    }

    /// <summary>Verifies a Document can own a partial selection spanning into and out of code.</summary>
    [Fact]
    public async Task DocumentSelection_WhenCodeViewIsEmbedded_SelectsPartialCodeInTheCombinedStreamAsync()
    {
        var code = new CodeView
        {
            Code = "alpha🙂omega\nsecond",
            Height = Length.Cells(2),
        };
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph("before"),
                new DocumentBlockControl(code),
                new DocumentParagraph("after"),
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(24, 6),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(document.SelectAll, "select complete document text");
        var complete = string.Empty;
        await surface.UpdateAsync(() => complete = document.SelectedText, "read complete document selection");
        complete.ShouldBe("before\nalpha🙂omega\nsecond\nafter");
        var start = complete.IndexOf("pha", StringComparison.Ordinal);
        var end = complete.IndexOf("after", StringComparison.Ordinal) + 2;

        await surface.UpdateAsync(
            () => document.SetSelection(new Selection(start, end)),
            "select across partial code and following document text");

        var copied = string.Empty;
        await surface.UpdateAsync(() => copied = document.CopySelection(), "copy partial code selection");
        copied.ShouldBe("pha🙂omega\nsecond\naf");
        code.Selection.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies Document keyboard selection into hidden code expands and scrolls the
    /// nested CodeView while retaining Document ownership and copy semantics.</summary>
    [Fact]
    public async Task DocumentKeyboardSelection_WhenCaretEntersCollapsedCode_RevealsNestedViewportAsync()
    {
        var code = new CodeView
        {
            Code = "fn main() {\nhidden();\n}\nafter()\n",
            Language = "Rust",
            Height = Length.Cells(1),
        };
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph("before"),
                new DocumentBlockControl(code),
                new DocumentParagraph("after"),
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(24, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 4).First(code.IsFoldStart);
        var hiddenOffset = "before\n".Length + "fn main() {\n".Length;

        await surface.UpdateAsync(
            () =>
            {
                _ = code.SetFolded(foldStart, true);
                document.SetSelection(new Selection(hiddenOffset, hiddenOffset));
                document.Focus().ShouldBeTrue();
            },
            "place the document caret inside collapsed code");
        var shiftRight = new KeyEventArgs(new Stroke(
            Code.Right,
            character: null,
            nativeCode: 0,
            Modifiers.Shift,
            KeyAction.Press));
        await surface.UpdateAsync(
            () => _ = Router.Route(document, Events.Key, shiftRight),
            "extend the document selection into collapsed code");

        await surface.UpdateAsync(
            () =>
            {
                document.SelectedText.ShouldBe("h");
                document.CopySelection().ShouldBe("h");
                code.Selection.IsEmpty.ShouldBeTrue();
                code.IsFolded(foldStart).ShouldBeFalse();
                code.VerticalOffset.ShouldBe(1);
                document.VerticalOffset.ShouldBe(0);
            },
            "verify nested reveal and document copy ownership");
    }

    /// <summary>Verifies Document paints its selection over syntax cells without selecting the child.</summary>
    [Fact]
    public async Task DocumentSelection_WhenCodeGlyphIsSelected_PaintsOnlyTheMappedCodeCellAsync()
    {
        var selectedBackground = Color.Rgb(255, 0, 255);
        var code = new CodeView { Code = "alpha", Height = Length.Cells(1) };
        var document = new Document
        {
            Style = DocumentStyle.Default with
            {
                SelectionFace = new Face(
                    Color.Rgb(250, 250, 250),
                    selectedBackground,
                    TerminalAttributes.Bold,
                    Underline.None,
                    Color.Default)
            },
            Blocks = { new DocumentBlockControl(code) }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;
        await surface.UpdateAsync(
            () => snapshot = GetSelectableSnapshot(code),
            "project embedded code glyph geometry");
        var firstGlyph = snapshot!.Glyphs[0];
        var selectedPoint = new Point(
            code.Bounds.X + firstGlyph.Bounds.X,
            code.Bounds.Y + firstGlyph.Bounds.Y);
        var gutterPoint = new Point(code.Bounds.X, code.Bounds.Y);
        var adjacentPoint = new Point(selectedPoint.X + firstGlyph.Bounds.Width, selectedPoint.Y);
        var gutterStyle = surface.Cell(gutterPoint).Style;
        var adjacentStyle = surface.Cell(adjacentPoint).Style;

        await surface.UpdateAsync(
            () => document.SetSelection(new Selection(0, 1)),
            "select first embedded code glyph");

        surface.Cell(selectedPoint).Text.ShouldBe("a");
        surface.Cell(selectedPoint).Style.Attributes.ShouldBe(TerminalAttributes.Bold);
        surface.Cell(selectedPoint).Style.Background.ShouldBe(selectedBackground);
        surface.Cell(gutterPoint).Style.ShouldBe(gutterStyle);
        surface.Cell(adjacentPoint).Style.ShouldBe(adjacentStyle);
        code.Selection.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies a code drag transfers capture to Document after the shared threshold.</summary>
    [Fact]
    public async Task Pointer_WhenEmbeddedCodeDragCrossesThreshold_TransfersSelectionToDocumentAsync()
    {
        var code = new CodeView { Code = "abcdef", Height = Length.Cells(1) };
        var document = new Document { Blocks = { new DocumentBlockControl(code) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(new Point(2, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(code);
        await surface.Pointer.MovePressedToAsync(new Point(3, 0));

        surface.ShouldHaveCapture(document);
        surface.ShouldHaveFocus(document);

        await surface.Pointer.ReleaseAsync();
        var selected = string.Empty;
        await surface.UpdateAsync(() => selected = document.SelectedText, "read code drag selection");
        selected.ShouldBe("a");
        code.Selection.IsEmpty.ShouldBeTrue();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies an edge-held Document drag scrolls the nested CodeView on fake time.</summary>
    [Fact]
    public async Task Pointer_WhenEmbeddedCodeDragLeavesRightEdge_AutoScrollsCodeViewFirstAsync()
    {
        var code = new CodeView
        {
            Code = "01234567890123456789",
            Width = Length.Cells(8),
            Height = Length.Cells(2),
            Style = CodeViewStyle.Default with { Border = default },
        };
        var document = new Document
        {
            Width = Length.Cells(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = DocumentStyle.Default with { Border = default },
            Blocks = { new DocumentBlockControl(code) }
        };
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 3),
            time,
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(code, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(code, new Point(3, 0));
        surface.ShouldHaveCapture(document);
        var edge = new Point(code.Bounds.Right, code.Bounds.Y);
        await surface.Pointer.MovePressedToAsync(edge);

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(49), "advance before nested code autoscroll");
        code.HorizontalOffset.ShouldBe(0);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "complete nested code autoscroll interval");

        code.HorizontalOffset.ShouldBe(1);
        document.VerticalOffset.ShouldBe(0);
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies code scrolling preserves a Document range while code mutation clears it.</summary>
    [Fact]
    public async Task DocumentSelection_WhenCodeScrollsThenMutates_PreservesThenClearsTheRangeAsync()
    {
        var code = new CodeView
        {
            Code = "01234567890123456789\nsecond\nthird",
            Width = Length.Cells(8),
            Height = Length.Cells(2),
        };
        var document = new Document { Blocks = { new DocumentBlockControl(code) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => document.SetSelection(new Selection(2, 8)),
            "select embedded code range");
        await surface.UpdateAsync(
            () => code.ScrollSelectableTextViewport(4, 1).ShouldBeTrue(),
            "scroll embedded code viewport");
        var selected = string.Empty;
        await surface.UpdateAsync(() => selected = document.SelectedText, "read selection after code scroll");
        selected.ShouldBe("234567");

        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;
        await surface.UpdateAsync(() => code.Code = "replacement", "mutate embedded code semantics");
        await surface.UpdateAsync(() => selected = document.SelectedText, "refresh selection after code mutation");

        selected.ShouldBeEmpty();
        changes.ShouldBe(1);
    }

    /// <summary>Verifies keyboard focus recolors every physical flat border edge, not just the
    /// logical value.</summary>
    [Fact]
    public async Task Keyboard_WhenCodeViewReceivesFocus_RecolorsItsOwnBorderAsync()
    {
        // Arrange
        var view = new CodeView
        {
            Code = "fn main() {}\n",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var theme = view.Theme.ShouldNotBeNull();
        view.ActualBorder.Foreground.Literal.ShouldBe(theme.ResolveColor(SemanticColor.ControlBorder));

        var normalBorder = theme.ResolveColor(SemanticColor.ControlBorder);
        var normalStyles = view.ResolveBorderStyles(view.GetAppearanceState());
        normalStyles.Top.Foreground.ShouldBe(normalBorder);
        normalStyles.Right.Foreground.ShouldBe(normalBorder);
        normalStyles.Bottom.Foreground.ShouldBe(normalBorder);
        normalStyles.Left.Foreground.ShouldBe(normalBorder);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        view.IsFocused.ShouldBeTrue();
        var focusedBorder = theme.ResolveColor(SemanticColor.FocusedBorder);
        view.ActualBorder.Foreground.Literal.ShouldBe(focusedBorder);

        // Assert - every physical edge, not just the logical value, paints the authored focused
        // color.
        var focusedStyles = view.ResolveBorderStyles(view.GetAppearanceState());
        focusedStyles.Top.Foreground.ShouldBe(focusedBorder);
        focusedStyles.Right.Foreground.ShouldBe(focusedBorder);
        focusedStyles.Bottom.Foreground.ShouldBe(focusedBorder);
        focusedStyles.Left.Foreground.ShouldBe(focusedBorder);
    }

    /// <summary>Verifies a focused Turbo Vision CodeView shows the authored flat "focusedBorder"
    /// color on every edge instead of the Sunken shade/highlight split its own geometry - inherited
    /// from "container.normal.border.relief": "sunken", the only non-Flat relief left in any
    /// bundled theme - would otherwise substitute. <see cref="Theme.GetFocusableContainerStyleSet"/>
    /// rebases Input's Focused "border.foreground": "focusedBorder" delta onto Container's Normal
    /// geometry (Sunken relief included), so this exercises the border-relief-vs-authored-Foreground
    /// bypass at the one bundled-theme site where it is still observable.</summary>
    [Fact]
    public async Task Keyboard_WhenTurboVisionCodeViewReceivesFocus_ShowsFlatActiveFrameAsync()
    {
        // Arrange
        var view = new CodeView
        {
            Code = "fn main() {}\n",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = ThemeCatalog.Load("turbo-vision"),
            "apply the Turbo Vision theme");
        var theme = view.Theme.ShouldNotBeNull();

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        view.IsFocused.ShouldBeTrue();
        var focusedBorder = theme.ResolveColor(SemanticColor.FocusedBorder);
        view.ActualBorder.Foreground.Literal.ShouldBe(focusedBorder);

        // Assert - every physical edge resolves to the flat focused color, not a Sunken split.
        var focusedStyles = view.ResolveBorderStyles(view.GetAppearanceState());
        focusedStyles.Top.Foreground.ShouldBe(focusedBorder);
        focusedStyles.Right.Foreground.ShouldBe(focusedBorder);
        focusedStyles.Bottom.Foreground.ShouldBe(focusedBorder);
        focusedStyles.Left.Foreground.ShouldBe(focusedBorder);
    }

    private static SelectableTextSnapshot GetSelectableSnapshot<TSource>(TSource source)
        where TSource : ISelectableTextSource =>
        source.GetSelectableTextSnapshot();

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

    /// <summary>Verifies token boundaries inside a terminal grapheme never split the cluster that
    /// rendering commits to a cell; the style owning its first UTF-16 code unit owns it whole.</summary>
    [Fact]
    public async Task Render_WhenTokenBoundariesSplitExtendedGraphemes_DrawsEveryClusterAtomicallyAsync()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-code-view-graphemes-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "graphemes.xml"),
                """
                <language name="Graphemes" section="Sources" extensions="*.g" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                        <DetectChar attribute="Accent" context="#stay" char="e"/>
                        <StringDetect attribute="Accent" context="#stay" String="❤"/>
                        <StringDetect attribute="Accent" context="#stay" String="👩"/>
                        <StringDetect attribute="Accent" context="#stay" String="🇵"/>
                      </context>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                      <itemData name="Accent" defStyleNum="dsKeyword"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);
            var view = new CodeView
            {
                Catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName),
                Language = "Graphemes",
                Code = "e\u0301 ❤️ 👩‍💻 🇵🇹",
            };
            await using var surface = await ComponentSurface.MountAsync(
                view,
                new Size(20, 2),
                TestThemes.BorderlessContainer,
                TestContext.Current.CancellationToken);

            surface.Cell(new Point(2, 0)).Text.ShouldBe("e\u0301");
            surface.Cell(new Point(4, 0)).Text.ShouldBe("❤️");
            surface.Cell(new Point(7, 0)).Text.ShouldBe("👩‍💻");
            surface.Cell(new Point(10, 0)).Text.ShouldBe("🇵🇹");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
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

    /// <summary>Verifies mounted fold geometry uses the same one-cell tab policy as rendering, so
    /// a visibly deeper two-space child folds beneath its one-tab parent.</summary>
    [Fact]
    public async Task Folding_WhenTabAndSpaceIndentationAreMixed_MatchesRenderedCellDepthAsync()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-code-view-tabs-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "indented.xml"),
                """
                <language name="Indented" section="Sources" extensions="*.i" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts><context name="Normal" attribute="Normal Text" lineEndContext="#stay"/></contexts>
                    <itemDatas><itemData name="Normal Text" defStyleNum="dsNormal"/></itemDatas>
                  </highlighting>
                  <general><folding indentationsensitive="true"/></general>
                </language>
                """);
            var view = new CodeView
            {
                Catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName),
                Code = "\tparent\n  child\nroot",
                Language = "Indented",
            };
            await using var surface = await ComponentSurface.MountAsync(
                view,
                new Size(20, 5),
                TestThemes.BorderlessContainer,
                TestContext.Current.CancellationToken);

            var range = view.FoldRanges.ShouldHaveSingleItem();
            range.StartLine.ShouldBe(0);
            range.EndLine.ShouldBe(1);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
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

    /// <summary>Verifies Shift+Up immediately repaints cells removed from a shrinking selection
    /// without requiring a later pointer event to invalidate the retained code surface.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftUpShrinksSelection_RepaintsDeselectedCellsImmediatelyAsync()
    {
        var view = new CodeView
        {
            Code = "abc\ndef",
            IsFoldingEnabled = false,
        };
        view.SetSelection(new Selection(1, 5));
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(8, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => view.Focus().ShouldBeTrue(), "focus the code view");
        var deselectedPoint = new Point(1, 0);
        var selectedStyle = surface.Cell(deselectedPoint).Style;

        await surface.Keyboard.PressAsync(Code.Up, Modifiers.Shift);

        view.Selection.ShouldBe(new Selection(1, 1));
        surface.Cell(deselectedPoint).Text.ShouldBe("b");
        surface.Cell(deselectedPoint).Style.ShouldNotBe(selectedStyle);
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

    /// <summary>Verifies a primary press joined to another held button is not treated as the
    /// exclusive one-shot fold command.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Pointer_WhenPrimaryPressIsChorded_DoesNotToggleTheFoldAsync(int heldButton)
    {
        var view = new CodeView { Code = "fn f() {\n    g();\n}\n", Language = "Rust" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 3).First(view.IsFoldStart);
        var point = await surface.ResolvePointAsync(view, new Point(0, foldStart));
        var chord = Buttons.Primary | (heldButton == 1 ? Buttons.Middle : Buttons.Secondary);
        var eventArgs = new PointerEventArgs(new Pointer(
            point,
            pixels: null,
            chord,
            PointerAction.Press,
            wheelX: 0,
            wheelY: 0,
            Modifiers.None,
            isMotion: false,
            isCellPositionInferred: false));

        await surface.UpdateAsync(
            () => Router.Route(view, Events.Pointer, eventArgs),
            "route chorded primary press over fold gutter");

        view.IsFolded(foldStart).ShouldBeFalse();
    }

    /// <summary>Verifies a collapsed fold's inline indicator participates in horizontal extent
    /// and can be reached by scrolling to the end.</summary>
    [Fact]
    public async Task Fold_WhenCollapsed_IncludesReachableIndicatorInHorizontalExtentAsync()
    {
        var view = new CodeView { Code = "a {\n  b\n}\n", Language = "Rust" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(8, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 3).First(view.IsFoldStart);
        var expandedWidth = view.Extent.Width;

        await surface.UpdateAsync(() => view.SetFolded(foldStart, true), "collapse short fold");

        view.Extent.Width.ShouldBe(expandedWidth + 6);
        await surface.UpdateAsync(
            () => view.HorizontalOffset = view.Extent.Width - view.Viewport.Width,
            "scroll to collapsed indicator");
        var firstRow = string.Concat(Enumerable.Range(0, 8).Select(x => surface.Cell(new Point(x, 0)).Text));
        firstRow.ShouldContain("(...)");
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

    /// <summary>Verifies the default context-menu Copy command uses the same application clipboard
    /// route as Ctrl+C without requiring the legacy detached-control delegate.</summary>
    [Fact]
    public async Task ContextMenu_WhenCopyIsInvoked_UsesApplicationClipboardRouteAsync()
    {
        var view = new CodeView { Code = "abcdef", Height = Length.Cells(1) };
        var target = new TextInput { Height = Length.Cells(1) };
        var root = new Stack { Children = { view, target } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var menu = view.ContextMenu.ShouldBeOfType<CodeViewContextMenu>();
        await surface.UpdateAsync(() => view.SetSelection(new Selection(1, 4)), "select code to copy");

        await surface.Pointer.RightClickAsync(view, new Point(3, 0));
        await surface.UpdateAsync(
            () => menu.Items[0].ShouldBeOfType<MenuItem>().PerformInvoke(),
            "invoke default code copy command");
        await surface.UpdateAsync(() => target.Focus().ShouldBeTrue(), "focus paste target");
        await surface.SendAsync(Encoding.ASCII.GetBytes("\u001b[118;5u"), "press terminal Control+V");

        target.Text.ShouldBe("bcd");
    }

    /// <summary>Verifies repeated common caret navigation reuses the immutable complete selection
    /// map instead of re-enumerating every grapheme in a large source document.</summary>
    [Fact]
    public async Task Keyboard_WhenNavigatingLargeDocument_ReusesCompleteSelectionMapAsync()
    {
        var view = new CodeView
        {
            Code = string.Join('\n', Enumerable.Repeat("0123456789", 10_000)),
            Height = Length.Cells(3),
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => view.Focus().ShouldBeTrue(), "focus large code view");

        await surface.Keyboard.PressAsync(Code.Right);
        var buildsAfterWarmNavigation = view.TextSelectionMapBuildCount;
        await surface.Keyboard.PressAsync(Code.Right);

        buildsAfterWarmNavigation.ShouldBeGreaterThan(0);
        view.TextSelectionMapBuildCount.ShouldBe(buildsAfterWarmNavigation);
    }

    /// <summary>Verifies real terminal Ctrl+C publishes the focused embedded CodeView selection
    /// exactly once through Application while leaving the legacy writer unused.</summary>
    [Fact]
    public async Task Keyboard_WhenControlCIsPressed_UsesApplicationCopyRouteAsync()
    {
        var view = new CodeView { Code = "abcdef\n", Height = Length.Cells(1) };
        var document = new Document
        {
            Height = Length.Cells(2),
            Blocks = { new DocumentBlockControl(view) },
        };
        var target = new TextInput { Height = Length.Cells(1) };
        var root = new Stack { Children = { document, target } };
        var legacyWrites = 0;
        view.ClipboardWriter = _ => legacyWrites++;
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                document.SelectAll();
                view.SetSelection(new Selection(1, 4));
                view.Focus().ShouldBeTrue();
            },
            "focus embedded code with distinct local and document selections");

        await surface.SendAsync(Encoding.ASCII.GetBytes("\u001b[99;5u"), "press terminal Control+C");
        await surface.UpdateAsync(() => target.Focus().ShouldBeTrue(), "focus clipboard paste target");
        await surface.SendAsync(Encoding.ASCII.GetBytes("\u001b[118;5u"), "press terminal Control+V");

        target.Text.ShouldBe("bcd");
        legacyWrites.ShouldBe(0);
    }

    /// <summary>Verifies swapping the live Theme repaints a syntax color role even when the
    /// unresolved CodeViewStyle stays declaratively identical - the common case for every symbolic
    /// SemanticColor role CodeViewStyle.Complete assigns - by changing only SemanticColor.Cyan,
    /// which the "container" style section this control falls back to never authors.</summary>
    [Fact]
    public async Task Theme_WhenOnlyASyntaxSemanticColorChanges_RepaintsTheTokenAsync()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-view-theme-cyan-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "builtin-role.xml"),
                """
                <language name="BuiltInRoleTest" section="Sources" extensions="*.ext" version="1" kateversion="5.0">
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
            var themeA = WithSemanticColor(SemanticColor.Cyan, Color.Rgb(10, 20, 30));
            var themeB = WithSemanticColor(SemanticColor.Cyan, Color.Rgb(240, 230, 220));
            var view = new CodeView
            {
                Catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName),
                Language = "BuiltInRoleTest",
                Code = "!\n",
            };
            await using var surface = await ComponentSurface.MountAsync(
                view,
                new Size(10, 4),
                themeA,
                TestContext.Current.CancellationToken);
            var before = surface.Cell(new Point(3, 1)).Style.Foreground;

            await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap the Cyan semantic color");

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

    /// <summary>Verifies the default <see cref="CodeView.Overflow"/> - <see cref="Overflow.Visible"/>
    /// - leaves a long line's presentation exactly as before soft wrapping existed: one
    /// presentation row per source line, an unbounded content extent wider than the viewport, and
    /// ordinary horizontal scrolling. This is the regression guard for every unwrapped call site
    /// this feature repointed at the new presentation-row projection.</summary>
    [Fact]
    public async Task Overflow_WhenLeftAtDefault_LeavesLongLinesUnwrappedAndHorizontallyScrollableAsync()
    {
        var longLine = new string('x', 40);
        var view = new CodeView { Code = $"{longLine}\nshort" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(10, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        view.Overflow.ShouldBe(Overflow.Visible);
        view.Extent.Height.ShouldBe(2);
        view.Extent.Width.ShouldBeGreaterThan(view.Viewport.Width);

        await surface.UpdateAsync(
            () => view.ScrollSelectableTextViewport(100, 0).ShouldBeTrue(),
            "scroll the unwrapped long line horizontally");

        view.HorizontalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies switching <see cref="CodeView.Overflow"/> to <see cref="Overflow.WrapAnywhere"/>
    /// disables the horizontal extent - it settles to exactly the viewport's own width - and splits
    /// a line that used to overflow horizontally into more than one presentation row.</summary>
    [Fact]
    public async Task Overflow_WhenSetToWrapAnywhere_DisablesHorizontalExtentAndAddsPresentationRowsAsync()
    {
        var view = new CodeView { Code = new string('x', 60), IsFoldingEnabled = false };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 10),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var unwrappedHeight = view.Extent.Height;
        view.Extent.Width.ShouldBeGreaterThan(view.Viewport.Width);

        await surface.UpdateAsync(() => view.Overflow = Overflow.WrapAnywhere, "enable wrap-anywhere");

        view.Extent.Width.ShouldBe(view.Viewport.Width);
        view.Extent.Height.ShouldBeGreaterThan(unwrappedHeight);
        view.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies <see cref="Overflow.Clip"/> keeps a long line to exactly one presentation
    /// row - unlike <see cref="Overflow.Wrap"/>/<see cref="Overflow.WrapAnywhere"/>, which add rows
    /// - disables the horizontal extent the same as every other non-<see cref="Overflow.Visible"/>
    /// value, and renders the hard-clipped prefix of the line with no ellipsis glyph anywhere in the
    /// row.</summary>
    [Fact]
    public async Task Overflow_WhenSetToClip_KeepsOnePresentationRowAndRendersClippedPrefixWithNoEllipsisAsync()
    {
        var view = new CodeView { Code = new string('a', 40), Overflow = Overflow.Clip, IsFoldingEnabled = false };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(10, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        view.Extent.Height.ShouldBe(1);
        view.Extent.Width.ShouldBe(view.Viewport.Width);

        for (var x = 0; x < view.Viewport.Width; x++)
        {
            surface.Cell(new Point(x, 0)).Text.ShouldBe("a");
        }
    }

    /// <summary>Verifies <see cref="Overflow.Ellipsis"/> keeps a long line to exactly one
    /// presentation row, disables the horizontal extent the same as <see cref="Overflow.Clip"/>, and
    /// truncates the rendered row with the shared <see cref="ControlGlyphs.Text"/> ellipsis glyph in
    /// its final visible cell rather than a literal source character.</summary>
    [Fact]
    public async Task Overflow_WhenSetToEllipsis_KeepsOnePresentationRowAndRendersTrailingEllipsisAsync()
    {
        var view = new CodeView { Code = new string('a', 40), Overflow = Overflow.Ellipsis, IsFoldingEnabled = false };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(10, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        view.Extent.Height.ShouldBe(1);
        view.Extent.Width.ShouldBe(view.Viewport.Width);

        for (var x = 0; x < view.Viewport.Width - 1; x++)
        {
            surface.Cell(new Point(x, 0)).Text.ShouldBe("a");
        }

        surface.Cell(new Point(view.Viewport.Width - 1, 0)).Text.ShouldBe("…");
    }

    /// <summary>Verifies <see cref="Overflow.Ellipsis"/> resolves the shared ellipsis glyph through
    /// its ambiguous-width-safe fallback instead of drawing the raw two-cell "…" directly. The
    /// truncation point always lands on the row's last available cell, so under
    /// <see cref="Ambiguous.Wide"/> a raw, unresolved "…" would not fit alongside its own
    /// continuation cell there and the wide-cluster clip policy would silently drop it - leaving
    /// that cell blank - rather than corrupting the fold indicator that would otherwise follow it.
    /// Resolving through the fallback keeps the glyph exactly one cell so it always renders.</summary>
    [Fact]
    public async Task Overflow_WhenSetToEllipsisUnderAmbiguousWide_RendersOneCellFallbackGlyphAsync()
    {
        var view = new CodeView { Code = new string('a', 40), Overflow = Overflow.Ellipsis, IsFoldingEnabled = false };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(10, 5),
            TerminalOptions.Minimal with
            {
                Capabilities = TerminalCapabilities.Conservative with { AmbiguousWidth = Ambiguous.Wide }
            },
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        view.Extent.Height.ShouldBe(1);
        view.Extent.Width.ShouldBe(view.Viewport.Width);

        for (var x = 0; x < view.Viewport.Width - 1; x++)
        {
            surface.Cell(new Point(x, 0)).Text.ShouldBe("a");
        }

        var lastCell = surface.Cell(new Point(view.Viewport.Width - 1, 0));
        lastCell.Text.ShouldBe(".");
    }

    /// <summary>Verifies clicking a wrapped line's continuation row (its second or later
    /// presentation row) lands the caret on the correct logical offset - the same offset the
    /// clicked cell's own rendered character came from - rather than clamping to the first row.</summary>
    [Fact]
    public async Task Pointer_WhenClickingAWrappedContinuationRow_LandsOnTheCorrectLogicalOffsetAsync()
    {
        var line = string.Concat(Enumerable.Range(0, 60).Select(static index => (char) ('a' + (index % 26))));
        var view = new CodeView { Code = line, Overflow = Overflow.WrapAnywhere, IsFoldingEnabled = false };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(16, 10),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        view.Extent.Height.ShouldBeGreaterThan(1);

        await surface.Pointer.ClickAsync(view, new Point(0, 0));
        var firstRowCaret = view.Selection.Caret;
        surface.Cell(new Point(0, 0)).Text.ShouldBe(line[firstRowCaret].ToString());

        await surface.Pointer.ClickAsync(view, new Point(0, 1));
        var secondRowCaret = view.Selection.Caret;

        secondRowCaret.ShouldBeGreaterThan(firstRowCaret);
        surface.Cell(new Point(0, 1)).Text.ShouldBe(line[secondRowCaret].ToString());
    }

    /// <summary>Verifies a syntax token that spans across a wrap boundary keeps its own color on
    /// every presentation row it covers, proving <c>DrawLine</c> re-seeks the correct token for
    /// each wrapped row instead of defaulting to the line's first token.</summary>
    [Fact]
    public async Task Render_WhenAKeywordTokenSpansAWrapBoundary_KeepsItsColorAcrossPresentationRowsAsync()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-code-view-wrap-token-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "wraptoken.xml"),
                """
                <language name="WrapToken" section="Sources" extensions="*.wt" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                        <StringDetect attribute="Accent" context="#stay" String="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"/>
                      </context>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                      <itemData name="Accent" defStyleNum="dsKeyword"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);
            var code = new string('a', 20) + new string('b', 40);
            var view = new CodeView
            {
                Catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName),
                Language = "WrapToken",
                Code = code,
                Overflow = Overflow.WrapAnywhere,
                IsFoldingEnabled = false,
            };
            await using var surface = await ComponentSurface.MountAsync(
                view,
                new Size(8, 20),
                TestThemes.BorderlessContainer,
                TestContext.Current.CancellationToken);

            view.Extent.Height.ShouldBeGreaterThan(1);

            var normalColor = surface.Cell(new Point(0, 0)).Style.Foreground;
            Point? firstKeywordCell = null;

            for (var y = 0; y < view.Extent.Height && firstKeywordCell is null; y++)
            {
                for (var x = 0; x < view.Viewport.Width; x++)
                {
                    if (surface.Cell(new Point(x, y)).Text == "b")
                    {
                        firstKeywordCell = new Point(x, y);
                        break;
                    }
                }
            }

            _ = firstKeywordCell.ShouldNotBeNull();
            var keywordColor = surface.Cell(firstKeywordCell.Value).Style.Foreground;
            keywordColor.ShouldNotBe(normalColor);

            Point? laterRowKeywordCell = null;

            for (var y = firstKeywordCell.Value.Y + 1; y < view.Extent.Height && laterRowKeywordCell is null; y++)
            {
                for (var x = 0; x < view.Viewport.Width; x++)
                {
                    if (surface.Cell(new Point(x, y)).Text == "b")
                    {
                        laterRowKeywordCell = new Point(x, y);
                        break;
                    }
                }
            }

            _ = laterRowKeywordCell.ShouldNotBeNull();
            surface.Cell(laterRowKeywordCell.Value).Style.Foreground.ShouldBe(keywordColor);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies folding still hides interior lines and reports the same collapsed row
    /// count while <see cref="CodeView.Overflow"/> is a soft-wrapping value - folding operates on
    /// whole logical lines regardless of the presentation-row projection.</summary>
    [Fact]
    public async Task Fold_WhenCollapsedWithWrapEnabled_HidesInteriorLinesTheSameAsVisibleOverflowAsync()
    {
        var view = new CodeView
        {
            Code = "fn main() {\n    let x = 1;\n}\nafter()\n",
            Language = "Rust",
            Overflow = Overflow.Wrap,
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var foldStart = Enumerable.Range(0, 4).First(view.IsFoldStart);

        await surface.UpdateAsync(() => view.SetFolded(foldStart, true), "collapse the fold with wrap enabled");

        view.Extent.Height.ShouldBe(3);
        view.IsFolded(foldStart).ShouldBeTrue();

        await surface.UpdateAsync(() => view.SetFolded(foldStart, false), "expand the fold again");

        // 5, not 4: NormalizedCode.Split('\n') on this trailing-newline source yields a trailing
        // empty logical line, exactly as IsFoldingEnabled_WhenDisabledWhileCollapsed_ShowsEveryLineAgainAsync
        // already established for the identical Code string.
        view.Extent.Height.ShouldBe(5);
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
