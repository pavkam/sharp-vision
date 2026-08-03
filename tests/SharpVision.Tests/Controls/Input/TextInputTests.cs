// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Input;

using TerminalText = Terminal.Input.Text;

/// <summary>Verifies TextInput validation, editing, events, input, rendering, and history.</summary>
public sealed class TextInputTests
{
    /// <summary>Verifies a text field is discoverable through light intrinsic chrome by default.</summary>
    [ComponentUnitEvidence(typeof(TextInput))]
    [Fact]
    public void Properties_WhenConstructed_UsesLightFieldBorder()
    {
        // Arrange
        var control = new TextInput();

        // Act

        // Assert
        control.ActualBorder.Sides.ShouldBe(BorderSide.All);
        control.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    }

    /// <summary>Verifies conservative defaults and every direct assignment validates before mutation.</summary>
    [Fact]
    public void Properties_WhenAssignmentsAreInvalid_PreservePreviousState()
    {
        var control = new TextInput();

        control.Text.ShouldBeEmpty();
        control.CaretIndex.ShouldBe(0);
        control.SelectionStart.ShouldBe(0);
        control.SelectionLength.ShouldBe(0);
        control.MaxLength.ShouldBe(0);
        control.IsReadOnly.ShouldBeFalse();
        control.AcceptsReturn.ShouldBeFalse();
        control.AcceptsTab.ShouldBeFalse();
        control.CanFocus.ShouldBeTrue();

        _ = Should.Throw<ArgumentNullException>(() => control.Text = null!);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.MaxLength = -1);
        control.Text = "Ae\u0301Z";
        _ = Should.Throw<ArgumentException>(() => control.CaretIndex = 2);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.SelectionStart = 20);
        _ = Should.Throw<ArgumentException>(() => control.PasswordCharacter = new Rune('\n'));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.CursorShape = (CursorShape) 99);

        control.Text.ShouldBe("Ae\u0301Z");
        control.CaretIndex.ShouldBe(control.Text.Length);
        control.PasswordCharacter.ShouldBeNull();
        control.CursorShape.ShouldBe(CursorShape.Block);
    }

    /// <summary>Verifies cursor-shape mutation requests only a new semantic render.</summary>
    [Fact]
    public void CursorShape_WhenChanged_InvalidatesRenderOnly()
    {
        var control = new TextInput();
        control.Measure(new Constraint(4, 3));
        control.Arrange(new Rect(0, 0, 4, 3));
        using Frame frame = new(new Size(4, 3));
        control.Render(frame.Canvas);

        control.CursorShape = CursorShape.Underline;

        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies cancellable proposal precedes one atomic committed notification sequence.</summary>
    [Fact]
    public void Text_WhenChangingIsCancelled_PreservesStateAndEventOrder()
    {
        var control = new TextInput { Text = "A" };
        List<string> order = [];
        control.TextChanging += (_, eventArgs) =>
        {
            order.Add($"changing:{control.Text}:{eventArgs.Proposal.Text}");
            eventArgs.Cancel = eventArgs.Proposal.Text == "blocked";
        };
        control.TextChanged += (_, eventArgs) =>
            order.Add($"text:{eventArgs.PreviousText}>{eventArgs.Text}:{control.CaretIndex}");
        control.SelectionChanged += (_, eventArgs) =>
            order.Add($"selection:{eventArgs.Previous.Caret}>{eventArgs.Selection.Caret}");

        control.Text = "blocked";
        control.Text.ShouldBe("A");
        control.Text = "界";

        order.ShouldBe([
            "changing:A:blocked",
            "changing:A:界",
            "text:A>界:1"
        ]);
    }

    /// <summary>Verifies typed text and owned paste share policy and grapheme maximum handling.</summary>
    [Fact]
    public void Dispatch_WhenTextAndPasteArrive_AppliesPolicyAndMaximum()
    {
        var control = new TextInput { MaxLength = 3 };

        Route(control, new TextEventArgs(new TerminalText(new Rune('界'))), Events.Text);
        Route(control, new PasteEventArgs(new Paste("e\u0301👩‍💻Z"u8)), Events.Paste);

        control.Text.ShouldBe("界e\u0301👩‍💻");
        Edit.GraphemeCount(control.Text).ShouldBe(3);
        Route(control, new TextEventArgs(new TerminalText(new Rune('\n'))), Events.Text);
        control.Text.ShouldBe("界e\u0301👩‍💻");

        control.AcceptsReturn = true;
        control.MaxLength = 0;
        Route(control, new TextEventArgs(new TerminalText(new Rune('\n'))), Events.Text);
        control.Text.ShouldEndWith("\n");
    }

    /// <summary>Verifies keys outside the editor command set remain available to routed input.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsUnhandled_RaisesInheritedKeyDownWithoutConsumingIt()
    {
        // Arrange
        var control = new TextInput();
        var raised = 0;
        control.KeyDown += (_, _) => raised++;
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.F1,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        // Act
        Route(control, eventArgs, Events.Key);

        // Assert
        eventArgs.Handled.ShouldBeFalse();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies navigation, extension, word movement, and deletion use grapheme boundaries.</summary>
    [Fact]
    public void Dispatch_WhenEditingKeysArrive_UsesDirectionalGraphemeSelection()
    {
        var control = new TextInput { Text = "one e\u0301👩‍💻" };

        Key(control, Code.Left, Modifiers.Shift);
        Key(control, Code.Left, Modifiers.Shift);
        control.SelectionLength.ShouldBe(7);
        Key(control, Code.Left, Modifiers.None);
        control.SelectionLength.ShouldBe(0);
        control.CaretIndex.ShouldBe(4);
        Key(control, Code.Right, Modifiers.Control);
        control.CaretIndex.ShouldBe(control.Text.Length);
        Key(control, Code.Backspace, Modifiers.None);

        control.Text.ShouldBe("one e\u0301");
        Edit.IsBoundary(control.Text, control.CaretIndex).ShouldBeTrue();
    }

    /// <summary>Verifies Left visits every grapheme boundary exactly once across mixed ASCII,
    /// combining-mark, and emoji ZWJ graphemes, proving the cached binary-search fast path in
    /// MoveCaretPrevious matches Edit's own boundary ground truth exactly (see #42).</summary>
    [Fact]
    public void Dispatch_WhenHoldingLeftAcrossMixedGraphemeKinds_VisitsEveryGraphemeBoundaryExactlyOnce()
    {
        var text = "aé👩‍💻界ébb";
        var control = new TextInput { Text = text };
        Key(control, Code.End, Modifiers.None);
        var steps = 0;

        while (control.CaretIndex > 0)
        {
            var before = control.CaretIndex;
            Key(control, Code.Left, Modifiers.None);
            control.CaretIndex.ShouldBeLessThan(before);
            Edit.IsBoundary(text, control.CaretIndex).ShouldBeTrue();
            steps++;
        }

        steps.ShouldBe(Edit.GraphemeCount(text));
    }

    /// <summary>Verifies Ctrl+Left's cached fast path (MoveCaretPreviousWord) lands on exactly the
    /// same caret index as Edit.MovePreviousWord's own ground truth at every step across mixed
    /// word/whitespace/punctuation/emoji graphemes (see #42).</summary>
    [Fact]
    public void Dispatch_WhenHoldingControlLeftAcrossMixedGraphemeKinds_MatchesEditMovePreviousWordAtEveryStep()
    {
        var text = "one  two_3 👩‍💻! four";
        var control = new TextInput { Text = text };
        Key(control, Code.End, Modifiers.None);
        var expected = new Selection(text.Length, text.Length);

        while (expected.Caret > 0)
        {
            expected = Edit.MovePreviousWord(text, expected, extend: false).Selection;
            Key(control, Code.Left, Modifiers.Control);
            control.CaretIndex.ShouldBe(expected.Caret);
        }
    }

    /// <summary>Verifies the boundary cache backing Left rebuilds for a completely replaced Text
    /// value instead of serving offsets computed for the previous string instance - a stale cache
    /// from a combining-mark grapheme would skip a boundary once the text becomes plain ASCII of
    /// the same UTF-16 length (see #42).</summary>
    [Fact]
    public void Dispatch_WhenTextIsReplacedAfterCachingBoundaries_RebuildsForTheNewGraphemeStructure()
    {
        var control = new TextInput { Text = "ébb" };
        Key(control, Code.End, Modifiers.None);
        Key(control, Code.Left, Modifiers.None);

        control.Text = "aaaa";
        Key(control, Code.End, Modifiers.None);

        Key(control, Code.Left, Modifiers.None);
        control.CaretIndex.ShouldBe(3);
        Key(control, Code.Left, Modifiers.None);
        control.CaretIndex.ShouldBe(2);
        Key(control, Code.Left, Modifiers.None);
        control.CaretIndex.ShouldBe(1);
        Key(control, Code.Left, Modifiers.None);
        control.CaretIndex.ShouldBe(0);
    }

    /// <summary>Verifies bounded undo and redo retain immutable text and selection snapshots.</summary>
    [Fact]
    public void Undo_WhenHistoryExists_RestoresTextSelectionAndRedo()
    {
        var control = new TextInput { UndoLimit = 2, Text = "A" };
        control.Text = "AB";
        control.Text = "ABC";

        control.CanUndo.ShouldBeTrue();
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("AB");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("A");
        control.Undo().ShouldBeFalse();
        control.Redo().ShouldBeTrue();
        control.Text.ShouldBe("AB");
    }

    /// <summary>Verifies read-only suppresses mutation while single-line Enter submits committed text.</summary>
    [Fact]
    public void Dispatch_WhenReadOnlyOrSubmitted_UsesDocumentedBehavior()
    {
        var control = new TextInput { Text = "value", IsReadOnly = true };
        SubmittedEventArgs? submitted = null;
        control.Submitted += (_, eventArgs) => submitted = eventArgs;

        Route(control, new TextEventArgs(new TerminalText(new Rune('X'))), Events.Text);
        Key(control, Code.Backspace, Modifiers.None);
        Key(control, Code.Enter, Modifiers.None);

        control.Text.ShouldBe("value");
        _ = submitted.ShouldNotBeNull();
        submitted.Text.ShouldBe("value");
    }

    /// <summary>Verifies password rendering masks every cluster and focused caret reaches the frame.</summary>
    [Fact]
    public void Render_WhenPasswordIsFocused_MasksSourceAndSetsVisibleCursor()
    {
        var control = new TextInput
        {
            Text = "Ae\u0301👩‍💻",
            PasswordCharacter = new Rune('*'),
            Width = Length.Cells(6)
        };
        control.SetTheme(TestThemes.BorderlessInput);
        control.SetFocused(true);
        new LayoutEngine().Layout(control, new Size(6, 1));
        using Frame frame = new(new Size(6, 1));

        control.Render(frame.Canvas);

        Cells(frame, 3).ShouldBe("***");
        frame.Cursor.Visible.ShouldBeTrue();
        frame.Cursor.Position.ShouldBe(new Point(3, 0));
        Encoding.UTF8.GetString(CopyOccupied(frame)).ShouldNotContain("A");
    }

    /// <summary>Verifies a focused editor clipped above the viewport never requests an off-frame cursor.</summary>
    [Fact]
    public void Render_WhenFocusedCaretIsOutsideCanvas_LeavesCursorHidden()
    {
        var control = new TextInput { Bounds = new Rect(0, -1, 12, 1), Text = "Scrolled out" };
        control.SetFocused(true);
        using Frame frame = new(new Size(12, 2));

        Should.NotThrow(() => control.Render(frame.Canvas));

        frame.Cursor.Visible.ShouldBeFalse();
    }

    /// <summary>Verifies selected cells render reversed without splitting a wide grapheme.</summary>
    [Fact]
    public void Render_WhenSelectionContainsWideRune_StylesCompleteOwnedCells()
    {
        var control = new TextInput { Text = "A界Z" };
        control.SetTheme(TestThemes.BorderlessInput);
        control.Select(start: 1, length: 1);
        new LayoutEngine().Layout(control, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        control.Render(frame.Canvas);

        (frame.GetCell(new Point(1, 0)).Style.Attributes & Attributes.Reverse)
            .ShouldBe(Attributes.Reverse);
        (frame.GetCell(new Point(2, 0)).Style.Attributes & Attributes.Reverse)
            .ShouldBe(Attributes.Reverse);
        frame.GetCell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies default intrinsic chrome reserves editor text and caret exactly once.</summary>
    [Fact]
    public void Render_WhenConstructed_InsetsEditorAndCaretInsideHeavyFrame()
    {
        var control = new TextInput { Width = Length.Cells(6), Height = Length.Cells(3), Text = "A" };
        control.SetFocused(true);
        new LayoutEngine().Layout(control, new Size(6, 3));
        using Frame frame = new(new Size(6, 3));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("┏");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("┓");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("┗");
        FrameOracle.Get(frame, new Point(5, 2)).ShouldBe("┛");
        frame.Cursor.Visible.ShouldBeTrue();
        frame.Cursor.Position.ShouldBe(new Point(2, 1));
    }

    /// <summary>Verifies a TextInput framework rail remains inside its intrinsic border.</summary>
    [Fact]
    public void ScrollBars_WhenBorderIsSet_ContainsRailInsideBorder()
    {
        var control = new TextInput
        {
            Width = Length.Cells(8),
            Height = Length.Cells(5),
            AcceptsReturn = true,
            Text = "one\ntwo\nthree\nfour",
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always
        };

        new LayoutEngine().Layout(control, new Size(8, 5));

        control.HitTest(new Point(6, 1)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Vertical);
        control.HitTest(new Point(7, 1)).ShouldBeSameAs(control);
    }

    /// <summary>Verifies pointer press and inferred-pixel drag focus, capture, and select boundaries.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerDrags_SelectsByRenderedCellsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new TextInput { Bounds = new Rect(0, 0, 8, 1), Text = "A界e\u0301Z" };
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            using PointerManager capture = new(control);

            _ = capture.Dispatch(Pointer(new Point(0, 0), PointerAction.Press, new Point(5, 5)));
            _ = capture.Dispatch(Pointer(new Point(4, 0), PointerAction.Move, new Point(45, 5)));
            _ = capture.Dispatch(Pointer(new Point(4, 0), PointerAction.Release, new Point(45, 5)));

            focus.Focused.ShouldBeSameAs(control);
            capture.Captured.ShouldBeNull();
            control.SelectionStart.ShouldBe(0);
            control.SelectionLength.ShouldBe(4);
            Edit.IsBoundary(control.Text, control.CaretIndex).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a primary double-click selects the complete Unicode word beneath the pointer.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerDoubleClicksWord_SelectsCompleteWordAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new TextInput { Bounds = new Rect(0, 0, 20, 1), Text = "alpha cafe\u0301 beta" };
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            var clock = new ManualTimeProvider();
            using PointerManager capture = new(control, clock);
            var point = new Point(8, 0);

            _ = capture.Dispatch(Pointer(point, PointerAction.Press, new Point(85, 5)));
            _ = capture.Dispatch(Pointer(point, PointerAction.Release, new Point(85, 5)));
            clock.Advance(TimeSpan.FromMilliseconds(200));
            _ = capture.Dispatch(Pointer(point, PointerAction.Press, new Point(85, 5)));
            _ = capture.Dispatch(Pointer(point, PointerAction.Release, new Point(85, 5)));

            focus.Focused.ShouldBeSameAs(control);
            control.SelectedText.ShouldBe("cafe\u0301");
            control.SelectionStart.ShouldBe(6);
            control.SelectionLength.ShouldBe(5);
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies caret visibility scrolls correctly from selection-only key navigation with no
    /// intervening layout pass, so the content-width cache Commit reuses instead of remeasuring
    /// (see #42) stays correct across repeated moves.</summary>
    [Fact]
    public void Dispatch_WhenArrowKeyMovesCaretPastViewportWithoutRelayout_ScrollsUsingCachedWidth()
    {
        var control = new TextInput { Text = "abcdefghij", CaretIndex = 0 };
        control.SetTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(control, new Size(4, 1));

        control.HorizontalOffset.ShouldBe(0);

        for (var index = 0; index < 6; index++)
        {
            Key(control, Code.Right, Modifiers.None);
        }

        control.CaretIndex.ShouldBe(6);
        control.HorizontalOffset.ShouldBeGreaterThan(0);

        for (var index = 0; index < 6; index++)
        {
            Key(control, Code.Left, Modifiers.None);
        }

        control.CaretIndex.ShouldBe(0);
        control.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies caret visibility updates horizontal and vertical offsets after resize.</summary>
    [Fact]
    public void Arrange_WhenCaretExceedsViewport_ScrollsAndClampsAfterResize()
    {
        var control = new TextInput
        {
            AcceptsReturn = true,
            Text = "123456\nabcdef\nXYZ",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        control.SetTheme(TestThemes.BorderlessInput);
        var engine = new LayoutEngine();

        engine.Layout(control, new Size(3, 2));
        control.HorizontalOffset.ShouldBe(2);
        control.VerticalOffset.ShouldBe(2);

        control.CaretIndex = 6;
        engine.Layout(control, new Size(3, 2));
        control.HorizontalOffset.ShouldBe(5);
        control.VerticalOffset.ShouldBe(0);
        engine.Layout(control, new Size(10, 5));
        control.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies a wheel scroll moves the editor while movement remains and bubbles at its endpoint.</summary>
    [Fact]
    public void Dispatch_WhenWheelTargetsOverflowingEditor_ScrollsAndBubblesAtEndpoint()
    {
        var control = new TextInput
        {
            AcceptsReturn = true,
            Text = "abcdef\none\ntwo\nthree",
            CaretIndex = 0
        };
        control.SetTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(control, new Size(4, 2));

        var first = Wheel(wheelX: 1, wheelY: -1);
        Route(control, first, Events.Pointer);

        control.HorizontalOffset.ShouldBe(1);
        control.VerticalOffset.ShouldBe(1);
        first.Handled.ShouldBeTrue();

        Route(control, Wheel(wheelX: 100, wheelY: -100), Events.Pointer);
        control.HorizontalOffset.ShouldBe(4);
        control.VerticalOffset.ShouldBe(4);

        var endpoint = Wheel(wheelX: 1, wheelY: -1);
        Route(control, endpoint, Events.Pointer);

        endpoint.Handled.ShouldBeFalse();
    }

    /// <summary>Verifies overflowing multiline input exposes a configured canonical vertical scrollbar.</summary>
    [Fact]
    public void ScrollBars_WhenMultilineContentOverflows_ExposesCanonicalVerticalRail()
    {
        var control = new TextInput
        {
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            AcceptsReturn = true,
            Text = "one\ntwo\nthree\nfour\nfive",
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = ScrollBarStyle.ThinLine
        };
        control.PropagateTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(control, new Size(8, 3));

        var rail = control.HitTest(new Point(7, 0)).ShouldBeOfType<ScrollBar>();
        rail.Orientation.ShouldBe(Orientation.Vertical);
        rail.ActualStyle.ShouldBe(ScrollBarStyle.ThinLine);
    }

    /// <summary>Verifies an editor at its wheel endpoint leaves the routed delta for its enclosing viewport.</summary>
    [Fact]
    public void Dispatch_WhenEditorWheelReachesEndpoint_OffersNextDeltaToEnclosingViewport()
    {
        var input = new TextInput
        {
            Width = Length.Cells(5),
            Height = Length.Cells(2),
            AcceptsReturn = true,
            Text = "one\ntwo\nthree\nfour",
            CaretIndex = 0
        };
        var content = new Stack();
        content.Children.Add(input);
        content.Children.Add(new ProbeControl(new Size(5, 8)));
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { content }
        };
        input.SetTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(outer, new Size(5, 3));

        Route(input, Wheel(wheelX: 0, wheelY: -100), Events.Pointer);
        input.VerticalOffset.ShouldBe(4);
        outer.VerticalOffset.ShouldBe(0);

        var endpoint = Wheel(wheelX: 0, wheelY: -1);
        Route(input, endpoint, Events.Pointer);

        outer.VerticalOffset.ShouldBe(1);
        endpoint.Handled.ShouldBeTrue();
    }

    /// <summary>Verifies a notification exception preserves the committed atomic state.</summary>
    [Fact]
    public void Text_WhenChangedHandlerThrows_PreservesCommittedStateAndFutureEdits()
    {
        var control = new TextInput();
        var failure = new InvalidOperationException("observer");

        void Handler(object? sender, TextChangedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            throw failure;
        }

        control.TextChanged += Handler;

        Should.Throw<InvalidOperationException>(() => control.Text = "A").ShouldBeSameAs(failure);
        control.Text.ShouldBe("A");
        control.CaretIndex.ShouldBe(1);
        control.TextChanged -= Handler;
        control.Text = "B";
        control.Text.ShouldBe("B");
    }

    /// <summary>Verifies typed-input observers cannot be mistaken for rejected edit policy.</summary>
    [Fact]
    public void Dispatch_WhenObserverThrowsArgumentException_PropagatesAfterCommit()
    {
        var control = new TextInput();
        var failure = new ArgumentException("observer");
        control.TextChanged += (_, _) => throw failure;

        Should.Throw<ArgumentException>(() =>
                Route(control, new TextEventArgs(new TerminalText(new Rune('A'))), Events.Text))
            .ShouldBeSameAs(failure);

        control.Text.ShouldBe("A");
        control.CaretIndex.ShouldBe(1);
    }

    /// <summary>Verifies standard select-all, undo, and redo shortcuts use immutable snapshots.</summary>
    [Fact]
    public void Dispatch_WhenControlShortcutsArrive_SelectsAndRestoresHistory()
    {
        var control = new TextInput { Text = "A" };
        control.Text = "AB";

        CharacterKey(control, new Rune('a'), Modifiers.Control);
        control.SelectionStart.ShouldBe(0);
        control.SelectionLength.ShouldBe(2);
        CharacterKey(control, new Rune('z'), Modifiers.Control);
        control.Text.ShouldBe("A");
        CharacterKey(control, new Rune('y'), Modifiers.Control);
        control.Text.ShouldBe("AB");
    }

    /// <summary>Verifies copy/cut ownership, read-only behavior, and password secrecy defaults.</summary>
    [Fact]
    public void CutSelection_WhenSelectionExists_ReturnsOwnedTextAndHonorsSecurityPolicy()
    {
        var control = new TextInput { Text = "A界Z" };
        control.Select(1, 1);

        control.CopySelection().ShouldBe("界");
        control.CutSelection().ShouldBe("界");
        control.Text.ShouldBe("AZ");

        control.Text = "secret";
        control.Select(0, control.Text.Length);
        control.IsReadOnly = true;
        control.CutSelection().ShouldBe("secret");
        control.Text.ShouldBe("secret");
        control.PasswordCharacter = new Rune('*');
        control.CopySelection().ShouldBeEmpty();
        control.CutSelection().ShouldBeEmpty();
        control.Text.ShouldBe("secret");
    }

    /// <summary>Verifies a consumer can replace a selection through the public primitive and reach
    /// the same undo/redo history as ordinary keyboard-driven edits (see #68).</summary>
    [Fact]
    public void ReplaceSelection_WhenSelectionExists_ReplacesAndParticipatesInUndoHistory()
    {
        var control = new TextInput { Text = "Hello World" };
        control.Select(6, 5);

        control.ReplaceSelection("there").ShouldBeTrue();

        control.Text.ShouldBe("Hello there");
        control.CaretIndex.ShouldBe("Hello there".Length);
        control.SelectionLength.ShouldBe(0);
        control.CanUndo.ShouldBeTrue();
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("Hello World");
    }

    /// <summary>Verifies replacement inserts at the caret when there is no selection, and that
    /// grapheme-unsafe Unicode (combining marks, ZWJ emoji sequences) commits as complete clusters
    /// rather than splitting them.</summary>
    [Fact]
    public void ReplaceSelection_WhenNoSelection_InsertsCompleteGraphemeClustersAtCaret()
    {
        var control = new TextInput { Text = "AZ", CaretIndex = 1 };

        control.ReplaceSelection("é👩‍💻").ShouldBeTrue();

        control.Text.ShouldBe("Aé👩‍💻Z");
        Edit.IsBoundary(control.Text, control.CaretIndex).ShouldBeTrue();
    }

    /// <summary>Verifies a read-only control declines the edit and reports no commit.</summary>
    [Fact]
    public void ReplaceSelection_WhenReadOnly_DeclinesAndPreservesText()
    {
        var control = new TextInput { Text = "value", IsReadOnly = true };
        control.Select(0, control.Text.Length);

        control.ReplaceSelection("changed").ShouldBeFalse();

        control.Text.ShouldBe("value");
    }

    /// <summary>Verifies a TextChanging cancellation declines the edit exactly as it does for
    /// keyboard-driven edits, preserving the prior text and reporting no commit.</summary>
    [Fact]
    public void ReplaceSelection_WhenTextChangingCancels_DeclinesAndPreservesText()
    {
        var control = new TextInput { Text = "A" };
        control.TextChanging += (_, eventArgs) => eventArgs.Cancel = true;

        control.ReplaceSelection("B").ShouldBeFalse();

        control.Text.ShouldBe("A");
    }

    /// <summary>Verifies content that would exceed MaxLength after retaining the untouched prefix
    /// and suffix declines the edit rather than silently truncating.</summary>
    [Fact]
    public void ReplaceSelection_WhenRetainedTextAlreadyMeetsMaxLength_Declines()
    {
        var control = new TextInput { Text = "ABC", MaxLength = 3, CaretIndex = 3 };

        control.ReplaceSelection("D").ShouldBeFalse();

        control.Text.ShouldBe("ABC");
    }

    /// <summary>Verifies a multiline control accepts an embedded line break through the same
    /// primitive used for AcceptsReturn-gated Enter handling.</summary>
    [Fact]
    public void ReplaceSelection_WhenMultilineAcceptsReturn_InsertsEmbeddedLineBreak()
    {
        var control = new TextInput { Text = "AB", AcceptsReturn = true, CaretIndex = 1 };

        control.ReplaceSelection("\n").ShouldBeTrue();

        control.Text.ShouldBe("A\nB");
    }

    /// <summary>Verifies password masking does not block editing through the public primitive —
    /// only CopySelection/CutSelection source disclosure is suppressed.</summary>
    [Fact]
    public void ReplaceSelection_WhenPasswordMasked_StillCommitsTheEdit()
    {
        var control = new TextInput { Text = "secret", PasswordCharacter = new Rune('*') };
        control.Select(0, control.Text.Length);

        control.ReplaceSelection("hunter2").ShouldBeTrue();

        control.Text.ShouldBe("hunter2");
    }

    /// <summary>Verifies a mounted, focused control reflects a programmatic replacement in its
    /// rendered content exactly as a keyboard-driven edit would.</summary>
    [Fact]
    public void ReplaceSelection_WhenMounted_RendersReplacedContent()
    {
        var control = new TextInput { Text = "Hello", Width = Length.Cells(8) };
        control.SetTheme(TestThemes.BorderlessInput);
        control.SetFocused(true);
        control.Select(0, control.Text.Length);
        new LayoutEngine().Layout(control, new Size(8, 1));
        using Frame frame = new(new Size(8, 1));

        control.ReplaceSelection("Bye").ShouldBeTrue();
        control.Render(frame.Canvas);

        Cells(frame, 3).ShouldBe("Bye");
    }

    /// <summary>Verifies vertical navigation maps the current rendered column to an adjacent line.</summary>
    [Fact]
    public void Dispatch_WhenUpArrives_MovesToNearestBoundaryOnPreviousLine()
    {
        var control = new TextInput { AcceptsReturn = true, Text = "abc\n12345" };

        Key(control, Code.Up, Modifiers.None);

        control.CaretIndex.ShouldBe(3);
    }

    /// <summary>Verifies Up and Down snap to the nearest cell column across rows mixing single-cell
    /// ASCII and a two-cell CJK grapheme, proving the cached row-lookup fast path
    /// (<c>IndexAtRowFast</c>, backing <c>MoveVertical</c>) matches the exact boundary and half-cell
    /// snap the prior full-document-per-row scan produced (see #42).</summary>
    [Fact]
    public void Dispatch_WhenHoldingUpAndDownAcrossMixedGraphemeWidths_SnapsToTheExactExpectedBoundary()
    {
        // Row 0 "ab" (columns 0-2) ends at offset 2.
        // Row 1 "界c" (界: two cells at column 0-2; c: one cell at column 2-3) spans offsets 3-5.
        // Row 2 "xy" (columns 0-2) spans offsets 6-8, the document end.
        var control = new TextInput { AcceptsReturn = true, Text = "ab\n界c\nxy" };

        // End of row 0 (column 2) descends to the exact matching column on row 1, past 界.
        control.Select(2, 0);
        Key(control, Code.Down, Modifiers.None);
        control.CaretIndex.ShouldBe(4);

        // The same column descends again to the document end on row 2.
        Key(control, Code.Down, Modifiers.None);
        control.CaretIndex.ShouldBe(8);

        // Ascending retraces the identical boundaries back to row 0.
        Key(control, Code.Up, Modifiers.None);
        control.CaretIndex.ShouldBe(4);
        Key(control, Code.Up, Modifiers.None);
        control.CaretIndex.ShouldBe(2);

        // Column 1 on row 0 (after 'a', mid-cluster of the two-cell 界 on row 1) snaps to 界's far
        // half-cell boundary, matching the original scan's exact midpoint tie-break rule.
        control.Select(1, 0);
        Key(control, Code.Down, Modifiers.None);
        control.CaretIndex.ShouldBe(4);
    }

    /// <summary>Verifies losing focus during pointer selection releases capture and held state.</summary>
    [Fact]
    public async Task Dispatch_WhenFocusLeavesDuringPointerDrag_CancelsCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 2) };
            var control = new TextInput { Bounds = new Rect(0, 0, 8, 1), Text = "select" };
            var other = new ProbeControl { Bounds = new Rect(10, 0, 2, 1), Focusable = true };
            root.Children.Add(control);
            root.Children.Add(other);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager capture = new(root);

            _ = capture.Dispatch(Pointer(new Point(0, 0), PointerAction.Press, new Point(5, 5)));
            capture.Captured.ShouldBeSameAs(control);
            focus.Focus(other).ShouldBeTrue();

            capture.Captured.ShouldBeNull();
            control.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    private static void Key(TextInput control, Code code, Modifiers modifiers) =>
        Route(
            control,
            new KeyEventArgs(new Stroke(
                code,
                character: null,
                nativeCode: 0,
                modifiers,
                KeyAction.Press)),
            Events.Key);

    private static void CharacterKey(TextInput control, Rune character, Modifiers modifiers) =>
        Route(
            control,
            new KeyEventArgs(new Stroke(
                Code.Character,
                character,
                nativeCode: 0,
                modifiers,
                KeyAction.Press)),
            Events.Key);

    private static void Route<T>(TextInput control, T eventArgs, Event<T> routedEvent)
        where T : RoutedEventArgs => Router.Route(control, routedEvent, eventArgs);

    private static Pointer Pointer(Point cells, PointerAction action, Point pixels) => new(
        cells,
        pixels,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: true);

    private static PointerEventArgs Wheel(int wheelX, int wheelY) => new(new Pointer(
        cells: default,
        pixels: null,
        Buttons.None,
        PointerAction.Wheel,
        wheelX,
        wheelY,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false));

    private static string Cells(Frame frame, int count)
    {
        var result = new StringBuilder(count);

        for (var x = 0; x < count; x++)
        {
            _ = result.Append(FrameOracle.Get(frame, new Point(x, 0)));
        }

        return result.ToString();
    }

    private static byte[] CopyOccupied(Frame frame)
    {
        List<byte> result = [];

        for (var x = 0; x < frame.Size.Width; x++)
        {
            var point = new Point(x, 0);

            if (frame.GetCell(point).IsContinuation)
            {
                continue;
            }

            var length = frame.GetGraphemeByteCount(point);

            if (length == 0)
            {
                continue;
            }

            var bytes = new byte[length];
            _ = frame.CopyGrapheme(point, bytes);
            result.AddRange(bytes);
        }

        return [.. result];
    }
}
