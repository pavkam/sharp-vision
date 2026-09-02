// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

using SharpVision.Tests.Support;

/// <summary>Verifies the shared text-selection system across Text children of a selection-owning
/// Stack: document ordering, Unicode-safe ranges, keyboard extension, and clipboard copy.</summary>
public sealed class TextSelectionInteractionTests
{
    private static Task<ComponentSurface> MountAsync(ControlBase control, int width, int height, ManualTimeProvider? time = null) =>
        time is null
            ? ComponentSurface.MountAsync(control, new Size(width, height), TestThemes.BorderlessContainer, TestContext.Current.CancellationToken)
            : ComponentSurface.MountAsync(control, new Size(width, height), time, TestThemes.BorderlessContainer, TestContext.Current.CancellationToken);

    private static void ShouldHighlight(ComponentSurface surface, int y, int[] selected, int[] unselected)
    {
        var background = TerminalPalette.Project(
            surface.Application.Theme.ResolveColor(SemanticColor.SelectedControl),
            ColorDepth.Basic16);

        foreach (var x in selected)
        {
            surface.Cell(new Point(x, y)).Style.Background.ShouldBe(background, $"cell ({x},{y}) should be selected");
        }

        foreach (var x in unselected)
        {
            surface.Cell(new Point(x, y)).Style.Background.ShouldNotBe(background, $"cell ({x},{y}) should not be selected");
        }
    }

    private static Stack Owner(params ControlBase[] children)
    {
        var owner = new Stack { IsFocusable = true, IsTextSelectionEnabled = true };

        foreach (var child in children)
        {
            owner.Children.Add(child);
        }

        return owner;
    }

    /// <summary>Verifies a backward drag across three stacked children keeps the anchor and caret
    /// directional, yields the selected text in document order, and paints only the cells inside
    /// the range - including both cells of a wide glyph.</summary>
    [Fact]
    public async Task Pointer_WhenDraggedBackwardAcrossStackedChildren_SelectsInDocumentOrderAsync()
    {
        // Arrange
        var owner = Owner(new ControlText("ab"), new ControlText("界c"), new ControlText("de"));
        await using var surface = await MountAsync(owner, 4, 3);

        // Act
        await surface.Pointer.MoveToAsync(owner.Children[2], new Point(1, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(owner.Children[0], new Point(1, 0));
        surface.ShouldHaveCapture(owner);
        await surface.Pointer.ReleaseAsync();

        // Assert
        var (selection, selected) = await surface.ReadAsync(() => (owner.TextSelection, owner.SelectedText));
        selection.ShouldBe(new Selection(5, 1));
        selected.ShouldBe("b界cd");
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(owner);
        ShouldHighlight(surface, 0, [1], [0]);
        ShouldHighlight(surface, 1, [0, 1, 2], [3]);
        ShouldHighlight(surface, 2, [0], [1]);
    }

    /// <summary>Verifies Control+A on the focused owner selects the aggregated stream in child
    /// order (reversed for a reversed Stack) and Control+C publishes it so a TextInput can paste it.</summary>
    [Theory]
    [InlineData(false, "ab界")]
    [InlineData(true, "界ab")]
    public async Task Keyboard_WhenControlAThenControlC_CopiesTheAggregatedStreamInChildOrderAsync(bool reverse, string expected)
    {
        // Arrange
        var owner = Owner(new ControlText("ab"), new ControlText("界"));
        owner.Reverse = reverse;
        var target = new TextInput { ScrollBars = ScrollBars.None, Width = Length.Cells(8), Height = Length.Cells(1) };
        var root = new Stack { Children = { owner, target } };
        await using var surface = await MountAsync(root, 8, 3);
        await surface.FocusAsync(owner);

        // Act
        await surface.ControlAsync('a');
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe(expected);
        await surface.ControlAsync('c');
        await surface.FocusAsync(target);
        await surface.ControlAsync('v');

        // Assert
        target.Text.ShouldBe(expected);
    }

    /// <summary>Verifies Shift+End, Home, Shift+Right, and Control+Shift+Right extend and collapse
    /// across the boundary between horizontal siblings as one continuous stream.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftNavigationCrossesSiblings_ExtendsAcrossTheBoundaryAsync()
    {
        // Arrange
        var owner = Owner(new ControlText("ab"), new ControlText("cd"));
        owner.Orientation = Orientation.Horizontal;
        await using var surface = await MountAsync(owner, 6, 1);
        await surface.FocusAsync(owner);
        await surface.UpdateAsync(() => owner.SetTextSelection(new Selection(1, 1)), "caret after 'a'");

        // Act and assert - Shift+End reaches the end of the second sibling
        await surface.Keyboard.PressAsync(Code.End, Modifiers.Shift);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(1, 4));
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe("bcd");
        ShouldHighlight(surface, 0, [1, 2, 3], [0]);

        // Act and assert - Home collapses at the row start
        await surface.Keyboard.PressAsync(Code.Home);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(0, 0));
        ShouldHighlight(surface, 0, [], [0, 1, 2, 3]);

        // Act and assert - Shift+Right walks graphemes across the sibling seam
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe("abc");
        ShouldHighlight(surface, 0, [0, 1, 2], [3]);

        // Act and assert - Control+Shift+Right finishes the word spanning both siblings
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Control | Modifiers.Shift);
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe("abcd");

