// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies CheckBox transitions, events, ownership, styling, and cells.</summary>
public sealed class CheckBoxTests
{
    /// <summary>Verifies the reported ThemeRole matches the Input-derived profile ActualStyle
    /// actually resolves against, instead of the unrelated default ThemeRole.ControlBase the
    /// unoverridden base property previously reported (see #157).</summary>
    [Fact]
    public void ThemeRole_WhenResolved_MatchesTheInputProfileActualStyleUses()
    {
        var checkBox = new CheckBox();

        checkBox.ResolvedThemeRole.ShouldBe(ThemeRole.Input);
    }

    /// <summary>Verifies local style ownership overrides Theme fallback and clearing restores it.</summary>
    [ComponentUnitEvidence(typeof(CheckBox))]
    [Fact]
    public void Style_WhenThemeAndLocalValuesChange_UsesDocumentedPrecedence()
    {
        var theme = ThemeWithInputProfile(CheckBoxStyle.Square.Appearance);
        var checkBox = new CheckBox();

        checkBox.SetTheme(theme);
        checkBox.Style.ShouldBeNull();
        checkBox.ActualStyle.ShouldBe(ThemeOwnedStyle(theme.Input));
        checkBox.Style = CheckBoxStyle.Tick;
        checkBox.ActualStyle.ShouldBe(CheckBoxStyle.Tick);
        checkBox.Style = null;
        checkBox.ActualStyle.ShouldBe(ThemeOwnedStyle(theme.Input));
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

    /// <summary>Verifies defaults and the two-state user cycle.</summary>
    [Fact]
    public void Activate_WhenTwoState_CyclesFalseAndTrue()
    {
        var checkBox = new CheckBox();

        checkBox.IsChecked.ShouldBe(false);
        checkBox.IsThreeState.ShouldBeFalse();
        checkBox.Text.ShouldBeNull();
        checkBox.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        checkBox.Style.ShouldBeNull();
        checkBox.ActualStyle.ShouldBe(ThemeOwnedStyle(Themes.Dark.Input));
        checkBox.PerformClick();
        checkBox.IsChecked.ShouldBe(true);
        checkBox.PerformClick();
        checkBox.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies the command runs after the toggle commits and its events raise (#62).</summary>
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
        var checkBox = new CheckBox { IsThreeState = true };

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
        var checkBox = new CheckBox { IsThreeState = true, IsChecked = null };
        List<string> order = [];
        checkBox.Unchecked += (_, eventArgs) =>
        {
            checkBox.IsThreeState.ShouldBeFalse();
            checkBox.IsChecked.ShouldBe(false);
            eventArgs.Previous.ShouldBeNull();
            order.Add("unchecked");
        };
        checkBox.StateChanged += (_, _) => order.Add("changed");

        checkBox.IsThreeState = false;

        order.ShouldBe(["unchecked", "changed"]);
    }

    /// <summary>Verifies observers never see two-state mode paired with an indeterminate value.</summary>
    [Fact]
    public void IsThreeState_WhenDisabledFromNull_StagesBothPropertiesBeforeNotification()
    {
        var checkBox = new CheckBox { IsThreeState = true, IsChecked = null };
        List<string?> properties = [];
        checkBox.PropertyChanged += (_, eventArgs) =>
        {
            (checkBox.IsThreeState || checkBox.IsChecked is not null).ShouldBeTrue();
            properties.Add(eventArgs.PropertyName);
        };

        checkBox.IsThreeState = false;

        properties.ShouldBe([nameof(CheckBox.IsThreeState), nameof(CheckBox.IsChecked)]);
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
        frame.GetCell(new Point(3, 0)).IsContinuation.ShouldBeTrue();
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
        var checkBox = new CheckBox { IsThreeState = true };

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
        checkBox.Text.ShouldBeOfType<ControlText>().Content.ShouldBe("Accept");
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
            CheckBoxMarkStyle.Brackets,
            custom,
            CheckBoxStyle.Default.Appearance);
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
                (CheckBoxMarkStyle) 99,
                CheckBoxGlyphs.Default,
                CheckBoxStyle.Default.Appearance));
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

    private static void Key(CheckBox checkBox, KeyAction action) => Router.Route(
        checkBox,
        Events.Key,
        new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            action)));

    private static Theme ThemeWithInputProfile(ThemeProfile input)
    {
        var theme = new Theme();
        theme.SetProfiles(theme.Control, input, theme.Container, theme.Window, theme.Popup);
        theme.Freeze();
        return theme;
    }

    private static CheckBoxStyle ThemeOwnedStyle(ThemeProfile input) =>
        new(
            CheckBoxStyle.Default.MarkStyle,
            CheckBoxStyle.Default.Glyphs,
            ControlStyleProfiles.WithoutChrome(input));
}
