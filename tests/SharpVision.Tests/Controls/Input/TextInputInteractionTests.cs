// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Support;

/// <summary>Verifies pointer gestures, context-menu commands, wrapping, and scrolling of a mounted
/// TextInput, asserting rendered rows and the terminal cursor rather than offsets alone.</summary>
public sealed class TextInputInteractionTests
{
    private static TextInput Editor(string text, int width, int height = 1, bool multiline = false, bool wrap = false) => new()
    {
        AcceptsReturn = multiline,
        WordWrap = wrap,
        Text = text,
        ScrollBars = ScrollBars.None,
        Width = Length.Cells(width),
        Height = Length.Cells(height)
    };

    private static Task<ComponentSurface> MountAsync(ControlBase control, int width, int height = 1, ManualTimeProvider? time = null) =>
        time is null
            ? ComponentSurface.MountAsync(control, new Size(width, height), TestThemes.BorderlessInput, TestContext.Current.CancellationToken)
            : ComponentSurface.MountAsync(control, new Size(width, height), time, TestThemes.BorderlessInput, TestContext.Current.CancellationToken);

    /// <summary>Verifies clicking each column places the caret at the nearest grapheme boundary,
    /// clamps past the end, focuses the editor, and moves the terminal cursor.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(5, 3)]
    public async Task Pointer_WhenColumnIsClicked_PlacesCaretAtNearestBoundaryAsync(int column, int expectedCaret)
    {
        // Arrange
        var input = Editor("abc", 6);
        await using var surface = await MountAsync(input, 6);
        input.IsFocused.ShouldBeFalse();

        // Act
        await surface.Pointer.ClickAsync(input, new Point(column, 0));

        // Assert
        surface.ShouldHaveFocus(input);
        input.CaretIndex.ShouldBe(expectedCaret);
        input.SelectionLength.ShouldBe(0);
        surface.ShouldHaveCursor(new Point(expectedCaret, 0), visible: true);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies clicking a lower row of a multiline editor places the caret on that
    /// logical line, and clicking a blank row below the text lands on the last line.</summary>
    [Fact]
    public async Task Pointer_WhenRowIsClicked_PlacesCaretOnThatLineAsync()
    {
        // Arrange
        var input = Editor("ab\ncdef", 6, 3, multiline: true);
        await using var surface = await MountAsync(input, 6, 3);

        // Act and assert - second row
        await surface.Pointer.ClickAsync(input, new Point(2, 1));
        input.CaretIndex.ShouldBe(5);
        surface.ShouldHaveCursor(new Point(2, 1), visible: true);

        // Act and assert - blank row below content resolves to the last line
        await surface.Pointer.ClickAsync(input, new Point(1, 2));
        input.CaretIndex.ShouldBe(4);
        surface.ShouldHaveCursor(new Point(1, 1), visible: true);
    }

    /// <summary>Verifies double-click selects the word under the pointer, triple-click selects the
    /// whole line, and a click after the multi-click interval collapses again.</summary>
    [Fact]
    public async Task Pointer_WhenClickedRepeatedly_SelectsWordThenLineThenCollapsesAsync()
    {
        // Arrange
        var input = Editor("alpha beta", 12);
        var time = new ManualTimeProvider();
        await using var surface = await MountAsync(input, 12, 1, time);
        var target = new Point(7, 0);

        // Act - double click
        await surface.Pointer.ClickAsync(input, target);
        await surface.Pointer.ClickAsync(input, target);

        // Assert
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("beta");
        input.SelectionStart.ShouldBe(6);
        input.SelectionLength.ShouldBe(4);
        surface.ShouldReverse(0, [6, 7, 8, 9], [0, 4, 5]);
        surface.ShouldHaveCapture(null);

        // Act - triple click
        await surface.Pointer.ClickAsync(input, target);

        // Assert
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("alpha beta");
        surface.ShouldReverse(0, [0, 5, 9], [10]);

        // Act - a click after the interval is a fresh single click
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(600), "let the multi-click interval lapse");
        await surface.Pointer.ClickAsync(input, target);

        // Assert
        input.SelectionLength.ShouldBe(0);
        input.CaretIndex.ShouldBe(7);
        surface.ShouldReverse(0, [], [0, 6, 7, 9]);
    }

