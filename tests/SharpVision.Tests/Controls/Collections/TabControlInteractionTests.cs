// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TabControl interaction through mounted surfaces: Delete-key closure with
/// cancellation and repair, disabled and hidden headers, overflow under Scroll and Clip policies
/// across resizes, fixed header widths, wide headers, and focus traversal into page content.</summary>
public sealed class TabControlInteractionTests
{
    /// <summary>Verifies Delete on a closeable selected page raises CloseRequested, a cancelling
    /// handler keeps the page, an accepted close removes it and repairs selection to the nearest
    /// eligible page, and Delete on a non-closeable page does nothing.</summary>
    [Fact]
    public async Task Keyboard_WhenDeleteClosesSelectedTab_HonorsCancellationAndRepairsSelectionAsync()
    {
        // Arrange
        var tabs = CreateTabs("One", "Two", "Three");
        tabs.Items[1].IsClosable = true;
        var requests = new List<string>();
        var cancel = true;
        tabs.CloseRequested += (_, eventArgs) =>
        {
            requests.Add(eventArgs.Item.HeaderText);
            eventArgs.Cancel = cancel;
        };
        var selections = new List<(int Previous, int Current)>();
        tabs.SelectionChanged += (_, eventArgs) => selections.Add((eventArgs.PreviousIndex, eventArgs.CurrentIndex));
        await using var surface = await ComponentSurface.MountAsync(tabs, new Size(30, 4), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedIndex.ShouldBe(1);

        // Act cancelled close
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        requests.ShouldBe(["Two"]);
        tabs.Items.Count.ShouldBe(3);
        tabs.SelectedIndex.ShouldBe(1);
        RowText(surface, 0, 22).ShouldBe(" One │ Two │ Three    ");

        // Act accepted close
        cancel = false;
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert removal and repair
        requests.ShouldBe(["Two", "Two"]);
        tabs.Items.Count.ShouldBe(2);
        tabs.SelectedIndex.ShouldBe(1);
        tabs.SelectedItem.ShouldNotBeNull().HeaderText.ShouldBe("Three");
        selections.ShouldBe([(0, 1), (1, 1)]);
        RowText(surface, 0, 22).ShouldBe(" One │ Three          ");
        RowText(surface, 2, 22).ShouldBe("Three content         ");

        // Act Delete on a page that is not closeable
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        requests.Count.ShouldBe(2);
        tabs.Items.Count.ShouldBe(2);
        tabs.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies a disabled header ignores pointer selection and a collapsed page removes
    /// its header from the strip so the following header slides left.</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsDisabledOrPageIsCollapsed_DoesNotSelectAndReflowsStripAsync()
    {
        // Arrange
        var tabs = CreateTabs("One", "Two", "Three");
        tabs.Items[1].IsEnabled = false;
        var changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(tabs, new Size(30, 4), TestContext.Current.CancellationToken);

        // Act click the disabled header
        await surface.Pointer.ClickAsync(tabs.HeaderAt(1));

        // Assert
        tabs.SelectedIndex.ShouldBe(0);
        changes.ShouldBe(0);
        surface.ShouldHaveState(tabs.HeaderAt(1), VisualState.Disabled);

        // Act collapse the disabled page
        await surface.UpdateAsync(() => tabs.Items[1].Visibility = Visibility.Collapsed, "collapse page");

        // Assert the strip reflows and the collapsed header owns no cell to click
        RowText(surface, 0, 22).ShouldBe(" One │ Three          ");
        RowText(surface, 0, 22).ShouldNotContain("Two");

        // Act click where Three now sits
        await surface.Pointer.ClickAsync(tabs.HeaderAt(2));

        // Assert
        tabs.SelectedIndex.ShouldBe(2);
        changes.ShouldBe(1);
        RowText(surface, 2, 22).ShouldBe("Three content         ");
    }

    /// <summary>Verifies the Scroll overflow policy keeps the selected header revealed through a
    /// shrinking resize and returns the strip to its origin once the width grows again.</summary>
    [Fact]
    public async Task Resize_WhenScrollPolicyOverflows_RevealsSelectedHeaderAndRestoresOriginAsync()
    {
        // Arrange
        var tabs = CreateTabs("Alpha", "Bravo", "Charlie", "Delta", "Echo");
        tabs.HeaderOverflowPolicy = TabHeaderOverflowPolicy.Scroll;
        await using var surface = await ComponentSurface.MountAsync(tabs, new Size(44, 4), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);
        tabs.SelectedIndex.ShouldBe(4);
        tabs.HeaderAt(0).Bounds.X.ShouldBe(0);
        tabs.HeaderAt(4).Bounds.Right.ShouldBeLessThanOrEqualTo(44);

        // Act shrink
        await surface.ResizeAsync(new Size(20, 4));

        // Assert the selected header is inside the strip and the first scrolled away
        var echo = tabs.HeaderAt(4).Bounds;
        echo.Right.ShouldBeLessThanOrEqualTo(20);
        echo.X.ShouldBeGreaterThanOrEqualTo(0);
        tabs.HeaderAt(0).Bounds.X.ShouldBeLessThan(0);
        surface.Cell(new Point(echo.X + 1, 0)).Text.ShouldBe("E");
        surface.Cell(new Point(echo.X + 4, 0)).Text.ShouldBe("o");
        RowText(surface, 2, 12).ShouldBe("Echo content");

        // Act grow back
        await surface.ResizeAsync(new Size(44, 4));

        // Assert
        tabs.HeaderAt(0).Bounds.X.ShouldBe(0);
        tabs.HeaderAt(4).Bounds.Right.ShouldBeLessThanOrEqualTo(44);
        surface.Cell(new Point(1, 0)).Text.ShouldBe("A");
    }

    /// <summary>Verifies the Clip policy leaves an overflowing header unrendered while keyboard
    /// selection still reaches its page and shows its content.</summary>
    [Fact]
    public async Task Keyboard_WhenClipPolicyHidesHeader_StillSelectsAndShowsThePageAsync()
    {
        // Arrange
        var tabs = CreateTabs("Alpha", "Bravo", "Charlie", "Delta", "Echo");
        await using var surface = await ComponentSurface.MountAsync(tabs, new Size(20, 4), TestContext.Current.CancellationToken);
        RowText(surface, 0, 20).ShouldNotContain("Echo");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);

        // Assert
        tabs.SelectedIndex.ShouldBe(4);
        RowText(surface, 0, 20).ShouldNotContain("Echo");
        RowText(surface, 2, 20).ShouldBe("Echo content        ");

        // Act back to the first header
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert wrap-around
        tabs.SelectedIndex.ShouldBe(0);
        RowText(surface, 2, 20).ShouldBe("Alpha content       ");
    }

    /// <summary>Verifies a fixed HeaderWidth sizes every header identically, clips a long label
    /// inside its cell budget, and keeps the dividers on the fixed grid.</summary>
    [Fact]
    public async Task Render_WhenHeaderWidthIsFixed_ClipsLabelsToTheGridAsync()
    {
        // Arrange
        var tabs = CreateTabs("Configuration", "Go", "Third");
        tabs.HeaderWidth = Length.Cells(8);
        await using var surface = await ComponentSurface.MountAsync(tabs, new Size(30, 4), TestContext.Current.CancellationToken);

        // Assert
        tabs.HeaderAt(0).Bounds.Width.ShouldBe(8);
        tabs.HeaderAt(1).Bounds.Width.ShouldBe(8);
        tabs.HeaderAt(2).Bounds.X.ShouldBe(18);
        RowText(surface, 0, 27).ShouldBe(" Configu│ Go     │ Third   ");

        // Act
        await surface.Pointer.ClickAsync(tabs, new Point(10, 0));

        // Assert
        tabs.SelectedIndex.ShouldBe(1);
        RowText(surface, 2, 10).ShouldBe("Go content");
    }

    /// <summary>Verifies wide-character headers occupy continuation cells, size the header by
    /// cell width, place the divider after the full glyphs, and remain clickable on any cell.</summary>
    [Fact]
    public async Task Render_WhenHeadersAreWide_UsesCellWidthForLayoutAndHitTestingAsync()
    {
        // Arrange
        var tabs = CreateTabs("日本", "Ünï");
        await using var surface = await ComponentSurface.MountAsync(tabs, new Size(20, 4), TestContext.Current.CancellationToken);

        // Assert geometry
        tabs.HeaderAt(0).Bounds.Width.ShouldBe(6);
        surface.Cell(new Point(1, 0)).Text.ShouldBe("日");
        surface.Cell(new Point(2, 0)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(3, 0)).Text.ShouldBe("本");
        surface.Cell(new Point(4, 0)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(6, 0)).Text.ShouldBe("│");
        surface.Cell(new Point(8, 0)).Text.ShouldBe("Ü");
        tabs.HeaderAt(1).Bounds.X.ShouldBe(7);

        // Act click the second header then a continuation cell of the first
        await surface.Pointer.ClickAsync(tabs, new Point(9, 0));
        tabs.SelectedIndex.ShouldBe(1);
        await surface.Pointer.ClickAsync(tabs, new Point(4, 0));

        // Assert
        tabs.SelectedIndex.ShouldBe(0);
        RowText(surface, 2, 10).ShouldBe("日本 conte");
    }

    /// <summary>Verifies Tab continues from the strip into the selected page's focusable content
    /// and Shift+Tab returns, a page switch releases focus from the hidden content, and the
    /// hidden page's content is unreachable until its page is selected again.</summary>
    [Fact]
    public async Task Focus_WhenTraversingIntoPageContentAndSwitchingPages_FollowsTheSelectedPageAsync()
    {
        // Arrange
        var input = new TextInput { Text = "value" };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        tabs.Items.Add(new TabItem { HeaderText = "Edit", Content = input });
        tabs.Items.Add(new TabItem { HeaderText = "View", Content = new ControlText("read only") });
        var trailing = new Button { Text = "After" };
        var stack = new Stack { Children = { tabs, trailing } };
        tabs.Height = Length.Cells(4);
        await using var surface = await ComponentSurface.MountAsync(stack, new Size(30, 8), TestContext.Current.CancellationToken);

        // Act traverse into the page
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(tabs);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(tabs);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);

        // Act switch pages while the content owns focus
        await surface.Pointer.ClickAsync(tabs.HeaderAt(1));

        // Assert focus left the hidden content
        tabs.SelectedIndex.ShouldBe(1);
        input.IsFocused.ShouldBeFalse();
        input.EffectiveIsVisible.ShouldBeFalse();
        surface.Application.Focus.Focused.ShouldNotBeSameAs(input);

        // Act Tab from the strip now skips the hidden input
        await surface.UpdateAsync(() => tabs.Focus().ShouldBeTrue(), "focus strip");
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(trailing);

        // Act reselect the page: content is reachable again but not auto-focused
        await surface.Pointer.ClickAsync(tabs.HeaderAt(0));
        tabs.SelectedIndex.ShouldBe(0);
        input.IsFocused.ShouldBeFalse();
        await surface.UpdateAsync(() => tabs.Focus().ShouldBeTrue(), "focus strip again");
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);
    }

    /// <summary>Verifies selecting a disabled or collapsed page programmatically is rejected
    /// before any state changes.</summary>
    [Fact]
    public void SelectedIndex_WhenTargetPageIsUnavailable_ThrowsAndPreservesSelection()
    {
        var tabs = CreateTabs("One", "Two", "Three");
        tabs.Items[1].IsEnabled = false;
        tabs.Items[2].Visibility = Visibility.Collapsed;
        var changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;

        _ = Should.Throw<InvalidOperationException>(() => tabs.SelectedIndex = 1);
        _ = Should.Throw<InvalidOperationException>(() => tabs.SelectedItem = tabs.Items[2]);

        tabs.SelectedIndex.ShouldBe(0);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies Delete on the only remaining closeable page empties the strip and body,
    /// clears the selection, keeps focus on the TabControl, and leaves every later key inert.</summary>
    [Fact]
    public async Task Keyboard_WhenDeleteClosesTheLastRemainingTab_EmptiesStripAndBodyAsync()
    {
        // Arrange
        var tabs = CreateTabs("Only");
        tabs.Items[0].IsClosable = true;
        var selections = new List<(int Previous, int Current)>();
        tabs.SelectionChanged += (_, eventArgs) => selections.Add((eventArgs.PreviousIndex, eventArgs.CurrentIndex));
        var failures = new List<Exception>();
        await using var surface = await ComponentSurface.MountAsync(tabs, new Size(20, 4), TestContext.Current.CancellationToken);
        surface.Application.UnhandledException += (_, eventArgs) => failures.Add(eventArgs.Exception);
        await surface.Keyboard.PressAsync(Code.Tab);
        RowText(surface, 0, 20).ShouldBe(" Only               ");
        RowText(surface, 2, 20).ShouldBe("Only content        ");

        // Act
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        failures.ShouldBeEmpty();
        tabs.Items.Count.ShouldBe(0);
        tabs.SelectedIndex.ShouldBe(-1);
        tabs.SelectedItem.ShouldBeNull();
        selections.ShouldBe([(0, -1)]);
        surface.ShouldHaveFocus(tabs);
        RowText(surface, 0, 20).ShouldBe("                    ");
        RowText(surface, 2, 20).ShouldBe("                    ");
        RowText(surface, 3, 20).ShouldBe("                    ");

        // Act every navigation key on the empty control
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        failures.ShouldBeEmpty();
        tabs.SelectedIndex.ShouldBe(-1);
        selections.Count.ShouldBe(1);
        RowText(surface, 0, 20).ShouldBe("                    ");
        RowText(surface, 2, 20).ShouldBe("                    ");
    }

    /// <summary>Verifies Delete on the last-index page repairs the selection to its nearest
    /// predecessor, re-rendering the strip without the closed header and the body with the
    /// predecessor's content.</summary>
    [Fact]
    public async Task Keyboard_WhenDeleteClosesTheLastIndexPage_RepairsToThePredecessorAsync()
    {
        // Arrange
        var tabs = CreateTabs("One", "Two", "Three");

        foreach (var item in tabs.Items)
        {
            item.IsClosable = true;
        }

        var selections = new List<(int Previous, int Current)>();
        tabs.SelectionChanged += (_, eventArgs) => selections.Add((eventArgs.PreviousIndex, eventArgs.CurrentIndex));
        await using var surface = await ComponentSurface.MountAsync(tabs, new Size(30, 4), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);
        tabs.SelectedIndex.ShouldBe(2);
        RowText(surface, 2, 22).ShouldBe("Three content         ");

        // Act
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        tabs.Items.Count.ShouldBe(2);
        tabs.SelectedIndex.ShouldBe(1);
        tabs.SelectedItem.ShouldNotBeNull().HeaderText.ShouldBe("Two");
        selections.ShouldBe([(0, 2), (2, 1)]);
        RowText(surface, 0, 22).ShouldBe(" One │ Two            ");
        RowText(surface, 2, 22).ShouldBe("Two content           ");

        // Act again from the new last index
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        tabs.Items.Count.ShouldBe(1);
        tabs.SelectedIndex.ShouldBe(0);
        RowText(surface, 0, 22).ShouldBe(" One                  ");
        RowText(surface, 2, 22).ShouldBe("One content           ");
    }

    /// <summary>Verifies a header only partly inside the strip under the Scroll policy still
    /// selects from its visible cells, and the selection reveals the whole header.</summary>
    [Fact]
    public async Task Pointer_WhenPartiallyClippedHeaderIsClicked_SelectsAndRevealsItAsync()
    {
        // Arrange
        var tabs = CreateTabs("Alpha", "Bravo", "Charlie", "Delta", "Echo");
        tabs.HeaderOverflowPolicy = TabHeaderOverflowPolicy.Scroll;
        await using var surface = await ComponentSurface.MountAsync(tabs, new Size(18, 4), TestContext.Current.CancellationToken);
        var charlie = tabs.HeaderAt(2).Bounds;
        charlie.X.ShouldBeLessThan(18);
        charlie.Right.ShouldBeGreaterThan(18);
        surface.Cell(new Point(charlie.X + 1, 0)).Text.ShouldBe("C");

        // Act
        await surface.Pointer.ClickAsync(tabs, new Point(charlie.X + 1, 0));

        // Assert
        tabs.SelectedIndex.ShouldBe(2);
        var revealed = tabs.HeaderAt(2).Bounds;
        revealed.X.ShouldBeGreaterThanOrEqualTo(0);
        revealed.Right.ShouldBeLessThanOrEqualTo(18);
        surface.Cell(new Point(revealed.X + 1, 0)).Text.ShouldBe("C");
        surface.Cell(new Point(revealed.X + 7, 0)).Text.ShouldBe("e");
        RowText(surface, 2, 15).ShouldBe("Charlie content");
        tabs.HeaderAt(0).Bounds.X.ShouldBeLessThan(0);
    }

    private static TabControl CreateTabs(params string[] headers)
    {
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        foreach (var header in headers)
        {
            tabs.Items.Add(new TabItem { HeaderText = header, Content = new ControlText($"{header} content") });
        }

        return tabs;
    }

    private static string RowText(ComponentSurface surface, int y, int width)
    {
        var text = new StringBuilder();

        for (var x = 0; x < width; x++)
        {
            var cell = surface.Cell(new Point(x, y));

            if (!cell.Continuation)
            {
                _ = text.Append(cell.Text);
            }
        }

        return text.ToString();
    }
}
