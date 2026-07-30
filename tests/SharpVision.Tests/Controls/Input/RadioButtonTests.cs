// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies RadioButton grouping, transactions, navigation, ownership, and cells.</summary>
public sealed class RadioButtonTests
{
    /// <summary>Verifies local style ownership overrides Theme fallback and clearing restores it.</summary>
    [Fact]
    public void Style_WhenThemeAndLocalValuesChange_UsesDocumentedPrecedence()
    {
        var theme = CreateTheme(RadioButtonStyle.Glyph);
        var radio = new RadioButton();

        radio.SetTheme(theme);
        radio.Style.ShouldBeNull();
        radio.ActualStyle.MarkStyle.ShouldBe(RadioButtonStyle.Default.MarkStyle);
        radio.ActualStyle.Glyphs.ShouldBe(RadioButtonStyle.Default.Glyphs);
        radio.Style = RadioButtonStyle.Parentheses;
        radio.ActualStyle.MarkStyle.ShouldBe(RadioButtonStyle.Parentheses.MarkStyle);
        radio.ActualStyle.Glyphs.ShouldBe(RadioButtonStyle.Parentheses.Glyphs);
        radio.Style = null;
        radio.ActualStyle.ShouldBe(RadioButtonStyle.Default);
    }

    /// <summary>Verifies a mark-width style change invalidates measurement.</summary>
    [Fact]
    public void Style_WhenMarkWidthChanges_InvalidatesMeasure()
    {
        var radio = new RadioButton { Style = RadioButtonStyle.Parentheses };
        radio.Clear(Invalidation.All);

        radio.Style = RadioButtonStyle.Glyph;

        radio.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies groups start empty and user activation selects without toggling off.</summary>
    [Fact]
    public void PerformClick_WhenGroupStartsEmpty_SelectsOnlyOnce()
    {
        var radio = new RadioButton();
        var checkedEvents = 0;
        radio.Checked += (_, _) => checkedEvents++;

        radio.PerformClick();
        radio.PerformClick();

        radio.IsChecked.ShouldBeTrue();
        checkedEvents.ShouldBe(1);
    }

    /// <summary>Verifies null-name siblings remain mutually exclusive.</summary>
    [Fact]
    public void IsChecked_WhenSiblingSelectionChanges_CommitsExclusiveStateBeforeEvents()
    {
        var parent = new Stack();
        var first = new RadioButton { IsChecked = true };
        var second = new RadioButton();
        parent.Children.Add(first);
        parent.Children.Add(second);
        List<string> order = [];
        first.Unchecked += (_, eventArgs) =>
        {
            first.IsChecked.ShouldBeFalse();
            second.IsChecked.ShouldBeTrue();
            eventArgs.Previous.ShouldBeSameAs(first);
            eventArgs.Current.ShouldBeSameAs(second);
            order.Add("old");
        };
        second.Checked += (_, _) => order.Add("new");
        second.SelectionChanged += (_, _) => order.Add("changed");

        second.IsChecked = true;

        order.ShouldBe(["old", "new", "changed"]);
    }

    /// <summary>Verifies old-member property observers see the complete new group selection.</summary>
    [Fact]
    public void IsChecked_WhenSiblingSelectionChanges_StagesBothFieldsBeforePropertyNotifications()
    {
        var parent = new Stack();
        var first = new RadioButton { IsChecked = true };
        var second = new RadioButton();
        parent.Children.Add(first);
        parent.Children.Add(second);
        var observed = false;
        first.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(RadioButton.IsChecked))
            {
                first.IsChecked.ShouldBeFalse();
                second.IsChecked.ShouldBeTrue();
                observed = true;
            }
        };

        second.IsChecked = true;