    /// <summary>Verifies double-click on whitespace selects only that grapheme, and on a wide
    /// character selects the complete two-cell cluster.</summary>
    [Fact]
    public async Task Pointer_WhenNonWordGraphemeIsDoubleClicked_SelectsOnlyThatGraphemeAsync()
    {
        // Arrange
        var input = Editor("ab 界-c", 8);
        var time = new ManualTimeProvider();
        await using var surface = await MountAsync(input, 8, 1, time);

        // Act and assert - the space
        await surface.Pointer.ClickAsync(input, new Point(2, 0));
        await surface.Pointer.ClickAsync(input, new Point(2, 0));
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe(" ");
        surface.ShouldReverse(0, [2], [1, 3, 4]);

        // Act and assert - the wide cluster, hit on its leading cell
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(600), "lapse");
        await surface.Pointer.ClickAsync(input, new Point(3, 0));
        await surface.Pointer.ClickAsync(input, new Point(3, 0));
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("界");
        surface.ShouldReverse(0, [3, 4], [2, 5]);

        // Act and assert - the same cluster hit on its trailing cell selects it too, not its
        // right-hand neighbour: a multi-click addresses the glyph under the pointer, whereas a
        // single click resolves the nearer caret boundary
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(600), "lapse");
        await surface.Pointer.ClickAsync(input, new Point(4, 0));
        input.CaretIndex.ShouldBe(4);
        await surface.Pointer.ClickAsync(input, new Point(4, 0));
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("界");
        surface.ShouldReverse(0, [3, 4], [2, 5]);

        // Act and assert - the punctuation after it is its own non-word grapheme
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(600), "lapse");
        await surface.Pointer.ClickAsync(input, new Point(5, 0));
        await surface.Pointer.ClickAsync(input, new Point(5, 0));
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("-");
    }

    /// <summary>Verifies triple-click in a multiline editor selects only the clicked logical line.</summary>
    [Fact]
    public async Task Pointer_WhenTripleClickedOnMultiline_SelectsOnlyThatLineAsync()
    {
        // Arrange
        var input = Editor("one\ntwo\nthree", 8, 3, multiline: true);
        var time = new ManualTimeProvider();
        await using var surface = await MountAsync(input, 8, 3, time);
        var target = new Point(1, 1);

        // Act
        await surface.Pointer.ClickAsync(input, target);
        await surface.Pointer.ClickAsync(input, target);
        await surface.Pointer.ClickAsync(input, target);

        // Assert
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("two");
        input.SelectionStart.ShouldBe(4);
        input.SelectionLength.ShouldBe(3);
        surface.ShouldReverse(0, [], [0, 1, 2]);
        surface.ShouldReverse(1, [0, 1, 2], [3]);
        surface.ShouldReverse(2, [], [0, 1, 2, 3, 4]);
    }

    /// <summary>Verifies a right-to-left drag selects backward with the caret at the range start.</summary>
    [Fact]
    public async Task Pointer_WhenDraggedBackward_SelectsWithCaretAtStartAsync()
    {
        // Arrange
        var input = Editor("abcdef", 8);
        await using var surface = await MountAsync(input, 8);

        // Act
        await surface.Pointer.DragAsync(input, new Point(5, 0), new Point(2, 0));

        // Assert
        input.SelectionStart.ShouldBe(2);
        input.SelectionLength.ShouldBe(3);
        input.CaretIndex.ShouldBe(2);
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("cde");
        surface.ShouldReverse(0, [2, 3, 4], [0, 1, 5]);
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(input);
    }

    /// <summary>Verifies dragging past the right edge of a narrow editor extends the selection to
    /// the end of the line and scrolls the viewport so the caret stays visible; releasing outside
    /// the control keeps the selection and drops capture.</summary>
    [Fact]
    public async Task Pointer_WhenDraggedPastTheRightEdge_ExtendsToLineEndAndScrollsAsync()
    {
        // Arrange
        var input = new TextInput { Text = "abcdefgh", ScrollBars = ScrollBars.None, Width = Length.Cells(4), Height = Length.Cells(1) };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(10, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        surface.ShouldRender("abcd");

        // Act
        await surface.Pointer.MoveToAsync(input, new Point(1, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(7, 0));
        surface.ShouldHaveCapture(input);

        // Assert - the press anchored on the cell the user saw (no focus-time scroll jump), the
        // selection reaches the glyph under the pointer, and the viewport followed the caret
        input.SelectionStart.ShouldBe(1);
        input.CaretIndex.ShouldBe(7);
        input.HorizontalOffset.ShouldBe(4);
        surface.ShouldRender("efgh");
        surface.ShouldReverse(0, [0, 1, 2], [3]);
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);

        // Act - release outside the editor
        await surface.Pointer.MovePressedToAsync(new Point(8, 1));
        await surface.Pointer.ReleaseAsync();

        // Assert - beyond the last glyph resolves to the line end
        surface.ShouldHaveCapture(null);
        input.SelectionStart.ShouldBe(1);
        input.SelectionLength.ShouldBe(7);
        input.HorizontalOffset.ShouldBe(5);
        surface.ShouldRender("fgh");
        surface.ShouldHaveFocus(input);
    }

    /// <summary>Verifies dragging downward across rows selects across the line break, and
    /// dragging below the viewport of a scrolled editor extends to the last line and scrolls.</summary>
    [Fact]
    public async Task Pointer_WhenDraggedAcrossRowsAndBelowTheViewport_SelectsAcrossBreaksAndScrollsAsync()
    {
        // Arrange
        var input = new TextInput
        {
            AcceptsReturn = true,
            Text = "ab\ncd\nef\ngh",
            CaretIndex = 0,
            ScrollBars = ScrollBars.None,
            Width = Length.Cells(4),
            Height = Length.Cells(2)
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(6, 4),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("a");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("c");

        // Act - drag from the first row into the second
        await surface.Pointer.MoveToAsync(input, new Point(1, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(1, 1));

        // Assert
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("b\nc");

        // Act - drag below the editor's bottom row
        await surface.Pointer.MovePressedToAsync(new Point(1, 3));

        // Assert - selection reaches the last line and the viewport scrolled to reveal it
        input.CaretIndex.ShouldBe(10);
        input.VerticalOffset.ShouldBe(2);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("e");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("g");
        surface.ShouldReverse(1, [0], [2]);
        surface.ShouldHaveCursor(new Point(1, 1), visible: true);
        await surface.Pointer.ReleaseAsync();
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("b\ncd\nef\ng");
    }

    /// <summary>Verifies hover toggles the pointer-over state without focusing, and leaving
    /// clears it.</summary>
    [Fact]
    public async Task Pointer_WhenHoveredAndLeft_TracksPointerOverWithoutFocusAsync()
    {
        // Arrange
        var input = Editor("ab", 4);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(10, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Pointer.MoveToAsync(input);
        surface.ShouldHaveState(input, VisualState.IsPointerOver);
        input.IsFocused.ShouldBeFalse();
        await surface.Pointer.MoveToAsync(new Point(8, 1));
        surface.ShouldHaveState(input, VisualState.Normal);
        await surface.Pointer.MoveToAsync(input);
        surface.ShouldHaveState(input, VisualState.IsPointerOver);
        await surface.Pointer.LeaveAsync();
        input.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies a wheel scroll on a focused editor moves the viewport away from the caret,
    /// and the next keystroke chases the caret back into view.</summary>
    [Fact]
    public async Task Pointer_WhenWheeledAwayFromTheCaret_NextEditRevealsTheCaretAsync()
    {
        // Arrange
        var input = Editor("1\n2\n3\n4", 4, 2, multiline: true);
        await using var surface = await MountAsync(input, 4, 2);
        await surface.Keyboard.PressAsync(Code.Tab);
        input.VerticalOffset.ShouldBe(2);
        surface.ShouldRender("""
                             3
                             4
                             """);

        // Act - wheel up twice
        await surface.Pointer.WheelAsync(input, default, wheelY: 1);
        await surface.Pointer.WheelAsync(input, default, wheelY: 1);

        // Assert - viewport moved, caret unchanged and off-screen
        input.VerticalOffset.ShouldBe(0);
        input.CaretIndex.ShouldBe(7);
        surface.ShouldRender("""
                             1
                             2
                             """);
        surface.ShouldHaveCursor(default, visible: false);

        // Act - typing reveals the caret again
        await surface.Keyboard.TypeAsync("x");

        // Assert
        input.VerticalOffset.ShouldBe(2);
        surface.ShouldRender("""
                             3
                             4x
                             """);
        surface.ShouldHaveCursor(new Point(2, 1), visible: true);
    }

    /// <summary>Verifies a right-click opens the default context menu with its documented items
    /// painted, Escape closes it, and focus stays on the editor.</summary>
    [Fact]
    public async Task ContextMenu_WhenOpenedByRightClickThenEscape_PaintsItemsAndClosesAsync()
    {
        // Arrange
        var input = new TextInput { Text = "hello", Width = Length.Cells(12), Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(30, 14),
            TestContext.Current.CancellationToken);
        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        await surface.Pointer.ClickAsync(input, new Point(1, 1));
        surface.ShouldHaveFocus(input);

        // Act
        await surface.Pointer.RightClickAsync(input, new Point(2, 1));

        // Assert - open and painted
        menu.IsOpen.ShouldBeTrue();
        var popup = (Popup) menu.Presentation;
        var bounds = popup.SurfaceBounds;
        bounds.X.ShouldBe(2);
        bounds.Y.ShouldBe(1);
        var rows = new List<string>();

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            var row = new StringBuilder();

            for (var x = bounds.X; x < bounds.Right; x++)
            {
                _ = row.Append(surface.Cell(new Point(x, y)).Text);
            }

            rows.Add(row.ToString());
        }

        rows.ShouldContain(row => row.Contains("Undo", StringComparison.Ordinal) && row.Contains("Ctrl+Z", StringComparison.Ordinal));
        rows.ShouldContain(row => row.Contains("Cut", StringComparison.Ordinal));
        rows.ShouldContain(row => row.Contains("Paste", StringComparison.Ordinal));
        rows.ShouldContain(row => row.Contains("Select All", StringComparison.Ordinal));

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        menu.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(input);
        surface.Cell(new Point(bounds.X, bounds.Y + 2)).Text.ShouldNotBe("U");
    }

    /// <summary>Verifies each context-menu command invoked by pointer acts on the editor: Select
    /// All, Cut (publishing to the clipboard), Paste, Undo, and Redo.</summary>
    [Fact]
    public async Task ContextMenu_WhenItemsAreClicked_RunEachEditorCommandAsync()
    {
        // Arrange
        var input = new TextInput { Text = "hello", Width = Length.Cells(12), Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(30, 14),
            TestContext.Current.CancellationToken);
        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        var undo = (MenuItem) menu.Items[0];
        var redo = (MenuItem) menu.Items[1];
        var cut = (MenuItem) menu.Items[3];
        var copy = (MenuItem) menu.Items[4];
        var paste = (MenuItem) menu.Items[5];
        var selectAll = (MenuItem) menu.Items[7];

        // Act and assert - Select All
        await surface.Pointer.RightClickAsync(input, new Point(2, 1));
        await surface.Pointer.ClickAsync(selectAll);
        menu.IsOpen.ShouldBeFalse();
        input.SelectionStart.ShouldBe(0);
        input.SelectionLength.ShouldBe(5);

        // Act and assert - Cut publishes and removes
        await surface.Pointer.RightClickAsync(input, new Point(2, 1));
        cut.IsEnabled.ShouldBeTrue();
        copy.IsEnabled.ShouldBeTrue();
        await surface.Pointer.ClickAsync(cut);
        input.Text.ShouldBe(string.Empty);

        // Act and assert - Paste restores from the application clipboard
        await surface.Pointer.RightClickAsync(input, new Point(2, 1));
        paste.IsEnabled.ShouldBeTrue();
        await surface.Pointer.ClickAsync(paste);
        input.Text.ShouldBe("hello");
        input.CaretIndex.ShouldBe(5);

        // Act and assert - Undo reverts the paste, Redo reapplies it
        await surface.Pointer.RightClickAsync(input, new Point(2, 1));
        undo.IsEnabled.ShouldBeTrue();
        await surface.Pointer.ClickAsync(undo);
        input.Text.ShouldBe(string.Empty);
        await surface.Pointer.RightClickAsync(input, new Point(2, 1));
        redo.IsEnabled.ShouldBeTrue();
        await surface.Pointer.ClickAsync(redo);
        input.Text.ShouldBe("hello");
        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a primary click outside the open context menu light-dismisses it, and a
    /// right-click on a disabled editor never opens it.</summary>
    [Fact]
    public async Task ContextMenu_WhenClickedOutsideOrEditorDisabled_ClosesOrNeverOpensAsync()
    {
        // Arrange
        var input = new TextInput { Text = "hello", Width = Length.Cells(12), Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(30, 14),
            TestContext.Current.CancellationToken);
        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();

        // Act and assert - light dismiss on the editor itself
        await surface.Pointer.RightClickAsync(input, new Point(2, 1));
        menu.IsOpen.ShouldBeTrue();
        await surface.Pointer.ClickAsync(input, new Point(1, 1));
        menu.IsOpen.ShouldBeFalse();

        // Act and assert - disabled editor
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable editor");
        await surface.Pointer.RightClickAsync(input, new Point(2, 1));
        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies word wrap reflows typed text across rows, scrolls vertically to keep the
    /// caret on screen, and unwinds when the text shrinks - asserting the visible rows each step.</summary>
    [Fact]
    public async Task Keyboard_WhenTypingUnderWordWrap_ReflowsAndScrollsToKeepTheCaretVisibleAsync()
    {
        // Arrange
        var input = Editor(string.Empty, 5, 2, multiline: true, wrap: true);
        await using var surface = await MountAsync(input, 5, 2);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("aaaa bbbb");

        // Assert - two visual rows, caret at the end of the second
        surface.ShouldRender("""
                             aaaa
                             bbbb
                             """);
        surface.ShouldHaveCursor(new Point(4, 1), visible: true);
        input.VerticalOffset.ShouldBe(0);

        // Act - a third visual row pushes the first out of view
        await surface.Keyboard.TypeAsync(" cccc");

        // Assert
        input.VerticalOffset.ShouldBe(1);
        surface.ShouldRender("""
                             bbbb
                             cccc
                             """);
        surface.ShouldHaveCursor(new Point(4, 1), visible: true);

        // Act - deleting the third row unwinds the scroll
        for (var i = 0; i < 5; i++)
        {
            await surface.Keyboard.PressAsync(Code.Backspace);
        }

        // Assert
        input.Text.ShouldBe("aaaa bbbb");
        input.VerticalOffset.ShouldBe(0);
        surface.ShouldRender("""
                             aaaa
                             bbbb
                             """);
        surface.ShouldHaveCursor(new Point(4, 1), visible: true);

        // Act and assert - Up moves to the same column on the previous visual row
        await surface.Keyboard.PressAsync(Code.Up);
        input.CaretIndex.ShouldBe(4);
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);
    }

    /// <summary>Verifies an explicit line break under word wrap starts a new visual row, and
    /// Home/End move within the visual row only.</summary>
    [Fact]
    public async Task Keyboard_WhenWrappedTextHasHardBreaks_HomeAndEndStayOnTheVisualRowAsync()
    {
        // Arrange
        var input = Editor("abcdef\nxy", 4, 3, multiline: true, wrap: true);
        await using var surface = await MountAsync(input, 4, 3);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldRender("""
                             abcd
                             ef
                             xy
                             """);
        surface.ShouldHaveCursor(new Point(2, 2), visible: true);

        // Act and assert - Up lands on the wrapped tail; Home and End address the logical line,
        // so Home from the tail returns to the very start of the wrapped line
        await surface.Keyboard.PressAsync(Code.Up);
        surface.ShouldHaveCursor(new Point(2, 1), visible: true);
        await surface.Keyboard.PressAsync(Code.Home);
        input.CaretIndex.ShouldBe(0);
        surface.ShouldHaveCursor(new Point(0, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.End);
        input.CaretIndex.ShouldBe(6);
        surface.ShouldHaveCursor(new Point(2, 1), visible: true);
        await surface.Keyboard.PressAsync(Code.Down);
        input.CaretIndex.ShouldBe(9);
        surface.ShouldHaveCursor(new Point(2, 2), visible: true);
    }

    /// <summary>Verifies a non-wrapped multiline editor scrolls vertically with the caret through
    /// Up/Down and PageUp/PageDown, rendering the correct rows each time.</summary>
    [Fact]
    public async Task Keyboard_WhenNavigatingVerticallyPastTheViewport_ScrollsRowsWithTheCaretAsync()
    {
        // Arrange
        var input = Editor("1\n2\n3\n4\n5", 4, 2, multiline: true);
        await using var surface = await MountAsync(input, 4, 2);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldRender("""
                             4
                             5
                             """);
        surface.ShouldHaveCursor(new Point(1, 1), visible: true);

        // Act and assert - Up twice scrolls one row
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Up);
        input.CaretIndex.ShouldBe(5);
        surface.ShouldRender("""
                             3
                             4
                             """);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);

        // Act and assert - PageUp moves by the viewport height and scrolls to the top
        await surface.Keyboard.PressAsync(Code.PageUp);
        input.CaretIndex.ShouldBe(1);
        surface.ShouldRender("""
                             1
                             2
                             """);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);

        // Act and assert - PageDown twice reaches the last row
        await surface.Keyboard.PressAsync(Code.PageDown);
        input.CaretIndex.ShouldBe(5);
        surface.ShouldRender("""
                             2
                             3
                             """);
        await surface.Keyboard.PressAsync(Code.PageDown);
        input.CaretIndex.ShouldBe(9);
        surface.ShouldRender("""
                             4
                             5
                             """);
        surface.ShouldHaveCursor(new Point(1, 1), visible: true);
    }

    /// <summary>Verifies typing past the right edge scrolls one column per character keeping the
    /// caret visible, and Home scrolls back to column zero.</summary>
    [Fact]
    public async Task Keyboard_WhenTypingPastTheRightEdge_ScrollsOneColumnPerCharacterAsync()
    {
        // Arrange
        var input = Editor(string.Empty, 3);
        await using var surface = await MountAsync(input, 3);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.TypeAsync("ab");
        surface.ShouldRender("ab");
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
        await surface.Keyboard.TypeAsync("c");
        input.HorizontalOffset.ShouldBe(1);
        surface.ShouldRender("bc");
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
        await surface.Keyboard.TypeAsync("d");
        surface.ShouldRender("cd");
        await surface.Keyboard.PressAsync(Code.Home);
        input.HorizontalOffset.ShouldBe(0);
        surface.ShouldRender("abc");
        surface.ShouldHaveCursor(new Point(0, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.End);
        surface.ShouldRender("cd");
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
    }

    /// <summary>Verifies Shift+Down under word wrap extends the selection to the same visual
    /// column on the next visual row, painting both partial rows.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftDownUnderWordWrap_ExtendsAcrossVisualRowsAsync()
    {
        // Arrange
        var input = Editor("abcdefgh", 4, 2, wrap: true);
        await using var surface = await MountAsync(input, 4, 2);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.CaretIndex = 1, "caret after 'a'");

        // Act
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Shift);

        // Assert
        input.SelectionStart.ShouldBe(1);
        input.CaretIndex.ShouldBe(5);
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("bcde");
        surface.ShouldReverse(0, [1, 2, 3], [0]);
        surface.ShouldReverse(1, [0], [1, 2, 3]);
        surface.ShouldHaveCursor(new Point(1, 1), visible: true);
    }

    /// <summary>Verifies pressing a visible cell of an unfocused editor whose caret sits out of
    /// view places the caret on that cell without scrolling the viewport first, while keyboard
    /// focus still reveals the stored caret. Before the fix, the press focused the editor and the
    /// focus hook scrolled the old caret into view before the press was hit-tested, so the caret
    /// landed on whatever had scrolled under the pointer and the text jumped.</summary>
    [Fact]
    public async Task Pointer_WhenUnfocusedOverflowingEditorIsPressed_PlacesCaretOnThePressedCellWithoutScrollingAsync()
    {
        // Arrange
        var input = new TextInput { Text = "abcdefgh", ScrollBars = ScrollBars.None, Width = Length.Cells(4), Height = Length.Cells(1) };
        var button = new Button { Text = "Go", Height = Length.Cells(1) };
        var root = new Stack { Children = { input, button } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(10, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        input.CaretIndex.ShouldBe(8);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("a");

        // Act - click the visible 'b'
        await surface.Pointer.ClickAsync(input, new Point(1, 0));

        // Assert - caret before 'b', viewport untouched, cursor on the pressed cell
        surface.ShouldHaveFocus(input);
        input.CaretIndex.ShouldBe(1);
        input.HorizontalOffset.ShouldBe(0);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("a");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("d");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);

        // Act - move the caret to the end, leave by keyboard, scroll back with the wheel, return by keyboard
        await surface.Keyboard.PressAsync(Code.End);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("f");
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(button);
        await surface.Pointer.WheelAsync(input, default, wheelX: -1);
        await surface.Pointer.WheelAsync(input, default, wheelX: -1);
        input.HorizontalOffset.ShouldBe(3);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);

        // Assert - keyboard focus still chases the stored caret into view
        surface.ShouldHaveFocus(input);
        input.HorizontalOffset.ShouldBe(5);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("f");
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);
    }
}
