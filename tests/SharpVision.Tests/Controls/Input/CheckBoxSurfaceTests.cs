// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies CheckBox states and activation through a mounted terminal surface.</summary>
public sealed class CheckBoxSurfaceTests
{
    /// <summary>Verifies the unchecked bracket mark and wide label retain exact cell ownership.</summary>
    [Fact]
    public async Task Render_WhenUncheckedUnicodeContentIsMounted_ShowsExactNormalCellsAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Content = new ControlText("界") };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldHaveState(checkBox, VisualState.Normal);
        surface.ShouldRender("[ ] 界");
        surface.Cell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
        var wide = surface.Cell(new Point(4, 0));
        wide.Text.ShouldBe("界");
        wide.Width.ShouldBe(2);
        surface.Cell(new Point(5, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies hover, held press, release, focus, and pointer activation compose correctly.</summary>
    [ComponentBehaviorEvidence(
        typeof(CheckBox),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.UnavailableCleanup)]
    [Fact]
    public async Task Pointer_WhenCheckBoxIsClicked_ComposesStatesAndTogglesWithPointerCauseAsync()
    {
        // Arrange
        ActivationCause? cause = null;
        var checkBox = new CheckBox { Content = new ControlText("Choice") };
        checkBox.Checked += (_, eventArgs) => cause = eventArgs.Cause;
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        var hoveredForeground = ThemeColorHelper.HoveredForeground(Themes.Dark);
        var focusedForeground = ThemeColorHelper.FocusedForeground(Themes.Dark);

        // Act and assert hover
        await surface.Pointer.MoveToAsync(checkBox);
        surface.ShouldHaveState(checkBox, VisualState.PointerOver);
        surface.Cell(default).Style.Foreground.ShouldBe(hoveredForeground);
        surface.Cell(default).Style.Background.ShouldBe(ReferenceColors.Get(0));

        // Act and assert held press
        await surface.Pointer.PressAsync();
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldHaveState(checkBox, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldRender("[ ] Choice");

        // Act and assert release
        await surface.Pointer.ReleaseAsync();
        checkBox.IsChecked.ShouldBe(true);
        cause.ShouldBe(ActivationCause.Pointer);
        surface.ShouldHaveState(checkBox, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldRender("[✓] Choice");
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
        surface.Cell(new Point(4, 0)).Style.Background.ShouldBe(ReferenceColors.Get(0));

        // Act unavailable while another activation is held
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(checkBox);
        await surface.UpdateAsync(() => checkBox.IsEnabled = false, "disable held CheckBox");

        // Assert cleanup without another toggle
        checkBox.IsChecked.ShouldBe(true);
        checkBox.IsPressed.ShouldBeFalse();
        checkBox.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies complete Space actions reach checked and indeterminate states with keyboard cause.</summary>
    [ComponentBehaviorEvidence(
        typeof(CheckBox),
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.KeyboardActivation)]
    [Fact]
    public async Task Keyboard_WhenThreeStateCheckBoxCompletesSpace_CyclesThroughIntendedStatesAsync()
    {
        // Arrange
        List<ActivationCause> causes = [];
        var checkBox = new CheckBox { Content = new ControlText("Option"), IsThreeState = true };
        checkBox.StateChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        var focusedForeground = ThemeColorHelper.FocusedForeground(Themes.Dark);

        // Act and assert focus
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveState(checkBox, VisualState.Focused);
        surface.ShouldRender("[ ] Option");

        await surface.Keyboard.PressAsync(Code.Right);
        checkBox.IsChecked.ShouldBe(false);

        // Act
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        checkBox.IsChecked.ShouldBeNull();
        causes.ShouldBe([ActivationCause.Keyboard, ActivationCause.Keyboard]);
        surface.ShouldHaveState(checkBox, VisualState.Focused);
        surface.ShouldRender("[─] Option");
        surface.Cell(default).Style.Foreground.ShouldBe(focusedForeground);
        surface.Cell(default).Style.Background.ShouldBe(ReferenceColors.Get(0));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
        surface.Cell(new Point(4, 0)).Style.Background.ShouldBe(ReferenceColors.Get(0));
    }

    /// <summary>Verifies disabled checked state refuses keyboard and pointer activation.</summary>
    [Fact]
    public async Task Input_WhenCheckedCheckBoxIsDisabled_PreservesValueAndMutedAppearanceAsync()
    {
        // Arrange
        var changes = 0;
        var checkBox = new CheckBox { Content = new ControlText("Disabled"), IsChecked = true };
        checkBox.StateChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        var selectedContent = surface.Cell(new Point(4, 0)).Style.Foreground;
        selectedContent.IsRgb.ShouldBeTrue();
        selectedContent.ShouldBe(ReferenceColors.Get(15));

        // Act
        await surface.UpdateAsync(() => checkBox.IsEnabled = false, "disable checked CheckBox");
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Pointer.ClickAsync(checkBox);

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        checkBox.IsFocused.ShouldBeFalse();
        changes.ShouldBe(0);
        surface.ShouldHaveState(checkBox, VisualState.Disabled);
        surface.ShouldRender("[✓] Disabled");
        var expectedDisabledFg = Palette.Project(ThemeColorHelper.DisabledForeground(Themes.Dark), ColorDepth.Basic16);
        var foreground = surface.Cell(default).Style.Foreground;
        foreground.IsRgb.ShouldBeTrue();
        foreground.ShouldBe(expectedDisabledFg);
        var contentForeground = surface.Cell(new Point(4, 0)).Style.Foreground;
        contentForeground.IsRgb.ShouldBeTrue();
        contentForeground.ShouldBe(expectedDisabledFg);

        // Act and assert restored availability
        await surface.UpdateAsync(() => checkBox.IsEnabled = true, "re-enable checked CheckBox");
        surface.Cell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
    }

    /// <summary>Verifies tiny bounds clip the mark without emitting content outside the control.</summary>
    [Fact]
    public async Task Render_WhenCheckBoxIsTwoCellsWide_ClipsMarkAndContentAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Content = new ControlText("Hidden") };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(2, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("[ ");

        // Act and assert expanded surface
        await surface.ResizeAsync(new Size(10, 1));
        surface.ShouldRender("[ ] Hidden");
    }
}
