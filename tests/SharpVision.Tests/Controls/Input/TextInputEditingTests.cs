// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Support;

/// <summary>Verifies every keyboard editing command of a mounted TextInput through real terminal
/// key bytes, asserting text, selection, events, and the rendered cells and cursor together.</summary>
public sealed class TextInputEditingTests
{
    private static TextInput Editor(string text, int width, int height = 1, bool multiline = false) => new()
    {
        AcceptsReturn = multiline,
        Text = text,
        ScrollBars = ScrollBars.None,
        Width = Length.Cells(width),
        Height = Length.Cells(height)
    };

    private static Task<ComponentSurface> MountAsync(ControlBase control, int width, int height = 1) =>
        ComponentSurface.MountAsync(
            control,
            new Size(width, height),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

    /// <summary>Verifies Delete at the end and Backspace at the start are inert: no text change, no
    /// TextChanged, and the caret stays put.</summary>
    [Fact]
    public async Task Keyboard_WhenBackspaceAtStartOrDeleteAtEnd_LeavesTextAndEventsUntouchedAsync()
    {
        // Arrange
        var input = Editor("ab", 4);
        var changes = 0;
        input.TextChanged += (_, _) => changes++;
        await using var surface = await MountAsync(input, 4);
        await surface.Keyboard.PressAsync(Code.Tab);
        input.CaretIndex.ShouldBe(2);

        // Act
        await surface.Keyboard.PressAsync(Code.Delete);
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.Backspace);

        // Assert
        input.Text.ShouldBe("ab");
        changes.ShouldBe(0);
        input.CaretIndex.ShouldBe(0);
        surface.ShouldRender("ab");
        surface.ShouldHaveCursor(new Point(0, 0), visible: true);
    }

    /// <summary>Verifies Backspace and Delete remove exactly one complete grapheme on either side of
    /// the caret, repainting both the removed wide cell and the cursor column.</summary>
    [Fact]
    public async Task Keyboard_WhenBackspaceAndDeleteRemoveGraphemes_UpdatesCellsAndCursorAsync()
    {
        // Arrange
        var input = Editor("ab界c", 6);
        await using var surface = await MountAsync(input, 6);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldRender("ab界c");
        surface.ShouldHaveCursor(new Point(5, 0), visible: true);

        // Act and assert - Backspace removes the trailing narrow grapheme
        await surface.Keyboard.PressAsync(Code.Backspace);
        input.Text.ShouldBe("ab界");
        surface.ShouldRender("ab界");
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);

