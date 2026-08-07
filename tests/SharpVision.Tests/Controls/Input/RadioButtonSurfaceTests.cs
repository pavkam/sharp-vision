// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Styling;

/// <summary>Verifies RadioButton groups and appearance through a mounted terminal surface.</summary>
public sealed class RadioButtonSurfaceTests
{
    /// <summary>Verifies an unselected group renders exact marks and wide Unicode ownership.</summary>
    [Fact]
    public async Task Render_WhenRadioGroupStartsEmpty_ShowsExactUnselectedUnicodeRowsAsync()
    {
        // Arrange
        var first = Radio("One");
        var skipped = Radio("Skip", enabled: false);
        var third = Radio("界");
        var group = Group(first, skipped, third);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        first.IsChecked.ShouldBeFalse();
        skipped.IsChecked.ShouldBeFalse();
        third.IsChecked.ShouldBeFalse();
        surface.ShouldRender("""
                             ( ) One
                             ( ) Skip
                             ( ) 界
                             """);
        var wide = surface.Cell(new Point(4, 2));
        wide.Text.ShouldBe("界");
        wide.Width.ShouldBe(2);
        surface.Cell(new Point(5, 2)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeColorHelper.InactiveBorder(ThemeCatalog.Dark), ColorDepth.Basic16));
    }

    /// <summary>Verifies parenthesized marks show exact unchecked and checked terminal rows.</summary>
    [Fact]
    public async Task Render_WhenMarkStyleUsesParentheses_ShowsExactStateRowsAsync()
    {
        var uncheckedRadio = Radio("Off");
        uncheckedRadio.Style = RadioButtonStyle.Parentheses;
        var checkedRadio = Radio("On", isChecked: true);
        checkedRadio.Style = RadioButtonStyle.Parentheses;
        var group = Group(uncheckedRadio, checkedRadio);

        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("""
                             ( ) Off
                             (•) On
                             """);
    }

    /// <summary>Verifies a checked mark uses the theme's checked-state foreground.</summary>
    [Fact]
    public async Task Render_WhenCheckedAppearanceUsesLiteralForeground_ColorsTheCompleteMarkAsync()
    {
        var color = Color.Rgb(0x32, 0xC8, 0x78);
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            extraStyles: """, "radioButton": { "checked": { "face": { "foreground": "#32c878" } } } """));
        var radio = new RadioButton
        {
            IsChecked = true,
            Style = RadioButtonStyle.Parentheses
        };

        await using var surface = await ComponentSurface.MountAsync(
            radio,
            new Size(3, 1),
            theme,
            TestContext.Current.CancellationToken);

        var expected = TerminalPalette.Project(color, ColorDepth.Basic16);
        surface.ShouldRender("(•)");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(expected);
    }

    /// <summary>Verifies Space selection and arrows skip disabled members and wrap.</summary>
    [ComponentBehaviorEvidence(
        typeof(RadioButton),
        ComponentBehavior.Mounted |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.Activation |
        ComponentBehavior.KeyboardActivation)]
    [Fact]
    public async Task Keyboard_WhenRadioGroupNavigates_SelectsEligibleMembersAndWrapsAsync()
    {
        // Arrange
        List<ActivationCause> causes = [];
        var first = Radio("One");
        var skipped = Radio("Skip", enabled: false);
        var third = Radio("界");
        first.SelectionChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);
        third.SelectionChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);
        var group = Group(first, skipped, third);
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act and assert initial keyboard selection
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        first.IsChecked.ShouldBeTrue();
        surface.ShouldHaveState(first, VisualState.Focused);

        // Act and assert disabled skipping
        await surface.Keyboard.PressAsync(Code.Down);
        first.IsChecked.ShouldBeFalse();
        third.IsChecked.ShouldBeTrue();
        third.Focused.ShouldBeTrue();
        surface.ShouldHaveState(third, VisualState.Focused);
        surface.ShouldRender("""
                             ( ) One
                             ( ) Skip
                             (•) 界
                             """);
        surface.Cell(new Point(0, 2)).Style.Foreground.ShouldBe(ReferenceColors.Get(14));
        surface.Cell(new Point(4, 2)).Style.Foreground.ShouldBe(ReferenceColors.Get(15));

        // Act and assert wrapping
        await surface.Keyboard.PressAsync(Code.Down);
        first.IsChecked.ShouldBeTrue();
        first.Focused.ShouldBeTrue();
        third.IsChecked.ShouldBeFalse();
        causes.ShouldBe([
            ActivationCause.Keyboard,
            ActivationCause.Keyboard,
            ActivationCause.Keyboard
        ]);
    }

    /// <summary>Verifies primary-click selection is exclusive and reports pointer cause.</summary>
    [ComponentBehaviorEvidence(
        typeof(RadioButton),
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.PressRelease |
        ComponentBehavior.UnavailableCleanup)]
    [ComponentBehaviorEvidence(typeof(RadioButton), ComponentBehavior.PointerActivation)]
    [Fact]
    public async Task Pointer_WhenDifferentRadioIsClicked_MovesExclusiveSelectionAsync()
    {
        // Arrange
        ActivationCause? cause = null;
        var first = Radio("One", isChecked: true);
        var second = Radio("Two");
        second.SelectionChanged += (_, eventArgs) => cause = eventArgs.Cause;
        var group = Group(first, second);
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        var initialMark = surface.Cell(default).Style.Foreground;
        initialMark.IsRgb.ShouldBeTrue();
        initialMark.ShouldBe(ReferenceColors.Get(14));
        var initialContent = surface.Cell(new Point(4, 0)).Style.Foreground;
        initialContent.IsRgb.ShouldBeTrue();
        initialContent.ShouldBe(ReferenceColors.Get(15));

        // Act hover and held press
        await surface.Pointer.MoveToAsync(second);
        await surface.Pointer.PressAsync();

        // Assert held state before semantic activation
        second.IsChecked.ShouldBeFalse();
        surface.ShouldHaveState(second, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldHaveCapture(second);

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert
        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
        cause.ShouldBe(ActivationCause.Pointer);
        surface.ShouldHaveState(second, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldRender("""
                             ( ) One
                             (•) Two
                             """);

        // Act unavailable while held again
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => second.Enabled = false, "disable held RadioButton");

        // Assert cleanup preserves the completed selection
        second.IsChecked.ShouldBeTrue();
        second.Pressed.ShouldBeFalse();
        second.Focused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies a retained selected value remains visible but wholly muted while disabled.</summary>
    [Fact]
    public async Task Render_WhenSelectedRadioIsDisabled_MutesMarkAndContentAsync()
    {
        // Arrange
        var radio = Radio("Locked");

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            radio,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => radio.IsChecked = true, "select RadioButton programmatically");
        surface.ShouldRender("(•) Locked");
        var selectedMark = surface.Cell(default).Style.Foreground;
        selectedMark.IsRgb.ShouldBeTrue();
        selectedMark.ShouldBe(ReferenceColors.Get(14));
        var selectedContent = surface.Cell(new Point(4, 0)).Style.Foreground;
        selectedContent.IsRgb.ShouldBeTrue();
        selectedContent.ShouldBe(ReferenceColors.Get(15));

        // Act
        await surface.UpdateAsync(() => radio.Enabled = false, "disable selected RadioButton");

        // Assert
        radio.IsChecked.ShouldBeTrue();
        surface.ShouldHaveState(radio, VisualState.Disabled);
        surface.ShouldRender("(•) Locked");
        var expectedDisabledFg = TerminalPalette.Project(ThemeColorHelper.DisabledForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        var mark = surface.Cell(default).Style.Foreground;
        mark.IsRgb.ShouldBeTrue();
        mark.ShouldBe(expectedDisabledFg);
        var content = surface.Cell(new Point(4, 0)).Style.Foreground;
        content.IsRgb.ShouldBeTrue();
        content.ShouldBe(expectedDisabledFg);

        // Act and assert restored availability
        await surface.UpdateAsync(() => radio.Enabled = true, "re-enable selected RadioButton");
        surface.Cell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(14));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
    }

    private static RadioButton Radio(
        string content,
        bool enabled = true,
        bool isChecked = false) => new()
        {
            Text = content,
            GroupName = "surface",
            IsChecked = isChecked,
            Enabled = enabled
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
}
