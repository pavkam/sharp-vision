// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves every ComboBox interaction through a mounted terminal surface: the keys and
/// pointer gestures that open, browse, accept, clear, and dismiss the drop-down; the event order
/// each path publishes; how the open popup survives item, size, theme, visibility, and lifetime
/// changes; and the rendered cells the user actually sees after each step.</summary>
public sealed class ComboBoxInteractionTests
{
    #region Opening and closing

    /// <summary>Verifies Enter and Space both open a focused closed field, publishing
    /// PropertyChanged(IsOpen) before DropDownOpened, and render the connected rows.</summary>
    [Theory]
    [InlineData("enter")]
    [InlineData("space")]
    public async Task Open_WhenEnterOrSpaceIsPressedWhileClosed_OpensAndPublishesInOrderAsync(string key)
    {
        var combo = NewCombo(["Alpha", "Beta", "Gamma"], 1);
        var events = new List<string>();
        Observe(combo, events);
        await using var surface = await MountAsync(combo, new Size(12, 7));
        await surface.Keyboard.PressAsync(Code.Tab);

        if (key == "enter")
        {
            await surface.Keyboard.PressAsync(Code.Enter);
        }
        else
        {
            await surface.Keyboard.TypeAsync(" ");
        }

        combo.IsOpen.ShouldBeTrue();
        events.ShouldBe(["PropertyChanged:IsOpen", "DropDownOpened"]);
        surface.ShouldHaveFocus(combo);
        var list = combo.GetDropDownList();
        list.Bounds.Y.ShouldBe(combo.Bounds.Bottom);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("A");
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y + 1)).Text.ShouldBe("B");
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y + 2)).Text.ShouldBe("G");
        list.ActiveIndex.ShouldBe(1);
        list.SelectedIndex.ShouldBe(1);
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(combo);
    }

    /// <summary>Verifies Space while the popup is open toggles the field closed without accepting
    /// the browsed row, as the keyboard table documents ("Opens or closes the ComboBox field; it
    /// does not accept the current popup item").</summary>
    [Fact]
    public async Task Close_WhenSpaceIsPressedWhileOpen_ClosesWithoutAcceptingAsync()
    {
        var combo = NewCombo(["Alpha", "Beta", "Gamma"], 0);
        var events = new List<string>();
        Observe(combo, events);
        await using var surface = await MountAsync(combo, new Size(12, 7));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Down);
        combo.GetDropDownList().ActiveIndex.ShouldBe(1);
        events.Clear();

        await surface.Keyboard.TypeAsync(" ");

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
        combo.GetDropDownList().SelectedIndex.ShouldBe(0);
        combo.GetDropDownList().ActiveIndex.ShouldBe(0);
        events.ShouldBe(["PropertyChanged:IsOpen", "DropDownClosed"]);
        surface.ShouldHaveFocus(combo);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldRender("""
                             ┏━━━━━━━━┓
                             ┃Alpha  ▼┃
                             ┗━━━━━━━━┛
                             """);
    }

    /// <summary>Verifies Space typed while open still prefers a type-ahead match over closing.</summary>
    [Fact]
    public async Task Dispatch_WhenSpaceMatchesAnItemPrefixWhileOpen_SelectsInsteadOfClosingAsync()
    {
        var combo = NewCombo(["Alpha", " Spaced"], 0);
        await using var surface = await MountAsync(combo, new Size(12, 7));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);

        await surface.Keyboard.TypeAsync(" ");

        combo.IsOpen.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies Enter accepts the browsed row and publishes SelectionChanged before the
    /// close notifications, with the exact added and removed indexes.</summary>
    [Fact]
    public async Task Accept_WhenEnterIsPressedOnBrowsedRow_PublishesSelectionBeforeCloseAsync()
    {
        var combo = NewCombo(["Alpha", "Beta", "Gamma"], 0);
        var events = new List<string>();
        Observe(combo, events);
        ListSelectionChangedEventArgs? change = null;
        combo.SelectionChanged += (_, eventArgs) => change = eventArgs;
        await using var surface = await MountAsync(combo, new Size(12, 7));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        events.Clear();

        await surface.Keyboard.PressAsync(Code.Enter);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(2);
        events.ShouldBe([
            "PropertyChanged:SelectedIndex",
            "SelectionChanged",
            "PropertyChanged:IsOpen",
            "DropDownClosed"
        ]);
        var actual = change.ShouldNotBeNull();
        actual.AddedIndexes.ToArray().ShouldBe([2]);
        actual.RemovedIndexes.ToArray().ShouldBe([0]);
        surface.ShouldHaveFocus(combo);
        surface.ShouldRender("""
                             ┏━━━━━━━━┓
                             ┃Gamma  ▼┃
                             ┗━━━━━━━━┛
                             """);
    }

    /// <summary>Verifies Enter on the row that was already selected closes the popup without
    /// publishing any selection notification.</summary>
    [Fact]
    public async Task Accept_WhenEnterAcceptsTheAlreadySelectedRow_ClosesWithoutSelectionEventsAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 1);
        var events = new List<string>();
        Observe(combo, events);
        await using var surface = await MountAsync(combo, new Size(12, 7));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        events.Clear();

        await surface.Keyboard.PressAsync(Code.Enter);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(1);
        events.ShouldBe(["PropertyChanged:IsOpen", "DropDownClosed"]);
    }

    /// <summary>Verifies a click anywhere on the field, including the indicator cell, toggles the
    /// popup: the first click opens, the second closes without accepting.</summary>
    [Fact]
    public async Task Click_WhenFieldOrIndicatorIsClicked_TogglesDropDownAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        var closed = 0;
        combo.DropDownClosed += (_, _) => closed++;
        await using var surface = await MountAsync(combo, new Size(12, 7));
        var indicator = new Point(combo.Bounds.Width - 2, 1);

        await surface.Pointer.ClickAsync(combo, indicator);

        combo.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(combo);
        surface.Cell(new Point(combo.Bounds.X + indicator.X, combo.Bounds.Y + 1)).Text.ShouldBe("▼");

        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Pointer.ClickAsync(combo, new Point(1, 1));

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
        combo.GetDropDownList().ActiveIndex.ShouldBe(0);
        closed.ShouldBe(1);
        surface.ShouldHaveFocus(combo);
        combo.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a press that is released outside the field cancels activation: nothing
    /// opens, the pressed state and capture are released, and focus stays on the field.</summary>
    [Fact]
    public async Task Press_WhenReleasedOutsideTheField_DoesNotOpenAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        var root = new Overlay { Children = { combo } };
        await using var surface = await MountAsync(root, new Size(14, 8));
        await surface.Pointer.MoveToAsync(combo);
        await surface.Pointer.PressAsync();
        combo.IsPressed.ShouldBeTrue();
        surface.ShouldHaveCapture(combo);

        await surface.Pointer.MovePressedToAsync(new Point(1, 7));
        combo.IsPressed.ShouldBeFalse();
        await surface.Pointer.ReleaseAsync();

        combo.IsOpen.ShouldBeFalse();
        combo.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(combo);
    }

    /// <summary>Verifies a secondary-button click neither opens nor focuses the field, while hover
    /// still tracks pointer presence.</summary>
    [Fact]
    public async Task RightClick_WhenFieldIsRightClicked_DoesNotOpenAsync()
    {
        var combo = NewCombo(["Alpha"], 0);
        await using var surface = await MountAsync(combo, new Size(12, 5));

        await surface.Pointer.RightClickAsync(combo);

        combo.IsOpen.ShouldBeFalse();
        combo.IsPointerOver.ShouldBeTrue();
        combo.IsFocused.ShouldBeFalse();
        combo.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies opening with no items presents an empty popup that Enter cannot accept
    /// and Escape closes, with the placeholder face unchanged throughout.</summary>
    [Fact]
    public async Task Open_WhenItemsAreEmpty_PresentsEmptyPopupThatEnterKeepsOpenAsync()
    {
        var combo = new ComboBox { Placeholder = "Pick", Width = Length.Cells(10), Height = Length.Cells(3) };
        var events = new List<string>();
        Observe(combo, events);
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(Code.Enter);
        combo.IsOpen.ShouldBeTrue();

        await surface.Keyboard.PressAsync(Code.Enter);
        combo.IsOpen.ShouldBeTrue("Enter belongs to the open session even with nothing to accept");
        combo.SelectedIndex.ShouldBe(-1);

        await surface.Keyboard.PressAsync(Code.Escape);

        combo.IsOpen.ShouldBeFalse();
        events.ShouldBe(["PropertyChanged:IsOpen", "DropDownOpened", "PropertyChanged:IsOpen", "DropDownClosed"]);
        surface.Cell(new Point(1, 1)).Text.ShouldBe("P");
    }

    #endregion

    #region Closed-state keyboard

    /// <summary>Verifies every closed navigation key commits immediately, publishes exactly one
    /// SelectionChanged, repaints the face, and never opens the popup.</summary>
    [Theory]
    [InlineData(Code.Up, 3)]
    [InlineData(Code.Left, 3)]
    [InlineData(Code.Down, 5)]
    [InlineData(Code.Right, 5)]
    [InlineData(Code.Home, 0)]
    [InlineData(Code.End, 9)]
    public async Task Dispatch_WhenClosedNavigationKeyIsPressed_CommitsWithoutOpeningAsync(Code code, int expected)
    {
        var combo = NewCombo(NumberedItems(10), 4);
        var changes = new List<int[]>();
        combo.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs.AddedIndexes.ToArray());
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(code);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(expected);
        changes.ShouldBe([[expected]]);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.Cell(new Point(6, 1)).Text.ShouldBe(expected.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Verifies a held (repeating) navigation key keeps committing while closed.</summary>
    [Fact]
    public async Task Dispatch_WhenClosedNavigationKeyRepeats_KeepsCommittingAsync()
    {
        var combo = NewCombo(["Zero", "One", "Two", "Three"], 0);
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.RepeatAsync(Code.Down);
        await surface.Keyboard.RepeatAsync(Code.Down);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(3);
    }

    /// <summary>Verifies closed-state paging after the popup has been laid out once moves by the
    /// visible page height instead of a single row.</summary>
    [Fact]
    public async Task Dispatch_WhenClosedPageKeyIsPressedAfterAnOpen_MovesByPopupPageAsync()
    {
        var combo = NewCombo(NumberedItems(20), 10);
        combo.DropDownHeight = Length.Cells(4);
        await using var surface = await MountAsync(combo, new Size(12, 10));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Escape);
        combo.IsOpen.ShouldBeFalse();

        await surface.Keyboard.PressAsync(Code.PageDown);
        var pagedDown = combo.SelectedIndex;
        await surface.Keyboard.PressAsync(Code.PageUp);

        pagedDown.ShouldBeGreaterThan(11, "a page is more than one row once the viewport has a height");
        pagedDown.ShouldBeLessThanOrEqualTo(14);
        combo.SelectedIndex.ShouldBe(10);
        combo.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a modified navigation key while closed leaves the field untouched.</summary>
    [Theory]
    [InlineData(Modifiers.Control)]
    [InlineData(Modifiers.Alt)]
    [InlineData(Modifiers.Shift)]
    public async Task Dispatch_WhenClosedNavigationKeyCarriesModifier_LeavesFieldUnchangedAsync(Modifiers modifiers)
    {
        var combo = NewCombo(["Zero", "One", "Two"], 1);
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(Code.Down, modifiers);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(1);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies printable text while closed is ignored: no selection, no popup.</summary>
    [Fact]
    public async Task Dispatch_WhenTextIsTypedWhileClosed_DoesNothingAsync()
    {
        var combo = NewCombo(["Alpha", "Beta", "Gamma"], 0);
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.TypeAsync("g");

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
        changes.ShouldBe(0);
        surface.ShouldHaveFocus(combo);
    }

    /// <summary>Verifies Delete while closed clears to the placeholder, reports the removed index,
    /// and a second clearing key with nothing selected publishes nothing more.</summary>
    [Fact]
    public async Task Dispatch_WhenDeleteIsPressedWhileClosed_ClearsToPlaceholderAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 1);
        combo.Placeholder = "Pick";
        var changes = new List<ListSelectionChangedEventArgs>();
        combo.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(Code.Delete);

        combo.SelectedIndex.ShouldBe(-1);
        combo.SelectedItem.ShouldBeNull();
        combo.IsOpen.ShouldBeFalse();
        changes.Count.ShouldBe(1);
        changes[0].RemovedIndexes.ToArray().ShouldBe([1]);
        changes[0].AddedIndexes.ToArray().ShouldBeEmpty();
        surface.ShouldRender("""
                             ┏━━━━━━━━┓
                             ┃Pick   ▼┃
                             ┗━━━━━━━━┛
                             """);

        await surface.Keyboard.PressAsync(Code.Backspace);

        changes.Count.ShouldBe(1);
        combo.SelectedIndex.ShouldBe(-1);
    }

    #endregion

    #region Open-state keyboard

    /// <summary>Verifies every open navigation key moves only the provisional row, exactly once,
    /// while the committed selection, focus, and popup stay put.</summary>
    [Theory]
    [InlineData(Code.Up, 3)]
    [InlineData(Code.Left, 3)]
    [InlineData(Code.Down, 5)]
    [InlineData(Code.Right, 5)]
    [InlineData(Code.Home, 0)]
    [InlineData(Code.End, 9)]
    [InlineData(Code.PageUp, 1)]
    [InlineData(Code.PageDown, 7)]
    public async Task Dispatch_WhenOpenNavigationKeyIsPressed_MovesProvisionalRowOnlyAsync(Code code, int expected)
    {
        var combo = NewCombo(NumberedItems(10), 4);
        combo.DropDownHeight = Length.Cells(3);
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;
        await using var surface = await MountAsync(combo, new Size(12, 8));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        var list = combo.GetDropDownList();

        await surface.Keyboard.PressAsync(code);

        combo.IsOpen.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(4);
        list.ActiveIndex.ShouldBe(expected);
        list.SelectedIndex.ShouldBe(expected);
        changes.ShouldBe(0);
        surface.ShouldHaveFocus(combo);
        surface.Cell(new Point(6, 1)).Text.ShouldBe("4", "the face shows the committed value, not the browsed row");
    }

    /// <summary>Verifies a Shift- or Control-modified navigation key while open leaves the
    /// provisional row unchanged and the popup open.</summary>
    [Theory]
    [InlineData(Modifiers.Control)]
    [InlineData(Modifiers.Shift)]
    [InlineData(Modifiers.Alt)]
    public async Task Dispatch_WhenOpenNavigationKeyCarriesModifier_LeavesProvisionalRowAsync(Modifiers modifiers)
    {
        var combo = NewCombo(["Zero", "One", "Two"], 1);
        await using var surface = await MountAsync(combo, new Size(12, 7));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);

        await surface.Keyboard.PressAsync(Code.Down, modifiers);

        combo.IsOpen.ShouldBeTrue();
        combo.GetDropDownList().ActiveIndex.ShouldBe(1);
        combo.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies type-ahead while open commits immediately, repaints the face, and Escape
    /// afterwards keeps the typed selection because the typed commit superseded the opening snapshot.</summary>
    [Fact]
    public async Task Dispatch_WhenTypeAheadCommitsThenEscape_KeepsTypedSelectionAsync()
    {
        var combo = NewCombo(["Alpha", "Beta", "Gamma"], 0);
        var changes = new List<int[]>();
        combo.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs.AddedIndexes.ToArray());
        await using var surface = await MountAsync(combo, new Size(12, 7));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);

        await surface.Keyboard.TypeAsync("g");

        combo.IsOpen.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(2);
        combo.GetDropDownList().ActiveIndex.ShouldBe(2);
        changes.ShouldBe([[2]]);
        surface.Cell(new Point(1, 1)).Text.ShouldBe("G");

        await surface.Keyboard.PressAsync(Code.Escape);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(2);
        changes.Count.ShouldBe(1);
        surface.Cell(new Point(1, 1)).Text.ShouldBe("G");
    }

    /// <summary>Verifies a two-character prefix typed through real terminal bytes narrows the
    /// match, and that the prefix is discarded when the popup closes and reopens.</summary>
    [Fact]
    public async Task Dispatch_WhenPrefixIsTypedAcrossSessions_ResetsOnReopenAsync()
    {
        var combo = NewCombo(["Apple", "Avocado", "Apricot", "Banana"], 0);
        await using var surface = await MountAsync(combo, new Size(12, 8));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);

        await surface.Keyboard.TypeAsync("ap");
        combo.SelectedIndex.ShouldBe(2, "'a' moves to Avocado, then 'ap' narrows to Apricot");

        await surface.Keyboard.PressAsync(Code.Escape);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.TypeAsync("b");

        combo.SelectedIndex.ShouldBe(3, "a stale 'ap' prefix would have made 'apb' match nothing");
    }

    /// <summary>Verifies Delete while open clears the committed selection, keeps the popup open,
    /// and Escape afterwards leaves the cleared state alone.</summary>
    [Fact]
    public async Task Dispatch_WhenDeleteIsPressedWhileOpen_ClearsAndKeepsPopupOpenAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 1);
        combo.Placeholder = "Pick";
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        var list = combo.GetDropDownList();

        await surface.Keyboard.PressAsync(Code.Delete);

        combo.IsOpen.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(-1);
        combo.SelectedItem.ShouldBeNull();
        // The open session keeps its provisional highlight on the current row (the arrange pass
        // mirrors the list's selection to its current item), so only the committed value clears.
        list.ActiveIndex.ShouldBe(1);
        list.SelectedIndex.ShouldBe(1);
        surface.Cell(new Point(1, 1)).Text.ShouldBe("P");

        await surface.Keyboard.PressAsync(Code.Escape);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(-1);
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
        surface.Cell(new Point(1, 1)).Text.ShouldBe("P");
    }

    /// <summary>Verifies Enter after an open-session Delete re-accepts the still-highlighted
    /// current row, since the provisional row survived the clear.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterFollowsAnOpenDelete_AcceptsTheHighlightedRowAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 1);
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Delete);
        combo.SelectedIndex.ShouldBe(-1);

        await surface.Keyboard.PressAsync(Code.Enter);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(1);
        surface.Cell(new Point(1, 1)).Text.ShouldBe("B");
    }

    /// <summary>Verifies AllowNull=false leaves Delete inert while open: the selection, list row,
    /// and popup are all preserved.</summary>
    [Fact]
    public async Task AllowNull_WhenFalseWhileOpen_IgnoresDeleteAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 1);
        combo.AllowNull = false;
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);

        await surface.Keyboard.PressAsync(Code.Delete);

        combo.IsOpen.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(1);
        combo.GetDropDownList().SelectedIndex.ShouldBe(1);
        surface.Cell(new Point(1, 1)).Text.ShouldBe("B");
    }

    /// <summary>Verifies plain Tab and Shift+Tab close the popup without acceptance and then
    /// continue traversal to the adjacent sibling.</summary>
    [Theory]
    [InlineData(Modifiers.None)]
    [InlineData(Modifiers.Shift)]
    public async Task Tab_WhenPressedWhileOpen_ClosesAndMovesFocusAsync(Modifiers modifiers)
    {
        var combo = NewCombo(["Alpha", "Beta", "Gamma"], 0);
        var before = new Button { Text = "Before" };
        var after = new Button { Text = "After" };
        var root = new Stack { Children = { before, combo, after } };
        var closed = 0;
        combo.DropDownClosed += (_, _) => closed++;
        await using var surface = await MountAsync(root, new Size(14, 12));
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(combo).ShouldBeTrue(), "focus the field");
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Down);

        await surface.Keyboard.PressAsync(Code.Tab, modifiers);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
        combo.GetDropDownList().ActiveIndex.ShouldBe(0);
        closed.ShouldBe(1);
        surface.ShouldHaveFocus(modifiers == Modifiers.Shift ? before : after);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies programmatic focus outside the open field is refused by its modal plane,
    /// so the popup cannot be orphaned by a focus move it never observed.</summary>
    [Fact]
    public async Task Focus_WhenMovedOutsideWhileOpen_IsRefusedByTheModalPlaneAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        var sibling = new Button { Text = "Other" };
        var root = new Stack { Children = { combo, sibling } };
        await using var surface = await MountAsync(root, new Size(14, 10));
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(combo).ShouldBeTrue(), "focus the field");
        await surface.Keyboard.PressAsync(Code.Enter);

        var moved = true;
        await surface.UpdateAsync(() => moved = surface.Application.Focus.Focus(sibling), "focus a control outside the plane");

        moved.ShouldBeFalse();
        combo.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(combo);
    }

    #endregion

    #region Rows, items, sizing, and chrome while open

    /// <summary>Verifies replacing Items while open repaints the rows, shrinks the popup to the
    /// new count, and keeps the session open with a normalized in-range selection.</summary>
    [Fact]
    public async Task Items_WhenReplacedWhileOpen_RepaintsRowsAndShrinksPopupAsync()
    {
        var combo = NewCombo(["Alpha", "Beta", "Gamma"], 2);
        var root = new Overlay { Children = { combo } };
        await using var surface = await MountAsync(root, new Size(12, 8));
        await surface.UpdateAsync(() => combo.IsOpen = true, "open the field");
        var list = combo.GetDropDownList();
        list.Bounds.Height.ShouldBe(3);

        await surface.UpdateAsync(() => combo.Items = ["Xray", "Yankee"], "replace the item domain while open");

        combo.IsOpen.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(-1, "the old index 2 no longer exists");
        list.Bounds.Height.ShouldBe(2);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("X");
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y + 1)).Text.ShouldBe("Y");
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y + 2)).Text.ShouldNotBe("G");

        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Enter);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedItem.ShouldBeOneOf("Xray", "Yankee");
    }

    /// <summary>Verifies a SelectionChanged handler that replaces Items (and thereby the
    /// selection) during keyboard acceptance owns the newer decision: the accepted session's close
    /// is superseded by a fresh open session over the new domain, which then closes normally.</summary>
    [Fact]
    public async Task SelectionChanged_WhenHandlerReplacesItemsDuringAccept_StartsFreshSessionOverNewDomainAsync()
    {
        var combo = NewCombo(["Alpha", "Beta", "Gamma"], 0);
        var reentered = 0;
        combo.SelectionChanged += (_, _) =>
        {
            if (reentered++ == 0)
            {
                combo.Items = ["Replaced"];
            }
        };
        var closed = 0;
        combo.DropDownClosed += (_, _) => closed++;
        await using var surface = await MountAsync(combo, new Size(12, 7));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Down);

        await surface.Keyboard.PressAsync(Code.Enter);

        reentered.ShouldBeGreaterThanOrEqualTo(1);
        combo.Items.ShouldBe(["Replaced"]);
        combo.SelectedIndex.ShouldBe(-1, "the accepted index 1 fell outside the replacement domain");
        combo.IsOpen.ShouldBeTrue("a replacement decision reopens a fresh session rather than being dismissed");
        closed.ShouldBe(1);
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(combo);
        surface.ShouldHaveFocus(combo);
        var list = combo.GetDropDownList();
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("R");

        await surface.Keyboard.PressAsync(Code.Escape);

        combo.IsOpen.ShouldBeFalse();
        closed.ShouldBe(2);
        combo.SelectedIndex.ShouldBe(-1);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies DropDownHeight caps the visible rows: Auto shows every row while a fixed
    /// cap leaves the list scrollable and End reveals the last row inside that cap.</summary>
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 2)]
    public async Task DropDownHeight_WhenAutoOrFixed_CapsVisibleRowsAsync(bool fixedCap, int expectedRows)
    {
        var combo = NewCombo(["One", "Two", "Three", "Four", "Five"], 0);
        combo.DropDownHeight = fixedCap ? Length.Cells(2) : Length.Auto;
        var root = new Overlay { Children = { combo } };
        await using var surface = await MountAsync(root, new Size(12, 10));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        var list = combo.GetDropDownList();

        list.Bounds.Height.ShouldBe(expectedRows);

        await surface.Keyboard.PressAsync(Code.End);

        list.ActiveIndex.ShouldBe(4);
        (list.VerticalOffset > 0).ShouldBe(fixedCap);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Bottom - 1)).Text.ShouldBe("F");
    }

    /// <summary>Verifies shrinking the host while the popup is open keeps it open and clamped
    /// inside the surface, and growing it back restores the full row count.</summary>
    [Fact]
    public async Task Resize_WhenHostShrinksWhileOpen_KeepsPopupInsideSurfaceAsync()
    {
        var combo = NewCombo(["One", "Two", "Three", "Four", "Five", "Six"], 0);
        combo.VerticalAlignment = VerticalAlignment.Top;
        var root = new Overlay { Children = { combo } };
        await using var surface = await MountAsync(root, new Size(14, 12));
        await surface.UpdateAsync(() => combo.IsOpen = true, "open the field");
        var popup = OwnedTree.Find<Popup>(combo).ShouldNotBeNull();
        var list = combo.GetDropDownList();
        list.Bounds.Height.ShouldBe(6);

        await surface.ResizeAsync(new Size(14, 6));

        // Neither side of the field has room for six rows any more, so the popup clamps into the
        // surface (overlapping the field if it must) instead of spilling past the bottom edge.
        combo.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.Y.ShouldBeGreaterThanOrEqualTo(0);
        popup.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(6);
        list.Bounds.Height.ShouldBeInRange(1, 5);
        list.Bounds.Bottom.ShouldBeLessThanOrEqualTo(6);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("O");

        await surface.ResizeAsync(new Size(14, 12));

        combo.IsOpen.ShouldBeTrue();
        list.Bounds.Height.ShouldBe(6);
    }

    /// <summary>Verifies rows realized from an ItemTemplate with focusable content keep keyboard
    /// focus on the field: browsing and Enter acceptance work without focus entering a row.</summary>
    [Fact]
    public async Task ItemTemplate_WhenRowsHostFocusableContent_KeepsFocusOnFieldAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        combo.ItemTemplate = item => new Button { Text = (string) item! };
        await using var surface = await MountAsync(combo, new Size(14, 7));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        var buttons = OwnedTree.FindAll<Button>(combo);
        buttons.Count.ShouldBe(2);

        await surface.Keyboard.PressAsync(Code.Down);

        surface.ShouldHaveFocus(combo);
        buttons.ShouldAllBe(button => !button.IsFocused);

        await surface.Keyboard.PressAsync(Code.Enter);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(1);
        surface.ShouldHaveFocus(combo);
    }

    /// <summary>Verifies a PopupChrome override assigned while open repaints the frame at once,
    /// and resetting it returns the themed glyphs while the popup stays open.</summary>
    [Fact]
    public async Task PopupChrome_WhenChangedWhileOpen_RepaintsFrameImmediatelyAsync()
    {
        var combo = NewCombo(["One", "Two"], 0);
        var root = new Overlay { Children = { combo } };
        await using var surface = await MountAsync(root, new Size(12, 7));
        await surface.UpdateAsync(() => combo.IsOpen = true, "open the field");
        var popup = OwnedTree.Find<Popup>(combo).ShouldNotBeNull();
        var corner = new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Bottom - 1);
        var themed = surface.Cell(corner).Text;
        themed.ShouldNotBe("+");

        await surface.UpdateAsync(
            () => combo.PopupChrome = new PopupChrome
            {
                Border = new Border(BorderSide.All, BorderGlyphStyle.Ascii, Color.Default, Color.Transparent, TerminalAttributes.None)
            },
            "override the popup chrome while open");

        combo.IsOpen.ShouldBeTrue();
        surface.Cell(corner).Text.ShouldBe("+");

        await surface.UpdateAsync(combo.ResetPopupChrome, "reset the popup chrome while open");

        combo.IsOpen.ShouldBeTrue();
        surface.Cell(corner).Text.ShouldBe(themed);
    }

    /// <summary>Verifies swapping the application theme while open keeps the popup open, repaints
    /// the field with the new surface color, and keeps the rows legible.</summary>
    [Fact]
    public async Task Theme_WhenSwappedWhileOpen_KeepsPopupOpenAndRepaintsAsync()
    {
        var combo = NewCombo(["One", "Two"], 0);
        var root = new Overlay { Children = { combo } };
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 7),
            options,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => combo.IsOpen = true, "open the field");
        var list = combo.GetDropDownList();
        var theme = ThemeCatalog.Load("catppuccin-mocha");

        await surface.UpdateAsync(() => surface.Application.Theme = theme, "swap the theme while open");

        combo.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(combo);
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(ThemeColorHelper.Surface(theme));
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("O");
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y + 1)).Text.ShouldBe("T");

        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Enter);

        combo.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies a field too narrow for its label still renders its frame and indicator
    /// and opens a popup whose rows are painted.</summary>
    [Fact]
    public async Task Render_WhenFieldIsTiny_KeepsIndicatorInsideFrameAndOpensAsync()
    {
        var combo = new ComboBox { Items = ["Alpha"], SelectedIndex = 0, Width = Length.Cells(4), Height = Length.Cells(3) };
        var root = new Overlay { Children = { combo } };
        await using var surface = await MountAsync(root, new Size(8, 6));

        surface.ShouldRender("""
                             ┏━━┓
                             ┃ ▼┃
                             ┗━━┛
                             """);

        await surface.Pointer.ClickAsync(combo);

        combo.IsOpen.ShouldBeTrue();
        var list = combo.GetDropDownList();
        list.Bounds.Width.ShouldBeGreaterThan(0);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("A");
    }

    #endregion

    #region Availability and lifetime while open

    /// <summary>Verifies hiding the field while open closes the popup once, restores the opening
    /// row, and showing it again does not reopen; a later Enter opens a fresh session.</summary>
    [Fact]
    public async Task Visibility_WhenHiddenWhileOpen_ClosesOnceAndDoesNotReopenOnShowAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        var closed = 0;
        combo.DropDownClosed += (_, _) => closed++;
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Down);

        await surface.UpdateAsync(() => combo.Visibility = Visibility.Hidden, "hide the open field");

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
        combo.GetDropDownList().ActiveIndex.ShouldBe(0);
        closed.ShouldBe(1);
        surface.Application.Modality.Active.ShouldBeNull();

        await surface.UpdateAsync(() => combo.Visibility = Visibility.Visible, "show the field again");

        combo.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);

        combo.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies removing the open field from its parent closes the popup, restores the
    /// opening row, releases the modal scope, and leaves the detached control reusable.</summary>
    [Fact]
    public async Task Detach_WhenRemovedFromParentWhileOpen_ClosesAndRestoresSelectionAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        var root = new Overlay { Children = { combo } };
        var closed = 0;
        combo.DropDownClosed += (_, _) => closed++;
        await using var surface = await MountAsync(root, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Down);

        await surface.UpdateAsync(() => root.Children.Remove(combo).ShouldBeTrue(), "detach the open field");

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
        combo.GetDropDownList().ActiveIndex.ShouldBe(0);
        closed.ShouldBe(1);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(null);
        surface.ShouldRender("");

        await surface.UpdateAsync(() => root.Children.Add(combo), "re-attach the field");
        await surface.UpdateAsync(() => combo.IsOpen = true, "reopen after re-attachment");

        combo.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(combo);
    }

    /// <summary>Verifies disposing the open field releases its modal scope, focus, and cells
    /// without throwing, and its cleared events never fire again.</summary>
    [Fact]
    public async Task Dispose_WhenDisposedWhileOpen_ReleasesScopeFocusAndCellsAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        var root = new Overlay { Children = { combo } };
        var closed = 0;
        combo.DropDownClosed += (_, _) => closed++;
        await using var surface = await MountAsync(root, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);

        await surface.UpdateAsync(() => Should.NotThrow(combo.Dispose), "dispose the open field");

        combo.IsDisposed.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(null);
        surface.ShouldRender("");
        closed.ShouldBeLessThanOrEqualTo(1);
    }

    /// <summary>Verifies the modal light-dismiss registration survives a disable/enable cycle: a
    /// click outside still closes a popup opened after re-enabling and focus returns to the field.</summary>
    [Fact]
    public async Task LightDismiss_WhenFieldWasDisabledAndReEnabled_StillClosesOnOutsideClickAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        var outside = new ControlText("outside");
        Overlay.SetTop(outside, Length.Cells(7));
        var root = new Overlay { Children = { combo, outside } };
        var closed = 0;
        combo.DropDownClosed += (_, _) => closed++;
        await using var surface = await MountAsync(root, new Size(14, 9));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.UpdateAsync(() => combo.IsEnabled = false, "disable the open field");
        closed.ShouldBe(1);
        await surface.UpdateAsync(() => combo.IsEnabled = true, "re-enable the field");
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        combo.IsOpen.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Down);

        await surface.Pointer.ClickAsync(outside);

        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
        closed.ShouldBe(2);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(combo);
    }

    /// <summary>Verifies a DropDownOpened handler that immediately closes the field ends with a
    /// closed popup and exactly one DropDownClosed, with the field still usable afterwards.</summary>
    [Fact]
    public async Task DropDownOpened_WhenHandlerClosesImmediately_EndsClosedWithSingleClosedEventAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        var events = new List<string>();
        Observe(combo, events);
        var closeFromOpened = true;
        combo.DropDownOpened += (_, _) =>
        {
            if (closeFromOpened)
            {
                combo.IsOpen = false;
            }
        };
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(Code.Enter);

        combo.IsOpen.ShouldBeFalse();
        events.Count(entry => entry == "DropDownClosed").ShouldBe(1);
        events.Count(entry => entry == "DropDownOpened").ShouldBe(1);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(combo);

        closeFromOpened = false;
        await surface.Keyboard.PressAsync(Code.Enter);

        combo.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a Popup.Closing observer that re-requests the close reenters the
    /// coordinator harmlessly: one DropDownClosed, focus back on the field, scope released.</summary>
    [Fact]
    public async Task Closing_WhenObserverClosesAgain_PublishesClosedOnceAsync()
    {
        var combo = NewCombo(["Alpha", "Beta"], 0);
        var events = new List<string>();
        Observe(combo, events);
        var popup = OwnedTree.Find<Popup>(combo).ShouldNotBeNull();
        popup.Closing += (_, _) => combo.IsOpen = false;
        await using var surface = await MountAsync(combo, new Size(12, 6));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        events.Clear();

        await surface.Keyboard.PressAsync(Code.Escape);

        combo.IsOpen.ShouldBeFalse();
        events.ShouldBe(["PropertyChanged:IsOpen", "DropDownClosed"]);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(combo);
    }

    /// <summary>Verifies the field can be reopened immediately after an accepted close: the
    /// accepted session releases its open-state guard and a fresh session begins.</summary>
    [Fact]
    public async Task Reopen_WhenReopenedAfterAcceptedClose_StartsFreshSessionAsync()
    {
        var combo = NewCombo(["Alpha", "Beta", "Gamma"], 0);
        var opened = 0;
        combo.DropDownOpened += (_, _) => opened++;
        await using var surface = await MountAsync(combo, new Size(12, 7));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Enter);
        combo.SelectedIndex.ShouldBe(1);

        await surface.Keyboard.PressAsync(Code.Enter);
        var list = combo.GetDropDownList();

        combo.IsOpen.ShouldBeTrue();
        opened.ShouldBe(2);
        list.ActiveIndex.ShouldBe(1);

        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Escape);

        combo.SelectedIndex.ShouldBe(1, "the new session's opening row is the previously accepted one");
        list.ActiveIndex.ShouldBe(1);
    }

    #endregion

    private static ComboBox NewCombo(string[] items, int selectedIndex) => new()
    {
        Items = items,
        SelectedIndex = selectedIndex,
        Width = Length.Cells(10),
        Height = Length.Cells(3)
    };

    private static string[] NumberedItems(int count) =>
        [.. Enumerable.Range(0, count).Select(index => $"Item {index}")];

    private static Task<ComponentSurface> MountAsync(ControlBase control, Size size) =>
        ComponentSurface.MountAsync(control, size, TestContext.Current.CancellationToken);

    private static void Observe(ComboBox combo, List<string> events)
    {
        combo.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(ComboBox.IsOpen) or nameof(ComboBox.SelectedIndex))
            {
                events.Add($"PropertyChanged:{eventArgs.PropertyName}");
            }
        };
        combo.SelectionChanged += (_, _) => events.Add("SelectionChanged");
        combo.DropDownOpened += (_, _) => events.Add("DropDownOpened");
        combo.DropDownClosed += (_, _) => events.Add("DropDownClosed");
    }
}
