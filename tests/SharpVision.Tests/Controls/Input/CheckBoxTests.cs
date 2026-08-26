// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies CheckBox transitions, events, ownership, styling, and cells.</summary>
public sealed class CheckBoxTests
{
    /// <summary>Verifies a state-specific callback that commits a newer state suppresses the stale
    /// outer general notification.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsChecked_WhenSpecificEventReenters_PublishesOnlyTheCurrentGeneralState(bool outerValue)
    {
        // Arrange
        var checkBox = new CheckBox { IsChecked = !outerValue };
        var observations = new List<(bool? EventValue, bool? LiveValue)>();

        void Reenter(object? sender, CheckChangedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            checkBox.IsChecked = !outerValue;
        }

        if (outerValue)
        {
            checkBox.Checked += Reenter;
        }
        else
        {
            checkBox.Unchecked += Reenter;
        }

        checkBox.StateChanged += (_, eventArgs) =>
            observations.Add((eventArgs.Current, checkBox.IsChecked));

        // Act
        checkBox.IsChecked = outerValue;

        // Assert
        checkBox.IsChecked.ShouldBe(!outerValue);
        observations.ShouldBe([(!outerValue, !outerValue)]);
    }

    /// <summary>Verifies disabling three-state mode does not publish its normalization after an
    /// Unchecked callback commits a newer checked state.</summary>
    [Fact]
    public void ThreeState_WhenNormalizationCallbackReenters_PublishesOnlyTheCurrentGeneralState()
    {
        // Arrange
        var checkBox = new CheckBox { ThreeState = true, IsChecked = null };
        var observations = new List<(bool? EventValue, bool? LiveValue)>();
        checkBox.Unchecked += (_, _) => checkBox.IsChecked = true;
        checkBox.StateChanged += (_, eventArgs) =>
            observations.Add((eventArgs.Current, checkBox.IsChecked));

        // Act
        checkBox.ThreeState = false;

        // Assert
        checkBox.ThreeState.ShouldBeFalse();
        checkBox.IsChecked.ShouldBe(true);
        observations.ShouldBe([(true, true)]);
    }