        // Act and assert - Left then Backspace removes the grapheme before the wide cluster
        await surface.Keyboard.PressAsync(Code.Left);
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.Backspace);
        input.Text.ShouldBe("a界");
        surface.ShouldRender("a界");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);

        // Act and assert - Delete removes the whole two-cell cluster after the caret
        await surface.Keyboard.PressAsync(Code.Delete);
        input.Text.ShouldBe("a");
        surface.ShouldRender("a");
        surface.Cell(new Point(1, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 0)).Text.ShouldBe(" ");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }

    /// <summary>Verifies Backspace and Delete over a selection remove only the selected range and
    /// collapse the caret at its start.</summary>
    [Theory]
    [InlineData(Code.Backspace)]
    [InlineData(Code.Delete)]
    public async Task Keyboard_WhenSelectionExists_BackspaceOrDeleteRemovesOnlyTheSelectionAsync(Code code)
    {
        // Arrange
        var input = Editor("hello world", 12);
        await using var surface = await MountAsync(input, 12);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.Select(6, 5), "select 'world'");
        surface.ShouldReverse(0, [6, 7, 8, 9, 10], [0, 1, 2, 3, 4, 5]);

        // Act
        await surface.Keyboard.PressAsync(code);

        // Assert
        input.Text.ShouldBe("hello ");
        input.SelectionLength.ShouldBe(0);
        input.CaretIndex.ShouldBe(6);
        surface.ShouldRender("hello ");
        surface.ShouldReverse(0, [], [0, 1, 2, 3, 4, 5, 6]);
        surface.ShouldHaveCursor(new Point(6, 0), visible: true);
    }

    /// <summary>Verifies Shift+Home and Shift+End extend from a fixed anchor in either direction,
    /// painting exactly the selected cells, and that a plain arrow collapses to the near edge.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftHomeAndShiftEndExtend_PaintsExactlyTheSelectedCellsAsync()
    {
        // Arrange
        var input = Editor("abcd", 6);
        await using var surface = await MountAsync(input, 6);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.CaretIndex = 2, "place caret in the middle");

        // Act and assert - Shift+Home selects backward from the anchor
        await surface.Keyboard.PressAsync(Code.Home, Modifiers.Shift);
        input.SelectionStart.ShouldBe(0);
        input.SelectionLength.ShouldBe(2);
        input.CaretIndex.ShouldBe(0);
        surface.ShouldReverse(0, [0, 1], [2, 3]);
        surface.ShouldHaveCursor(new Point(0, 0), visible: true);

        // Act and assert - Shift+End swings the caret past the anchor to the end
        await surface.Keyboard.PressAsync(Code.End, Modifiers.Shift);
        input.SelectionStart.ShouldBe(2);
        input.SelectionLength.ShouldBe(2);
        input.CaretIndex.ShouldBe(4);
        surface.ShouldReverse(0, [2, 3], [0, 1]);
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);

        // Act and assert - Left collapses to the selection start without moving further
        await surface.Keyboard.PressAsync(Code.Left);
        input.SelectionLength.ShouldBe(0);
        input.CaretIndex.ShouldBe(2);
        surface.ShouldReverse(0, [], [0, 1, 2, 3]);

        // Act and assert - Shift+Right twice then Right collapses to the selection end
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        input.SelectionStart.ShouldBe(2);
        input.SelectionLength.ShouldBe(2);
        await surface.Keyboard.PressAsync(Code.Right);
        input.SelectionLength.ShouldBe(0);
        input.CaretIndex.ShouldBe(4);
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);
    }

    /// <summary>Verifies Control+Left/Right jump to Unicode word starts across punctuation, and
    /// Control+Shift+Left extends the selection by a whole word.</summary>
    [Fact]
    public async Task Keyboard_WhenControlArrowsJumpWords_MovesAcrossPunctuationAndExtendsAsync()
    {
        // Arrange
        var input = Editor("foo bar-baz qux", 16);
        await using var surface = await MountAsync(input, 16);
        await surface.Keyboard.PressAsync(Code.Tab);
        input.CaretIndex.ShouldBe(15);

        // Act and assert - backward jumps land on word starts, treating '-' as a separator
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Control);
        input.CaretIndex.ShouldBe(12);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Control);
        input.CaretIndex.ShouldBe(8);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Control);
        input.CaretIndex.ShouldBe(4);
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);

        // Act and assert - forward jumps land on the next word start
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Control);
        input.CaretIndex.ShouldBe(8);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Control);
        input.CaretIndex.ShouldBe(12);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Control);
        input.CaretIndex.ShouldBe(15);

        // Act and assert - Control+Shift+Left extends by one word and paints it
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Control | Modifiers.Shift);
        input.SelectionStart.ShouldBe(12);
        input.SelectionLength.ShouldBe(3);
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("qux");
        surface.ShouldReverse(0, [12, 13, 14], [11]);

        // Act and assert - Control+Shift+Right from the collapsed start re-covers the same word
        await surface.Keyboard.PressAsync(Code.Left);
        input.CaretIndex.ShouldBe(12);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Control | Modifiers.Shift);
        input.SelectionStart.ShouldBe(12);
        input.SelectionLength.ShouldBe(3);
    }

    /// <summary>Verifies Control+Backspace and Control+Delete are application-command chords the
    /// editor leaves unhandled: the text never changes and the inherited KeyDown still observes
    /// the unconsumed stroke.</summary>
    [Fact]
    public async Task Keyboard_WhenControlBackspaceOrDeleteArrives_LeavesTextUnchangedAndUnhandledAsync()
    {
        // Arrange
        var input = Editor("abc def", 8);
        var observed = new List<(Code Code, bool Handled)>();
        input.KeyDown += (_, eventArgs) => observed.Add((eventArgs.Stroke.Code, eventArgs.IsHandled));
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Backspace, Modifiers.Control);
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.ControlDeleteAsync();

        // Assert
        input.Text.ShouldBe("abc def");
        surface.ShouldRender("abc def");
        observed.ShouldContain((Code.Backspace, false));
        observed.ShouldContain((Code.Delete, false));
    }

    /// <summary>Verifies Shift+Delete still deletes (Shift is a text-entry modifier), unlike the
    /// Control chord.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftDeleteArrives_DeletesLikePlainDeleteAsync()
    {
        // Arrange
        var input = Editor("abc", 4);
        await using var surface = await MountAsync(input, 4);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Home);

        // Act
        await surface.ShiftDeleteAsync();

        // Assert
        input.Text.ShouldBe("bc");
        surface.ShouldRender("bc");
    }

    /// <summary>Verifies Control+A selects the whole text, paints every cell, keeps the caret at
    /// the end, and typing then replaces the entire selection with the typed character.</summary>
    [Fact]
    public async Task Keyboard_WhenControlAThenTyped_SelectsAllThenOvertypesTheSelectionAsync()
    {
        // Arrange
        var input = Editor("ab界", 6);
        await using var surface = await MountAsync(input, 6);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Home);

        // Act
        await surface.ControlAsync('a');

        // Assert
        input.SelectionStart.ShouldBe(0);
        input.SelectionLength.ShouldBe(3);
        input.CaretIndex.ShouldBe(3);
        input.Text.ShouldBe("ab界");
        surface.ShouldReverse(0, [0, 1, 2, 3], [4]);
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);

        // Act - overtype
        await surface.Keyboard.TypeAsync("z");

        // Assert
        input.Text.ShouldBe("z");
        input.SelectionLength.ShouldBe(0);
        surface.ShouldRender("z");
        surface.ShouldReverse(0, [], [0, 1, 2, 3]);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }

    /// <summary>Verifies Control+C publishes the selection to the application clipboard and
    /// Control+V inserts it at the caret through the ordinary edit path (undoable, rendered).</summary>
    [Fact]
    public async Task Keyboard_WhenControlCThenControlV_RoundTripsThroughTheApplicationClipboardAsync()
    {
        // Arrange
        var input = Editor("abc", 8);
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.Select(1, 2), "select 'bc'");

        // Act
        await surface.ControlAsync('c');
        input.Text.ShouldBe("abc");
        input.SelectionLength.ShouldBe(2);
        await surface.Keyboard.PressAsync(Code.End);
        await surface.ControlAsync('v');

        // Assert
        input.Text.ShouldBe("abcbc");
        input.CaretIndex.ShouldBe(5);
        surface.ShouldRender("abcbc");
        surface.ShouldHaveCursor(new Point(5, 0), visible: true);
        input.CanUndo.ShouldBeTrue();
        await surface.ControlAsync('z');
        input.Text.ShouldBe("abc");
    }

    /// <summary>Verifies Control+X removes the selection and publishes it, so a following
    /// Control+V restores it; Control+V with nothing ever copied changes nothing.</summary>
    [Fact]
    public async Task Keyboard_WhenControlXThenControlV_CutsAndRestoresTheSelectionAsync()
    {
        // Arrange
        var input = Editor("hello", 8);
        var changes = 0;
        input.TextChanged += (_, _) => changes++;
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert - paste before anything was copied is a no-op
        await surface.ControlAsync('v');
        input.Text.ShouldBe("hello");
        changes.ShouldBe(0);

        // Act and assert - cut removes and publishes
        await surface.UpdateAsync(() => input.Select(0, 2), "select 'he'");
        await surface.ControlAsync('x');
        input.Text.ShouldBe("llo");
        input.CaretIndex.ShouldBe(0);
        surface.ShouldRender("llo");
        changes.ShouldBe(1);

        // Act and assert - paste restores at the caret
        await surface.ControlAsync('v');
        input.Text.ShouldBe("hello");
        surface.ShouldRender("hello");
        changes.ShouldBe(2);
    }

    /// <summary>Verifies a password editor never discloses its text through Control+C or
    /// Control+X: the clipboard stays empty and the text is not cut.</summary>
    [Fact]
    public async Task Keyboard_WhenPasswordIsCopiedOrCut_NeverPublishesOrRemovesTextAsync()
    {
        // Arrange
        var input = Editor("secret", 8);
        input.PasswordCharacter = new Rune('*');
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.ControlAsync('a');
        surface.ShouldRender("******");
        surface.ShouldReverse(0, [0, 1, 2, 3, 4, 5], [6]);

        // Act
        await surface.ControlAsync('c');
        await surface.ControlAsync('x');
        await surface.Keyboard.PressAsync(Code.End);
        await surface.ControlAsync('v');

        // Assert
        input.Text.ShouldBe("secret");
        surface.ShouldRender("******");
    }

    /// <summary>Verifies Control+Z undoes a coalesced typed run in one step and Control+Y redoes it,
    /// repainting cells and cursor each way.</summary>
    [Fact]
    public async Task Keyboard_WhenControlZAndControlY_UndoAndRedoTypedRunsAsync()
    {
        // Arrange
        var input = Editor(string.Empty, 8);
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("ab");
        await surface.Keyboard.PasteAsync("xy");
        await surface.Keyboard.TypeAsync("z");
        surface.ShouldRender("abxyz");

        // Act and assert - one undo per edit unit
        await surface.ControlAsync('z');
        input.Text.ShouldBe("abxy");
        await surface.ControlAsync('z');
        input.Text.ShouldBe("ab");
        await surface.ControlAsync('z');
        input.Text.ShouldBe(string.Empty);
        surface.ShouldRender(string.Empty);
        surface.ShouldHaveCursor(new Point(0, 0), visible: true);
        input.CanUndo.ShouldBeFalse();

        // Act and assert - redo walks forward again
        await surface.ControlAsync('y');
        input.Text.ShouldBe("ab");
        await surface.ControlAsync('y');
        await surface.ControlAsync('y');
        input.Text.ShouldBe("abxyz");
        surface.ShouldRender("abxyz");
        surface.ShouldHaveCursor(new Point(5, 0), visible: true);
        input.CanRedo.ShouldBeFalse();
    }

    /// <summary>Verifies a repeated Control+Z stroke does not undo a second time.</summary>
    [Fact]
    public async Task Keyboard_WhenControlZRepeats_UndoesOnlyOnceAsync()
    {
        // Arrange
        var input = Editor(string.Empty, 8);
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PasteAsync("a");
        await surface.Keyboard.PasteAsync("b");
        input.Text.ShouldBe("ab");

        // Act - the initial press undoes the second paste
        await surface.ControlAsync('z');

        // Assert
        input.Text.ShouldBe("a");
        surface.ShouldRender("a");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
        input.CanUndo.ShouldBeTrue();

        // Act - the held-key repeat of the same stroke
        await surface.SendAsync(
            Encoding.ASCII.GetBytes(FormattableString.Invariant($"\u001b[{(int) 'z'};5:2u")),
            "repeat Control+z");

        // Assert - a repeat carries KeyAction.Repeat, which the undo command ignores, so the
        // first paste survives until a fresh initial press
        input.Text.ShouldBe("a");
        surface.ShouldRender("a");
        input.CanUndo.ShouldBeTrue();
        await surface.ControlAsync('z');
        input.Text.ShouldBe(string.Empty);
        surface.ShouldRender(string.Empty);
    }

    /// <summary>Verifies a programmatic Text assignment is itself an undo unit, so Control+Z
    /// restores the previously typed value.</summary>
    [Fact]
    public async Task Keyboard_WhenTextIsAssignedThenControlZ_RestoresThePreviousTextAsync()
    {
        // Arrange
        var input = Editor("old", 8);
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.Text = "new", "assign text");
        surface.ShouldRender("new");

        // Act
        await surface.ControlAsync('z');

        // Assert
        input.Text.ShouldBe("old");
        surface.ShouldRender("old");
    }

    /// <summary>Verifies Enter on a read-only multiline editor neither inserts nor submits, while a
    /// read-only single-line editor still submits its value.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterOnReadOnlyEditors_FollowsReturnPolicyAsync()
    {
        // Arrange
        var multi = Editor("ab", 4, 2, multiline: true);
        multi.IsReadOnly = true;
        var single = Editor("ab", 4);
        single.IsReadOnly = true;
        var submitted = new List<string>();
        multi.Submitted += (_, eventArgs) => submitted.Add("multi:" + eventArgs.Text);
        single.Submitted += (_, eventArgs) => submitted.Add("single:" + eventArgs.Text);
        await using var multiSurface = await MountAsync(multi, 4, 2);
        await using var singleSurface = await MountAsync(single, 4);
        await multiSurface.Keyboard.PressAsync(Code.Tab);
        await singleSurface.Keyboard.PressAsync(Code.Tab);

        // Act
        await multiSurface.Keyboard.PressAsync(Code.Enter);
        await singleSurface.Keyboard.PressAsync(Code.Enter);

        // Assert
        multi.Text.ShouldBe("ab");
        single.Text.ShouldBe("ab");
        submitted.ShouldBe(["single:ab"]);
    }

    /// <summary>Verifies Enter in a multiline editor replaces the current selection with one line
    /// break and moves the caret to the start of the next rendered row.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterReplacesSelectionInMultiline_InsertsBreakAndMovesCaretAsync()
    {
        // Arrange
        var input = Editor("abcd", 6, 2, multiline: true);
        await using var surface = await MountAsync(input, 6, 2);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.Select(1, 2), "select 'bc'");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Text.ShouldBe("a\nd");
        input.CaretIndex.ShouldBe(2);
        input.SelectionLength.ShouldBe(0);
        surface.ShouldRender("""
                             a
                             d
                             """);
        surface.ShouldHaveCursor(new Point(0, 1), visible: true);
    }

    /// <summary>Verifies Tab leaves an editor that does not accept tabs and moves focus to the next
    /// tab stop, while an editor that accepts tabs inserts a tab rendered as spaces up to the next
    /// four-cell stop and keeps focus.</summary>
    [Fact]
    public async Task Keyboard_WhenTabIsPressed_MovesFocusOrInsertsTabPerPolicyAsync()
    {
        // Arrange
        var input = new TextInput { Text = "ab", ScrollBars = ScrollBars.None, Width = Length.Cells(8), Height = Length.Cells(1) };
        var button = new Button { Text = "Go", Height = Length.Cells(1) };
        var root = new Stack { Children = { input, button } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.FocusAsync(input);

        // Act and assert - Tab leaves the editor
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(button);
        input.Text.ShouldBe("ab");

        // Act and assert - Shift+Tab returns
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(input);

        // Act and assert - AcceptsTab inserts a tab padded to the next stop
        await surface.UpdateAsync(() => input.AcceptsTab = true, "accept tabs");
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);
        input.Text.ShouldBe("ab\t");
        input.CaretIndex.ShouldBe(3);
        surface.Cell(new Point(2, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(3, 0)).Text.ShouldBe(" ");
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);

        // Act and assert - Shift+Tab is still a text-entry stroke while tabs are accepted
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(input);
        input.Text.ShouldBe("ab\t\t");
        surface.ShouldHaveCursor(new Point(8 - 1, 0), visible: true);
    }

    /// <summary>Verifies a bracketed paste containing a line break is rejected as one transaction
    /// by a single-line editor, and accepted verbatim (CRLF kept as one atomic grapheme) by a
    /// multiline editor whose caret navigation never splits the pair.</summary>
    [Fact]
    public async Task Keyboard_WhenMultiLineTextIsPasted_RejectsSingleLineAndKeepsCrLfAtomicAsync()
    {
        // Arrange
        var single = Editor("x", 6);
        var changes = 0;
        single.TextChanged += (_, _) => changes++;
        await using var singleSurface = await MountAsync(single, 6);
        await singleSurface.Keyboard.PressAsync(Code.Tab);

        // Act and assert - single-line rejects the complete payload
        await singleSurface.Keyboard.PasteAsync("a\r\nb");
        single.Text.ShouldBe("x");
        changes.ShouldBe(0);
        singleSurface.ShouldRender("x");

        // Arrange multiline
        var multi = Editor(string.Empty, 6, 3, multiline: true);
        await using var multiSurface = await MountAsync(multi, 6, 3);
        await multiSurface.Keyboard.PressAsync(Code.Tab);

        // Act
        await multiSurface.Keyboard.PasteAsync("a\r\nb\nc");

        // Assert - verbatim text, three rendered rows, caret after 'c'
        multi.Text.ShouldBe("a\r\nb\nc");
        multiSurface.ShouldRender("""
                                  a
                                  b
                                  c
                                  """);
        multiSurface.ShouldHaveCursor(new Point(1, 2), visible: true);

        // Act and assert - Left never lands between CR and LF
        await multiSurface.Keyboard.PressAsync(Code.Up);
        await multiSurface.Keyboard.PressAsync(Code.Home);
        multi.CaretIndex.ShouldBe(3);
        await multiSurface.Keyboard.PressAsync(Code.Left);
        multi.CaretIndex.ShouldBe(1);
        multiSurface.ShouldHaveCursor(new Point(1, 0), visible: true);
        await multiSurface.Keyboard.PressAsync(Code.Right);
        multi.CaretIndex.ShouldBe(3);
        multiSurface.ShouldHaveCursor(new Point(0, 1), visible: true);
    }

    /// <summary>Verifies a paste carrying a tab is rejected unless AcceptsTab is set, and a paste
    /// exceeding MaxLength is truncated at a grapheme boundary counted in graphemes.</summary>
    [Fact]
    public async Task Keyboard_WhenPasteViolatesTabOrLengthPolicy_RejectsOrTruncatesAtGraphemesAsync()
    {
        // Arrange
        var input = Editor(string.Empty, 8);
        input.MaxLength = 2;
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert - tab rejected wholesale
        await surface.Keyboard.PasteAsync("a\tb");
        input.Text.ShouldBe(string.Empty);

        // Act and assert - truncated to two graphemes, not two chars
        await surface.Keyboard.PasteAsync("e\u0301界x");
        input.Text.ShouldBe("e\u0301界");
        input.CaretIndex.ShouldBe(3);
        surface.ShouldRender("e\u0301界");
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);

        // Act and assert - AcceptsTab admits the tab
        await surface.UpdateAsync(() =>
        {
            input.MaxLength = 0;
            input.AcceptsTab = true;
        }, "lift policy");
        await surface.Keyboard.PasteAsync("\t");
        input.Text.ShouldBe("e\u0301界\t");
    }

    /// <summary>Verifies MaxLength is enforced on typing (extra characters ignored), on paste, and
    /// on programmatic assignment (throws and preserves the rendered state).</summary>
    [Fact]
    public async Task MaxLength_WhenTypingPastingAndAssigning_EnforcesTheGraphemeLimitAsync()
    {
        // Arrange
        var input = Editor(string.Empty, 8);
        input.MaxLength = 2;
        var changes = 0;
        input.TextChanged += (_, _) => changes++;
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert - typing stops at the limit
        await surface.Keyboard.TypeAsync("abc");
        input.Text.ShouldBe("ab");
        changes.ShouldBe(2);
        surface.ShouldRender("ab");

        // Act and assert - over-long assignment throws before mutation
        var failure = await Should.ThrowAsync<ArgumentException>(
            () => surface.UpdateAsync(() => input.Text = "abcd", "assign too long"));
        failure.ParamName.ShouldBe("value");
        input.Text.ShouldBe("ab");
        surface.ShouldRender("ab");

        // Act and assert - a selection may still be overtyped at the limit
        await surface.UpdateAsync(() => input.Select(0, 2), "select all");
        await surface.Keyboard.TypeAsync("z");
        input.Text.ShouldBe("z");
    }

    /// <summary>Verifies the placeholder shows dimmed whenever the editor is empty - including while
    /// focused, behind the caret - and hides the moment the text becomes non-empty, matching the
    /// shared InputBase placeholder contract that NumberInput and friends already follow.</summary>
    [Fact]
    public async Task Placeholder_WhenFocusAndTextChange_ShowsWhileEmptyAndHidesOnceTypedAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Placeholder = "Name",
            ScrollBars = ScrollBars.None,
            Width = Length.Cells(8),
            Height = Length.Cells(1)
        };
        var button = new Button { Text = "Go", Height = Length.Cells(1) };
        var root = new Stack { Children = { input, button } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("N");
        (surface.Cell(new Point(0, 0)).Style.Attributes & TerminalAttributes.Dim).ShouldBe(TerminalAttributes.Dim);

        // Act and assert - focus keeps the placeholder visible behind the caret
        await surface.FocusAsync(input);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("N");
        (surface.Cell(new Point(0, 0)).Style.Attributes & TerminalAttributes.Dim).ShouldBe(TerminalAttributes.Dim);
        surface.ShouldHaveCursor(new Point(0, 0), visible: true);

        // Act and assert - typing hides it, and deleting back to empty restores it while still focused
        await surface.Keyboard.TypeAsync("x");
        surface.Cell(new Point(0, 0)).Text.ShouldBe("x");
        await surface.Keyboard.PressAsync(Code.Backspace);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("N");
        surface.ShouldHaveCursor(new Point(0, 0), visible: true);

        // Act and assert - losing focus while empty keeps it showing
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(button);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("N");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("e");

        // Act and assert - non-empty text never shows it
        await surface.UpdateAsync(() => input.Text = "v", "assign text while unfocused");
        surface.Cell(new Point(0, 0)).Text.ShouldBe("v");
        surface.Cell(new Point(1, 0)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies an over-wide placeholder is clipped to the viewport and a placeholder
    /// containing a line break stops at the break.</summary>
    [Fact]
    public async Task Placeholder_WhenTooWideOrMultiLine_ClipsToTheFirstRowAsync()
    {
        // Arrange
        var wide = new TextInput { Placeholder = "Placeholder", ScrollBars = ScrollBars.None, Width = Length.Cells(5), Height = Length.Cells(1) };
        var broken = new TextInput { Placeholder = "ab\ncd", ScrollBars = ScrollBars.None, Width = Length.Cells(6), Height = Length.Cells(2) };
        await using var wideSurface = await MountAsync(wide, 5);
        await using var brokenSurface = await MountAsync(broken, 6, 2);

        // Assert
        wideSurface.ShouldRender("Place");
        brokenSurface.ShouldRender("""
                                   ab

                                   """);
    }

    /// <summary>Verifies a password editor masks every grapheme with one cell, keeps the real text
    /// in Text, selects and deletes by whole graphemes, and positions the cursor per mask.</summary>
    [Fact]
    public async Task Keyboard_WhenPasswordIsTypedAndEdited_MasksPerGraphemeAsync()
    {
        // Arrange
        var input = Editor(string.Empty, 8);
        input.PasswordCharacter = new Rune('*');
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("a界é");

        // Assert - three masks for three graphemes, cursor after the third
        input.Text.ShouldBe("a界é");
        surface.ShouldRender("***");
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);

        // Act and assert - Shift+Left selects the last grapheme as one mask cell
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);
        input.SelectionStart.ShouldBe(2);
        input.SelectionLength.ShouldBe(2);
        surface.ShouldReverse(0, [2], [0, 1]);

        // Act and assert - Backspace removes the whole selected grapheme
        await surface.Keyboard.PressAsync(Code.Backspace);
        input.Text.ShouldBe("a界");
        surface.ShouldRender("**");
        await surface.Keyboard.PressAsync(Code.Backspace);
        input.Text.ShouldBe("a");
        surface.ShouldRender("*");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }

    /// <summary>Verifies a read-only editor still selects with Shift and copies with Control+C
    /// (pasteable elsewhere), while Control+V, Control+X, and Control+Z are refused.</summary>
    [Fact]
    public async Task Keyboard_WhenReadOnly_AllowsSelectionAndCopyButRefusesEveryMutationAsync()
    {
        // Arrange
        var source = new TextInput { Text = "Read", IsReadOnly = true, ScrollBars = ScrollBars.None, Width = Length.Cells(8), Height = Length.Cells(1) };
        var target = new TextInput { ScrollBars = ScrollBars.None, Width = Length.Cells(8), Height = Length.Cells(1) };
        var root = new Stack { Children = { source, target } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.FocusAsync(source);

        // Act - select "ad" backwards and copy
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);
        surface.ShouldReverse(0, [2, 3], [0, 1]);
        await surface.ControlAsync('c');
        await surface.ControlAsync('x');
        await surface.ControlAsync('v');
        await surface.ControlAsync('z');

        // Assert - source untouched
        source.Text.ShouldBe("Read");
        source.SelectionLength.ShouldBe(2);

        // Act - paste into the editable sibling
        await surface.FocusAsync(target);
        await surface.ControlAsync('v');

        // Assert
        target.Text.ShouldBe("ad");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("a");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("d");
    }

    /// <summary>Verifies assigning Text while focused with an active selection collapses the caret
    /// at the new end, clears the selection, publishes one SelectionChanged, and repaints.</summary>
    [Fact]
    public async Task Text_WhenAssignedWhileFocusedWithSelection_ResetsCaretAndSelectionAsync()
    {
        // Arrange
        var input = Editor("hello", 8);
        var selections = new List<(Selection Previous, Selection Current)>();
        input.SelectionChanged += (_, eventArgs) => selections.Add((eventArgs.Previous, eventArgs.Selection));
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.Select(1, 3), "select 'ell'");
        selections.Clear();

        // Act
        await surface.UpdateAsync(() => input.Text = "hi", "assign shorter text");

        // Assert
        input.CaretIndex.ShouldBe(2);
        input.SelectionLength.ShouldBe(0);
        selections.ShouldBe([(new Selection(1, 4), new Selection(2, 2))]);
        surface.ShouldRender("hi");
        surface.ShouldReverse(0, [], [0, 1, 2, 3, 4]);
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
    }

    /// <summary>Verifies the event order for one typed character: TextChanging with the proposal,
    /// TextChanged with previous and new text, then SelectionChanged with the caret move.</summary>
    [Fact]
    public async Task Events_WhenCharacterIsTyped_RaiseChangingThenChangedThenSelectionChangedAsync()
    {
        // Arrange
        var input = Editor("a", 4);
        var order = new List<string>();
        input.TextChanging += (_, eventArgs) => order.Add($"changing:{eventArgs.Proposal.Text}:{eventArgs.Proposal.Selection.Caret}");
        input.TextChanged += (_, eventArgs) => order.Add($"changed:{eventArgs.PreviousText}>{eventArgs.Text}:{input.CaretIndex}");
        input.SelectionChanged += (_, eventArgs) => order.Add($"selection:{eventArgs.Previous.Caret}>{eventArgs.Selection.Caret}");
        await using var surface = await MountAsync(input, 4);
        await surface.Keyboard.PressAsync(Code.Tab);
        order.Clear();

        // Act
        await surface.Keyboard.TypeAsync("b");

        // Assert
        order.ShouldBe(["changing:ab:2", "changed:a>ab:2", "selection:1>2"]);
    }

    /// <summary>Verifies a cancelled TextChanging leaves the typed character out of both the text
    /// and the rendered cells, with no TextChanged or SelectionChanged.</summary>
    [Fact]
    public async Task Events_WhenTextChangingCancels_LeavesTextCellsAndSelectionUntouchedAsync()
    {
        // Arrange
        var input = Editor("a", 4);
        var raised = 0;
        input.TextChanging += (_, eventArgs) => eventArgs.Cancel = true;
        input.TextChanged += (_, _) => raised++;
        input.SelectionChanged += (_, _) => raised++;
        await using var surface = await MountAsync(input, 4);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("b");
        await surface.Keyboard.PasteAsync("cd");
        await surface.Keyboard.PressAsync(Code.Backspace);

        // Assert
        input.Text.ShouldBe("a");
        raised.ShouldBe(0);
        surface.ShouldRender("a");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }

    /// <summary>Verifies disabling a focused editor mid-edit drops focus and cursor, ignores typing,
    /// and re-enabling then focusing resumes at the preserved caret.</summary>
    [Fact]
    public async Task IsEnabled_WhenClearedMidEdit_DropsFocusThenResumesAtThePreservedCaretAsync()
    {
        // Arrange
        var input = Editor(string.Empty, 8);
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("ab");
        await surface.Keyboard.PressAsync(Code.Left);

        // Act
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable mid-edit");

        // Assert
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(input, VisualState.Disabled);
        surface.ShouldRender("ab");
        surface.ShouldHaveCursor(default, visible: false);
        await surface.Keyboard.TypeAsync("c");
        input.Text.ShouldBe("ab");

        // Act
        await surface.UpdateAsync(() => input.IsEnabled = true, "re-enable");
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(input);
        input.CaretIndex.ShouldBe(1);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
        await surface.Keyboard.TypeAsync("c");
        input.Text.ShouldBe("acb");
    }

    /// <summary>Verifies losing focus mid-selection keeps the selection range and its painted
    /// cells but hides the caret, and regaining focus shows it again without moving it.</summary>
    [Fact]
    public async Task Focus_WhenLostMidSelection_KeepsTheRangeAndHidesTheCaretAsync()
    {
        // Arrange
        var input = new TextInput { Text = "abcd", ScrollBars = ScrollBars.None, Width = Length.Cells(8), Height = Length.Cells(1) };
        var button = new Button { Text = "Go", Height = Length.Cells(1) };
        var root = new Stack { Children = { input, button } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.FocusAsync(input);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(button);
        input.SelectionStart.ShouldBe(2);
        input.SelectionLength.ShouldBe(2);
        surface.ShouldReverse(0, [2, 3], [0, 1]);
        surface.ShouldHaveCursor(default, visible: false);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);

        // Assert
        surface.ShouldHaveFocus(input);
        input.SelectionLength.ShouldBe(2);
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
    }

    /// <summary>Verifies shrinking the surface under a focused editor scrolls the viewport so the
    /// end-of-text caret stays visible, rendering the trailing characters rather than blanking.</summary>
    [Fact]
    public async Task ResizeAsync_WhenViewportShrinksUnderTheCaret_KeepsTheCaretVisibleAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "abcdef",
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await MountAsync(input, 8);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldRender("abcdef");
        surface.ShouldHaveCursor(new Point(6, 0), visible: true);

        // Act
        await surface.ResizeAsync(new Size(4, 1));

        // Assert
        input.HorizontalOffset.ShouldBe(3);
        surface.ShouldRender("def");
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);

        // Act - Home scrolls back to the start
        await surface.Keyboard.PressAsync(Code.Home);
        input.HorizontalOffset.ShouldBe(0);
        surface.ShouldRender("abcd");
        surface.ShouldHaveCursor(new Point(0, 0), visible: true);
    }

    /// <summary>Verifies switching the application theme while focused with a selection keeps the
    /// text, cursor, and selection adornment in place under the new palette.</summary>
    [Fact]
    public async Task Theme_WhenChangedWhileFocusedWithSelection_KeepsTextCursorAndSelectionAsync()
    {
        // Arrange
        var input = new TextInput { Text = "abcd", ScrollBars = ScrollBars.None, Width = Length.Cells(8), Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(8, 3),
            ThemeCatalog.Dark,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);
        surface.ShouldHaveCursor(new Point(4, 1), visible: true);
        var before = surface.Cell(new Point(1, 1)).Style.Background;

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = ThemeCatalog.White, "switch to the white theme");

        // Assert
        surface.Cell(new Point(1, 1)).Text.ShouldBe("a");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("d");
        surface.Cell(new Point(1, 1)).Style.Background.ShouldNotBe(before);
        surface.ShouldReverse(1, [4], [1, 2, 3]);
        surface.ShouldHaveCursor(new Point(4, 1), visible: true);
        surface.ShouldHaveFocus(input);

        // Act and assert - editing continues under the new theme
        await surface.Keyboard.TypeAsync("z");
        input.Text.ShouldBe("abcz");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("z");
    }

    /// <summary>Verifies a one-cell editor still accepts input and keeps a visible caret, and a
    /// two-cell editor shows the last character beside the caret.</summary>
    [Theory]
    [InlineData(1, " ", 0)]
    [InlineData(2, "b", 1)]
    public async Task Layout_WhenBoundsAreTiny_KeepsCaretVisibleAndTextIntactAsync(int width, string expectedRow, int cursorX)
    {
        // Arrange
        var input = Editor(string.Empty, width);
        await using var surface = await MountAsync(input, width);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("ab");

        // Assert
        input.Text.ShouldBe("ab");
        surface.ShouldRender(expectedRow);
        surface.ShouldHaveCursor(new Point(cursorX, 0), visible: true);
    }

    /// <summary>Verifies Shift+Up/Down extend across logical lines to the nearest boundary at the
    /// same column, and Home/End stay within the caret's own line.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftUpDownAndHomeEndOnMultiline_StayLineAwareAsync()
    {
        // Arrange
        var input = Editor("abc\ndef", 6, 2, multiline: true);
        await using var surface = await MountAsync(input, 6, 2);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.CaretIndex = 6, "caret after 'de'");

        // Act and assert - Shift+Up
        await surface.Keyboard.PressAsync(Code.Up, Modifiers.Shift);
        input.CaretIndex.ShouldBe(2);
        input.SelectionStart.ShouldBe(2);
        input.SelectionLength.ShouldBe(4);
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("c\nde");
        surface.ShouldReverse(0, [2], [0, 1]);
        surface.ShouldReverse(1, [0, 1], [2]);
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);

        // Act and assert - Shift+Down back collapses the extension, then End/Home stay on line 2
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Shift);
        input.CaretIndex.ShouldBe(6);
        input.SelectionLength.ShouldBe(0);
        await surface.Keyboard.PressAsync(Code.End);
        input.CaretIndex.ShouldBe(7);
        await surface.Keyboard.PressAsync(Code.Home);
        input.CaretIndex.ShouldBe(4);
        surface.ShouldHaveCursor(new Point(0, 1), visible: true);
        await surface.Keyboard.PressAsync(Code.Home, Modifiers.Shift);
        input.SelectionLength.ShouldBe(0);
    }

    /// <summary>Verifies an emoji ZWJ sequence is one two-cell grapheme for typing, caret movement,
    /// selection painting, and deletion.</summary>
    [Fact]
    public async Task Keyboard_WhenEmojiSequenceIsEdited_TreatsItAsOneWideGraphemeAsync()
    {
        // Arrange
        const string emoji = "👩‍💻";
        var input = Editor(string.Empty, 6);
        await using var surface = await MountAsync(input, 6);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("a" + emoji + "b");

        // Assert
        input.Text.ShouldBe("a" + emoji + "b");
        surface.Cell(new Point(1, 0)).Text.ShouldBe(emoji);
        surface.Cell(new Point(2, 0)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(3, 0)).Text.ShouldBe("b");
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);

        // Act and assert - Left twice lands before the emoji, never inside it
        await surface.Keyboard.PressAsync(Code.Left);
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.Left);
        input.CaretIndex.ShouldBe(1);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);

        // Act and assert - Shift+Right selects both cells; Delete removes the whole sequence
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        input.SelectionLength.ShouldBe(emoji.Length);
        surface.ShouldReverse(0, [1, 2], [0, 3]);
        await surface.Keyboard.PressAsync(Code.Delete);
        input.Text.ShouldBe("ab");
        surface.ShouldRender("ab");
    }

    /// <summary>Verifies a typed character while a selection is active replaces it in one undoable
    /// step that a single Control+Z reverts, restoring the selection.</summary>
    [Fact]
    public async Task Keyboard_WhenOvertypingASelectionThenUndo_RestoresTextAndSelectionAsync()
    {
        // Arrange
        var input = Editor("abcd", 6);
        await using var surface = await MountAsync(input, 6);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.Select(1, 2), "select 'bc'");

        // Act
        await surface.Keyboard.TypeAsync("X");
        input.Text.ShouldBe("aXd");
        await surface.ControlAsync('z');

        // Assert
        input.Text.ShouldBe("abcd");
        input.SelectionStart.ShouldBe(1);
        input.SelectionLength.ShouldBe(2);
        surface.ShouldRender("abcd");
        surface.ShouldReverse(0, [1, 2], [0, 3]);
    }
}
