// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies every ListView keyboard binding and pointer gesture per selection mode
/// through mounted surfaces: navigation keys, Space and Enter, Ctrl+A, modified keys, empty
/// snapshots, disabled endpoints, pointer press/release/drag/right-click/wheel, focus traversal,
/// visibility changes while focused, and handlers that mutate or dispose the list mid-gesture.</summary>
public sealed class ListViewInteractionTests
{
    private static readonly object?[] _letters = ["A", "B", "C", "D", "E"];

    /// <summary>Verifies each navigation key moves the active row identically in every mode
    /// while only Single and Multiple also move the exclusive selection with it. Twelve rows in a
    /// five-row viewport keep a page step (five rows, one retained overlap row) distinct from the
    /// Home/End endpoints.</summary>
    /// <param name="mode">The selection mode under test.</param>
    [Theory]
    [InlineData(ListSelectionMode.None)]
    [InlineData(ListSelectionMode.Single)]
    [InlineData(ListSelectionMode.Multiple)]
    public async Task Keyboard_WhenNavigationKeysArePressed_MoveActiveRowAndSelectionPerModeAsync(ListSelectionMode mode)
    {
        // Arrange
        var list = CreateList(mode);
        list.Items = Enumerable.Range(0, 12).Select(value => (object?) $"R{value}").ToArray();
        list.ScrollBars = ScrollBars.Vertical;
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(list);
        var selects = mode != ListSelectionMode.None;

        void ShouldSitOn(int index)
        {
            list.ActiveIndex.ShouldBe(index);
            list.SelectedIndex.ShouldBe(selects ? index : -1);
            list.SelectedItems.Count.ShouldBe(selects ? 1 : 0);
        }

        // Act and assert each binding in turn
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        ShouldSitOn(2);

        await surface.Keyboard.PressAsync(Code.End);
        ShouldSitOn(11);
        list.VerticalOffset.ShouldBe(7);

        await surface.Keyboard.PressAsync(Code.Home);
        ShouldSitOn(0);
        list.VerticalOffset.ShouldBe(0);

        await surface.Keyboard.PressAsync(Code.Right);
        ShouldSitOn(1);
        await surface.Keyboard.PressAsync(Code.Left);
        ShouldSitOn(0);
        await surface.Keyboard.PressAsync(Code.Up);
        ShouldSitOn(0);

        await surface.Keyboard.PressAsync(Code.PageDown);
        ShouldSitOn(5);
        list.VerticalOffset.ShouldBe(1);
        await surface.Keyboard.PressAsync(Code.PageDown);
        ShouldSitOn(10);
        list.VerticalOffset.ShouldBe(6);
        await surface.Keyboard.PressAsync(Code.PageUp);
        ShouldSitOn(5);
        list.VerticalOffset.ShouldBe(5);
        await surface.Keyboard.PressAsync(Code.PageUp);
        ShouldSitOn(0);
        list.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies key repeat reports continue navigation exactly like initial presses.</summary>
    [Fact]
    public async Task Keyboard_WhenDownRepeats_ContinuesNavigationPerRepeatAsync()
    {
        // Arrange
        var list = CreateList(ListSelectionMode.Single);
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.RepeatAsync(Code.Down);
        await surface.Keyboard.RepeatAsync(Code.Down);

        // Assert
        list.ActiveIndex.ShouldBe(3);
        list.SelectedIndex.ShouldBe(3);
    }

    /// <summary>Verifies Home and End land on the nearest enabled row when the endpoints are
    /// disabled, and a move past the last enabled row is left unhandled with state unchanged.</summary>
    [Fact]
    public async Task Keyboard_WhenEndpointsAreDisabled_HomeAndEndLandOnNearestEnabledRowAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = CreateList(ListSelectionMode.Single, realized);
        realized[0].IsEnabled = false;
        realized[4].IsEnabled = false;
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.End);
        list.ActiveIndex.ShouldBe(3);
        list.SelectedIndex.ShouldBe(3);