    /// <summary>Verifies local style ownership overrides Theme fallback and clearing restores it.</summary>
    [Fact]
    public void Style_WhenThemeAndLocalValuesChange_UsesDocumentedPrecedence()
    {
        // "blocks" resolves CheckBox's theme-wide glyph family to Square - a leaf declares no
        // theme section of its own any more, so the glyph family is the only surviving way to
        // make the theme's own resolution differ from the Tick preset asserted below.
        var theme = ThemeCatalog.Parse(ThemeJson.Create(glyphs: "blocks"));
        var checkBox = new CheckBox();

        checkBox.SetTheme(theme);
        checkBox.Style.ShouldBeNull();
        checkBox.ActualStyle.ShouldBe(CheckBoxStyle.Definition.Resolve(null, theme));
        checkBox.Style = CheckBoxStyle.Tick;
        checkBox.ActualStyle.ShouldBe(CheckBoxStyle.Tick);
        checkBox.Style = null;
        checkBox.ActualStyle.ShouldBe(CheckBoxStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a mark-width style change invalidates measurement.</summary>
    [Fact]
    public void Style_WhenMarkWidthChanges_InvalidatesMeasure()
    {
        var checkBox = new CheckBox { Style = CheckBoxStyle.Brackets };
        checkBox.Clear(Invalidation.All);

        checkBox.Style = CheckBoxStyle.Square;

        checkBox.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies an AffixGap-only style change invalidates measurement.</summary>
    [Fact]
    public void Style_WhenAffixGapChanges_InvalidatesMeasure()
    {
        var checkBox = new CheckBox { Style = CheckBoxStyle.Brackets };
        checkBox.Clear(Invalidation.All);

        checkBox.Style = CheckBoxStyle.Brackets with { AffixGap = CheckBoxStyle.Brackets.AffixGap + 1 };

        checkBox.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies defaults and the two-state user cycle.</summary>
    [Fact]
    public void Activate_WhenTwoState_CyclesFalseAndTrue()
    {
        var checkBox = new CheckBox();

        checkBox.IsChecked.ShouldBe(false);
        checkBox.ThreeState.ShouldBeFalse();
        checkBox.Text.ShouldBeEmpty();
        checkBox.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        checkBox.Style.ShouldBeNull();
        checkBox.ActualStyle.ShouldBe(CheckBoxStyle.Definition.Resolve(null, ThemeCatalog.Dark));
        checkBox.PerformClick();
        checkBox.IsChecked.ShouldBe(true);
        checkBox.PerformClick();
        checkBox.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies PerformClick rejects use after disposal.</summary>
    [Fact]
    public void PerformClick_WhenDisposed_Throws()
    {
        // Arrange
        var checkBox = new CheckBox();
        checkBox.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(checkBox.PerformClick);
    }

    /// <summary>Verifies PerformClick on a disabled CheckBox does nothing.</summary>
    [Fact]
    public void PerformClick_WhenDisabled_IsNoOp()
    {
        // Arrange
        var checkBox = new CheckBox { IsEnabled = false };
        var raised = 0;
        checkBox.StateChanged += (_, _) => raised++;

        // Act
        checkBox.PerformClick();

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        raised.ShouldBe(0);
    }

    /// <summary>Verifies the command runs after the toggle commits and its events raise.</summary>
    [Fact]
    public void PerformClick_WhenCommandCanExecute_TogglesThenExecutesExactlyOnce()
    {
        List<string> order = [];
        var parameter = new object();
        var command = new ProbeCommand { Executing = _ => order.Add("command") };
        var checkBox = new CheckBox { Command = command, CommandParameter = parameter };
        checkBox.StateChanged += (_, _) =>
        {
            checkBox.IsChecked.ShouldBe(true);
            order.Add("state");
        };

        checkBox.PerformClick();

        order.ShouldBe(["state", "command"]);
        command.Queries.ShouldBe([parameter]);
        command.Executions.ShouldBe([parameter]);
    }

    /// <summary>Verifies a false CanExecute suppresses the command but never the toggle itself,
    /// unlike Button, where the same query gates the click.</summary>
    [Fact]
    public void PerformClick_WhenCommandCannotExecute_StillToggles()
    {
        var command = new ProbeCommand { CanExecuteValue = false };
        var checkBox = new CheckBox { Command = command };

        checkBox.PerformClick();

        checkBox.IsChecked.ShouldBe(true);
        command.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies activation executes the entry binding even when state callbacks rebind and dispose the control.</summary>
    [Fact]
    public void PerformClick_WhenStateCallbackRebindsAndDisposes_ExecutesCapturedCommand()
    {
        var originalParameter = new object();
        var original = new ProbeCommand();
        var replacement = new ProbeCommand();
        var checkBox = new CheckBox { Command = original, CommandParameter = originalParameter };
        checkBox.StateChanged += (_, _) =>
        {
            checkBox.Command = replacement;
            checkBox.CommandParameter = new object();
            checkBox.Dispose();
        };

        checkBox.PerformClick();

        original.Executions.ShouldBe([originalParameter]);
        replacement.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies the default arrangement hugs the mark, separator, and label instead of stretching.</summary>
    [Fact]
    public void Arrange_WhenDefaultAlignmentIsUsed_HugsMeasuredContentWidth()
    {
        var checkBox = new CheckBox { Text = "Choice" };

        new LayoutEngine().Layout(checkBox, new Size(20, 1));

        checkBox.DesiredSize.ShouldBe(new Size(10, 1));
        checkBox.Bounds.ShouldBe(new Rect(0, 0, 10, 1));
    }

    /// <summary>Verifies three-state activation follows false, true, null, false.</summary>
    [Fact]
    public void Activate_WhenThreeState_CyclesInDocumentedOrder()
    {
        var checkBox = new CheckBox { ThreeState = true };

        checkBox.PerformClick();
        checkBox.PerformClick();
        checkBox.IsChecked.ShouldBeNull();
        checkBox.PerformClick();

        checkBox.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies invalid null assignment throws before changing two-state value.</summary>
    [Fact]
    public void IsChecked_WhenNullInTwoState_ThrowsBeforeMutation()
    {
        var checkBox = new CheckBox { IsChecked = true };

        _ = Should.Throw<ArgumentException>(() => checkBox.IsChecked = null);

        checkBox.IsChecked.ShouldBe(true);
    }

    /// <summary>Verifies disabling three-state normalizes null with exact notification order.</summary>
    [Fact]
    public void IsThreeState_WhenDisabledFromNull_CommitsFalseBeforeEvents()
    {
        var checkBox = new CheckBox { ThreeState = true, IsChecked = null };
        List<string> order = [];
        checkBox.Unchecked += (_, eventArgs) =>
        {
            checkBox.ThreeState.ShouldBeFalse();
            checkBox.IsChecked.ShouldBe(false);
            eventArgs.Previous.ShouldBeNull();
            order.Add("unchecked");
        };
        checkBox.StateChanged += (_, _) => order.Add("changed");

        checkBox.ThreeState = false;

        order.ShouldBe(["unchecked", "changed"]);
    }

    /// <summary>Verifies observers never see two-state mode paired with an indeterminate value.</summary>
    [Fact]
    public void IsThreeState_WhenDisabledFromNull_StagesBothPropertiesBeforeNotification()
    {
        var checkBox = new CheckBox { ThreeState = true, IsChecked = null };
        List<string?> properties = [];
        checkBox.PropertyChanged += (_, eventArgs) =>
        {
            (checkBox.ThreeState || checkBox.IsChecked is not null).ShouldBeTrue();
            properties.Add(eventArgs.PropertyName);
        };

        checkBox.ThreeState = false;

        properties.ShouldBe([nameof(CheckBox.ThreeState), nameof(CheckBox.IsChecked)]);
        checkBox.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies specific events precede StateChanged from committed programmatic state.</summary>
    [Fact]
    public void IsChecked_WhenChangedProgrammatically_RaisesSpecificThenGeneral()
    {
        var checkBox = new CheckBox();
        List<string> order = [];
        checkBox.Checked += (_, eventArgs) =>
        {
            checkBox.IsChecked.ShouldBe(true);
            eventArgs.Cause.ShouldBe(ActivationCause.Programmatic);
            order.Add("checked");
        };
        checkBox.StateChanged += (_, _) => order.Add("changed");

        checkBox.IsChecked = true;

        order.ShouldBe(["checked", "changed"]);
    }

    /// <summary>Verifies Space activation uses the shared keyboard path.</summary>
    [Fact]
    public void Route_WhenSpaceCompletes_TogglesWithKeyboardCause()
    {
        var checkBox = new CheckBox();
        ActivationCause? cause = null;
        checkBox.Checked += (_, eventArgs) => cause = eventArgs.Cause;

        Key(checkBox, KeyAction.Press);
        Key(checkBox, KeyAction.Release);

        checkBox.IsChecked.ShouldBe(true);
        cause.ShouldBe(ActivationCause.Keyboard);
    }

    /// <summary>Verifies mark, separator, Unicode content, layout, and cells.</summary>
    [Fact]
    public void Render_WhenCheckedWithContent_WritesExactMarkAndUnicodeCells()
    {
        var checkBox = new CheckBox { Text = "界", IsChecked = true, Style = CheckBoxStyle.Square };
        new LayoutEngine().Layout(checkBox, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        checkBox.Render(frame.Canvas);

        checkBox.DesiredSize.ShouldBe(new Size(4, 1));
        FrameOracle.Get(frame, default).ShouldBe("☑");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("界");
        frame.GetCell(new Point(3, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies bracket and tick mark styles reserve stable documented cell widths.</summary>
    [Theory]
    [InlineData(CheckBoxMarkStyle.Brackets, "[✓]", 5)]
    [InlineData(CheckBoxMarkStyle.Tick, "✓", 3)]
    public void Render_WhenMarkStyleChanges_UsesExpectedMarkAndStableContentOffset(
        CheckBoxMarkStyle style,
        string mark,
        int width)
    {
        var checkBoxStyle = style == CheckBoxMarkStyle.Brackets
            ? CheckBoxStyle.Brackets
            : CheckBoxStyle.Tick;
        var checkBox = new CheckBox { Text = "Go", IsChecked = true, Style = checkBoxStyle };
        var size = new Size(width, 1);
        new LayoutEngine().Layout(checkBox, size);
        using Frame frame = new(size);

        checkBox.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe(mark[..1]);
        FrameOracle.Get(frame, new Point(style == CheckBoxMarkStyle.Brackets ? 4 : 2, 0)).ShouldBe("G");
    }

    /// <summary>Verifies the indeterminate three-state mark renders its documented bracket fallback.</summary>
    [Fact]
    public void Render_WhenIndeterminateWithBrackets_WritesExactMarkCells()
    {
        var checkBox = new CheckBox { ThreeState = true, IsChecked = null, Style = CheckBoxStyle.Brackets };
        var size = new Size(3, 1);
        new LayoutEngine().Layout(checkBox, size);
        using Frame frame = new(size);

        checkBox.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("[");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("─");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("]");
    }

    /// <summary>Verifies wide and control marks are rejected during construction.</summary>
    [Theory]
    [InlineData('\n')]
    [InlineData('界')]
    public void Constructor_WhenMarkIsInvalid_Throws(char value)
    {
        _ = Should.Throw<ArgumentException>(() => new CheckBoxGlyphs(
            new Rune(value),
            new Rune('x'),
            new Rune('-')));
    }

    /// <summary>Verifies PerformClick uses the same two-state cycle as PerformToggle.</summary>
    [Fact]
    public void PerformClick_WhenTwoState_CyclesFalseAndTrue()
    {
        var checkBox = new CheckBox();

        checkBox.IsChecked.ShouldBe(false);
        checkBox.PerformClick();
        checkBox.IsChecked.ShouldBe(true);
        checkBox.PerformClick();
        checkBox.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies PerformClick follows the three-state cycle.</summary>
    [Fact]
    public void PerformClick_WhenThreeState_CyclesInDocumentedOrder()
    {
        var checkBox = new CheckBox { ThreeState = true };

        checkBox.PerformClick();
        checkBox.PerformClick();
        checkBox.IsChecked.ShouldBeNull();
        checkBox.PerformClick();

        checkBox.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies the string constructor creates an unchecked CheckBox with text content.</summary>
    [Fact]
    public void Constructor_WithText_SetsTextContent()
    {
        var checkBox = new CheckBox("Accept");
        checkBox.Text.ShouldBe("Accept");
        checkBox.TextControl.ShouldNotBeNull().Content.ShouldBe("Accept");
        checkBox.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies the string constructor rejects null text.</summary>
    [Fact]
    public void Constructor_WithNullText_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() => new CheckBox(null!));

    /// <summary>Verifies an explicit style overrides defaults and clearing it restores theme resolution.</summary>
    [Fact]
    public void Style_WhenCustomized_OverridesDefaultsAndClearingRestores()
    {
        // Arrange
        var checkBox = new CheckBox();
        var defaultStyle = checkBox.ActualStyle;

        // Act
        var custom = new CheckBoxGlyphs(new Rune('·'), new Rune('x'), new Rune('~'));
        var customStyle = new CheckBoxStyle(
            CheckBoxStyle.Default.Face,
            CheckBoxStyle.Default.Border,
            CheckBoxStyle.Default.Shadow,
            CheckBoxMarkStyle.Brackets,
            custom);
        checkBox.Style = customStyle;

        // Assert custom
        checkBox.ActualStyle.ShouldBe(customStyle);

        // Act reset
        checkBox.Style = null;

        // Assert restored
        checkBox.ActualStyle.ShouldBe(defaultStyle);
    }

    /// <summary>Verifies StateChanged does not fire when setting the same value.</summary>
    [Fact]
    public void IsChecked_WhenSetToSameValue_DoesNotFireStateChanged()
    {
        // Arrange
        var checkBox = new CheckBox { IsChecked = true };
        var raised = 0;
        checkBox.StateChanged += (_, _) => raised++;

        // Act
        checkBox.IsChecked = true;

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies disposing the CheckBox prevents mutation.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        // Arrange
        var checkBox = new CheckBox();

        // Act
        checkBox.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() => checkBox.IsChecked = true);
    }

    /// <summary>Verifies invalid mark styles are rejected during style construction.</summary>
    [Fact]
    public void MarkStyle_WhenInvalid_ThrowsBeforeMutation()
    {
        // Arrange
        var checkBox = new CheckBox();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            _ = new CheckBoxStyle(
                CheckBoxStyle.Default.Face,
                CheckBoxStyle.Default.Border,
                CheckBoxStyle.Default.Shadow,
                (CheckBoxMarkStyle) 99,
                CheckBoxGlyphs.Default));
        checkBox.Style.ShouldBeNull();
    }

    /// <summary>Verifies text replacement while checked preserves check state and updates the
    /// owned caption child in place rather than replacing it.</summary>
    [Fact]
    public void Content_WhenReplacedWhileChecked_PreservesCheckState()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "First", IsChecked = true };
        var textControl = checkBox.TextControl;

        // Act
        checkBox.Text = "Second";

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        checkBox.Text.ShouldBe("Second");
        checkBox.TextControl.ShouldBeSameAs(textControl);
    }

    /// <summary>Verifies affixes default to null.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasNullAffixes()
    {
        var checkBox = new CheckBox();

        checkBox.StartAffix.ShouldBeNull();
        checkBox.EndAffix.ShouldBeNull();
    }

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the shared theme gap, over an equivalent captionless CheckBox with neither set - the mark's
    /// own three-cell bracket reservation stays constant regardless of affix presence.</summary>
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
        var checkBox = new CheckBox
        {
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };

        new LayoutEngine().Layout(checkBox, new Size(20, 3));

        checkBox.DesiredSize.Width.ShouldBe(expectedWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        using var checkBox = new CheckBox { Text = "Choice" };
        checkBox.Clear(Invalidation.All);

        checkBox.StartAffix = new Affix("!");

        checkBox.Pending.ShouldBe(Invalidation.All);
        checkBox.Clear(Invalidation.All);

        checkBox.StartAffix = null;

        checkBox.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only,
    /// the exact grading an animated affix (a spinner swapping frames) depends on.</summary>
    [Fact]
    public void StartAffix_WhenContentOrColorChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        using var checkBox = new CheckBox { Text = "Choice", StartAffix = new Affix("|") };
        checkBox.Clear(Invalidation.All);

        checkBox.StartAffix = new Affix("/");

        checkBox.Pending.ShouldBe(Invalidation.Render);
        checkBox.Clear(Invalidation.All);

        checkBox.StartAffix = new Affix("/", "?", SemanticColor.Warning);

        checkBox.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change (one cell to two cells) invalidates Measure again,
    /// not just Render, even though both values are non-null.</summary>
    [Fact]
    public void EndAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        using var checkBox = new CheckBox { Text = "Choice", EndAffix = new Affix("!") };
        checkBox.Clear(Invalidation.All);

        // U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        checkBox.EndAffix = new Affix("世");

        checkBox.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op, matching every other
    /// SetProperty-backed member.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        var affix = new Affix("!");
        using var checkBox = new CheckBox { Text = "Choice", StartAffix = affix };
        checkBox.Clear(Invalidation.All);

        checkBox.StartAffix = affix;

        checkBox.Pending.ShouldBe(Invalidation.None);
    }

    private static void Key(CheckBox checkBox, KeyAction action) => Router.Route(
        checkBox,
        Events.Key,
        new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            action)));
}