        observed.ShouldBeTrue();
    }

    /// <summary>Verifies named groups span containers but remain scoped to one root.</summary>
    [Fact]
    public void IsChecked_WhenNamedMembersAreInDifferentContainers_UsesRootScope()
    {
        var root = new Stack();
        var left = new Stack();
        var right = new Stack();
        root.Children.Add(left);
        root.Children.Add(right);
        var first = new RadioButton { GroupName = "density", IsChecked = true };
        var second = new RadioButton { GroupName = "density" };
        var unrelated = new RadioButton { GroupName = "theme", IsChecked = true };
        left.Children.Add(first);
        right.Children.Add(second);
        right.Children.Add(unrelated);

        second.IsChecked = true;

        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
        unrelated.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies named groups scope to the nearest enclosing Window instead of resolving
    /// across every independently-opened top-level window that ultimately shares one process-wide
    /// Screen root — two windows reusing the same reusable dialog template with the same
    /// GroupName must not corrupt each other's selection (see #113).</summary>
    [Fact]
    public void IsChecked_WhenNamedMembersAreInDifferentWindows_ScopesToOwningWindow()
    {
        var firstFirst = new RadioButton { GroupName = "alignment", IsChecked = true };
        var firstSecond = new RadioButton { GroupName = "alignment" };
        var firstWindow = new Window { Content = new Stack { Children = { firstFirst, firstSecond } } };

        var secondFirst = new RadioButton { GroupName = "alignment", IsChecked = true };
        var secondSecond = new RadioButton { GroupName = "alignment" };
        var secondWindow = new Window { Content = new Stack { Children = { secondFirst, secondSecond } } };
        var screen = new ProbeScreen();
        screen.AddPresentation(firstWindow);
        screen.AddPresentation(secondWindow);

        secondSecond.IsChecked = true;

        firstFirst.IsChecked.ShouldBeTrue();
        secondFirst.IsChecked.ShouldBeFalse();
        secondSecond.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies named groups span every owned slot even when no owner is a Container.</summary>
    [Fact]
    public void IsChecked_WhenNamedMembersUseNonContainerSlots_UsesCompleteOwnedRoot()
    {
        var root = new TraversalOwner();
        var branch = new TraversalOwner();
        var first = new RadioButton { GroupName = "density", IsChecked = true };
        var second = new RadioButton { GroupName = "density" };
        root.AddNormal(branch);
        branch.AddExcluded(first);
        root.AddPopup(second);

        second.IsChecked = true;

        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies unnamed groups remain local to their exact ownership slot.</summary>
    [Fact]
    public void IsChecked_WhenUnnamedMembersUseDifferentSlots_OnlyExactSlotIsExclusive()
    {
        var root = new TraversalOwner();
        var first = new RadioButton { IsChecked = true };
        var second = new RadioButton();
        var otherSlot = new RadioButton { IsChecked = true };
        root.AddNormal(first);
        root.AddNormal(second);
        root.AddExcluded(otherSlot);

        second.IsChecked = true;

        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
        otherSlot.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies attaching a prechecked member resolves duplicate selection atomically.</summary>
    [Fact]
    public void Add_WhenCheckedMemberJoinsGroup_UnchecksExistingMember()
    {
        var parent = new Stack();
        var first = new RadioButton { IsChecked = true };
        var second = new RadioButton { IsChecked = true };
        parent.Children.Add(first);

        parent.Children.Add(second);

        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies regrouping a selected member resolves its new group.</summary>
    [Fact]
    public void GroupName_WhenCheckedMemberMoves_ResolvesNewGroupAfterCommit()
    {
        var parent = new Stack();
        var first = new RadioButton { GroupName = "a", IsChecked = true };
        var second = new RadioButton { GroupName = "b", IsChecked = true };
        parent.Children.Add(first);
        parent.Children.Add(second);

        first.GroupName = "b";

        first.IsChecked.ShouldBeTrue();
        second.IsChecked.ShouldBeFalse();
    }

    /// <summary>Verifies GroupName observers see destination-group exclusivity already resolved.</summary>
    [Fact]
    public void GroupName_WhenCheckedMemberMoves_ResolvesDestinationBeforePropertyNotification()
    {
        var parent = new Stack();
        var first = new RadioButton { GroupName = "a", IsChecked = true };
        var second = new RadioButton { GroupName = "b", IsChecked = true };
        parent.Children.Add(first);
        parent.Children.Add(second);
        var observed = false;
        first.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(RadioButton.GroupName))
            {
                first.IsChecked.ShouldBeTrue();
                second.IsChecked.ShouldBeFalse();
                observed = true;
            }
        };

        first.GroupName = "b";

        observed.ShouldBeTrue();
    }

    /// <summary>Verifies reentrant old-member handlers suppress stale outer notifications.</summary>
    [Fact]
    public void IsChecked_WhenUncheckedHandlerReselects_DoesNotReportStaleSelection()
    {
        var parent = new Stack();
        var first = new RadioButton { IsChecked = true };
        var second = new RadioButton();
        var third = new RadioButton();
        parent.Children.Add(first);
        parent.Children.Add(second);
        parent.Children.Add(third);
        var secondChecked = 0;
        first.Unchecked += (_, _) => third.IsChecked = true;
        second.Checked += (_, _) =>
        {
            second.IsChecked.ShouldBeTrue();
            secondChecked++;
        };

        second.IsChecked = true;

        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeFalse();
        third.IsChecked.ShouldBeTrue();
        secondChecked.ShouldBe(0);
    }

    /// <summary>Verifies arrows skip unavailable members, wrap, focus, and select.</summary>
    [Fact]
    public async Task Route_WhenArrowMoves_UsesEligibleGroupOrderAndWrapsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new Stack();
            var first = new RadioButton();
            var skipped = new RadioButton { IsEnabled = false };
            var third = new RadioButton();
            root.Children.Add(first);
            root.Children.Add(skipped);
            root.Children.Add(third);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(first).ShouldBeTrue();

            Key(first, Code.Right);
            focus.Focused.ShouldBeSameAs(third);
            third.IsChecked.ShouldBeTrue();
            Key(third, Code.Right);

            focus.Focused.ShouldBeSameAs(first);
            first.IsChecked.ShouldBeTrue();
            third.IsChecked.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies content layout and selection marks render exact semantic cells.</summary>
    [Fact]
    public void Render_WhenSelectedWithUnicodeContent_WritesExactCells()
    {
        var radio = new RadioButton { IsChecked = true, Content = new ControlText("界") };
        new Engine().Layout(radio, new Size(6, 1));
        using Frame frame = new(new Size(6, 1));

        radio.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("(");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("•");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe(")");
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("界");
        frame.GetCell(new Point(5, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies parenthesized marks render exact state cells and reserve stable label space.</summary>
    [Theory]
    [InlineData(false, "( )", 4)]
    [InlineData(true, "(•)", 4)]
    public void Render_WhenMarkStyleUsesParentheses_RendersExpectedMarkAndContentOffset(
        bool isChecked,
        string expectedMark,
        int contentOffset)
    {
        var content = new ProbeControl(new Size(1, 1));
        var radio = new RadioButton
        {
            Content = content,
            IsChecked = isChecked,
            Style = RadioButtonStyle.Parentheses
        };
        new Engine().Layout(radio, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));

        radio.Render(frame.Canvas);

        for (var index = 0; index < expectedMark.Length; index++)
        {
            FrameOracle.Get(frame, new Point(index, 0)).ShouldBe(expectedMark[index].ToString());
        }

        content.Bounds.X.ShouldBe(contentOffset);
        radio.DesiredSize.ShouldBe(new Size(5, 1));
    }

    /// <summary>Verifies invalid mark styles are rejected before observable mutation.</summary>
    [Fact]
    public void MarkStyle_WhenInvalid_ThrowsBeforeMutation()
    {
        var radio = new RadioButton();

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            _ = new RadioButtonStyle(
                (RadioButtonMarkStyle) 99,
                RadioButtonGlyphs.Default,
                RadioButtonStyle.Default.Appearance));

        radio.Style.ShouldBeNull();
    }

    /// <summary>Verifies programmatic false leaves a valid empty group.</summary>
    [Fact]
    public void IsChecked_WhenSetFalse_AllowsNoSelection()
    {
        var radio = new RadioButton { IsChecked = true };

        radio.IsChecked = false;

        radio.IsChecked.ShouldBeFalse();
    }

    /// <summary>Verifies the string constructor creates an unselected RadioButton with text content.</summary>
    [Fact]
    public void Constructor_WithText_SetsTextContent()
    {
        var radio = new RadioButton("Option A");
        radio.Content.ShouldBeOfType<ControlText>().Content.ShouldBe("Option A");
        radio.IsChecked.ShouldBeFalse();
    }

    /// <summary>Verifies the string constructor rejects null text.</summary>
    [Fact]
    public void Constructor_WithNullText_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() => new RadioButton(null!));

    /// <summary>Verifies documented constructor defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var radio = new RadioButton();

        // Assert
        radio.IsChecked.ShouldBeFalse();
        radio.GroupName.ShouldBeNull();
        radio.Content.ShouldBeNull();
        radio.CanFocus.ShouldBeTrue();
        radio.IsHitTestVisible.ShouldBeTrue();
        radio.Style.ShouldBeNull();
        radio.ActualStyle.MarkStyle.ShouldBe(RadioButtonStyle.Parentheses.MarkStyle);
        radio.ActualStyle.Glyphs.ShouldBe(RadioButtonStyle.Parentheses.Glyphs);
    }

    /// <summary>Verifies an explicit glyph style overrides defaults and clearing it restores theme resolution.</summary>
    [Fact]
    public void Style_WhenCustomized_OverridesDefaultsAndClearingRestores()
    {
        // Arrange
        var radio = new RadioButton();
        var defaultStyle = radio.ActualStyle;

        // Act
        var customStyle = new RadioButtonStyle(
            RadioButtonMarkStyle.Circle,
            new RadioButtonGlyphs(new Rune('-'), new Rune('+')),
            RadioButtonStyle.Default.Appearance);
        radio.Style = customStyle;

        // Assert custom
        radio.ActualStyle.ShouldBe(customStyle);

        // Act reset
        radio.Style = null;

        // Assert restored
        radio.ActualStyle.ShouldBe(defaultStyle);
    }

    /// <summary>Verifies disposing the RadioButton prevents mutation.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        // Arrange
        var radio = new RadioButton();

        // Act
        radio.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() => radio.IsChecked = true);
    }

    /// <summary>Verifies PerformClick on a disabled RadioButton is a no-op.</summary>
    [Fact]
    public void PerformClick_WhenDisabled_IsNoOp()
    {
        // Arrange
        var radio = new RadioButton { IsEnabled = false };
        var raised = 0;
        radio.Checked += (_, _) => raised++;

        // Act
        radio.PerformClick();

        // Assert
        radio.IsChecked.ShouldBeFalse();
        raised.ShouldBe(0);
    }

    private static void Key(RadioButton radio, Code code) => Router.Route(
        radio,
        Events.Key,
        new KeyEventArgs(new Stroke(
            code,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));

    private static Theme CreateTheme(RadioButtonStyle _)
    {
        var theme = new Theme();

        theme.Freeze();
        return theme;
    }
}