        await surface.Keyboard.PressAsync(Code.Down);
        list.ActiveIndex.ShouldBe(3);
        list.SelectedIndex.ShouldBe(3);

        await surface.Keyboard.PressAsync(Code.Home);
        list.ActiveIndex.ShouldBe(1);
        list.SelectedIndex.ShouldBe(1);

        await surface.Keyboard.PressAsync(Code.Up);
        list.ActiveIndex.ShouldBe(1);
        list.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies an empty snapshot leaves every navigation, activation, and select-all
    /// key inert: no active or selected row, no events, and focus retained.</summary>
    [Fact]
    public async Task Keyboard_WhenItemsAreEmpty_EveryBindingIsInertAsync()
    {
        // Arrange
        var list = new UiListView
        {
            Items = [],
            SelectionMode = ListSelectionMode.Multiple,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var events = 0;
        list.SelectionChanging += (_, _) => events++;
        list.SelectionChanged += (_, _) => events++;
        list.ItemInvoked += (_, _) => events++;
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Keyboard.PressAsync(Code.PageUp);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        await PressControlAAsync(surface);

        // Assert
        surface.ShouldHaveFocus(list);
        list.ActiveIndex.ShouldBe(-1);
        list.SelectedIndex.ShouldBe(-1);
        list.SelectedItems.ShouldBeEmpty();
        events.ShouldBe(0);
        surface.ShouldRender("");
    }

    /// <summary>Verifies Space applies the mode's selection gesture without invoking: a toggle
    /// in Multiple, a no-op re-selection in Single, and nothing at all in None.</summary>
    /// <param name="mode">The selection mode under test.</param>
    [Theory]
    [InlineData(ListSelectionMode.None)]
    [InlineData(ListSelectionMode.Single)]
    [InlineData(ListSelectionMode.Multiple)]
    public async Task Keyboard_WhenSpaceIsPressed_AppliesSelectionGestureWithoutInvokingAsync(ListSelectionMode mode)
    {
        // Arrange
        var list = CreateList(mode);
        var changes = new List<string>();
        var invoked = 0;
        list.SelectionChanged += (_, eventArgs) => changes.Add($"{Join(eventArgs.AddedIndexes)}:{Join(eventArgs.RemovedIndexes)}");
        list.ItemInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        changes.Clear();

        // Act
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        var afterFirst = list.SelectedItems.ToArray();
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        invoked.ShouldBe(0);
        list.ActiveIndex.ShouldBe(1);

        if (mode == ListSelectionMode.Multiple)
        {
            afterFirst.ShouldBeEmpty();
            list.SelectedItems.ShouldBe(new object?[] { "B" });
            changes.ShouldBe([":1", "1:"]);
        }
        else if (mode == ListSelectionMode.Single)
        {
            afterFirst.ShouldBe(["B"]);
            list.SelectedItems.ShouldBe(new object?[] { "B" });
            changes.ShouldBeEmpty();
        }
        else
        {
            afterFirst.ShouldBeEmpty();
            list.SelectedItems.ShouldBeEmpty();
            changes.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies Enter invokes the active row in every mode without touching a
    /// multiple selection assembled beforehand.</summary>
    /// <param name="mode">The selection mode under test.</param>
    [Theory]
    [InlineData(ListSelectionMode.None)]
    [InlineData(ListSelectionMode.Single)]
    [InlineData(ListSelectionMode.Multiple)]
    public async Task Keyboard_WhenEnterIsPressed_InvokesActiveRowWithoutChangingSelectionAsync(ListSelectionMode mode)
    {
        // Arrange
        var list = CreateList(mode);
        var invoked = new List<(int Index, object? Item, ActivationCause Cause)>();
        list.ItemInvoked += (_, eventArgs) => invoked.Add((eventArgs.Index, eventArgs.Item, eventArgs.Cause));
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);

        if (mode == ListSelectionMode.Multiple)
        {
            // Additive programmatic selection also makes its index the active row.
            await surface.UpdateAsync(() => list.SetSelected(1, true).ShouldBeTrue(), "add a second selected row");
            list.SelectedItems.ShouldBe(new object?[] { "B", "E" });
        }
        else
        {
            await surface.Keyboard.PressAsync(Code.Home);
            await surface.Keyboard.PressAsync(Code.Down);
        }

        list.ActiveIndex.ShouldBe(1);
        var selectedBefore = list.SelectedItems.ToArray();
        var changes = 0;
        list.SelectionChanged += (_, _) => changes++;

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        invoked.ShouldBe([(1, "B", ActivationCause.Keyboard)]);
        changes.ShouldBe(0);
        list.SelectedItems.ShouldBe(selectedBefore);
        list.ActiveIndex.ShouldBe(1);
    }

    /// <summary>Verifies Control- and Alt-modified movement keys are left unhandled so neither the
    /// active row nor the selection moves. Shift navigation has its own range-selection contract.</summary>
    /// <param name="modifiers">The modifier held with the arrow key.</param>
    [Theory]
    [InlineData(Modifiers.Control)]
    [InlineData(Modifiers.Alt)]
    public async Task Keyboard_WhenMovementKeyCarriesCommandModifier_LeavesStateUnchangedAsync(Modifiers modifiers)
    {
        // Arrange
        var list = CreateList(ListSelectionMode.Multiple);
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        var changes = 0;
        list.SelectionChanged += (_, _) => changes++;

        // Act
        await surface.Keyboard.PressAsync(Code.Down, modifiers);
        await surface.Keyboard.PressAsync(Code.Up, modifiers);
        await surface.Keyboard.PressAsync(Code.Home, modifiers);
        await surface.Keyboard.PressAsync(Code.End, modifiers);

        // Assert
        list.ActiveIndex.ShouldBe(1);
        list.SelectedItems.ShouldBe(new object?[] { "B" });
        changes.ShouldBe(0);
    }

    /// <summary>Verifies Ctrl+A selects every available row in Multiple mode exactly once, skips a
    /// disabled row, is idempotent, and is inert in Single and None modes.</summary>
    /// <param name="mode">The selection mode under test.</param>
    [Theory]
    [InlineData(ListSelectionMode.None)]
    [InlineData(ListSelectionMode.Single)]
    [InlineData(ListSelectionMode.Multiple)]
    public async Task Keyboard_WhenControlAIsPressed_SelectsEveryAvailableRowOnlyInMultipleModeAsync(ListSelectionMode mode)
    {
        // Arrange
        List<ControlText> realized = [];
        var list = CreateList(mode, realized);
        realized[2].IsEnabled = false;
        var changes = new List<string>();
        list.SelectionChanged += (_, eventArgs) => changes.Add($"{Join(eventArgs.AddedIndexes)}:{Join(eventArgs.RemovedIndexes)}");
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await PressControlAAsync(surface);
        await PressControlAAsync(surface);

        // Assert
        if (mode == ListSelectionMode.Multiple)
        {
            list.SelectedItems.ShouldBe(new object?[] { "A", "B", "D", "E" });
            changes.ShouldBe(["0,1,3,4:"]);
        }
        else
        {
            list.SelectedItems.ShouldBeEmpty();
            changes.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies the programmatic surface reaches the same committed state as the
    /// keyboard: SelectAll equals Ctrl+A, SetSelected removes one index only, and clearing
    /// through SelectedIndex reports every removed index.</summary>
    [Fact]
    public async Task Programmatic_WhenSelectAllSetSelectedAndClearRun_MatchKeyboardOutcomesAsync()
    {
        // Arrange
        var list = CreateList(ListSelectionMode.Multiple);
        var changes = new List<string>();
        list.SelectionChanged += (_, eventArgs) => changes.Add($"{Join(eventArgs.AddedIndexes)}:{Join(eventArgs.RemovedIndexes)}");
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act keyboard select-all, then the programmatic twin
        await PressControlAAsync(surface);
        var keyboardSelection = list.SelectedItems.ToArray();
        await surface.UpdateAsync(() => list.SelectedIndex = -1, "clear selection");
        await surface.UpdateAsync(list.SelectAll, "select all programmatically");

        // Assert parity
        list.SelectedItems.ShouldBe(keyboardSelection);
        changes.ShouldBe(["0,1,2,3,4:", ":0,1,2,3,4", "0,1,2,3,4:"]);

        // Act single removal then Space toggle on the active row
        changes.Clear();
        await surface.UpdateAsync(() => list.SetSelected(2, false).ShouldBeTrue(), "deselect one");
        list.SelectedItems.ShouldBe(new object?[] { "A", "B", "D", "E" });
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        list.SelectedItems.ShouldBe(new object?[] { "C" });
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        list.SelectedItems.ShouldBeEmpty();
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert plain navigation replaced the set and Space toggled the same index twice
        list.SelectedItems.ShouldBe(new object?[] { "C" });
        changes.ShouldBe([":2", ":1,3,4", "1:0", "2:1", ":2", "2:"]);
    }

    /// <summary>Verifies narrowing the mode while mounted keeps the lowest index for Single,
    /// clears for None, publishes the removed indexes, and repaints rows without selection.</summary>
    [Fact]
    public async Task SelectionMode_WhenNarrowedWhileMounted_NormalizesSelectionAndRepaintsRowsAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = CreateList(ListSelectionMode.Multiple, realized);
        var changes = new List<string>();
        list.SelectionChanged += (_, eventArgs) => changes.Add($"{Join(eventArgs.AddedIndexes)}:{Join(eventArgs.RemovedIndexes)}");
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                _ = list.SetSelected(1, true);
                _ = list.SetSelected(3, true);
            },
            "select two rows");
        var selectionBackground = ThemeColorHelper.SelectionBackground(list.Theme.ShouldNotBeNull());
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(selectionBackground);
        surface.Cell(new Point(0, 3)).Style.Background.ShouldBe(selectionBackground);

        // Act narrow to Single
        await surface.UpdateAsync(() => list.SelectionMode = ListSelectionMode.Single, "narrow to Single");

        // Assert
        list.SelectedItems.ShouldBe(new object?[] { "B" });
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(selectionBackground);
        surface.Cell(new Point(0, 3)).Style.Background.ShouldNotBe(selectionBackground);

        // Act narrow to None
        await surface.UpdateAsync(() => list.SelectionMode = ListSelectionMode.None, "narrow to None");

        // Assert
        list.SelectedItems.ShouldBeEmpty();
        list.SelectedIndex.ShouldBe(-1);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldNotBe(selectionBackground);
        changes.ShouldBe(["1:", "3:", ":3", ":1"]);
        surface.Cell(new Point(0, 3)).Style.Background.ShouldNotBe(selectionBackground);
    }

    /// <summary>Verifies Control- and Shift-clicks in Single mode replace the selection rather
    /// than toggling or ranging, and a modified re-click of the selected row changes nothing.</summary>
    [Fact]
    public async Task Pointer_WhenModifiedClickInSingleMode_ReplacesInsteadOfTogglingAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = CreateList(ListSelectionMode.Single, realized);
        var changes = 0;
        list.SelectionChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Pointer.ClickAsync(realized[1].Parent.ShouldNotBeNull());
        list.SelectedItems.ShouldBe(new object?[] { "B" });
        changes.ShouldBe(1);

        await surface.Pointer.ClickAsync(realized[1].Parent.ShouldNotBeNull(), Modifiers.Control);
        list.SelectedItems.ShouldBe(new object?[] { "B" });
        changes.ShouldBe(1);

        await surface.Pointer.ClickAsync(realized[3].Parent.ShouldNotBeNull(), Modifiers.Control);
        list.SelectedItems.ShouldBe(new object?[] { "D" });
        changes.ShouldBe(2);

        await surface.Pointer.ClickAsync(realized[0].Parent.ShouldNotBeNull(), Modifiers.Shift);
        list.SelectedItems.ShouldBe(new object?[] { "A" });
        list.ActiveIndex.ShouldBe(0);
        changes.ShouldBe(3);
    }

