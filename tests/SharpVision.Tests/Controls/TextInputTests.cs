// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;


using SharpVision.Terminal.Input;
using SharpVision.Text;


using KeyAction = Terminal.Input.Action;
using TerminalText = Terminal.Input.Text;

/// <summary>Verifies TextInput validation, editing, events, input, rendering, and history.</summary>
public sealed class TextInputTests
{
    /// <summary>Verifies conservative defaults and every direct assignment validates before mutation.</summary>
    [Fact]
    public void Properties_WhenAssignmentsAreInvalid_PreservePreviousState()
    {
        TextInput control = new();

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

        control.Text.ShouldBe("Ae\u0301Z");
        control.CaretIndex.ShouldBe(control.Text.Length);
        control.PasswordCharacter.ShouldBeNull();
    }

    /// <summary>Verifies cancellable proposal precedes one atomic committed notification sequence.</summary>
    [Fact]
    public void Text_WhenChangingIsCancelled_PreservesStateAndEventOrder()
    {
        TextInput control = new() { Text = "A" };
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
            "text:A>界:1",
        ]);
    }

    /// <summary>Verifies typed text and owned paste share policy and grapheme maximum handling.</summary>
    [Fact]
    public void Dispatch_WhenTextAndPasteArrive_AppliesPolicyAndMaximum()
    {
        TextInput control = new() { MaxLength = 3 };

        Route(control, new TextEventArgs(new TerminalText(new Rune('界'))), Events.Text);
        Route(control, new PasteEventArgs(new Paste(Encoding.UTF8.GetBytes("e\u0301👩‍💻Z"))), Events.Paste);

        control.Text.ShouldBe("界e\u0301👩‍💻");
        Edit.GraphemeCount(control.Text).ShouldBe(3);
        Route(control, new TextEventArgs(new TerminalText(new Rune('\n'))), Events.Text);
        control.Text.ShouldBe("界e\u0301👩‍💻");

        control.AcceptsReturn = true;
        control.MaxLength = 0;
        Route(control, new TextEventArgs(new TerminalText(new Rune('\n'))), Events.Text);
        control.Text.ShouldEndWith("\n");
    }

    /// <summary>Verifies navigation, extension, word movement, and deletion use grapheme boundaries.</summary>
    [Fact]
    public void Dispatch_WhenEditingKeysArrive_UsesDirectionalGraphemeSelection()
    {
        TextInput control = new() { Text = "one e\u0301👩‍💻" };

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

    /// <summary>Verifies bounded undo and redo retain immutable text and selection snapshots.</summary>
    [Fact]
    public void Undo_WhenHistoryExists_RestoresTextSelectionAndRedo()
    {
        TextInput control = new()
        {
            UndoLimit = 2,
            Text = "A"
        };
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
        TextInput control = new() { Text = "value", IsReadOnly = true };
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
        TextInput control = new()
        {
            Text = "Ae\u0301👩‍💻",
            PasswordCharacter = new Rune('*'),
            Width = Length.Cells(6),
        };
        control.SetFocused(true);
        new Engine().Layout(control, new Size(6, 1));
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
        TextInput control = new()
        {
            Bounds = new Rect(0, -1, 12, 1),
            Text = "Scrolled out",
        };
        control.SetFocused(true);
        using Frame frame = new(new Size(12, 2));

        Should.NotThrow(() => control.Render(frame.Canvas));

        frame.Cursor.Visible.ShouldBeFalse();
    }

    /// <summary>Verifies selected cells render reversed without splitting a wide grapheme.</summary>
    [Fact]
    public void Render_WhenSelectionContainsWideRune_StylesCompleteOwnedCells()
    {
        TextInput control = new() { Text = "A界Z" };
        control.Select(start: 1, length: 1);
        new Engine().Layout(control, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        control.Render(frame.Canvas);

        (frame.GetCell(new Point(1, 0)).Style.Attributes & Attributes.Reverse)
            .ShouldBe(Attributes.Reverse);
        (frame.GetCell(new Point(2, 0)).Style.Attributes & Attributes.Reverse)
            .ShouldBe(Attributes.Reverse);
        frame.GetCell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies a configured input background fills every arranged cell instead of only the rendered text.</summary>
    [Fact]
    public void Render_WhenBackgroundIsStyled_FillsEntireInputBox()
    {
        Color background = Color.Indexed(24);
        ControlStyle<TextInput> style = ThemeTestSupport.OverlayStyle<TextInput>(
            (State.Normal, new ThemeOverlay(background: background)));
        TextInput control = new()
        {
            Width = Length.Cells(5),
            Text = "A",
            Style = style,
        };
        new Engine().Layout(control, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));

        control.Render(frame.Canvas);

        for (int x = 0; x < 5; x++)
        {
            frame.GetCell(new Point(x, 0)).Style.Background.ShouldBe(background);
        }
    }

    /// <summary>Verifies pointer press and inferred-pixel drag focus, capture, and select boundaries.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerDrags_SelectsByRenderedCellsAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            TextInput control = new()
            {
                Bounds = new Rect(0, 0, 8, 1),
                Text = "A界e\u0301Z",
            };
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            using CaptureManager capture = new(control);

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

    /// <summary>Verifies caret visibility updates horizontal and vertical offsets after resize.</summary>
    [Fact]
    public void Arrange_WhenCaretExceedsViewport_ScrollsAndClampsAfterResize()
    {
        TextInput control = new()
        {
            AcceptsReturn = true,
            Text = "123456\nabcdef\nXYZ",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Engine engine = new();

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
        TextInput control = new()
        {
            AcceptsReturn = true,
            Text = "abcdef\none\ntwo\nthree",
            CaretIndex = 0,
        };
        new Engine().Layout(control, new Size(4, 2));

        PointerEventArgs first = Wheel(wheelX: -1, wheelY: -1);
        Route(control, first, Events.Pointer);

        control.HorizontalOffset.ShouldBe(1);
        control.VerticalOffset.ShouldBe(1);
        first.Handled.ShouldBeTrue();

        Route(control, Wheel(wheelX: -100, wheelY: -100), Events.Pointer);
        control.HorizontalOffset.ShouldBe(4);
        control.VerticalOffset.ShouldBe(4);

        PointerEventArgs endpoint = Wheel(wheelX: -1, wheelY: -1);
        Route(control, endpoint, Events.Pointer);

        endpoint.Handled.ShouldBeFalse();
    }

    /// <summary>Verifies overflowing multiline input exposes a configured canonical vertical scrollbar.</summary>
    [Fact]
    public void ScrollBars_WhenMultilineContentOverflows_ExposesCanonicalVerticalRail()
    {
        TextInput control = new()
        {
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            AcceptsReturn = true,
            Text = "one\ntwo\nthree\nfour\nfive",
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarStyle.Thin,
            ScrollBarFill = ScrollBarFill.Line,
        };
        new Engine().Layout(control, new Size(8, 3));

        ScrollBar rail = control.HitTest(new Point(7, 0)).ShouldBeOfType<ScrollBar>();
        rail.Orientation.ShouldBe(Orientation.Vertical);
        rail.Chrome.ShouldBe(ScrollBarStyle.Thin);
        rail.Fill.ShouldBe(ScrollBarFill.Line);
    }

    /// <summary>Verifies an editor at its wheel endpoint leaves the routed delta for its enclosing viewport.</summary>
    [Fact]
    public void Dispatch_WhenEditorWheelReachesEndpoint_OffersNextDeltaToEnclosingViewport()
    {
        TextInput input = new()
        {
            Width = Length.Cells(5),
            Height = Length.Cells(2),
            AcceptsReturn = true,
            Text = "one\ntwo\nthree\nfour",
            CaretIndex = 0,
        };
        Stack content = new();
        content.Children.Add(input);
        content.Children.Add(new ProbeControl(new Size(5, 8)));
        ScrollView outer = new()
        {
            Content = content,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
        };
        new Engine().Layout(outer, new Size(5, 3));

        Route(input, Wheel(wheelX: 0, wheelY: -100), Events.Pointer);
        input.VerticalOffset.ShouldBe(4);
        outer.VerticalOffset.ShouldBe(0);

        PointerEventArgs endpoint = Wheel(wheelX: 0, wheelY: -1);
        Route(input, endpoint, Events.Pointer);

        outer.VerticalOffset.ShouldBe(1);
        endpoint.Handled.ShouldBeTrue();
    }

    /// <summary>Verifies a notification exception preserves the committed atomic state.</summary>
    [Fact]
    public void Text_WhenChangedHandlerThrows_PreservesCommittedStateAndFutureEdits()
    {
        TextInput control = new();
        InvalidOperationException failure = new("observer");
        void handler(object? sender, TextChangedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            throw failure;
        }
        control.TextChanged += handler;

        Should.Throw<InvalidOperationException>(() => control.Text = "A").ShouldBeSameAs(failure);
        control.Text.ShouldBe("A");
        control.CaretIndex.ShouldBe(1);
        control.TextChanged -= handler;
        control.Text = "B";
        control.Text.ShouldBe("B");
    }

    /// <summary>Verifies typed-input observers cannot be mistaken for rejected edit policy.</summary>
    [Fact]
    public void Dispatch_WhenObserverThrowsArgumentException_PropagatesAfterCommit()
    {
        TextInput control = new();
        ArgumentException failure = new("observer");
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
        TextInput control = new() { Text = "A" };
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
        TextInput control = new() { Text = "A界Z" };
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

    /// <summary>Verifies vertical navigation maps the current rendered column to an adjacent line.</summary>
    [Fact]
    public void Dispatch_WhenUpArrives_MovesToNearestBoundaryOnPreviousLine()
    {
        TextInput control = new()
        {
            AcceptsReturn = true,
            Text = "abc\n12345",
        };

        Key(control, Code.Up, Modifiers.None);

        control.CaretIndex.ShouldBe(3);
    }

    /// <summary>Verifies losing focus during pointer selection releases capture and held state.</summary>
    [Fact]
    public async Task Dispatch_WhenFocusLeavesDuringPointerDrag_CancelsCaptureAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ProbeContainer root = new() { Bounds = new Rect(0, 0, 20, 2) };
            TextInput control = new()
            {
                Bounds = new Rect(0, 0, 8, 1),
                Text = "select",
            };
            ProbeControl other = new()
            {
                Bounds = new Rect(10, 0, 2, 1),
                CanFocus = true,
            };
            root.Children.Add(control);
            root.Children.Add(other);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using CaptureManager capture = new(root);

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
        StringBuilder result = new(count);

        for (int x = 0; x < count; x++)
        {
            _ = result.Append(FrameOracle.Get(frame, new Point(x, 0)));
        }

        return result.ToString();
    }

    private static byte[] CopyOccupied(Frame frame)
    {
        List<byte> result = [];

        for (int x = 0; x < frame.Size.Width; x++)
        {
            Point point = new(x, 0);

            if (frame.GetCell(point).IsContinuation)
            {
                continue;
            }

            int length = frame.GetGraphemeByteCount(point);

            if (length == 0)
            {
                continue;
            }

            byte[] bytes = new byte[length];
            _ = frame.CopyGrapheme(point, bytes);
            result.AddRange(bytes);
        }

        return [.. result];
    }
}
