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

    /// <summary>Verifies an AffixGap-only style change invalidates measurement.</summary>
    [Fact]
    public void Style_WhenAffixGapChanges_InvalidatesMeasure()
    {
        var radio = new RadioButton { Style = RadioButtonStyle.Parentheses };
        radio.Clear(Invalidation.All);

        radio.Style = RadioButtonStyle.Parentheses with { AffixGap = RadioButtonStyle.Parentheses.AffixGap + 1 };

        radio.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a mark-gap style change invalidates measurement because it changes the
    /// terminal-cell reservation between a present mark and caption.</summary>
    [Fact]
    public void Style_WhenMarkGapChanges_InvalidatesMeasure()
    {
        var radio = new RadioButton { Style = RadioButtonStyle.Parentheses };
        radio.Clear(Invalidation.All);

        radio.Style = RadioButtonStyle.Parentheses with { MarkGap = 2 };

        radio.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies moving the mark to the opposite caption edge invalidates arrangement and
    /// rendering without needlessly remeasuring an unchanged intrinsic width.</summary>
    [Fact]
    public void Style_WhenMarkPlacementChanges_InvalidatesArrange()
    {
        var radio = new RadioButton { Style = RadioButtonStyle.Parentheses };
        radio.Clear(Invalidation.All);

        radio.Style = RadioButtonStyle.Parentheses with { MarkPlacement = SelectionMarkPlacement.Trailing };

        radio.Pending.ShouldBe(Invalidation.Arrange | Invalidation.Render);
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

    /// <summary>Verifies the command runs after group selection commits.</summary>
    [Fact]
    public void PerformClick_WhenCommandCanExecute_SelectsThenExecutesExactlyOnce()
    {
        List<string> order = [];
        var parameter = new object();
        var command = new ProbeCommand { Executing = _ => order.Add("command") };
        var radio = new RadioButton { Command = command, CommandParameter = parameter };
        radio.Checked += (_, _) => order.Add("checked");

        radio.PerformClick();

        order.ShouldBe(["checked", "command"]);
        command.Executions.ShouldBe([parameter]);
    }

    /// <summary>Verifies the command still executes when re-activating the already-checked
    /// member, unlike group selection itself, which is a hard no-op in that case.</summary>
    [Fact]
    public void PerformClick_WhenAlreadyCheckedMemberReactivates_StillExecutesCommand()
    {
        var command = new ProbeCommand();
        var radio = new RadioButton { Command = command };
        radio.PerformClick();

        radio.PerformClick();

        radio.IsChecked.ShouldBeTrue();
        command.Executions.Count.ShouldBe(2);
    }

    /// <summary>Verifies activation retains the entry command binding across reentrant selection callbacks.</summary>
    [Fact]
    public void PerformClick_WhenCheckedCallbackRebindsAndDisposes_ExecutesCapturedCommand()
    {
        var originalParameter = new object();
        var original = new ProbeCommand();
        var replacement = new ProbeCommand();
        var radio = new RadioButton { Command = original, CommandParameter = originalParameter };
        radio.Checked += (_, _) =>
        {
            radio.Command = replacement;
            radio.CommandParameter = new object();
            radio.Dispose();
        };

        radio.PerformClick();

        original.Executions.ShouldBe([originalParameter]);
        replacement.Executions.ShouldBeEmpty();
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

    /// <summary>Verifies an earlier group publication can remove or dispose the later staged
    /// member without publishing through that stale target.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsChecked_WhenEarlierCallbackInvalidatesLaterMember_SkipsLaterPublication(bool dispose)
    {
        var parent = new Stack();
        var first = new RadioButton { IsChecked = true };
        var second = new RadioButton();
        var published = 0;
        parent.Children.Add(first);
        parent.Children.Add(second);
        first.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(RadioButton.IsChecked))
            {
                if (dispose)
                {
                    second.Dispose();
                }
                else
                {
                    parent.Children.Remove(second).ShouldBeTrue();
                }
            }
        };
        second.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(RadioButton.IsChecked))
            {
                published++;
            }
        };

        second.IsChecked = true;

        published.ShouldBe(0);
    }

    /// <summary>Verifies a PropertyChanged callback that reenters during the IsChecked
    /// notification and clears selection again does not receive a superseded CanTabStop
    /// notification from the original commit afterward.</summary>
    [Fact]
    public void IsChecked_WhenPropertyChangedCallbackReentersDuringIsCheckedNotification_SkipsSupersededCanTabStopNotification()
    {
        // Arrange
        var radio = new RadioButton();
        List<string?> properties = [];
        var reentered = false;

        radio.PropertyChanged += (_, eventArgs) =>
        {
            properties.Add(eventArgs.PropertyName);

            if (eventArgs.PropertyName == nameof(RadioButton.IsChecked) && !reentered)
            {
                reentered = true;
                radio.IsChecked = false;
            }
        };

        // Act
        radio.IsChecked = true;

        // Assert
        radio.IsChecked.ShouldBeFalse();
        properties.ShouldBe(
            [nameof(RadioButton.IsChecked), nameof(RadioButton.IsChecked), nameof(RadioButton.CanTabStop)]);
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
    /// GroupName must not corrupt each other's selection.</summary>
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

    /// <summary>Verifies GroupName round-trips for an unchecked member, where no coordinator
    /// resolution is triggered by the assignment.</summary>
    [Fact]
    public void GroupName_WhenSetOnUncheckedMember_RoundTrips()
    {
        // Arrange and act
        var radio = new RadioButton { GroupName = "density" };

        // Assert
        radio.GroupName.ShouldBe("density");
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
        var radio = new RadioButton { IsChecked = true, Text = "界" };
        new LayoutEngine().Layout(radio, new Size(6, 1));
        using Frame frame = new(new Size(6, 1));

        radio.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("(");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("•");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe(")");
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("界");
        frame.GetCell(new Point(5, 0)).Continuation.ShouldBeTrue();
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
        var radio = new RadioButton
        {
            Text = "X",
            IsChecked = isChecked,
            Style = RadioButtonStyle.Parentheses
        };
        var content = radio.TextControl!;
        new LayoutEngine().Layout(radio, new Size(5, 1));
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
                RadioButtonStyle.Default.Face,
                RadioButtonStyle.Default.Border,
                RadioButtonStyle.Default.Shadow,
                (RadioButtonMarkStyle) 99,
                RadioButtonGlyphs.Default));

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
        radio.Text.ShouldBe("Option A");
        radio.TextControl.ShouldNotBeNull().Content.ShouldBe("Option A");
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
        radio.Text.ShouldBeEmpty();
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
            RadioButtonStyle.Default.Face,
            RadioButtonStyle.Default.Border,
            RadioButtonStyle.Default.Shadow,
            RadioButtonMarkStyle.Circle,
            new RadioButtonGlyphs(new Rune('-'), new Rune('+')));
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

    /// <summary>Verifies PerformClick rejects use after disposal.</summary>
    [Fact]
    public void PerformClick_WhenDisposed_Throws()
    {
        // Arrange
        var radio = new RadioButton();
        radio.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(radio.PerformClick);
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

    /// <summary>Verifies affixes default to null.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasNullAffixes()
    {
        var radio = new RadioButton();

        radio.StartAffix.ShouldBeNull();
        radio.EndAffix.ShouldBeNull();
    }

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the shared theme gap, over an equivalent captionless RadioButton with neither set - the
    /// mark's own three-cell parenthesized reservation stays constant regardless of affix
    /// presence.</summary>
    [Theory]
    [InlineData(false, false, 3)]
    [InlineData(true, false, 5)]
    [InlineData(false, true, 5)]
    [InlineData(true, true, 7)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedWidth)
    {
        var radio = new RadioButton
        {
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };

        new LayoutEngine().Layout(radio, new Size(20, 3));

        radio.DesiredSize.Width.ShouldBe(expectedWidth);
    }

    /// <summary>Verifies a custom mark gap contributes exact terminal cells only when the
    /// RadioButton has a caption.</summary>
    [Fact]
    public void Measure_WhenMarkGapIsCustomized_ReservesExactCaptionGap()
    {
        var captioned = new RadioButton
        {
            Text = "Go",
            Style = RadioButtonStyle.Glyph with { MarkGap = 3 }
        };
        var markOnly = new RadioButton
        {
            Style = RadioButtonStyle.Glyph with { MarkGap = 3 }
        };
        var clearedCaption = new RadioButton
        {
            Text = "Go",
            Style = RadioButtonStyle.Glyph with { MarkGap = 3 }
        };
        clearedCaption.Text = string.Empty;
        var engine = new LayoutEngine();

        engine.Layout(captioned, new Size(20, 3));
        engine.Layout(markOnly, new Size(20, 3));
        engine.Layout(clearedCaption, new Size(20, 3));

        captioned.DesiredSize.Width.ShouldBe(6);
        markOnly.DesiredSize.Width.ShouldBe(1);
        clearedCaption.DesiredSize.Width.ShouldBe(1);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        using var radio = new RadioButton { Text = "Option" };
        radio.Clear(Invalidation.All);

        radio.StartAffix = new Affix("!");

        radio.Pending.ShouldBe(Invalidation.All);
        radio.Clear(Invalidation.All);

        radio.StartAffix = null;

        radio.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only,
    /// the exact grading an animated affix (a spinner swapping frames) depends on.</summary>
    [Fact]
    public void StartAffix_WhenContentOrColorChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        using var radio = new RadioButton { Text = "Option", StartAffix = new Affix("|") };
        radio.Clear(Invalidation.All);

        radio.StartAffix = new Affix("/");

        radio.Pending.ShouldBe(Invalidation.Render);
        radio.Clear(Invalidation.All);

        radio.StartAffix = new Affix("/", "?", SemanticColor.Warning);

        radio.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change (one cell to two cells) invalidates Measure again,
    /// not just Render, even though both values are non-null.</summary>
    [Fact]
    public void EndAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        using var radio = new RadioButton { Text = "Option", EndAffix = new Affix("!") };
        radio.Clear(Invalidation.All);

        // U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        radio.EndAffix = new Affix("世");

        radio.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op, matching every other
    /// SetProperty-backed member.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        var affix = new Affix("!");
        using var radio = new RadioButton { Text = "Option", StartAffix = affix };
        radio.Clear(Invalidation.All);

        radio.StartAffix = affix;

        radio.Pending.ShouldBe(Invalidation.None);
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
