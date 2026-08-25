// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Text;

using Document = Controls.Documents.Document;

/// <summary>Verifies Document selection as a final subtree adornment over semantic glyph cells.</summary>
public sealed class DocumentSelectionSurfaceTests
{
    /// <summary>Verifies every validated explicit range, including a collapsed range, establishes
    /// the keyboard caret while preserving its directional anchor.</summary>
    [Fact]
    public async Task Keyboard_WhenSelectionIsSetExplicitly_EstablishesCollapsedAndDirectionalCaretAsync()
    {
        var document = new Document { Blocks = { new DocumentParagraph("abcd") } };
        await using var surface = await ComponentSurface.MountAsync(document, new Size(8, 2), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => { document.SetSelection(new Selection(1, 1)); document.Focus().ShouldBeTrue(); }, "set collapsed selection");
        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(() => document.Selection, TestContext.Current.CancellationToken))
            .ShouldBe(new Selection(1, 2));

        await surface.UpdateAsync(() => document.SetSelection(new Selection(3, 1)), "set directional selection");
        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(() => document.Selection, TestContext.Current.CancellationToken))
            .ShouldBe(new Selection(3, 2));
    }

    /// <summary>Verifies a synchronous selection callback that transfers focus commits selection
    /// but prevents keyboard reveal from scrolling afterward.</summary>
    [Fact]
    public async Task Keyboard_WhenSelectionChangedTransfersFocus_DoesNotRevealAsync()
    {
        var document = new Document { Height = Length.Cells(2), Blocks = { new DocumentParagraph("one two three four five six") } };
        var sibling = new TextInput();
        var root = new Stack { Children = { document, sibling } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(8, 4), TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(document, new Point(0, 0));
        document.SelectionChanged += (_, _) => sibling.Focus();

        await RouteKeyAsync(surface, document, Code.PageDown, Modifiers.Shift);

        document.VerticalOffset.ShouldBe(0);
        surface.ShouldHaveFocus(sibling);
        (await surface.Application.Dispatcher.InvokeAsync(() => document.Selection.IsEmpty, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Verifies Ctrl+A also stops before reveal when its synchronous selection callback
    /// transfers direct focus to another control.</summary>
    [Fact]
    public async Task Keyboard_WhenControlASelectionChangedTransfersFocus_DoesNotRevealAsync()
    {
        var document = new Document { Height = Length.Cells(2), Blocks = { new DocumentParagraph("one two three four five six") } };
        var sibling = new TextInput();
        var root = new Stack { Children = { document, sibling } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(8, 4), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        document.SelectionChanged += (_, _) => sibling.Focus();

        await RouteKeyAsync(surface, document, Code.Character, Modifiers.Control, new Rune('a'));

        document.VerticalOffset.ShouldBe(0);
        surface.ShouldHaveFocus(sibling);
        (await surface.Application.Dispatcher.InvokeAsync(() => document.SelectedText, TestContext.Current.CancellationToken))
            .ShouldBe(document.SelectionMap.Text);
    }

    /// <summary>Verifies an equivalent-text projection replacement from SelectionChanged discards
    /// the old visual-boundary geometry before reveal even though the semantic fingerprint matches.</summary>
    [Fact]
    public async Task Keyboard_WhenSelectionChangedReprojectsEquivalentText_DiscardsOldGeometryAffinityAsync()
    {
        const string text = "abcdefgh";
        var document = new Document { Blocks = { new DocumentParagraph(text) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(5, 3),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                document.SetSelection(new Selection(0, 1));
                document.Focus().ShouldBeTrue();
            },
            "establish selection before equivalent reflow");
        var projection = document.SelectionMap;
        document.SelectionChanged += (_, _) =>
        {
            document.Blocks.Clear();
            document.Blocks.Add(new DocumentParagraph(text));
        };

        await RouteKeyAsync(surface, document, Code.End, Modifiers.Shift);

        document.SelectionMap.ShouldNotBeSameAs(projection);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe(text);
    }
    /// <summary>Verifies page overlap cannot reduce a selection page step below one visual row in
    /// either direction.</summary>
    [Fact]
    public async Task Keyboard_WhenPageOverlapConsumesViewport_ShiftPageMovesOneVisualRowAsync()
    {
        var document = new Document
        {
            Height = Length.Cells(3),
            PageOverlap = 20,
            Blocks = { new DocumentParagraph("abcdefghijkl") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(4, 3),
            TestContext.Current.CancellationToken);
        var anchor = document.SelectionMap.HitTest(new Point(1, 1));
        await surface.UpdateAsync(
            () =>
            {
                document.SetSelection(new Selection(0, anchor));
                document.Focus().ShouldBeTrue();
            },
            "establish selection caret on middle visual row");

        await RouteKeyAsync(surface, document, Code.PageDown, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBe(document.SelectionMap.HitTest(new Point(1, 2)));

        await RouteKeyAsync(surface, document, Code.PageUp, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBe(anchor);
    }

    /// <summary>Verifies sticky vertical navigation advances through a geometry-free row using its
    /// deterministic endpoint, then restores the remembered column on the following text row.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftVerticalCrossesGeometryFreeRow_RestoresDesiredColumnAsync()
    {
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph("abcd"),
                new DocumentParagraph("wxyz")
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 4),
            TestContext.Current.CancellationToken);
        document.SelectionMap.Glyphs.ShouldNotContain(glyph => glyph.Bounds.Y == 1);
        await surface.Pointer.ClickAsync(document, new Point(3, 0));

        await RouteKeyAsync(surface, document, Code.Down, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBe(document.SelectionMap.HitTest(new Point(3, 1)));

        await RouteKeyAsync(surface, document, Code.Down, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBe(document.SelectionMap.HitTest(new Point(3, 2)));
    }

    /// <summary>Verifies keyboard caret reveal remains confined to the active modal plane and
    /// cannot scroll an enclosing container outside that plane.</summary>
    [Fact]
    public async Task Keyboard_WhenAncestorIsOutsideModalPlane_DoesNotRevealThroughAncestorAsync()
    {
        var document = new Document
        {
            Height = Length.Cells(1),
            Blocks = { new DocumentParagraph("abc") }
        };
        var plane = new Stack { Height = Length.Cells(1), Children = { document } };
        var host = new Stack
        {
            Height = Length.Cells(2),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children =
            {
                plane,
                new ControlText("tail") { Height = Length.Cells(6) }
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        await surface.UpdateAsync(
            () =>
            {
                document.SetSelection(new Selection(0, 1));
                scope = surface.Application.Modality.Enter(plane, initialFocus: document);
                host.ScrollBy(0, 1).ShouldBeTrue();
            },
            "hide modal document behind outer viewport");

        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);

        host.VerticalOffset.ShouldBe(1);
        surface.ShouldHaveFocus(document);
        await surface.UpdateAsync(scope!.Dispose, "leave document modal plane");
    }

    /// <summary>Verifies Application resolves Document as the nearest clipboard-copy source after
    /// Ctrl+A and publishes its normalized semantic selection exactly once.</summary>
    [Fact]
    public async Task Clipboard_WhenDocumentOwnsFocusAfterControlA_PublishesSelectedTextOnceAsync()
    {
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph("first"),
                new DocumentParagraph("second")
            }
        };
        var target = new TextInput { AcceptsReturn = true };
        var root = new Stack { Children = { document, target } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document clipboard owner");
        await RouteKeyAsync(surface, document, Code.Character, Modifiers.Control, new Rune('a'));
        var expected = await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken);

        await SendControlCharacterAsync(surface, 'c');
        await surface.UpdateAsync(() => target.Focus().ShouldBeTrue(), "focus clipboard paste target");
        await SendControlCharacterAsync(surface, 'v');

        target.Text.ShouldBe(expected);
    }

    /// <summary>Verifies a focused embedded TextInput remains the nearest clipboard-copy source and
    /// prevents its containing Document selection from replacing the copied value.</summary>
    [Fact]
    public async Task Clipboard_WhenEmbeddedSourceOwnsFocus_UsesEmbeddedSelectionAsync()
    {
        var input = new TextInput { Text = "inner" };
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph("outer"),
                new DocumentBlockControl(input)
            }
        };
        var target = new TextInput();
        var root = new Stack { Children = { document, target } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                document.SelectAll();
                input.Focus().ShouldBeTrue();
                input.Select(0, input.Text.Length);
            },
            "focus nearest embedded clipboard source");

        await SendControlCharacterAsync(surface, 'c');
        await surface.UpdateAsync(() => target.Focus().ShouldBeTrue(), "focus clipboard paste target");
        await SendControlCharacterAsync(surface, 'v');

        target.Text.ShouldBe("inner");
    }

    /// <summary>Verifies horizontal keyboard extension consumes one complete extended grapheme and
    /// preserves the directional anchor while crossing it again.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftHorizontalExtends_MovesByWholeGraphemesAsync()
    {
        var document = new Document { Blocks = { new DocumentParagraph("e\u0301👩‍💻x") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(document, new Point(0, 0));

        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("e\u0301");
        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("e\u0301👩‍💻");
        await RouteKeyAsync(surface, document, Code.Left, Modifiers.Shift);

        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 2));
    }

    /// <summary>Verifies horizontal extension preserves the anchor while the caret crosses it and
    /// saturates at both semantic endpoints.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftHorizontalCrossesAnchor_PreservesDirectionAndSaturatesAsync()
    {
        var document = new Document { Blocks = { new DocumentParagraph("abc") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                document.SetSelection(new Selection(2, 0));
                document.Focus().ShouldBeTrue();
            },
            "establish reverse document selection");

        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);
        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);
        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);
        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);

        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(2, 3));
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("c");
    }

    /// <summary>Verifies vertical extension remembers the original visual column while traversing
    /// a shorter row and returning to a longer row.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftVerticalCrossesShortRow_PreservesDesiredColumnAsync()
    {
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph("abcd"),
                new DocumentParagraph("x"),
                new DocumentParagraph("wxyz")
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 5),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(document, new Point(3, 0));

        await RouteKeyAsync(surface, document, Code.Down, Modifiers.Shift);
        await RouteKeyAsync(surface, document, Code.Down, Modifiers.Shift);

        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBe(document.SelectionMap.HitTest(new Point(3, 2)));
        await RouteKeyAsync(surface, document, Code.Up, Modifiers.Shift);
        await RouteKeyAsync(surface, document, Code.Up, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBe(document.SelectionMap.HitTest(new Point(3, 0)));
    }

    /// <summary>Verifies an explicit selection replacement resets the sticky column before the next
    /// vertical keyboard extension.</summary>
    [Fact]
    public async Task Keyboard_WhenSelectionIsSetExplicitly_ResetsDesiredColumnAsync()
    {
        var document = new Document { Blocks = { new DocumentParagraph("abcdwxyz") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(4, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(document, new Point(3, 0));
        await RouteKeyAsync(surface, document, Code.Down, Modifiers.Shift);
        await surface.UpdateAsync(
            () => document.SetSelection(new Selection(0, 1)),
            "replace selection and reset desired column");

        await RouteKeyAsync(surface, document, Code.Down, Modifiers.Shift);

        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBe(document.SelectionMap.HitTest(new Point(1, 1)));
    }

    /// <summary>Verifies Ctrl+A belongs only to a directly focused document and tolerates lock-state modifiers.</summary>
    [Fact]
    public async Task Keyboard_WhenControlAIsOwnedByDocument_SelectsAllWithLocksAsync()
    {
        var input = new TextInput { Text = "child" };
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph("before"),
                new DocumentBlockControl(input)
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => input.Focus().ShouldBeTrue(), "focus embedded input");

        await RouteKeyAsync(
            surface,
            input,
            Code.Character,
            Modifiers.Control | Modifiers.CapsLock | Modifiers.NumLock,
            new Rune('a'));
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBeEmpty();

        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        await RouteKeyAsync(
            surface,
            document,
            Code.Character,
            Modifiers.Control | Modifiers.CapsLock | Modifiers.NumLock,
            new Rune('a'));

        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe(document.SelectionMap.Text);
    }

    /// <summary>Verifies keyboard extension reveals the active caret with the minimum document scroll.</summary>
    [Fact]
    public async Task Keyboard_WhenCaretMovesBelowViewport_RevealsItAsync()
    {
        var document = new Document
        {
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            Blocks = { new DocumentParagraph("one two three four five six seven eight nine") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(document, new Point(0, 0));

        await RouteKeyAsync(surface, document, Code.PageDown, Modifiers.Shift);

        document.VerticalOffset.ShouldBeGreaterThan(0);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.IsEmpty,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Verifies keyboard extension can horizontally reveal a caret inside a clipped,
    /// non-wrapping code line.</summary>
    [Fact]
    public async Task Keyboard_WhenCodeBlockCaretMovesPastRightEdge_RevealsItHorizontallyAsync()
    {
        var document = new Document { Blocks = { new DocumentCodeBlock("abcdefghij") } };

        await AssertHorizontalCaretRevealAsync(document);
    }

    /// <summary>Verifies keyboard extension can horizontally reveal a caret inside an ordinary
    /// token whose grapheme width exceeds the document viewport.</summary>
    [Fact]
    public async Task Keyboard_WhenOverwideWordCaretMovesPastRightEdge_RevealsItHorizontallyAsync()
    {
        var document = new Document { Blocks = { new DocumentParagraph("abcdefghij") } };

        await AssertHorizontalCaretRevealAsync(document);
    }

    /// <summary>Verifies keyboard extension can horizontally reveal a caret inside a table whose
    /// intrinsic column width exceeds the document viewport.</summary>
    [Fact]
    public async Task Keyboard_WhenWideTableCaretMovesPastRightEdge_RevealsItHorizontallyAsync()
    {
        var table = new DocumentTable
        {
            Rows = { new DocumentTableRow { Cells = { new DocumentTableCell("abcdefghij") } } }
        };
        var document = new Document { Blocks = { table } };

        await AssertHorizontalCaretRevealAsync(document);
    }

    /// <summary>Verifies visual line and page commands extend from the established anchor and
    /// saturate without changing selection ownership.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftLineAndPageCommandsArePressed_ExtendsAndSaturatesAsync()
    {
        var document = new Document
        {
            Height = Length.Cells(3),
            PageOverlap = 1,
            Blocks = { new DocumentParagraph("abcdefghij klmnopqrst uvwxyz") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(document, new Point(3, 0));

        await RouteKeyAsync(surface, document, Code.End, Modifiers.Shift);
        var lineEnd = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken);
        lineEnd.ShouldBe(document.SelectionMap.VisualLineBoundary(3, end: true));

        await RouteKeyAsync(surface, document, Code.Home, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBe(document.SelectionMap.VisualLineBoundary(lineEnd, end: false));

        await RouteKeyAsync(surface, document, Code.PageDown, Modifiers.Shift);
        await RouteKeyAsync(surface, document, Code.PageDown, Modifiers.Shift);
        await RouteKeyAsync(surface, document, Code.PageDown, Modifiers.Shift);
        var saturated = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken);
        await RouteKeyAsync(surface, document, Code.PageDown, Modifiers.Shift);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(saturated);
    }

    /// <summary>Verifies the innermost source viewport receives its local caret offset before the
    /// document performs any outer reveal.</summary>
    [Fact]
    public async Task Keyboard_WhenCaretBelongsToNestedViewport_RevealsSourceFirstAsync()
    {
        var source = new DocumentSelectionSourceProbe();
        var document = new Document { Blocks = { new DocumentBlockControl(source) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 2),
            TestContext.Current.CancellationToken);
        var glyph = document.SelectionMap.Glyphs.First(item => item.Source is not null);
        await surface.Pointer.ClickAsync(document, PointAt(glyph));
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document selection owner");

        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);

        source.RevealedOffset.ShouldBe(1);
    }

    /// <summary>Verifies caret reveal propagates through an enclosing armed container after the
    /// document viewport itself has no remaining movement to consume.</summary>
    [Fact]
    public async Task Keyboard_WhenDocumentIsOutsideAncestorViewport_RevealsThroughAncestorAsync()
    {
        var document = new Document
        {
            Height = Length.Cells(1),
            Blocks = { new DocumentParagraph("abc") }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children =
            {
                new ControlText("lead") { Height = Length.Cells(5) },
                document,
                new ControlText("tail") { Height = Length.Cells(5) }
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => outer.ScrollBy(0, 5).ShouldBeTrue(),
            "show document for caret placement");
        await surface.Pointer.ClickAsync(document, new Point(0, 0));
        await surface.UpdateAsync(
            () => outer.ScrollBy(0, -5).ShouldBeTrue(),
            "hide focused document above ancestor viewport");

        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);

        outer.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies an enclosing horizontal viewport receives minimum caret reveal even though
    /// Document's own intrinsic scrolling remains vertical-only.</summary>
    [Fact]
    public async Task Keyboard_WhenDocumentIsOutsideHorizontalAncestorViewport_RevealsHorizontalAxisAsync()
    {
        var document = new Document
        {
            Width = Length.Cells(3),
            Height = Length.Cells(1),
            Blocks = { new DocumentParagraph("abc") }
        };
        var outer = new Stack
        {
            Orientation = Orientation.Horizontal,
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            Children =
            {
                new ControlText("lead") { Width = Length.Cells(5) },
                document,
                new ControlText("tail") { Width = Length.Cells(5) }
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(3, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => outer.ScrollBy(5, 0).ShouldBeTrue(),
            "show document for horizontal caret placement");
        await surface.Pointer.ClickAsync(document, new Point(0, 0));
        await surface.UpdateAsync(
            () => outer.ScrollBy(-5, 0).ShouldBeTrue(),
            "hide focused document beside ancestor viewport");

        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);

        outer.HorizontalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies synchronous semantic replacement from a nested reveal callback prevents
    /// stale keyboard geometry from being applied afterward.</summary>
    [Fact]
    public async Task Keyboard_WhenNestedRevealReplacesContent_DoesNotRecommitStaleSelectionAsync()
    {
        var source = new DocumentSelectionSourceProbe();
        var document = new Document { Blocks = { new DocumentBlockControl(source) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 2),
            TestContext.Current.CancellationToken);
        var glyph = document.SelectionMap.Glyphs.First(item => item.Source is not null);
        await surface.Pointer.ClickAsync(document, PointAt(glyph));
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document selection owner");
        source.RevealAction = () =>
        {
            document.Blocks.Clear();
            document.Blocks.Add(new DocumentParagraph("replacement"));
        };

        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);

        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBeEmpty();
        document.SelectionMap.Text.ShouldBe("replacement");
    }

    /// <summary>Verifies semantic replacement from the document's synchronous ScrollChanged callback
    /// clears the just-extended range without a stale post-reveal recommit.</summary>
    [Fact]
    public async Task Keyboard_WhenScrollChangedReplacesContent_DoesNotRecommitStaleSelectionAsync()
    {
        var document = new Document
        {
            Height = Length.Cells(2),
            Blocks = { new DocumentParagraph("one two three four five six seven eight") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(document, new Point(0, 0));
        document.ScrollChanged += (_, _) =>
        {
            document.Blocks.Clear();
            document.Blocks.Add(new DocumentParagraph("replacement"));
        };

        await RouteKeyAsync(surface, document, Code.PageDown, Modifiers.Shift);

        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBeEmpty();
        document.SelectionMap.Text.ShouldBe("replacement");
    }

    /// <summary>Verifies an already-handled routed key cannot extend the document selection.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftCommandIsAlreadyHandled_PreservesSelectionAsync()
    {
        var document = new Document { Blocks = { new DocumentParagraph("abc") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(document, new Point(0, 0));
        await surface.UpdateAsync(
            () => _ = surface.Application.Root.AddHandler(
                Events.Key,
                (_, eventArgs) => eventArgs.IsHandled = true),
            "install handled preview route");
        var key = new KeyEventArgs(new Stroke(Code.Right, null, 0, Modifiers.Shift, KeyAction.Press));

        await surface.UpdateAsync(() => _ = Router.Route(document, Events.Key, key), "route handled Shift+Right");

        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 0));
    }

    /// <summary>Verifies a synchronous ScrollChanged callback that releases capture prevents the
    /// same timer tick from committing a later endpoint with stale gesture state.</summary>
    [Fact]
    public async Task AutoScroll_WhenScrollChangedReleasesCapture_DoesNotCommitAfterCallbackAsync()
    {
        var document = ScrollableDocument();
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));
        var selectionBefore = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken);
        document.ScrollChanged += (_, _) => surface.Application.Capture.Release();

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "release capture from ScrollChanged");

        surface.ShouldHaveCapture(null);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(selectionBefore);
        var offset = document.VerticalOffset;
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "prove cancelled reentrant timer is inert");
        document.VerticalOffset.ShouldBe(offset);
    }

    /// <summary>Verifies semantic replacement from ScrollChanged cancels the stale anchor rather
    /// than committing it against the replacement projection.</summary>
    [Fact]
    public async Task AutoScroll_WhenScrollChangedReplacesContent_CancelsStaleProjectionAsync()
    {
        var document = ScrollableDocument();
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));
        document.ScrollChanged += (_, _) =>
        {
            document.Blocks.Clear();
            document.Blocks.Add(new DocumentParagraph("x"));
        };

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "replace content from ScrollChanged");

        surface.ShouldHaveCapture(null);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    /// <summary>Verifies moving the same selectable source to another semantic range cancels the
    /// stale anchor even when the complete text, source identity, and source text stay equal.</summary>
    [Fact]
    public async Task AutoScroll_WhenScrollChangedMovesSourceRange_CancelsStaleProjectionAsync()
    {
        var source = new DocumentSelectionSourceProbe();
        var sourceBlock = new DocumentBlockControl(source);
        var equalText = new DocumentParagraph("Probe");
        var document = ScrollableDocument();
        document.Blocks.Insert(0, equalText);
        document.Blocks.Insert(0, sourceBlock);
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        var textBefore = document.SelectionMap.Text;
        var occurrenceBefore = document.SelectionMap.Sources.Single();
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));
        document.ScrollChanged += (_, _) =>
        {
            _ = document.Blocks.Remove(sourceBlock);
            document.Blocks.Insert(1, sourceBlock);
        };

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "move source to another semantic range");

        surface.ShouldHaveCapture(null);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        document.SelectionMap.Text.ShouldBe(textBefore);
        var occurrenceAfter = document.SelectionMap.Sources.Single();
        occurrenceAfter.Source.ShouldBeSameAs(occurrenceBefore.Source);
        occurrenceAfter.Text.ShouldBe(occurrenceBefore.Text);
        occurrenceAfter.Range.ShouldNotBe(occurrenceBefore.Range);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    /// <summary>Verifies hide and disposal from a synchronous scroll callback leave no timer work
    /// capable of touching the unavailable document.</summary>
    [Fact]
    public async Task AutoScroll_WhenScrollChangedMakesDocumentUnavailable_StopsInsideCallbackAsync()
    {
        foreach (var dispose in new[] { false, true })
        {
            var document = ScrollableDocument();
            var time = new ManualTimeProvider();
            await using var surface = await ComponentSurface.MountAsync(
                document,
                new Size(12, 8),
                time,
                TestContext.Current.CancellationToken);
            await BeginEdgeDragAsync(surface, document, new Point(0, 3));
            document.ScrollChanged += (_, _) =>
            {
                if (dispose)
                {
                    document.Dispose();
                }
                else
                {
                    document.Visibility = Visibility.Hidden;
                }
            };

            await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "make document unavailable from ScrollChanged");
            await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "prove unavailable callback timer is inert");

            surface.ShouldHaveCapture(null);
            document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        }
    }

    /// <summary>Verifies modal scope traversal cannot escape to an AutoScroll ancestor outside the
    /// active plane when the document is already saturated.</summary>
    [Fact]
    public async Task AutoScroll_WhenAncestorIsOutsideModalPlane_DoesNotScrollItAsync()
    {
        var document = ScrollableDocument();
        var plane = new Stack
        {
            Height = Length.Cells(3),
            Children = { document }
        };
        var host = new Stack
        {
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children =
            {
                plane,
                new ControlText("tail") { Height = Length.Cells(8) }
            }
        };
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        await surface.UpdateAsync(
            () =>
            {
                document.ScrollToEnd().ShouldBeTrue();
                scope = surface.Application.Modality.Enter(plane, initialFocus: document);
            },
            "enter document modal plane at saturated endpoint");
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "attempt autoscroll across modal boundary");

        host.VerticalOffset.ShouldBe(0);
        await surface.Pointer.ReleaseAsync();
        await surface.UpdateAsync(scope!.Dispose, "leave document modal plane");
    }

    /// <summary>Verifies a removed, hidden, or disabled nested source cannot receive a later timer
    /// tick through stale viewport association.</summary>
    [Fact]
    public async Task AutoScroll_WhenAssociatedSourceBecomesIneligible_DoesNotScrollOrphanAsync()
    {
        foreach (var mode in new[] { "remove", "hide", "disable" })
        {
            var source = new DocumentSelectionSourceProbe { MaximumHorizontalOffset = 8 };
            var block = new DocumentBlockControl(source);
            var document = ScrollableDocument(block);
            var time = new ManualTimeProvider();
            await using var surface = await ComponentSurface.MountAsync(
                document,
                new Size(12, 8),
                time,
                TestContext.Current.CancellationToken);
            await surface.Pointer.MoveToAsync(source, new Point(0, 0));
            await surface.Pointer.PressAsync();
            await surface.Pointer.MovePressedToAsync(source, new Point(1, 0));
            await surface.Pointer.MovePressedToAsync(new Point(source.Bounds.Right, source.Bounds.Y));

            await surface.UpdateAsync(() =>
            {
                if (mode == "remove")
                {
                    _ = document.Blocks.Remove(block);
                }
                else if (mode == "hide")
                {
                    source.Visibility = Visibility.Hidden;
                }
                else
                {
                    source.IsEnabled = false;
                }
            }, $"{mode} associated selectable viewport");
            await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "tick after source eligibility change");

            source.HorizontalOffset.ShouldBe(0, mode);
            await surface.Application.Dispatcher.InvokeAsync(
                surface.Application.Capture.Release,
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies nested edge detection uses the inherited clipped aperture rather than a
    /// source's larger raw local viewport.</summary>
    [Fact]
    public async Task AutoScroll_WhenSourceViewportIsAncestorClipped_UsesClippedEdgeAsync()
    {
        var source = new DocumentSelectionSourceProbe { MaximumHorizontalOffset = 8 };
        var document = new Document
        {
            Width = Length.Cells(3),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Blocks = { new DocumentBlockControl(source) }
        };
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 4),
            time,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(source, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(source, new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(3, source.Bounds.Y));

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "scroll at clipped nested viewport edge");

        source.HorizontalOffset.ShouldBe(1);
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies an edge-held drag waits one complete interval, then advances exactly once
    /// and re-hit-tests the retained edge against the newly exposed row.</summary>
    [Fact]
    public async Task Pointer_WhenHeldOneCellBelowViewport_AutoScrollsAfterFiftyMillisecondsAsync()
    {
        // Arrange
        var document = ScrollableDocument();
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));
        var selectionBefore = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken);

        // Act and assert - a timer never fires early.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(49), "advance before document autoscroll");
        document.VerticalOffset.ShouldBe(0);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(selectionBefore);

        // Act and assert - the first complete period moves and extends through exposed content.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "complete document autoscroll interval");
        document.VerticalOffset.ShouldBe(1);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBeGreaterThan(selectionBefore.Caret);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies edge distance accelerates each deterministic tick and clamps at eight
    /// cells without replaying elapsed periods as extra movement.</summary>
    [Fact]
    public async Task Pointer_WhenHeldFarBelowViewport_ClampsAutoScrollSpeedAtEightAsync()
    {
        // Arrange
        var document = ScrollableDocument();
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 16),
            time,
            TestContext.Current.CancellationToken);
        await BeginEdgeDragAsync(surface, document, new Point(0, 14));

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "first accelerated document autoscroll tick");

        // Assert
        document.VerticalOffset.ShouldBe(8);

        // Act and assert - one further period contributes one further capped step.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "second accelerated document autoscroll tick");
        document.VerticalOffset.ShouldBe(16);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies upward edge scrolling uses the same cell-distance rule and clamps against
    /// the committed top endpoint without unsigned wraparound.</summary>
    [Fact]
    public async Task Pointer_WhenHeldAboveViewport_AutoScrollsTowardTopAsync()
    {
        // Arrange
        var document = ScrollableDocument();
        Overlay.SetTop(document, Length.Cells(2));
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.VerticalOffset = 8, "position document below top");
        await surface.Pointer.MoveToAsync(document, new Point(0, 1));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(document, new Point(1, 1));
        await surface.Pointer.MovePressedToAsync(new Point(1, 1));

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "scroll document upward at edge");

        // Assert
        document.VerticalOffset.ShouldBe(7);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies returning inside the viewport disposes the active timer and later fake-time
    /// advancement cannot move the document through a stale callback.</summary>
    [Fact]
    public async Task Pointer_WhenDragReturnsInsideViewport_StopsAutoScrollImmediatelyAsync()
    {
        // Arrange
        var document = ScrollableDocument();
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));

        // Act
        await surface.Pointer.MovePressedToAsync(document, new Point(1, 1));
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "advance after returning inside document");

        // Assert
        document.VerticalOffset.ShouldBe(0);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a selectable embedded viewport receives edge scrolling before the outer
    /// document, then a saturated inner viewport propagates the same attempt outward.</summary>
    [Fact]
    public async Task Pointer_WhenNestedViewportSaturates_PropagatesAutoScrollToDocumentAsync()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe
        {
            MaximumVerticalOffset = 1
        };
        var document = ScrollableDocument(new DocumentBlockControl(source));
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(source, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(source, new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(source.Bounds.X + 1, source.Bounds.Bottom));

        // Act and assert - the inner viewport moves first.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "scroll nested selectable viewport");
        source.VerticalOffset.ShouldBe(1);
        document.VerticalOffset.ShouldBe(0);

        // Act and assert - its saturated attempt reaches the enclosing document.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "propagate saturated nested autoscroll");
        source.VerticalOffset.ShouldBe(1);
        document.VerticalOffset.ShouldBe(1);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a held pointer above a nested viewport waits one full period, scrolls it
    /// upward with the correct sign, and immediately re-hits the newly exposed source row.</summary>
    [Fact]
    public async Task Pointer_WhenHeldAboveNestedViewport_AutoScrollsItUpwardAndRehitsAsync()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe
        {
            UsesVerticalProjection = true,
            MaximumVerticalOffset = 1,
            VerticalOffset = 1
        };
        var document = ScrollableDocument(new DocumentBlockControl(source));
        Overlay.SetTop(document, Length.Cells(2));
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(source, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(source, new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(source.Bounds.X + 1, source.Bounds.Y - 1));
        var caretBefore = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken);

        // Act and assert - no early movement.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(49), "advance before nested upward autoscroll");
        source.VerticalOffset.ShouldBe(1);

        // Act and assert - the newly exposed first row becomes the endpoint source.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "complete nested upward autoscroll interval");
        source.VerticalOffset.ShouldBe(0);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBeLessThan(caretBefore);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a held pointer left of a nested viewport scrolls it left and re-hits the
    /// newly exposed leading source cell.</summary>
    [Fact]
    public async Task Pointer_WhenHeldLeftOfNestedViewport_AutoScrollsItLeftAndRehitsAsync()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe
        {
            UsesHorizontalProjection = true,
            MaximumHorizontalOffset = 1,
            HorizontalOffset = 1
        };
        var document = ScrollableDocument(new DocumentBlockControl(source));
        Overlay.SetLeft(document, Length.Cells(2));
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(14, 8),
            time,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(source, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(source, new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(source.Bounds.X - 1, source.Bounds.Y));
        var caretBefore = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken);

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "scroll nested viewport left");

        // Assert
        source.HorizontalOffset.ShouldBe(0);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBeLessThan(caretBefore);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies an upward attempt rejected by a saturated nested viewport propagates to
    /// the document and re-hits the earlier logical row.</summary>
    [Fact]
    public async Task Pointer_WhenNestedViewportIsSaturatedAtTop_PropagatesUpwardToDocumentAsync()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe
        {
            UsesVerticalProjection = true,
            MaximumVerticalOffset = 1
        };
        var document = ScrollableDocument();
        document.Blocks.Clear();

        for (var index = 0; index < 4; index++)
        {
            document.Blocks.Add(new DocumentParagraph(FormattableString.Invariant($"before {index}")));
        }

        document.Blocks.Add(new DocumentBlockControl(source));

        for (var index = 0; index < 12; index++)
        {
            document.Blocks.Add(new DocumentParagraph(FormattableString.Invariant($"after {index}")));
        }

        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        var sourceContentRow = source.Bounds.Y - document.Bounds.Y;
        await surface.UpdateAsync(
            () => document.VerticalOffset = Math.Max(1, sourceContentRow - 1),
            "position saturated nested viewport below document top");
        await surface.Pointer.MoveToAsync(source, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(source, new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(source.Bounds.X + 1, source.Bounds.Y - 1));
        var offsetBefore = document.VerticalOffset;
        var caretBefore = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken);

        // Act and assert - the inner source rejects upward movement until the complete period.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(49), "advance before upward fallback");
        document.VerticalOffset.ShouldBe(offsetBefore);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "complete upward fallback interval");

        // Assert
        source.VerticalOffset.ShouldBe(0);
        document.VerticalOffset.ShouldBe(offsetBefore - 1);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBeLessThan(caretBefore);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a leftward attempt rejected by a saturated nested viewport propagates to
    /// an eligible horizontal ancestor and re-hits the newly exposed earlier document cell.</summary>
    [Fact]
    public async Task Pointer_WhenNestedViewportIsSaturatedAtLeft_PropagatesToAncestorAsync()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe
        {
            UsesHorizontalProjection = true,
            MaximumHorizontalOffset = 1
        };
        var document = new Document
        {
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Blocks =
            {
                new DocumentParagraph
                {
                    Inlines =
                    {
                        new DocumentTextRun("abcdef"),
                        new DocumentInlineControl(source)
                    }
                }
            }
        };
        var host = new Stack
        {
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            Children = { document }
        };
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(14, 6),
            time,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => host.HorizontalOffset = 4, "position nested source inside horizontal viewport");
        await surface.Pointer.MoveToAsync(source, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(source, new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(source.Bounds.X - 1, source.Bounds.Y));
        var caretBefore = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken);

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "propagate saturated leftward autoscroll");

        // Assert
        source.HorizontalOffset.ShouldBe(0);
        host.HorizontalOffset.ShouldBe(3);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBeLessThan(caretBefore);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a saturated document forwards its edge attempt to an enclosing intrinsic
    /// AutoScroll container instead of claiming movement at its own endpoint.</summary>
    [Fact]
    public async Task Pointer_WhenDocumentViewportSaturates_PropagatesAutoScrollToAncestorContainerAsync()
    {
        // Arrange
        var document = ScrollableDocument();
        var tail = new ControlText("tail")
        {
            Height = Length.Cells(8)
        };
        var host = new Stack
        {
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { document, tail }
        };
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.ScrollToEnd().ShouldBeTrue(), "saturate document viewport");
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "propagate document edge to ancestor container");

        // Assert
        document.VerticalOffset.ShouldBe(Math.Max(0, document.Extent.Height - document.Viewport.Height));
        host.VerticalOffset.ShouldBe(1);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies an ancestor offset committed before deferred arrangement still re-hits the
    /// pointer against newly exposed document text during that same timer tick.</summary>
    [Fact]
    public async Task Pointer_WhenAncestorAutoScrolls_RehitsNewlyExposedDocumentCellImmediatelyAsync()
    {
        // Arrange
        var document = new Document
        {
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Blocks = { new DocumentParagraph("abcdefghijklmnopqrst") }
        };
        var host = new Stack
        {
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            Children = { document }
        };
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(14, 6),
            time,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(document, new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(10, 0));
        var caretBefore = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken);

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "scroll ancestor before deferred arrangement");

        // Assert
        host.HorizontalOffset.ShouldBe(1);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection.Caret,
            TestContext.Current.CancellationToken)).ShouldBeGreaterThan(caretBefore);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a nested selectable viewport can consume horizontal edge motion even
    /// though the document itself intentionally supports vertical scrolling only.</summary>
    [Fact]
    public async Task Pointer_WhenHeldRightOfNestedViewport_AutoScrollsItHorizontallyAsync()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe
        {
            MaximumHorizontalOffset = 8
        };
        var document = ScrollableDocument(new DocumentBlockControl(source));
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(source, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(source, new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(source.Bounds.Right, source.Bounds.Y));

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "scroll nested viewport horizontally");

        // Assert
        source.HorizontalOffset.ShouldBe(1);
        document.VerticalOffset.ShouldBe(0);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies stopping and re-entering an edge creates one fresh full interval rather
    /// than retaining an old due time or subscribing a second ticking callback.</summary>
    [Fact]
    public async Task Pointer_WhenEdgeAutoScrollIsReentered_StartsOneFreshTimerAsync()
    {
        // Arrange
        var document = ScrollableDocument();
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(25), "partially advance first document timer");
        await surface.Pointer.MovePressedToAsync(document, new Point(1, 1));
        await surface.Pointer.MovePressedToAsync(new Point(0, 3));

        // Act and assert - the cancelled timer's remaining half-period cannot leak into this arm.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(25), "partially advance replacement document timer");
        document.VerticalOffset.ShouldBe(0);

        // Act and assert - exactly one replacement tick fires at its own complete interval.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(25), "complete replacement document timer");
        document.VerticalOffset.ShouldBe(1);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a document already at its lower endpoint remains stable across repeated
    /// timer periods while the held pointer stays beyond that edge.</summary>
    [Fact]
    public async Task Pointer_WhenAutoScrollEdgeIsSaturated_RemainsHarmlessAsync()
    {
        // Arrange
        var document = ScrollableDocument();
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.ScrollToEnd().ShouldBeTrue(), "scroll document to lower endpoint");
        var maximum = document.VerticalOffset;
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(150), "tick saturated document autoscroll");

        // Assert
        document.VerticalOffset.ShouldBe(maximum);

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies an ancestor-handled release closes and disposes an armed edge timer before
    /// the later due time, matching handled gesture cleanup.</summary>
    [Fact]
    public async Task Pointer_WhenAutoScrollReleaseIsHandled_StopsTimerAsync()
    {
        // Arrange
        var document = ScrollableDocument();
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => _ = surface.Application.Root.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs is { Phase: RoutingPhase.Preview, Pointer.Action: PointerAction.Release })
                {
                    eventArgs.IsHandled = true;
                }
            }),
            "handle document selection release at ancestor");
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));

        // Act
        await surface.SendAsync("\u001b[<0;1;4m"u8.ToArray(), "handled release during document autoscroll");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "advance after handled document release");

        // Assert
        document.VerticalOffset.ShouldBe(0);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies an unqualified legacy release uses the same immediate timer disposal path
    /// as an explicit primary release.</summary>
    [Fact]
    public async Task Pointer_WhenAutoScrollReceivesLegacyRelease_StopsTimerAsync()
    {
        // Arrange
        var document = ScrollableDocument();
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 8),
            time,
            TestContext.Current.CancellationToken);
        await BeginEdgeDragAsync(surface, document, new Point(0, 3));

        // Act
        await surface.SendAsync("\u001b[M##!"u8.ToArray(), "legacy release during document autoscroll");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "advance after legacy document release");

        // Assert
        document.VerticalOffset.ShouldBe(0);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies release, capture loss, focus loss, hide, disable, and disposal all suppress
    /// later deterministic timer work rather than mutating unavailable state.</summary>
    [Fact]
    public async Task Pointer_WhenAutoScrollGestureEndsOrBecomesUnavailable_SuppressesFutureTicksAsync()
    {
        foreach (var cancellation in Enum.GetValues<AutoScrollCancellation>())
        {
            var document = ScrollableDocument();
            var time = new ManualTimeProvider();
            await using var surface = await ComponentSurface.MountAsync(
                document,
                new Size(12, 8),
                time,
                TestContext.Current.CancellationToken);
            await BeginEdgeDragAsync(surface, document, new Point(0, 3));

            switch (cancellation)
            {
                case AutoScrollCancellation.Release:
                    await surface.Pointer.ReleaseAsync();
                    break;
                case AutoScrollCancellation.CaptureLoss:
                    await surface.UpdateAsync(surface.Application.Capture.Release, "release document capture");
                    break;
                case AutoScrollCancellation.TerminalFocusLoss:
                    await surface.SendAsync("\u001b[O"u8.ToArray(), "lose terminal focus during autoscroll");
                    break;
                case AutoScrollCancellation.Hide:
                    await surface.UpdateAsync(() => document.Visibility = Visibility.Hidden, "hide autoscrolling document");
                    break;
                case AutoScrollCancellation.Disable:
                    await surface.UpdateAsync(() => document.IsEnabled = false, "disable autoscrolling document");
                    break;
                case AutoScrollCancellation.Dispose:
                    await surface.UpdateAsync(document.Dispose, "dispose autoscrolling document");
                    break;
                case AutoScrollCancellation.Detach:
                    await surface.UpdateAsync(
                        () => ((Overlay) surface.Application.Root).Children.Remove(document),
                        "detach autoscrolling document");
                    break;
                default:
                    throw new InvalidOperationException("Unknown cancellation case.");
            }

            await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "advance after document gesture cancellation");
            document.VerticalOffset.ShouldBe(0, cancellation.ToString());
        }
    }

    /// <summary>Verifies crossing the shared one-cell threshold on an embedded button transfers
    /// capture to the document, selects caption text, and cancels activation.</summary>
    [Fact]
    public async Task Pointer_WhenButtonCaptionDragCrossesThreshold_SelectsWithoutClickingAsync()
    {
        // Arrange
        var button = new Button("Submit");
        var clicks = 0;
        PointerCaptureLossReason? captureLoss = null;
        button.Click += (_, _) => clicks++;
        button.LostPointerCapture += (_, eventArgs) => captureLoss = eventArgs.Reason;
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        var glyphs = document.SelectionMap.Glyphs.Where(glyph => glyph.Source is not null).ToArray();

        // Act
        await surface.Pointer.MoveToAsync(PointAt(glyphs[0]));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(button);
        await surface.Pointer.MovePressedToAsync(PointAt(glyphs[1]));

        // Assert - exactly one cell crosses the shared threshold.
        surface.ShouldHaveCapture(document);
        surface.ShouldHaveFocus(document);
        captureLoss.ShouldBe(PointerCaptureLossReason.Transferred);

        // Act
        await surface.Pointer.ReleaseAsync();

        // Assert
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("S");
        clicks.ShouldBe(0);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies no movement remains an ordinary embedded-button click while collapsing a
    /// stale document selection at the caption hit.</summary>
    [Fact]
    public async Task Pointer_WhenButtonPressStaysBelowThreshold_ClicksAndCollapsesSelectionAsync()
    {
        // Arrange
        var button = new Button("Submit");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(document.SelectAll, "select document");
        var glyph = document.SelectionMap.Glyphs.First(item => item.Source is not null);

        // Act
        await surface.Pointer.MoveToAsync(PointAt(glyph));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(1);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 0));
        surface.ShouldHaveFocus(button);
    }

    /// <summary>Verifies a checkbox drag cancels its pending toggle while a stationary click keeps
    /// the checkbox's ordinary activation behavior.</summary>
    [Fact]
    public async Task Pointer_WhenCheckboxIsDraggedAndThenClicked_OnlyClickTogglesAsync()
    {
        // Arrange
        var checkBox = new CheckBox("Choice");
        var document = new Document { Blocks = { new DocumentBlockControl(checkBox) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        var glyphs = document.SelectionMap.Glyphs.Where(glyph => glyph.Source is not null).ToArray();

        // Act - drag the label by one cell.
        await surface.Pointer.DragAsync(document, PointAt(glyphs[0]), PointAt(glyphs[1]));

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("C");

        // Act - no motion is still a click.
        await surface.Pointer.MoveToAsync(PointAt(glyphs[0]));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        checkBox.IsChecked.ShouldBe(true);
    }

    /// <summary>Verifies ordinary document text can be selected in either direction and preserves
    /// anchor/caret direction.</summary>
    [Fact]
    public async Task Pointer_WhenOrdinaryTextIsDraggedForwardAndBackward_PreservesDirectionAsync()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("abcd") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 2),
            TestContext.Current.CancellationToken);

        // Act and assert - forward.
        await surface.Pointer.DragAsync(document, new Point(0, 0), new Point(3, 0));
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 3));
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("abc");

        // Act and assert - backward.
        await surface.Pointer.DragAsync(document, new Point(3, 0), new Point(0, 0));
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(3, 0));
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("abc");
    }

    /// <summary>Verifies a buttonless legacy X10 release completes an active primary selection
    /// and releases document capture without changing its last dragged endpoint.</summary>
    [Fact]
    public async Task Pointer_WhenLegacyReleaseCompletesSelection_ReleasesCaptureAsync()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("abcd") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(document, new Point(2, 0));
        surface.ShouldHaveCapture(document);

        // Act - X10 selector three is an unqualified release at cell two.
        await surface.SendAsync("\u001b[M##!"u8.ToArray(), "legacy release during selection");

        // Assert
        surface.ShouldHaveCapture(null);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 2));
    }

    /// <summary>Verifies the leading and trailing cells of a wide grapheme resolve to opposite
    /// grapheme boundaries during click placement.</summary>
    [Fact]
    public async Task Pointer_WhenWideGraphemeCellsAreClicked_UsesMidpointBoundariesAsync()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("A界B") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 2),
            TestContext.Current.CancellationToken);

        // Act and assert - leading cell places before the wide grapheme.
        await surface.Pointer.ClickAsync(document, new Point(1, 0));
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(1, 1));

        // Act and assert - trailing cell places after it.
        await surface.Pointer.ClickAsync(document, new Point(2, 0));
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(2, 2));
    }

    /// <summary>Verifies capture loss after a selection drag ends the gesture and cannot revive the
    /// embedded button's cancelled press on the later physical release.</summary>
    [Fact]
    public async Task Pointer_WhenCaptureIsLostDuringButtonDrag_DoesNotActivateOnReleaseAsync()
    {
        // Arrange
        var button = new Button("Submit");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        var glyphs = document.SelectionMap.Glyphs.Where(glyph => glyph.Source is not null).ToArray();
        await surface.Pointer.MoveToAsync(PointAt(glyphs[0]));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(PointAt(glyphs[1]));
        surface.ShouldHaveCapture(document);

        // Act
        await surface.Application.Dispatcher.InvokeAsync(
            surface.Application.Capture.Release,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(0);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("S");
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies terminal focus loss cancels document capture before the routed focus
    /// notification and a later physical release cannot activate the original child.</summary>
    [Fact]
    public async Task Pointer_WhenTerminalFocusIsLostDuringButtonDrag_CancelsWithoutActivationAsync()
    {
        // Arrange
        var button = new Button("Submit");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        var glyphs = document.SelectionMap.Glyphs.Where(glyph => glyph.Source is not null).ToArray();
        await surface.Pointer.MoveToAsync(PointAt(glyphs[0]));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(PointAt(glyphs[1]));
        surface.ShouldHaveCapture(document);

        // Act
        await surface.SendAsync("\u001b[O"u8.ToArray(), "lose terminal focus");
        await surface.Pointer.ReleaseAsync();

        // Assert
        surface.ShouldHaveCapture(null);
        clicks.ShouldBe(0);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("S");
    }

    /// <summary>Verifies becoming unavailable during a captured selection drag clears capture and
    /// cannot complete the embedded child's already-cancelled press.</summary>
    [Fact]
    public async Task Pointer_WhenDocumentIsDisabledDuringButtonDrag_CancelsWithoutActivationAsync()
    {
        // Arrange
        var button = new Button("Submit");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        var glyphs = document.SelectionMap.Glyphs.Where(glyph => glyph.Source is not null).ToArray();
        await surface.Pointer.MoveToAsync(PointAt(glyphs[0]));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(PointAt(glyphs[1]));
        surface.ShouldHaveCapture(document);

        // Act
        await surface.UpdateAsync(() => document.IsEnabled = false, "disable selecting document");
        await surface.Pointer.ReleaseAsync();

        // Assert
        surface.ShouldHaveCapture(null);
        clicks.ShouldBe(0);
    }

    /// <summary>Verifies wheel and a later press remain ordinary routed input while selection
    /// capture is active instead of moving the caret or being consumed as drag motion.</summary>
    [Fact]
    public async Task Pointer_WhenNonDragRecordsArriveDuringSelection_PreservesCaretAndRoutesThemAsync()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("abcd") } };
        var host = new Stack { Children = { document } };
        var wheelRoutes = 0;
        var pressRoutes = 0;
        _ = host.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs.Phase != RoutingPhase.Bubble)
            {
                return;
            }

            if (eventArgs.Pointer.Action == PointerAction.Wheel)
            {
                wheelRoutes++;
            }
            else if (eventArgs.Pointer.Action == PointerAction.Press)
            {
                pressRoutes++;
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(12, 4),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(document, new Point(2, 0));
        surface.ShouldHaveCapture(document);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 2));
        var pressRoutesBefore = pressRoutes;

        // Act - one wheel-down report and one fresh primary press at a different cell.
        await surface.SendAsync("\u001b[<65;4;1M"u8.ToArray(), "wheel during document selection");
        await surface.SendAsync("\u001b[<0;4;1M"u8.ToArray(), "press during document selection");

        // Assert
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 2));
        wheelRoutes.ShouldBe(1);
        pressRoutes.ShouldBe(pressRoutesBefore + 1);
        surface.ShouldHaveCapture(document);

        // Act and assert - the original held gesture can still complete normally.
        await surface.Pointer.ReleaseAsync();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies an ancestor-consumed release closes an active captured gesture without
    /// committing the handled coordinate, retaining capture, or hijacking later pointer input.</summary>
    [Fact]
    public async Task Pointer_WhenAncestorConsumesSelectingRelease_CleansUpWithoutCommittingAsync()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("abcd") } };
        var host = new Stack { Children = { document } };
        var consumeRelease = true;
        _ = host.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (consumeRelease &&
                eventArgs is
                {
                    Phase: RoutingPhase.Preview,
                    Pointer.Action: PointerAction.Release
                })
            {
                consumeRelease = false;
                eventArgs.IsHandled = true;
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(10, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(document, new Point(2, 0));
        surface.ShouldHaveCapture(document);

        // Act - release at a different coordinate without first routing drag motion there.
        await surface.SendAsync("\u001b[<0;4;1m"u8.ToArray(), "handled release during document selection");

        // Assert
        surface.ShouldHaveCapture(null);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 2));

        // Act - a later ordinary click starts a fresh potential gesture and collapses normally.
        await surface.Pointer.LeaveAsync();
        await surface.Pointer.ClickAsync(document, new Point(1, 0));

        // Assert
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(1, 1));
    }

    /// <summary>Verifies an ancestor-consumed release cancels a potential click after the press has
    /// already collapsed stale selection, then leaves the next ordinary click fully eligible.</summary>
    [Fact]
    public async Task Pointer_WhenAncestorConsumesPotentialRelease_PreservesImmediatePressCollapseAsync()
    {
        // Arrange
        var button = new Button("Submit");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        var host = new Stack { Children = { document } };
        var consumeRelease = true;
        _ = host.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (consumeRelease &&
                eventArgs is
                {
                    Phase: RoutingPhase.Preview,
                    Pointer.Action: PointerAction.Release
                })
            {
                consumeRelease = false;
                eventArgs.IsHandled = true;
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(12, 4),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(document.SelectAll, "select before handled release");
        var glyph = document.SelectionMap.Glyphs.First(item => item.Source is not null);

        // Act
        await surface.Pointer.MoveToAsync(PointAt(glyph));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(button);
        await surface.Pointer.ReleaseAsync();

        // Assert
        surface.ShouldHaveCapture(null);
        clicks.ShouldBe(0);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 0));

        // Act and assert - the next unhandled click is not suppressed by stale gesture state.
        await surface.Pointer.MoveToAsync(PointAt(glyph));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(1);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 0));
    }

    /// <summary>Verifies an ancestor-consumed secondary release cannot close or move a captured
    /// primary selection, while the later primary release still completes it.</summary>
    [Fact]
    public async Task Pointer_WhenHandledSecondaryReleaseOccursDuringSelection_WaitsForPrimaryReleaseAsync()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("abcd") } };
        var host = new Stack { Children = { document } };
        var secondaryReleases = 0;
        _ = host.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs is
                {
                    Phase: RoutingPhase.Preview,
                    Pointer.Action: PointerAction.Release,
                    Pointer.Buttons: var buttons
                } &&
                (buttons & Buttons.Secondary) != 0)
            {
                secondaryReleases++;
                eventArgs.IsHandled = true;
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(10, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(document, new Point(2, 0));
        surface.ShouldHaveCapture(document);

        // Act - handled SGR secondary release at a different cell.
        await surface.SendAsync("\u001b[<2;4;1m"u8.ToArray(), "handled secondary release during selection");

        // Assert
        secondaryReleases.ShouldBe(1);
        surface.ShouldHaveCapture(document);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Selecting);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 2));

        // Act and assert
        await surface.Pointer.ReleaseAsync();
        surface.ShouldHaveCapture(null);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 2));
    }

    /// <summary>Verifies the language default uses the theme's selected text over selected control pair.</summary>
    [Fact]
    public void Default_WhenSelectionFaceResolves_UsesSelectedTextOverSelectedControl()
    {
        // Arrange and act
        var face = DocumentStyle.Default.SelectionFace;

        // Assert
        face.Foreground.SemanticColor.ShouldBe(SemanticColor.SelectedText);
        face.Background.SemanticColor.ShouldBe(SemanticColor.SelectedControl);
    }

    /// <summary>Verifies a live selection-face replacement repaints without changing logical selection.</summary>
    [Fact]
    public async Task Style_WhenSelectionFaceChanges_RepaintsSelectedCellsAndPreservesRangeAsync()
    {
        // Arrange
        var document = new Document
        {
            Style = DocumentStyle.Default with
            {
                SelectionFace = SelectionFace(Color.Rgb(0, 0, 90))
            },
            Blocks = { new DocumentParagraph("selected") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(document.SelectAll, "select document text");
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(
            TerminalPalette.Project(Color.Rgb(0, 0, 90), ColorDepth.Basic16));

        // Act
        await surface.UpdateAsync(
            () => document.Style = document.ActualStyle with
            {
                SelectionFace = SelectionFace(Color.Rgb(90, 0, 0))
            },
            "replace selection face");

        // Assert
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(
            TerminalPalette.Project(Color.Rgb(90, 0, 0), ColorDepth.Basic16));
        var selection = await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken);
        selection.ShouldBe(new Selection(0, "selected".Length));
    }

    /// <summary>Verifies scrolling repaints newly visible selected glyphs without styling scrollbar chrome.</summary>
    [Fact]
    public async Task Scroll_WhenSelectionSpansClippedContent_HighlightsOnlyVisibleSemanticGlyphsAsync()
    {
        // Arrange
        var selectionBackground = Color.Rgb(255, 0, 255);
        var document = new Document
        {
            Style = DocumentStyle.Default with
            {
                SelectionFace = SelectionFace(selectionBackground)
            },
            Blocks =
            {
                new DocumentParagraph("one"),
                new DocumentParagraph("two"),
                new DocumentParagraph("three")
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        var scrollbarBefore = surface.Cell(new Point(7, 0)).Style;

        // Act
        await surface.UpdateAsync(document.SelectAll, "select clipped document");
        await surface.UpdateAsync(() => document.VerticalOffset = 2, "scroll selection");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("t");
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(
            TerminalPalette.Project(selectionBackground, ColorDepth.Basic16));
        surface.Cell(new Point(7, 0)).Style.ShouldBe(scrollbarBefore);
    }

    /// <summary>Verifies selection replaces the configured face while preserving hyperlink identity.</summary>
    [Fact]
    public void Render_WhenLinkTextIsSelected_AppliesSelectionFaceAndPreservesHyperlink()
    {
        // Arrange
        var document = new Document
        {
            Style = DocumentStyle.Default with
            {
                SelectionFace = new Face(
                    Color.Rgb(250, 250, 250),
                    Color.Rgb(80, 0, 80),
                    TerminalAttributes.Bold,
                    Underline.Paired,
                    Color.Rgb(0, 220, 220))
            },
            Blocks =
            {
                new DocumentParagraph
                {
                    Inlines = { new DocumentLink("docs", "https://example.test/docs") }
                }
            }
        };
        using var initial = new DocumentRenderProbe(document, new Size(12, 2));
        document.SetSelection(new Selection(1, 3));

        // Act
        using var selected = new DocumentRenderProbe(document, new Size(12, 2));

        // Assert
        selected.Cell(0, 0).Style.ShouldBe(initial.Cell(0, 0).Style);
        selected.Cell(1, 0).Style.Foreground.ShouldBe(Color.Rgb(250, 250, 250));
        selected.Cell(1, 0).Style.Background.ShouldBe(Color.Rgb(80, 0, 80));
        selected.Cell(1, 0).Style.Attributes.ShouldBe(TerminalAttributes.Bold);
        selected.Cell(1, 0).Style.Underline.ShouldBe(Underline.Paired);
        selected.Cell(1, 0).Style.UnderlineColor.ShouldBe(Color.Rgb(0, 220, 220));
        selected.Cell(1, 0).Style.Hyperlink.ShouldBe("https://example.test/docs");
        selected.Cell(3, 0).Style.ShouldBe(initial.Cell(3, 0).Style);
    }

    /// <summary>Verifies descendant caption glyphs are highlighted after the child paints while its
    /// intrinsic border remains untouched.</summary>
    [Fact]
    public void Render_WhenEmbeddedButtonCaptionIsSelected_HighlightsCaptionButNotChrome()
    {
        // Arrange
        var button = new Button("Save")
        {
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        var document = new Document
        {
            Style = DocumentStyle.Default with
            {
                SelectionFace = new Face(
                    Color.Rgb(250, 250, 250),
                    Color.Rgb(0, 0, 90),
                    TerminalAttributes.Bold,
                    Underline.None,
                    Color.Default)
            },
            Blocks = { new DocumentBlockControl(button) }
        };
        using var initial = new DocumentRenderProbe(document, new Size(12, 4));
        var captionGlyph = document.SelectionMap.Glyphs.First(glyph => glyph.Source is not null);
        var borderPoint = new Point(button.Bounds.X, button.Bounds.Y);
        document.SelectAll();

        // Act
        using var selected = new DocumentRenderProbe(document, new Size(12, 4));

        // Assert
        selected.Cell(captionGlyph.Bounds.X, captionGlyph.Bounds.Y).Style.Background.ShouldBe(Color.Rgb(0, 0, 90));
        selected.Cell(borderPoint.X, borderPoint.Y).Style.ShouldBe(initial.Cell(borderPoint.X, borderPoint.Y).Style);
    }

    /// <summary>Verifies selecting a wide grapheme transforms its complete owner, not one half.</summary>
    [Fact]
    public void Render_WhenWideGraphemeIsSelected_StylesEveryOwnedCell()
    {
        // Arrange
        var document = new Document
        {
            Style = DocumentStyle.Default with
            {
                SelectionFace = new Face(
                    Color.Rgb(250, 250, 250),
                    Color.Rgb(0, 0, 90),
                    TerminalAttributes.None,
                    Underline.None,
                    Color.Default)
            },
            Blocks = { new DocumentParagraph("A界B") }
        };
        using var initial = new DocumentRenderProbe(document, new Size(10, 2));
        document.SetSelection(new Selection(1, 2));

        // Act
        using var selected = new DocumentRenderProbe(document, new Size(10, 2));

        // Assert
        selected.Cell(1, 0).Style.Background.ShouldBe(Color.Rgb(0, 0, 90));
        selected.Cell(2, 0).Style.Background.ShouldBe(Color.Rgb(0, 0, 90));
        selected.Cell(0, 0).Style.ShouldBe(initial.Cell(0, 0).Style);
        selected.Cell(3, 0).Style.ShouldBe(initial.Cell(3, 0).Style);
    }

    /// <summary>Verifies a wide owner clipped to one cell is omitted rather than half-painted or highlighted.</summary>
    [Fact]
    public void Render_WhenSelectedWideOwnerIsPartiallyClipped_LeavesTheVisibleCellUntouched()
    {
        // Arrange
        var document = new Document
        {
            Style = DocumentStyle.Default with
            {
                SelectionFace = SelectionFace(Color.Rgb(255, 0, 255))
            },
            Blocks = { new DocumentParagraph("界") }
        };
        using var initial = new DocumentRenderProbe(document, new Size(1, 2));
        document.SelectAll();

        // Act
        using var selected = new DocumentRenderProbe(document, new Size(1, 2));

        // Assert
        selected.Text(0, 0).ShouldBe(" ");
        selected.Cell(0, 0).ShouldBe(initial.Cell(0, 0));
    }

    private static async Task AssertHorizontalCaretRevealAsync(Document document)
    {
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(5, 3),
            TestContext.Current.CancellationToken);
        ScrollChangedEventArgs? horizontalChange = null;
        document.ScrollChanged += (_, eventArgs) =>
        {
            if (eventArgs.Offset.X != eventArgs.PreviousOffset.X)
            {
                horizontalChange = eventArgs;
            }
        };
        await surface.UpdateAsync(
            () =>
            {
                document.SetSelection(new Selection(0, 5));
                document.Focus().ShouldBeTrue();
            },
            "establish clipped document caret");

        await RouteKeyAsync(surface, document, Code.Right, Modifiers.Shift);

        document.Extent.Width.ShouldBeGreaterThan(document.Viewport.Width);
        horizontalChange.ShouldNotBeNull().Offset.X.ShouldBeGreaterThan(0);
        var snapshot = await surface.Application.Dispatcher.InvokeAsync(
            document.GetSelectableTextSnapshot,
            TestContext.Current.CancellationToken);
        var caretGlyph = snapshot.Glyphs.Single(glyph => glyph.Range == new Selection(5, 6));
        surface.Cell(new Point(caretGlyph.Bounds.X, caretGlyph.Bounds.Y)).Text.ShouldBe("f");
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.ScrollSelectableTextViewport(-100, 0),
            TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    private static Face SelectionFace(Color background) => new(
        Color.Rgb(250, 250, 250),
        background,
        TerminalAttributes.Bold,
        Underline.None,
        Color.Default);

    private static Task RouteKeyAsync(
        ComponentSurface surface,
        ControlBase target,
        Code code,
        Modifiers modifiers,
        Rune? character = null)
    {
        var eventArgs = new KeyEventArgs(new Stroke(code, character, 0, modifiers, KeyAction.Press));
        return surface.UpdateAsync(() => _ = Router.Route(target, Events.Key, eventArgs), $"route {modifiers}+{code}");
    }

    private static Task SendControlCharacterAsync(ComponentSurface surface, char character) =>
        surface.SendAsync(
            Encoding.ASCII.GetBytes(FormattableString.Invariant($"\u001b[{(int) character};5u")),
            $"press Control+{character}");

    private static Point PointAt(TextSelectionGlyph glyph) =>
        new(glyph.Bounds.X, glyph.Bounds.Y);

    private static Document ScrollableDocument(DocumentBlock? first = null)
    {
        var document = new Document
        {
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        if (first is not null)
        {
            document.Blocks.Add(first);
        }

        for (var index = 0; index < 24; index++)
        {
            document.Blocks.Add(new DocumentParagraph(FormattableString.Invariant($"line {index:D2}")));
        }

        return document;
    }

    private static async Task BeginEdgeDragAsync(ComponentSurface surface, Document document, Point outside)
    {
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(document, new Point(1, 0));
        surface.ShouldHaveCapture(document);
        await surface.Pointer.MovePressedToAsync(outside);
    }
}