    /// <summary>Verifies a primary press on one row released over another row activates nothing:
    /// selection and invocation stay untouched and capture is released.</summary>
    [Fact]
    public async Task Pointer_WhenPressReleasesOverAnotherRow_DoesNotSelectOrInvokeAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = CreateList(ListSelectionMode.Single, realized);
        var invoked = 0;
        list.ItemInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        var pressed = realized[1].Parent.ShouldNotBeNull();

        // Act
        await surface.Pointer.MoveToAsync(pressed);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pressed);
        pressed.IsPressed.ShouldBeTrue();
        await surface.Pointer.MovePressedToAsync(realized[3].Parent.ShouldNotBeNull(), new Point(0, 0));
        await surface.Pointer.ReleaseAsync();

        // Assert
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
        invoked.ShouldBe(0);
        surface.ShouldHaveCapture(null);
        pressed.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies a secondary-button click over a row neither selects nor invokes it.</summary>
    [Fact]
    public async Task Pointer_WhenRowIsRightClicked_DoesNotSelectOrInvokeAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = CreateList(ListSelectionMode.Single, realized);
        var invoked = 0;
        list.ItemInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.RightClickAsync(realized[2].Parent.ShouldNotBeNull());

        // Assert
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
        invoked.ShouldBe(0);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a second plain click on the already selected row invokes it again in the
    /// SingleClick default without publishing a second selection change.</summary>
    [Fact]
    public async Task Pointer_WhenSelectedRowIsClickedAgain_InvokesAgainWithoutReselectingAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = CreateList(ListSelectionMode.Single, realized);
        var invoked = 0;
        var changes = 0;
        list.ItemInvoked += (_, _) => invoked++;
        list.SelectionChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(realized[2].Parent.ShouldNotBeNull());
        await surface.Pointer.ClickAsync(realized[2].Parent.ShouldNotBeNull());

        // Assert
        invoked.ShouldBe(2);
        changes.ShouldBe(1);
        list.SelectedIndex.ShouldBe(2);
    }

    /// <summary>Verifies the wheel scrolls the viewport by LineSize per notch without moving the
    /// active or selected row, saturates at the top, and repaints the shifted rows.</summary>
    [Fact]
    public async Task Pointer_WhenWheelScrolls_MovesViewportOnlyAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = Enumerable.Range(0, 20).Select(index => (object?) $"Row {index}").ToArray(),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(realized[2].Parent.ShouldNotBeNull());
        var scrolls = new List<int>();
        list.ScrollChanged += (_, eventArgs) => scrolls.Add(eventArgs.Offset.Y);

        // Act
        await surface.Pointer.WheelAsync(list, new Point(1, 1), wheelY: -1);
        await surface.Pointer.WheelAsync(list, new Point(1, 1), wheelY: -1);

        // Assert
        list.VerticalOffset.ShouldBe(2);
        list.ActiveIndex.ShouldBe(2);
        list.SelectedIndex.ShouldBe(2);
        scrolls.ShouldBe([1, 2]);
        surface.ShouldRender("""
                             Row 2
                             Row 3
                             Row 4
                             Row 5
                             Row 6
                             """);

        // Act back past the top
        await surface.Pointer.WheelAsync(list, new Point(1, 1), wheelY: 1);
        await surface.Pointer.WheelAsync(list, new Point(1, 1), wheelY: 1);
        await surface.Pointer.WheelAsync(list, new Point(1, 1), wheelY: 1);

        // Assert saturation
        list.VerticalOffset.ShouldBe(0);
        scrolls.ShouldBe([1, 2, 1, 0]);
    }

    /// <summary>Verifies a template whose content is itself pressable still activates through the
    /// row wrapper: the row selects and invokes while the inner Button never clicks.</summary>
    [Fact]
    public async Task Pointer_WhenTemplateContainsButton_RowWrapperOwnsActivationAsync()
    {
        // Arrange
        List<Button> buttons = [];
        var clicks = 0;
        var list = new UiListView
        {
            ItemTemplate = item =>
            {
                var button = new Button { Text = (string) item! };
                button.Click += (_, _) => clicks++;
                buttons.Add(button);
                return button;
            },
            Items = _letters,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var invoked = new List<int>();
        list.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);
        await using var surface = await ComponentSurface.MountAsync(list, new Size(12, 15), TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(buttons[1]);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        clicks.ShouldBe(0);
        buttons[1].IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(list);
        list.SelectedIndex.ShouldBe(1);
        invoked.ShouldBe([1, 1]);
    }

    /// <summary>Verifies Tab leaves the list as a single stop and Shift+Tab returns to it with the
    /// active row and selection intact, so navigation resumes where it stopped.</summary>
    [Fact]
    public async Task Keyboard_WhenFocusLeavesAndReturns_RetainsActiveRowAndSelectionAsync()
    {
        // Arrange
        var first = CreateList(ListSelectionMode.Single);
        var second = CreateList(ListSelectionMode.Single);
        first.Height = Length.Cells(5);
        second.Height = Length.Cells(5);
        var stack = new Stack { Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(stack, new Size(8, 10), TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert the second list is the next stop and the first keeps its state
        surface.ShouldHaveFocus(second);
        first.ActiveIndex.ShouldBe(2);
        first.SelectedIndex.ShouldBe(2);
        second.ActiveIndex.ShouldBe(-1);

        // Act return and continue
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(first);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        first.ActiveIndex.ShouldBe(3);
        first.SelectedIndex.ShouldBe(3);
        second.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies an ItemInvoked handler that replaces the snapshot completes without stale
    /// access: the removed selection clears, the active row falls back, and later input operates
    /// on the replacement.</summary>
    [Fact]
    public async Task ItemInvoked_WhenHandlerReplacesItems_ContinuesOnReplacementSnapshotAsync()
    {
        // Arrange
        var list = CreateList(ListSelectionMode.Single);
        var invoked = new List<object?>();
        list.ItemInvoked += (_, eventArgs) =>
        {
            invoked.Add(eventArgs.Item);

            if (list.Items.Count > 1)
            {
                list.Items = ["X", "Y"];
            }
        };
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert the replacement took over
        list.Items.ShouldBe(new object?[] { "X", "Y" });
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(1);
        surface.ShouldRender("""
                             X
                             Y
                             """);

        // Act on the replacement
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        list.SelectedIndex.ShouldBe(0);
        invoked.ShouldBe(["D", "X"]);
    }

    /// <summary>Verifies a handler that disposes the list mid-invocation neither throws nor leaves
    /// focus on the disposed control.</summary>
    [Fact]
    public async Task ItemInvoked_WhenHandlerDisposesList_CompletesAndReleasesFocusAsync()
    {
        // Arrange
        var list = CreateList(ListSelectionMode.Single);
        var host = new Overlay { Children = { list } };
        list.ItemInvoked += (_, _) => list.Dispose();
        await using var surface = await ComponentSurface.MountAsync(host, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        list.IsDisposed.ShouldBeTrue();
        list.Parent.ShouldBeNull();
        surface.Application.Focus.Focused.ShouldNotBeSameAs(list);
        surface.ShouldRender("");
    }

    /// <summary>Verifies collapsing a focused list releases focus without dropping its selection,
    /// and restoring visibility repaints the same selected row.</summary>
    [Fact]
    public async Task Visibility_WhenFocusedListCollapsesAndReturns_ReleasesFocusAndKeepsSelectionAsync()
    {
        // Arrange
        var list = CreateList(ListSelectionMode.Single);
        var host = new Overlay { Children = { list } };
        await using var surface = await ComponentSurface.MountAsync(host, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        var selectionBackground = ThemeColorHelper.SelectionBackground(list.Theme.ShouldNotBeNull());

        // Act
        await surface.UpdateAsync(() => list.Visibility = Visibility.Collapsed, "collapse focused list");

        // Assert
        list.IsFocused.ShouldBeFalse();
        surface.Application.Focus.Focused.ShouldNotBeSameAs(list);
        list.SelectedIndex.ShouldBe(2);
        surface.ShouldRender("");

        // Act
        await surface.UpdateAsync(() => list.Visibility = Visibility.Visible, "restore list");

        // Assert
        list.SelectedIndex.ShouldBe(2);
        list.ActiveIndex.ShouldBe(2);
        surface.Cell(new Point(0, 2)).Style.Background.ShouldBe(selectionBackground);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldNotBe(selectionBackground);
    }

    /// <summary>Verifies swapping the application theme while a mounted list holds a selection
    /// repaints the selected row in the new theme's selection colour and the other rows in its
    /// ordinary colours, then restores the original colours when the first theme returns.</summary>
    [Fact]
    public async Task Theme_WhenSwappedWhileMountedWithSelection_RepaintsRowsInTheNewColorsAsync()
    {
        // Arrange
        var list = CreateList(ListSelectionMode.Single);
        await using var surface = await ComponentSurface.MountAsync(list, new Size(8, 5), TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(list, new Point(0, 1));
        list.SelectedIndex.ShouldBe(1);
        var original = surface.Application.Theme;
        var originalSelection = TerminalPalette.Project(ThemeColorHelper.SelectionBackground(original), ColorDepth.Basic16);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(originalSelection);
        var swapped = ThemeCatalog.Load("turbo-vision");
        var swappedSelection = TerminalPalette.Project(ThemeColorHelper.SelectionBackground(swapped), ColorDepth.Basic16);
        swappedSelection.ShouldNotBe(originalSelection);

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = swapped, "apply Turbo Vision");

        // Assert the selected row follows the new theme while its text and state survive
        list.SelectedIndex.ShouldBe(1);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(swappedSelection);
        surface.Cell(new Point(0, 1)).Text.ShouldBe("B");
        surface.Cell(new Point(0, 0)).Style.Background.ShouldNotBe(swappedSelection);
        surface.Cell(new Point(0, 0)).Style.Background.ShouldNotBe(originalSelection);
        surface.Cell(new Point(0, 2)).Text.ShouldBe("C");

        // Act keyboard selection after the swap keeps painting with the new theme
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        list.SelectedIndex.ShouldBe(2);
        surface.Cell(new Point(0, 2)).Style.Background.ShouldBe(swappedSelection);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldNotBe(swappedSelection);

        // Act restore
        await surface.UpdateAsync(() => surface.Application.Theme = original, "restore the original theme");

        // Assert
        surface.Cell(new Point(0, 2)).Style.Background.ShouldBe(originalSelection);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldNotBe(originalSelection);
    }

    private static UiListView CreateList(ListSelectionMode mode, List<ControlText>? realized = null) => new()
    {
        ItemTemplate = item => Add(realized, new ControlText((string) item!)),
        Items = _letters,
        SelectionMode = mode,
        ScrollBars = ScrollBars.None,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    private static ControlText Add(List<ControlText>? controls, ControlText control)
    {
        controls?.Add(control);
        return control;
    }

    private static Task PressControlAAsync(ComponentSurface surface) =>
        surface.SendAsync(Encoding.ASCII.GetBytes($"{(char) 27}[97;5u"), "press Ctrl+A");

    private static string Join(ReadOnlyMemory<int> values) => string.Join(',', values.ToArray());
}