        // Act and assert - Left without Shift collapses to the start
        await surface.Keyboard.PressAsync(Code.Left);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(0, 0));
    }

    /// <summary>Verifies a hidden child contributes nothing to the stream, and showing it again
    /// changes the stream identity so the stale range is cleared rather than remapped.</summary>
    [Fact]
    public async Task Selection_WhenChildVisibilityChanges_ExcludesHiddenTextAndClearsStaleRangeAsync()
    {
        // Arrange
        var hidden = new ControlText("cd") { Visibility = Visibility.Collapsed };
        var owner = Owner(new ControlText("ab"), hidden, new ControlText("ef"));
        await using var surface = await MountAsync(owner, 4, 3);
        await surface.FocusAsync(owner);

        // Act
        await surface.ControlAsync('a');

        // Assert
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe("abef");
        ShouldHighlight(surface, 0, [0, 1], []);
        ShouldHighlight(surface, 1, [0, 1], []);

        // Act
        await surface.UpdateAsync(() => hidden.Visibility = Visibility.Visible, "show the middle child");

        // Assert
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe(string.Empty);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(default);
        ShouldHighlight(surface, 0, [], [0, 1]);
        ShouldHighlight(surface, 1, [], [0, 1]);
        ShouldHighlight(surface, 2, [], [0, 1]);

        // Act and assert - the stream now includes the shown child
        await surface.ControlAsync('a');
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe("abcdef");
    }

    /// <summary>Verifies a double-click on the trailing cell of a wide glyph inside a Text child
    /// selects that glyph, and a triple-click selects the child's visual row.</summary>
    [Fact]
    public async Task Pointer_WhenWideGlyphIsMultiClicked_SelectsTheGlyphThenTheRowAsync()
    {
        // Arrange
        var text = new ControlText("ab 界-c");
        var owner = Owner(text, new ControlText("zz"));
        var time = new ManualTimeProvider();
        await using var surface = await MountAsync(owner, 8, 2, time);
        var trailing = new Point(4, 0);

        // Act - double click
        await surface.Pointer.ClickAsync(text, trailing);
        await surface.Pointer.ClickAsync(text, trailing);

        // Assert
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe("界");
        ShouldHighlight(surface, 0, [3, 4], [2, 5]);

        // Act - triple click
        await surface.Pointer.ClickAsync(text, trailing);

        // Assert - only the first row, not the sibling below
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe("ab 界-c");
        ShouldHighlight(surface, 0, [0, 5], []);
        ShouldHighlight(surface, 1, [], [0, 1]);
    }

    /// <summary>Verifies an owner with text selection disabled ignores Control+A and never starts
    /// a drag gesture or takes capture.</summary>
    [Fact]
    public async Task Selection_WhenDisabledOnTheOwner_IgnoresControlAAndDragsAsync()
    {
        // Arrange
        var owner = Owner(new ControlText("ab"), new ControlText("cd"));
        owner.IsTextSelectionEnabled = false;
        await using var surface = await MountAsync(owner, 4, 2);
        await surface.FocusAsync(owner);

        // Act
        await surface.ControlAsync('a');
        await surface.Pointer.MoveToAsync(owner.Children[0], new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(owner.Children[1], new Point(1, 0));

        // Assert
        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Idle);
        surface.ShouldHaveCapture(null);
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe(string.Empty);
        ShouldHighlight(surface, 0, [], [0, 1]);
        ShouldHighlight(surface, 1, [], [0, 1]);
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies Shift+Right and Shift+Left move by whole extended grapheme clusters -
    /// combining marks and ZWJ emoji - across a Text child.</summary>
    [Fact]
    public async Task Keyboard_WhenExtendingOverComplexGraphemes_MovesByWholeClustersAsync()
    {
        // Arrange
        const string emoji = "👩‍💻";
        var owner = Owner(new ControlText("é" + emoji + "x"));
        await using var surface = await MountAsync(owner, 8, 1);
        await surface.FocusAsync(owner);
        await surface.UpdateAsync(() => owner.SetTextSelection(default), "caret at start");

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(0, 2));
        ShouldHighlight(surface, 0, [0], [1, 2, 3]);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(0, 2 + emoji.Length));
        ShouldHighlight(surface, 0, [0, 1, 2], [3]);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(0, 2));
        await surface.Keyboard.PressAsync(Code.End, Modifiers.Shift);
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe("é" + emoji + "x");
    }

    /// <summary>Verifies Down through a shorter row remembers the desired column and lands on it
    /// again on the next longer row.</summary>
    [Fact]
    public async Task Keyboard_WhenMovingDownThroughAShorterRow_RemembersTheDesiredColumnAsync()
    {
        // Arrange
        var owner = Owner(new ControlText("abc"), new ControlText("d"), new ControlText("efg"));
        await using var surface = await MountAsync(owner, 4, 3);
        await surface.FocusAsync(owner);
        await surface.UpdateAsync(() => owner.SetTextSelection(new Selection(2, 2)), "caret after 'b'");

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Down);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(4, 4));
        await surface.Keyboard.PressAsync(Code.Down);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(6, 6));
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Up);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(2, 2));
    }

    /// <summary>Verifies PageDown and Shift+PageUp page through the visible rows of a scrolling
    /// owner and paint the extended range.</summary>
    [Fact]
    public async Task Keyboard_WhenPagingThroughAScrollingOwner_MovesByThePageDistanceAsync()
    {
        // Arrange
        var owner = Owner(
            new ControlText("1"),
            new ControlText("2"),
            new ControlText("3"),
            new ControlText("4"),
            new ControlText("5"),
            new ControlText("6"));
        owner.AutoScroll = true;
        owner.ScrollBars = ScrollBars.Vertical;
        owner.Height = Length.Cells(3);
        await using var surface = await MountAsync(owner, 4, 3);
        await surface.FocusAsync(owner);
        await surface.UpdateAsync(() => owner.SetTextSelection(default), "caret at start");
        // Act and assert - the aggregated projection only carries geometry for the rows inside
        // the viewport, so a page lands on the last visible row rather than beyond it
        await surface.Keyboard.PressAsync(Code.PageDown);
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(2, 2));
        await surface.ShiftPageUpAsync();
        (await surface.ReadAsync(() => owner.TextSelection)).ShouldBe(new Selection(2, 0));
        (await surface.ReadAsync(() => owner.SelectedText)).ShouldBe("12");
        ShouldHighlight(surface, 0, [0], []);
        ShouldHighlight(surface, 1, [0], []);
        ShouldHighlight(surface, 2, [], [0]);
    }
}
