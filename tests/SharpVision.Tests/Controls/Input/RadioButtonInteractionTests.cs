// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies every RadioButton and group-coordinator interaction - roving Tab entry, arrow
/// walking, exclusivity across reparenting and regrouping, event order, access keys, and mark styles -
/// through a mounted terminal surface, complementing the appearance-oriented RadioButtonSurfaceTests.</summary>
public sealed class RadioButtonInteractionTests
{
    /// <summary>Verifies Tab enters a group exactly once - at the first eligible member when none is
    /// checked, otherwise at the checked member - and leaves to the next control.</summary>
    [Fact]
    public async Task Keyboard_WhenTabEntersGroup_StopsOnlyAtRovingMemberAsync()
    {
        // Arrange
        var one = Radio("One");
        var two = Radio("Two");
        var three = Radio("Three");
        var next = NextButton();
        var stack = Group(one, two, three);
        stack.Children.Add(next);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 7),
            TestContext.Current.CancellationToken);

        // Act and assert no member checked
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(one);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(next);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(one);

        // Act and assert a checked member becomes the only stop: from the (no longer a stop)
        // first member, Tab lands on the checked member, then leaves the group
        await surface.UpdateAsync(() => two.IsChecked = true, "check the second member");
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(two);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(next);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(two);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(next);
        one.IsFocused.ShouldBeFalse();
        three.IsFocused.ShouldBeFalse();
        one.CanTabStop.ShouldBeFalse();
        two.CanTabStop.ShouldBeTrue();
        three.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies an arrow carrying Shift or an application-command modifier neither moves the
    /// selection nor is swallowed, matching the shared scalar-navigation policy, while lock state
    /// still walks the group.</summary>
    /// <param name="modifiers">The modifiers carried by the Down arrow.</param>
    /// <param name="moves">Whether the stroke is expected to walk the group.</param>
    [Theory]
    [InlineData(Modifiers.Shift, false)]
    [InlineData(Modifiers.Control, false)]
    [InlineData(Modifiers.Alt, false)]
    [InlineData(Modifiers.Super, false)]
    [InlineData(Modifiers.CapsLock, true)]
    [InlineData(Modifiers.NumLock, true)]
    public async Task Keyboard_WhenArrowCarriesModifier_WalksOnlyForLockStateAsync(Modifiers modifiers, bool moves)
    {
        // Arrange
        var one = Radio("One", isChecked: true);
        var two = Radio("Two");
        var events = Record(one, two);
        var stack = Group(one, two);
        var observed = new List<bool>();
        stack.KeyDown += (_, eventArgs) => observed.Add(eventArgs.IsHandled);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(one);

        // Act
        await surface.Keyboard.PressAsync(Code.Down, modifiers);

        // Assert
        surface.ShouldHaveFocus(moves ? two : one);
        two.IsChecked.ShouldBe(moves);
        one.IsChecked.ShouldBe(!moves);
        events.Count.ShouldBe(moves ? 3 : 0);
        observed.ShouldBe(moves ? [] : [false], "a walk is consumed before the ancestor sees it; a chorded arrow reaches the ancestor unhandled");
    }

    /// <summary>Verifies the bound command runs on every explicit activation gesture (Enter, Space,
    /// or a pointer press/release) with its parameter, including re-selecting the already checked
    /// member, but not on arrow-key group navigation - RadioGroupCoordinator's arrow path calls
    /// SelectInGroup directly and never routes through Activate. A command that cannot execute
    /// never suppresses the selection itself.</summary>
    [Fact]
    public async Task Command_WhenBoundToMember_RunsAfterEveryActivationWithoutGatingSelectionAsync()
    {
        // Arrange
        List<string> order = [];
        var command = new ProbeCommand { Executing = parameter => order.Add($"execute:{parameter}") };
        var one = Radio("One", isChecked: true);
        var two = Radio("Two");
        two.Command = command;
        two.CommandParameter = "two";
        two.Checked += (_, _) => order.Add("checked");
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two),
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act - arrow onto Two (selection only, no activation), then re-select it with Enter, then
        // click it (both explicit activations).
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Pointer.MoveToAsync(two, new Point(1, 0));
        await surface.Pointer.PressAsync();
        two.IsPressed.ShouldBeTrue("the press on the checked member arms a hold");
        surface.ShouldHaveCapture(two);
        await surface.Pointer.ReleaseAsync();

        // Assert
        two.IsChecked.ShouldBeTrue();
        order.ShouldBe(["checked", "execute:two", "execute:two"]);
        command.Executions.ShouldBe(["two", "two"]);

        // Act - a command that cannot execute still lets the selection move.
        command.CanExecuteValue = false;
        order.Clear();
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        two.IsChecked.ShouldBeTrue();
        one.IsChecked.ShouldBeFalse();
        order.ShouldBe(["checked"]);
        command.Executions.Count.ShouldBe(2, "arrow navigation only selects; it never activates the command");
    }

    /// <summary>Verifies swapping the mark style while mounted repaints every member's mark at once
    /// - the caption shifts with the narrower circle mark - and clearing the local style restores
    /// the themed parentheses.</summary>
    [Fact]
    public async Task Style_WhenMarkStyleSwapsWhileMounted_RepaintsMarksAndRestoresOnClearAsync()
    {
        // Arrange
        var one = Radio("One", isChecked: true);
        var two = Radio("Two");
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two),
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             (•) One
                             ( ) Two
                             """);
        var circle = RadioButtonStyle.Default with { MarkStyle = RadioButtonMarkStyle.Circle };

        // Act
        await surface.UpdateAsync(
            () =>
            {
                one.Style = circle;
                two.Style = circle;
            },
            "swap both members to the circle mark");

        // Assert - a one-cell mark, a gap, then the caption.
        surface.Cell(new Point(0, 0)).Text.ShouldNotBe("(");
        surface.Cell(new Point(0, 0)).Text.ShouldNotBe(surface.Cell(new Point(0, 1)).Text, "checked and unchecked marks differ");
        surface.Cell(new Point(1, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("O");
        surface.Cell(new Point(2, 1)).Text.ShouldBe("T");
        one.ActualStyle.MarkWidth.ShouldBe(1);

        // Act
        await surface.UpdateAsync(
            () =>
            {
                one.Style = null;
                two.Style = null;
            },
            "clear the local styles");

        // Assert
        surface.ShouldRender("""
                             (•) One
                             ( ) Two
                             """);
    }

    /// <summary>Verifies each arrow key moves focus and selection to the adjacent eligible member with
    /// wrapping, raising Unchecked on the old member before Checked and SelectionChanged on the new one.</summary>
    /// <param name="code">The arrow key to press.</param>
    /// <param name="reverse">Whether the key walks toward the preceding member.</param>
    [Theory]
    [InlineData(Code.Right, false)]
    [InlineData(Code.Down, false)]
    [InlineData(Code.Left, true)]
    [InlineData(Code.Up, true)]
    public async Task Keyboard_WhenArrowIsPressed_MovesSelectionWithWrapAndOrderedEventsAsync(Code code, bool reverse)
    {
        // Arrange
        var one = Radio("One", isChecked: true);
        var two = Radio("Two");
        var three = Radio("Three");
        var events = Record(one, two, three);
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two, three),
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(one);

        // Act
        await surface.Keyboard.PressAsync(code);

        // Assert
        var target = reverse ? three : two;
        var name = reverse ? "Three" : "Two";
        surface.ShouldHaveFocus(target);
        target.IsChecked.ShouldBeTrue();
        one.IsChecked.ShouldBeFalse();
        events.ShouldBe([
            $"Unchecked:One:One>{name}:Keyboard",
            $"Checked:{name}:One>{name}:Keyboard",
            $"SelectionChanged:{name}:One>{name}:Keyboard"
        ]);
        surface.ShouldRender(reverse
            ? """
              ( ) One
              ( ) Two
              (•) Three
              """
            : """
              ( ) One
              (•) Two
              ( ) Three
              """);
    }

    /// <summary>Verifies a held arrow keeps walking the group on every repeat and re-raises the
    /// selection events each time, wrapping at the end.</summary>
    [Fact]
    public async Task Keyboard_WhenArrowRepeats_KeepsWalkingAndReraisingEventsAsync()
    {
        // Arrange
        var one = Radio("One");
        var two = Radio("Two");
        var three = Radio("Three");
        var events = Record(one, two, three);
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two, three),
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.RepeatAsync(Code.Down);
        await surface.Keyboard.RepeatAsync(Code.Down);

        // Assert
        surface.ShouldHaveFocus(one);
        one.IsChecked.ShouldBeTrue();
        events.ShouldBe([
            "Checked:Two:null>Two:Keyboard",
            "SelectionChanged:Two:null>Two:Keyboard",
            "Unchecked:Two:Two>Three:Keyboard",
            "Checked:Three:Two>Three:Keyboard",
            "SelectionChanged:Three:Two>Three:Keyboard",
            "Unchecked:Three:Three>One:Keyboard",
            "Checked:One:Three>One:Keyboard",
            "SelectionChanged:One:Three>One:Keyboard"
        ]);
    }

    /// <summary>Verifies arrows skip disabled, hidden, collapsed, and non-focusable members in both
    /// directions and never select any of them.</summary>
    [Fact]
    public async Task Keyboard_WhenMembersAreIneligible_ArrowsSkipThemAsync()
    {
        // Arrange
        var one = Radio("One");
        var disabled = Radio("Disabled", enabled: false);
        var hidden = Radio("Hidden");
        hidden.Visibility = Visibility.Hidden;
        var collapsed = Radio("Collapsed");
        collapsed.Visibility = Visibility.Collapsed;
        var unfocusable = Radio("Unfocusable");
        unfocusable.IsFocusable = false;
        var six = Radio("Six");
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, disabled, hidden, collapsed, unfocusable, six),
            new Size(16, 6),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(one);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Down);
        surface.ShouldHaveFocus(six);
        six.IsChecked.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Down);
        surface.ShouldHaveFocus(one);
        one.IsChecked.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Up);
        surface.ShouldHaveFocus(six);
        six.IsChecked.ShouldBeTrue();
        disabled.IsChecked.ShouldBeFalse();
        hidden.IsChecked.ShouldBeFalse();
        collapsed.IsChecked.ShouldBeFalse();
        unfocusable.IsChecked.ShouldBeFalse();
    }

    /// <summary>Verifies a pointer click moves the exclusive selection with ordered events, a click on
    /// the checked member raises nothing, and a secondary click or an outside release changes nothing.</summary>
    [Fact]
    public async Task Pointer_WhenMembersAreClicked_MovesSelectionWithOrderedEventsAsync()
    {
        // Arrange
        var one = Radio("One", isChecked: true);
        var two = Radio("Two");
        var events = Record(one, two);
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two),
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(two);

        // Assert
        two.IsChecked.ShouldBeTrue();
        one.IsChecked.ShouldBeFalse();
        events.ShouldBe([
            "Unchecked:One:One>Two:Pointer",
            "Checked:Two:One>Two:Pointer",
            "SelectionChanged:Two:One>Two:Pointer"
        ]);

        // Act re-click the checked member, right-click the other, drag out of the other
        events.Clear();
        await surface.Pointer.ClickAsync(two);
        await surface.Pointer.RightClickAsync(one);
        await surface.Pointer.MoveToAsync(one);
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(11, 3));
        await surface.Pointer.ReleaseAsync();

        // Assert
        two.IsChecked.ShouldBeTrue();
        one.IsChecked.ShouldBeFalse();
        events.ShouldBeEmpty();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies removing the checked member leaves the group empty, the roving stop falls back
    /// to the first eligible member, and arrows still walk the remaining members.</summary>
    [Fact]
    public async Task Children_WhenCheckedMemberIsRemoved_LeavesGroupEmptyAndKeepsNavigationAsync()
    {
        // Arrange
        var one = Radio("One");
        var two = Radio("Two", isChecked: true);
        var three = Radio("Three");
        var events = Record(one, two, three);
        var stack = Group(one, two, three);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => stack.Children.Remove(two).ShouldBeTrue(), "remove checked member");

        // Assert
        one.IsChecked.ShouldBeFalse();
        three.IsChecked.ShouldBeFalse();
        two.IsChecked.ShouldBeTrue();
        surface.ShouldRender("""
                             ( ) One
                             ( ) Three
                             """);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(one);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        three.IsChecked.ShouldBeTrue();
        events.ShouldBe([
            "Checked:Three:null>Three:Keyboard",
            "SelectionChanged:Three:null>Three:Keyboard"
        ]);
    }

    /// <summary>Verifies a checked member joining another named group while mounted unchecks that
    /// group's checked member, and arrow navigation then walks the new group in tree order.</summary>
    [Fact]
    public async Task GroupName_WhenCheckedMemberJoinsAnotherGroupWhileMounted_ResolvesExclusivityAsync()
    {
        // Arrange
        var left1 = Radio("L1", group: "left", isChecked: true);
        var left2 = Radio("L2", group: "left");
        var right1 = Radio("R1", group: "right", isChecked: true);
        var right2 = Radio("R2", group: "right");
        var events = Record(left1, left2, right1, right2);
        var root = new Stack
        {
            Orientation = Orientation.Horizontal,
            Children = { Group(left1, left2), Group(right1, right2) }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(16, 2),
            TestContext.Current.CancellationToken);
        left1.IsChecked.ShouldBeTrue();
        right1.IsChecked.ShouldBeTrue();

        // Act
        await surface.UpdateAsync(() => left1.GroupName = "right", "move checked member to the right group");

        // Assert
        left1.IsChecked.ShouldBeTrue();
        right1.IsChecked.ShouldBeFalse();
        left2.IsChecked.ShouldBeFalse();
        events.ShouldBe([
            "Unchecked:R1:R1>L1:Programmatic",
            "SelectionChanged:L1:R1>L1:Programmatic"
        ]);

        // Act walk the new group in tree order from the moved member
        await surface.Pointer.ClickAsync(left1);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        right1.IsChecked.ShouldBeTrue();
        left1.IsChecked.ShouldBeFalse();
        surface.ShouldHaveFocus(right1);
    }

    /// <summary>Verifies a checked unnamed member reparented into another slot unchecks that slot's
    /// checked member, leaving the origin slot untouched.</summary>
    [Fact]
    public async Task Children_WhenCheckedUnnamedMemberMovesToAnotherSlot_UnchecksTheDestinationMemberAsync()
    {
        // Arrange
        var a1 = Radio("A1", group: null, isChecked: true);
        var a2 = Radio("A2", group: null);
        var b1 = Radio("B1", group: null, isChecked: true);
        var groupA = Group(a1, a2);
        var groupB = Group(b1);
        var root = new Stack { Orientation = Orientation.Horizontal, Children = { groupA, groupB } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(16, 2),
            TestContext.Current.CancellationToken);
        a1.IsChecked.ShouldBeTrue();
        b1.IsChecked.ShouldBeTrue();

        // Act
        await surface.UpdateAsync(
            () =>
            {
                _ = groupA.Children.Remove(a1);
                groupB.Children.Add(a1);
            },
            "reparent checked member");

        // Assert
        a1.IsChecked.ShouldBeTrue();
        b1.IsChecked.ShouldBeFalse();
        a2.IsChecked.ShouldBeFalse();
        surface.ShouldRender("""
                             ( ) A2( ) B1
                                   (•) A1
                             """);
    }

    /// <summary>Verifies Tab skips a group whose members are all ineligible.</summary>
    [Fact]
    public async Task Keyboard_WhenNoMemberIsEligible_TabSkipsTheGroupAsync()
    {
        // Arrange
        var one = Radio("One", enabled: false);
        var two = Radio("Two", enabled: false);
        var next = NextButton();
        var stack = Group(one, two);
        stack.Children.Add(next);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(next);
        one.CanTabStop.ShouldBeFalse();
        two.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies a one-member group selects itself from an arrow with the keyboard cause, and a
    /// further arrow raises nothing more.</summary>
    [Fact]
    public async Task Keyboard_WhenGroupHasOneMember_ArrowSelectsItOnceAsync()
    {
        // Arrange
        var only = Radio("Only");
        var events = Record(only);
        await using var surface = await ComponentSurface.MountAsync(
            Group(only),
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert
        only.IsChecked.ShouldBeTrue();
        surface.ShouldHaveFocus(only);
        events.ShouldBe(["Checked:Only:null>Only:Keyboard", "SelectionChanged:Only:null>Only:Keyboard"]);
        surface.ShouldRender("(•) Only");
    }

    /// <summary>Verifies Enter selects immediately and never toggles back, a Space hold selects only on
    /// its release, and a Tab mid-hold cancels the selection.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterOrSpaceActivates_SelectsOnceAndNeverTogglesAsync()
    {
        // Arrange
        var one = Radio("One");
        var two = Radio("Two");
        var next = NextButton();
        var events = Record(one, two);
        var stack = Group(one, two);
        stack.Children.Add(next);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(
            () => stack.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");

        // Act Space hold and release
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        surface.ShouldHaveState(one, VisualState.Focused | VisualState.Pressed);
        one.IsChecked.ShouldBeFalse();
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        // Assert
        one.IsChecked.ShouldBeTrue();
        events.ShouldBe(["Checked:One:null>One:Keyboard", "SelectionChanged:One:null>One:Keyboard"]);

        // Act Enter on the checked member
        events.Clear();
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert nothing toggles back
        one.IsChecked.ShouldBeTrue();
        events.ShouldBeEmpty();

        // Act Down to Two, then Space hold interrupted by Tab
        await surface.Keyboard.PressAsync(Code.Down);
        two.IsChecked.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Up);
        one.IsChecked.ShouldBeTrue();
        events.Clear();
        await surface.Keyboard.PressAsync(Code.Down);
        events.Clear();
        await surface.UpdateAsync(() => two.IsChecked = false, "clear the group");
        events.Clear();
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        surface.ShouldHaveState(two, VisualState.Focused | VisualState.Pressed);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        // Assert - the hold was cancelled on the member that lost focus, and the orphaned release
        // armed nothing on the new focus owner.
        surface.ShouldHaveFocus(next);
        surface.ShouldHaveState(two, VisualState.Normal);
        two.IsPressed.ShouldBeFalse();
        next.IsPressed.ShouldBeFalse();
        two.IsChecked.ShouldBeFalse();
        events.ShouldBeEmpty();
    }

    /// <summary>Verifies the access key selects and focuses its member from the neutral host focus,
    /// unchecking the previously selected member.</summary>
    [Fact]
    public async Task Keyboard_WhenAccessKeyIsPressed_SelectsAndFocusesMemberAsync()
    {
        // Arrange
        var one = Radio("&One", isChecked: true);
        var two = Radio("&Two");
        var events = Record(one, two);
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two),
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.SendAsync("\u001b[116;3:1u"u8.ToArray(), "press Alt+T");

        // Assert
        two.IsChecked.ShouldBeTrue();
        one.IsChecked.ShouldBeFalse();
        surface.ShouldHaveFocus(two);
        events.ShouldBe([
            "Unchecked:&One:&One>&Two:Keyboard",
            "Checked:&Two:&One>&Two:Keyboard",
            "SelectionChanged:&Two:&One>&Two:Keyboard"
        ]);
        surface.ShouldRender("""
                             ( ) One
                             (•) Two
                             """);
    }

    /// <summary>Verifies clearing IsChecked on the mounted checked member renders it unselected and
    /// publishes Unchecked then SelectionChanged with a null current member.</summary>
    [Fact]
    public async Task IsChecked_WhenClearedWhileMounted_RendersUnselectedAndPublishesAsync()
    {
        // Arrange
        var one = Radio("One", isChecked: true);
        var two = Radio("Two");
        var events = Record(one, two);
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two),
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             (•) One
                             ( ) Two
                             """);

        // Act
        await surface.UpdateAsync(() => one.IsChecked = false, "clear the checked member");

        // Assert
        one.IsChecked.ShouldBeFalse();
        two.IsChecked.ShouldBeFalse();
        events.ShouldBe(["Unchecked:One:One>null:Programmatic", "SelectionChanged:One:One>null:Programmatic"]);
        surface.ShouldRender("""
                             ( ) One
                             ( ) Two
                             """);
    }

    /// <summary>Verifies a disabled member ignores clicks yet keeps its retained selection until another
    /// member is selected, and a disabled checked member is skipped by Tab entry and arrows.</summary>
    [Fact]
    public async Task Pointer_WhenMemberIsDisabled_ClickIsInertAndSelectionIsRetainedAsync()
    {
        // Arrange
        var one = Radio("One", isChecked: true);
        var two = Radio("Two");
        var events = Record(one, two);
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two),
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => one.IsEnabled = false, "disable the checked member");

        // Act click the disabled member and the other member
        await surface.Pointer.ClickAsync(one);
        one.IsChecked.ShouldBeTrue();
        events.ShouldBeEmpty();
        await surface.Pointer.ClickAsync(two);

        // Assert the disabled member still gets unchecked
        two.IsChecked.ShouldBeTrue();
        one.IsChecked.ShouldBeFalse();
        events.ShouldBe([
            "Unchecked:One:One>Two:Pointer",
            "Checked:Two:One>Two:Pointer",
            "SelectionChanged:Two:One>Two:Pointer"
        ]);

        // Act disable the now-checked member and re-enable the other, then Tab in and walk
        await surface.UpdateAsync(
            () =>
            {
                two.IsEnabled = false;
                one.IsEnabled = true;
                surface.Application.Focus.Focus(null).ShouldBeTrue();
            },
            "swap enabled members");
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(one);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert the sole eligible member wraps to itself and takes the selection
        one.IsChecked.ShouldBeTrue();
        two.IsChecked.ShouldBeFalse();
        surface.ShouldHaveFocus(one);
    }

    /// <summary>Verifies the one-cell glyph style renders both states and selects from its mark cell.</summary>
    [Fact]
    public async Task Pointer_WhenGlyphStyleIsUsed_RendersOneCellMarksAndSelectsFromMarkCellAsync()
    {
        // Arrange
        var one = Radio("One");
        one.Style = RadioButtonStyle.Glyph;
        var two = Radio("Two");
        two.Style = RadioButtonStyle.Glyph;
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two),
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             ○ One
                             ○ Two
                             """);
        one.ActualStyle.MarkWidth.ShouldBe(1);

        // Act
        await surface.Pointer.ClickAsync(two, new Point(0, 0));

        // Assert
        two.IsChecked.ShouldBeTrue();
        surface.ShouldRender("""
                             ○ One
                             ◉ Two
                             """);

        // Act
        await surface.Pointer.ClickAsync(one, new Point(0, 0));

        // Assert
        one.IsChecked.ShouldBeTrue();
        two.IsChecked.ShouldBeFalse();
        surface.ShouldRender("""
                             ◉ One
                             ○ Two
                             """);
    }

    /// <summary>Verifies a named group spanning two containers walks its members in tree order and wraps
    /// across the container boundary.</summary>
    [Fact]
    public async Task Keyboard_WhenNamedGroupSpansContainers_ArrowsWalkTreeOrderAsync()
    {
        // Arrange
        var one = Radio("One");
        var two = Radio("Two");
        var three = Radio("Three");
        var root = new Stack
        {
            Orientation = Orientation.Vertical,
            Children = { Group(one, two), Group(three) }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(one);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Down);
        surface.ShouldHaveFocus(two);
        await surface.Keyboard.PressAsync(Code.Down);
        surface.ShouldHaveFocus(three);
        three.IsChecked.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Down);
        surface.ShouldHaveFocus(one);
        one.IsChecked.ShouldBeTrue();
        three.IsChecked.ShouldBeFalse();
        await surface.Keyboard.PressAsync(Code.Up);
        surface.ShouldHaveFocus(three);
    }

    /// <summary>Verifies unnamed members in different slots form independent groups: each slot keeps
    /// its own selection and arrows never leave the slot.</summary>
    [Fact]
    public async Task Pointer_WhenUnnamedGroupsLiveInDifferentSlots_SelectionsAreIndependentAsync()
    {
        // Arrange
        var a1 = Radio("A1", group: null);
        var a2 = Radio("A2", group: null);
        var b1 = Radio("B1", group: null);
        var root = new Stack
        {
            Orientation = Orientation.Horizontal,
            Children = { Group(a1, a2), Group(b1) }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(16, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(a1);
        await surface.Pointer.ClickAsync(b1);

        // Assert
        a1.IsChecked.ShouldBeTrue();
        b1.IsChecked.ShouldBeTrue();

        // Act
        await surface.Pointer.ClickAsync(a2);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        a1.IsChecked.ShouldBeTrue();
        a2.IsChecked.ShouldBeFalse();
        b1.IsChecked.ShouldBeTrue();
        surface.ShouldHaveFocus(a1);
    }

    /// <summary>Verifies an arrow whose focus transfer is cancelled by a focus-changing handler leaves
    /// focus and selection untouched and raises nothing.</summary>
    [Fact]
    public async Task Keyboard_WhenFocusMoveIsCancelled_ArrowLeavesSelectionUnchangedAsync()
    {
        // Arrange
        var one = Radio("One", isChecked: true);
        var two = Radio("Two");
        var events = Record(one, two);
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two),
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, two))
                {
                    eventArgs.Cancel = true;
                }
            },
            "refuse focus for the second member");

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        surface.ShouldHaveFocus(one);
        one.IsChecked.ShouldBeTrue();
        two.IsChecked.ShouldBeFalse();
        events.ShouldBeEmpty();
    }

    /// <summary>Verifies an arrow whose target leaves the group from its own GotFocus handler moves
    /// focus but does not select the now-foreign member.</summary>
    [Fact]
    public async Task Keyboard_WhenFocusCallbackRegroupsTarget_ArrowFocusesWithoutSelectingAsync()
    {
        // Arrange
        var one = Radio("One", isChecked: true);
        var two = Radio("Two");
        two.GotFocus += (_, _) => two.GroupName = "elsewhere";
        var events = Record(one, two);
        await using var surface = await ComponentSurface.MountAsync(
            Group(one, two),
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        surface.ShouldHaveFocus(two);
        two.GroupName.ShouldBe("elsewhere");
        two.IsChecked.ShouldBeFalse();
        one.IsChecked.ShouldBeTrue();
        events.ShouldBeEmpty();
    }

    private static RadioButton Radio(
        string text,
        string? group = "g",
        bool enabled = true,
        bool isChecked = false) => new()
        {
            Text = text,
            GroupName = group,
            IsChecked = isChecked,
            IsEnabled = enabled
        };

    private static Stack Group(params RadioButton[] members)
    {
        var group = new Stack { Orientation = Orientation.Vertical };

        foreach (var member in members)
        {
            group.Children.Add(member);
        }

        return group;
    }

    private static Button NextButton() => new("Next")
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        Width = Length.Cells(8),
        Height = Length.Cells(3)
    };

    private static List<string> Record(params RadioButton[] members)
    {
        List<string> events = [];

        foreach (var member in members)
        {
            member.Checked += (_, eventArgs) => events.Add(Describe("Checked", member, eventArgs));
            member.Unchecked += (_, eventArgs) => events.Add(Describe("Unchecked", member, eventArgs));
            member.SelectionChanged += (_, eventArgs) => events.Add(Describe("SelectionChanged", member, eventArgs));
        }

        return events;
    }

    private static string Describe(string name, RadioButton member, RadioButtonSelectionChangedEventArgs eventArgs) =>
        $"{name}:{member.Text}:{eventArgs.Previous?.Text ?? "null"}>{eventArgs.Current?.Text ?? "null"}:{eventArgs.Cause}";
}
