// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies NavigationView keyboard, access-key, pointer, filtering, header, scrolling,
/// focus-retention, and disposal behavior through mounted surfaces.</summary>
public sealed class NavigationViewInteractionTests
{
    /// <summary>Verifies Home and End jump across groups and sections to the first and last available
    /// entries, selecting them and moving the current marker.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeAndEndArePressed_JumpAcrossGroupsAndSectionsAsync()
    {
        // Arrange
        var first = new NavigationViewItem { Text = "First" };
        var child = new NavigationViewItem { Text = "Child" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(child);
        var last = new NavigationViewItem { Text = "Last" };
        var footer = new NavigationViewItem { Text = "Footer" };
        var view = CreateView(14);
        view.Items.Add(first);
        view.Items.Add(group);
        view.Items.Add(last);
        view.FooterItems.Add(footer);
        var changes = new List<(string? Previous, string? Current)>();
        view.SelectionChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousItem?.Text, eventArgs.CurrentItem?.Text));
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 6),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert End
        await surface.Keyboard.PressAsync(Code.End);
        view.SelectedItem.ShouldBeSameAs(footer);
        surface.Cell(new Point(1, 5)).Text.ShouldBe("›");

        // Act and assert Home
        await surface.Keyboard.PressAsync(Code.Home);
        view.SelectedItem.ShouldBeSameAs(first);
        surface.Cell(new Point(1, 0)).Text.ShouldBe("›");
        surface.Cell(new Point(1, 5)).Text.ShouldBe("·");
        changes.ShouldBe([(null, "Footer"), ("Footer", "First")]);
        surface.ShouldHaveFocus(view);
    }

    /// <summary>Verifies arrows skip the descendants of a collapsed group, Enter expands the group
    /// without changing the selection, and the next Down then enters the revealed child.</summary>
    [Fact]
    public async Task Keyboard_WhenGroupIsCollapsed_ArrowsSkipItsChildrenUntilExpandedAsync()
    {
        // Arrange
        var first = new NavigationViewItem { Text = "First" };
        var child = new NavigationViewItem { Text = "Child" };
        var group = new NavigationViewGroup { Header = "Group", IsExpanded = false };
        group.Items.Add(child);
        var last = new NavigationViewItem { Text = "Last" };
        var view = CreateView(14);
        view.Items.Add(first);
        view.Items.Add(group);
        view.Items.Add(last);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(first);

        // Act skip over the collapsed group
        await surface.Keyboard.PressAsync(Code.Down);
        (group.GetAppearanceState() & VisualState.Current).ShouldBe(VisualState.Current);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert the collapsed child was skipped
        view.SelectedItem.ShouldBeSameAs(last);
        child.EffectiveIsVisible.ShouldBeFalse();

        // Act go back, expand with Enter, and step into the child
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Enter);
        group.IsExpanded.ShouldBeTrue();
        view.SelectedItem.ShouldBeSameAs(last);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        view.SelectedItem.ShouldBeSameAs(child);
        surface.ShouldRender("""
                              · First
                              ▼ Group
                                › Child
                              · Last

                             """);
    }

    /// <summary>Verifies Space toggles the current group and invokes the current item exactly like
    /// Enter, firing the item's Invoked event once.</summary>
    [Fact]
    public async Task Keyboard_WhenSpaceIsPressed_TogglesGroupsAndInvokesItemsAsync()
    {
        // Arrange
        var child = new NavigationViewItem { Text = "Child" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(child);
        var item = new NavigationViewItem { Text = "Item" };
        var view = CreateView(14);
        view.Items.Add(group);
        view.Items.Add(item);
        var invoked = 0;
        item.Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act toggle the first entry (the group) with Space from a null current
        await surface.Keyboard.TypeAsync(" ");

        // Assert
        group.IsExpanded.ShouldBeFalse();
        view.SelectedItem.ShouldBeNull();

        // Act move to the item and invoke it with Space
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(item);
        invoked.ShouldBe(0);
        await surface.Keyboard.TypeAsync(" ");

        // Assert
        invoked.ShouldBe(1);
        view.SelectedItem.ShouldBeSameAs(item);
        surface.ShouldHaveFocus(view);
    }

    /// <summary>Verifies Enter with no current entry establishes the first available entry and
    /// applies its action: an item is invoked and selected.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterIsPressedWithoutCurrent_InvokesFirstAvailableItemAsync()
    {
        // Arrange
        var disabled = new NavigationViewItem { Text = "Off", IsEnabled = false };
        var first = new NavigationViewItem { Text = "First" };
        var view = CreateView(14);
        view.Items.Add(disabled);
        view.Items.Add(first);
        var invoked = 0;
        first.Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        invoked.ShouldBe(1);
        view.SelectedItem.ShouldBeSameAs(first);
        disabled.IsSelected.ShouldBeFalse();
    }

    /// <summary>Verifies an item access key focuses the view, selects and invokes the item, and a
    /// group access key focuses the view and toggles the group.</summary>
    [Fact]
    public async Task AccessKey_WhenItemOrGroupMnemonicMatches_InvokesOrTogglesAndFocusesAsync()
    {
        // Arrange
        var zoom = new NavigationViewItem { Text = "&Zoom" };
        var child = new NavigationViewItem { Text = "Child" };
        var tools = new NavigationViewGroup { Header = "&Tools" };
        tools.Items.Add(child);
        var button = new Button("Elsewhere");
        var view = CreateView(14);
        view.Items.Add(zoom);
        view.Items.Add(tools);
        var stack = new Stack();
        stack.Children.Add(button);
        stack.Children.Add(view);
        var invoked = 0;
        zoom.Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(14, 8),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(button);
        surface.ShouldHaveFocus(button);

        // Act Alt+Z
        await surface.SendAsync("\x1b[122;3:1u"u8.ToArray(), "Alt+Z");

        // Assert
        invoked.ShouldBe(1);
        view.SelectedItem.ShouldBeSameAs(zoom);
        surface.ShouldHaveFocus(view);

        // Act Alt+T twice
        await surface.SendAsync("\x1b[116;3:1u"u8.ToArray(), "Alt+T");
        tools.IsExpanded.ShouldBeFalse();
        child.EffectiveIsVisible.ShouldBeFalse();
        await surface.SendAsync("\x1b[116;3:1u"u8.ToArray(), "Alt+T again");

        // Assert
        tools.IsExpanded.ShouldBeTrue();
        view.SelectedItem.ShouldBeSameAs(zoom);
        surface.ShouldHaveFocus(view);
    }

    /// <summary>Verifies UseMnemonic=false renders the ampersand literally and stops the key from
    /// acting as an access key.</summary>
    [Fact]
    public async Task AccessKey_WhenUseMnemonicIsDisabled_RendersAmpersandAndIgnoresTheKeyAsync()
    {
        // Arrange
        var zoom = new NavigationViewItem { Text = "&Zoom", UseMnemonic = false };
        var view = CreateView(14);
        view.Items.Add(zoom);
        var invoked = 0;
        zoom.Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.SendAsync("\x1b[122;3:1u"u8.ToArray(), "Alt+Z");

        // Assert
        invoked.ShouldBe(0);
        view.SelectedItem.ShouldBeNull();
        surface.ShouldRender("""
                              · &Zoom

                             """);
    }

    /// <summary>Verifies Shift-modified arrows are left unhandled and do not move the selection,
    /// while a held (repeated) Down keeps moving.</summary>
    [Fact]
    public async Task Keyboard_WhenArrowIsShiftedOrRepeated_HonorsTheModifierPolicyAsync()
    {
        // Arrange
        var items = Enumerable.Range(0, 4).Select(index => new NavigationViewItem { Text = $"Item {index}" }).ToArray();
        var view = CreateView(14);

        foreach (var item in items)
        {
            view.Items.Add(item);
        }

        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(items[0]);

        // Act Shift+Down is not navigation
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Shift);
        view.SelectedItem.ShouldBeSameAs(items[0]);

        // Act repeated Down keeps moving
        await surface.Keyboard.RepeatAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(items[1]);
        await surface.Keyboard.RepeatAsync(Code.Down);

        // Assert
        view.SelectedItem.ShouldBeSameAs(items[2]);
    }

    /// <summary>Verifies a pointer press on a disabled item neither selects nor invokes it and leaves
    /// the previous selection rendered.</summary>
    [Fact]
    public async Task Pointer_WhenDisabledItemIsClicked_DoesNotChangeSelectionAsync()
    {
        // Arrange
        var first = new NavigationViewItem { Text = "First" };
        var disabled = new NavigationViewItem { Text = "Off", IsEnabled = false };
        var view = CreateView(14);
        view.Items.Add(first);
        view.Items.Add(disabled);
        var invoked = 0;
        disabled.Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(first);
        view.SelectedItem.ShouldBeSameAs(first);

        // Act
        await surface.Pointer.ClickAsync(disabled);

        // Assert
        invoked.ShouldBe(0);
        view.SelectedItem.ShouldBeSameAs(first);
        disabled.IsSelected.ShouldBeFalse();
        surface.Cell(new Point(1, 0)).Text.ShouldBe("›");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("·");
        surface.ShouldHaveFocus(view);
    }

    /// <summary>Verifies the filtering scenario: collapsing non-matching items and emptied groups
    /// repairs a hidden selection to the next visible item, keyboard navigation follows the visible
    /// order, and restoring visibility brings every row back without stealing the selection.</summary>
    [Fact]
    public async Task Visibility_WhenItemsAndGroupsAreFilteredThenRestored_RepairsAndRestoresRowsAsync()
    {
        // Arrange
        var apple = new NavigationViewItem { Text = "Apple" };
        var banana = new NavigationViewItem { Text = "Banana" };
        var cherry = new NavigationViewItem { Text = "Cherry" };
        var date = new NavigationViewItem { Text = "Date" };
        var fruits = new NavigationViewGroup { Header = "Fruits" };
        fruits.Items.Add(cherry);
        fruits.Items.Add(date);
        var eggplant = new NavigationViewItem { Text = "Eggplant" };
        var veg = new NavigationViewGroup { Header = "Veg" };
        veg.Items.Add(eggplant);
        var view = CreateView(14);
        view.Items.Add(apple);
        view.Items.Add(banana);
        view.Items.Add(fruits);
        view.Items.Add(veg);
        var changes = new List<string?>();
        view.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs.CurrentItem?.Text);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(apple);
        surface.ShouldRender("""
                              › Apple
                              · Banana
                              ▼ Fruits
                                · Cherry
                                · Date
                              ▼ Veg
                                · Eggplant

                             """);

        // Act filter to entries containing "an"
        await surface.UpdateAsync(
            () =>
            {
                apple.Visibility = Visibility.Collapsed;
                cherry.Visibility = Visibility.Collapsed;
                date.Visibility = Visibility.Collapsed;
                fruits.Visibility = Visibility.Collapsed;
            },
            "hide non-matching items and the emptied group");

        // Assert
        view.SelectedItem.ShouldBeSameAs(banana);
        surface.ShouldRender("""
                              › Banana
                              ▼ Veg
                                · Eggplant




                             """);

        // Act navigate through the visible order only
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(eggplant);

        // Act restore
        await surface.UpdateAsync(
            () =>
            {
                apple.Visibility = Visibility.Visible;
                cherry.Visibility = Visibility.Visible;
                date.Visibility = Visibility.Visible;
                fruits.Visibility = Visibility.Visible;
            },
            "restore every entry");

        // Assert
        view.SelectedItem.ShouldBeSameAs(eggplant);
        changes.ShouldBe(["Apple", "Banana", "Eggplant"]);
        surface.ShouldRender("""
                              · Apple
                              · Banana
                              ▼ Fruits
                                · Cherry
                                · Date
                              ▼ Veg
                                › Eggplant

                             """);
        surface.ShouldHaveFocus(view);
    }

    /// <summary>Verifies removing or disposing the selected item after layout repairs the selection
    /// to the next item, makes that item keyboard-current so the next arrow steps relative to it
    /// instead of jumping to a section endpoint, moves the rendered marker, and keeps focus on the
    /// view.</summary>
    [Theory]
    [InlineData("pointer", "remove")]
    [InlineData("keyboard", "remove")]
    [InlineData("keyboard", "dispose")]
    public async Task Items_WhenSelectedItemIsRemovedAfterLayout_RepairsAndKeepsFocusAsync(string selection, string removal)
    {
        // Arrange
        var first = new NavigationViewItem { Text = "First" };
        var second = new NavigationViewItem { Text = "Second" };
        var third = new NavigationViewItem { Text = "Third" };
        var view = CreateView(14);
        view.Items.Add(first);
        view.Items.Add(second);
        view.Items.Add(third);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 4),
            TestContext.Current.CancellationToken);

        if (selection == "pointer")
        {
            await surface.Pointer.ClickAsync(second);
            await surface.Pointer.MoveToAsync(view, new Point(0, 3));
        }
        else
        {
            await surface.Keyboard.PressAsync(Code.Tab);
            await surface.Keyboard.PressAsync(Code.Down);
            await surface.Keyboard.PressAsync(Code.Down);
        }

        view.SelectedItem.ShouldBeSameAs(second);

        // Act
        await surface.UpdateAsync(
            () =>
            {
                if (removal == "remove")
                {
                    view.Items.Remove(second).ShouldBeTrue();
                }
                else
                {
                    second.Dispose();
                }
            },
            $"{removal} the selected item");

        // Assert
        view.SelectedItem?.Text.ShouldBe("Third");
        second.IsSelected.ShouldBeFalse();
        view.Items.Count.ShouldBe(2);
        surface.ShouldRender("""
                              · First
                              › Third


                             """);
        surface.ShouldHaveFocus(view);
        await surface.Keyboard.PressAsync(Code.Up);
        view.SelectedItem.ShouldBeSameAs(first);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(third);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(third);
    }

    /// <summary>Verifies the header row toggles with the Header property after layout and shifts
    /// the entries beneath it.</summary>
    [Fact]
    public async Task Header_WhenSetAndClearedAfterLayout_TogglesTheHeaderRowAsync()
    {
        // Arrange
        var item = new NavigationViewItem { Text = "Item" };
        var view = CreateView(10);
        view.Items.Add(item);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender(" · Item   \n          \n          ");

        // Act set
        await surface.UpdateAsync(() => view.Header = "Menu", "set the header");

        // Assert
        surface.ShouldRender("Menu      \n · Item   \n          ");
        (surface.Cell(new Point(0, 0)).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        item.Bounds.Y.ShouldBe(1);

        // Act clear
        await surface.UpdateAsync(() => view.Header = string.Empty, "clear the header");

        // Assert
        surface.ShouldRender(" · Item   \n          \n          ");
        item.Bounds.Y.ShouldBe(0);
    }

    /// <summary>Verifies End reveals the last of many items by scrolling and Home scrolls back.</summary>
    [Fact]
    public async Task Keyboard_WhenManyItemsOverflow_EndAndHomeScrollTheCurrentEntryIntoViewAsync()
    {
        // Arrange
        var items = Enumerable.Range(0, 10).Select(index => new NavigationViewItem { Text = $"Item {index}" }).ToArray();
        var view = CreateView(14);

        foreach (var item in items)
        {
            view.Items.Add(item);
        }

        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act End
        await surface.Keyboard.PressAsync(Code.End);

        // Assert
        view.SelectedItem.ShouldBeSameAs(items[9]);
        view.VerticalOffset.ShouldBe(6);
        surface.Cell(new Point(1, 3)).Text.ShouldBe("›");
        surface.Cell(new Point(3, 3)).Text.ShouldBe("I");

        // Act Home
        await surface.Keyboard.PressAsync(Code.Home);

        // Assert
        view.SelectedItem.ShouldBeSameAs(items[0]);
        view.VerticalOffset.ShouldBe(0);
        surface.Cell(new Point(1, 0)).Text.ShouldBe("›");
    }

    /// <summary>Verifies adding and inserting items while the view is focused keeps focus on the
    /// view and the new entries join keyboard navigation at their positions.</summary>
    [Fact]
    public async Task Items_WhenAddedWhileFocused_KeepFocusAndJoinNavigationAsync()
    {
        // Arrange
        var first = new NavigationViewItem { Text = "First" };
        var view = CreateView(14);
        view.Items.Add(first);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(first);
        var inserted = new NavigationViewItem { Text = "Zero" };
        var appended = new NavigationViewItem { Text = "Last" };

        // Act
        await surface.UpdateAsync(
            () =>
            {
                view.Items.Insert(0, inserted);
                view.Items.Add(appended);
            },
            "insert before and append after the selection");

        // Assert
        surface.ShouldHaveFocus(view);
        view.SelectedItem.ShouldBeSameAs(first);
        await surface.Keyboard.PressAsync(Code.Up);
        view.SelectedItem.ShouldBeSameAs(inserted);
        await surface.Keyboard.PressAsync(Code.End);
        view.SelectedItem.ShouldBeSameAs(appended);
        surface.ShouldRender("""
                              · Zero
                              · First
                              › Last

                             """);
    }

    /// <summary>Verifies disposing the focused view clears application focus and its selection
    /// without faulting, and its former items report no selection.</summary>
    [Fact]
    public async Task Dispose_WhenViewIsFocusedAndSelected_ClearsFocusAndSelectionAsync()
    {
        // Arrange
        var item = new NavigationViewItem { Text = "Item" };
        var view = CreateView(14);
        view.Items.Add(item);
        var button = new Button("Other");
        var stack = new Stack();
        stack.Children.Add(view);
        stack.Children.Add(button);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(14, 6),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(item);
        surface.ShouldHaveFocus(view);
        view.SelectedItem.ShouldBeSameAs(item);

        // Act
        await surface.UpdateAsync(view.Dispose, "dispose the focused view");

        // Assert
        surface.ShouldHaveFocus(null);
        view.IsDisposed.ShouldBeTrue();
        view.SelectedItem.ShouldBeNull();
        item.IsSelected.ShouldBeFalse();
        stack.Children.Count.ShouldBe(1);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(button);
    }

    /// <summary>Verifies a SelectionChanged handler that re-selects another item publishes only the
    /// newest selection through the routed keyboard path.</summary>
    [Fact]
    public async Task SelectionChanged_WhenHandlerReselects_PublishesOnlyTheNewestSelectionAsync()
    {
        // Arrange
        var first = new NavigationViewItem { Text = "First" };
        var second = new NavigationViewItem { Text = "Second" };
        var third = new NavigationViewItem { Text = "Third" };
        var view = CreateView(14);
        view.Items.Add(first);
        view.Items.Add(second);
        view.Items.Add(third);
        var observed = new List<string?>();
        view.SelectionChanged += (_, eventArgs) =>
        {
            observed.Add(eventArgs.CurrentItem?.Text);

            if (ReferenceEquals(eventArgs.CurrentItem, second))
            {
                view.SelectItem(third);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        view.SelectedItem.ShouldBeSameAs(third);
        observed.ShouldBe(["First", "Second", "Third"]);
        second.IsSelected.ShouldBeFalse();
        surface.Cell(new Point(1, 2)).Text.ShouldBe("›");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("·");
    }

    private static NavigationView CreateView(int width) => new()
    {
        Width = Length.Cells(width),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
}
