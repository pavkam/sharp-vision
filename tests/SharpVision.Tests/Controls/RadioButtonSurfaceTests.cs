// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

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
            ○ One
            ○ Skip
            ○ 界
            """);
        var wide = surface.Cell(new Point(2, 2));
        wide.Text.ShouldBe("界");
        wide.Width.ShouldBe(2);
        surface.Cell(new Point(3, 2)).IsContinuation.ShouldBeTrue();
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(Color.Indexed(8));
    }

    /// <summary>Verifies Space selection and arrows skip disabled members and wrap.</summary>
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
        third.IsFocused.ShouldBeTrue();
        surface.ShouldHaveState(third, VisualState.Focused);
        surface.ShouldRender("""
            ○ One
            ○ Skip
            ◉ 界
            """);
        surface.Cell(new Point(0, 2)).Style.Foreground.ShouldBe(Color.Indexed(14));
        surface.Cell(new Point(2, 2)).Style.Foreground.ShouldBe(Color.Indexed(14));

        // Act and assert wrapping
        await surface.Keyboard.PressAsync(Code.Down);
        first.IsChecked.ShouldBeTrue();
        first.IsFocused.ShouldBeTrue();
        third.IsChecked.ShouldBeFalse();
        causes.ShouldBe([
            ActivationCause.Keyboard,
            ActivationCause.Keyboard,
            ActivationCause.Keyboard,
        ]);
    }

    /// <summary>Verifies primary-click selection is exclusive and reports pointer cause.</summary>
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
        initialMark.Kind.ShouldBe(ColorKind.Indexed);
        initialMark.Red.ShouldBe((byte) 14);
        var initialContent = surface.Cell(new Point(2, 0)).Style.Foreground;
        initialContent.Kind.ShouldBe(ColorKind.Indexed);
        initialContent.Red.ShouldBe((byte) 14);

        // Act
        await surface.Pointer.ClickAsync(second);

        // Assert
        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
        cause.ShouldBe(ActivationCause.Pointer);
        surface.ShouldHaveState(second, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldRender("""
            ○ One
            ◉ Two
            """);
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
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => radio.IsChecked = true, "select RadioButton programmatically");
        surface.ShouldRender("◉ Locked");
        var selectedMark = surface.Cell(default).Style.Foreground;
        selectedMark.Kind.ShouldBe(ColorKind.Indexed);
        selectedMark.Red.ShouldBe((byte) 14);
        var selectedContent = surface.Cell(new Point(2, 0)).Style.Foreground;
        selectedContent.Kind.ShouldBe(ColorKind.Indexed);
        selectedContent.Red.ShouldBe((byte) 14);

        // Act
        await surface.UpdateAsync(() => radio.IsEnabled = false, "disable selected RadioButton");

        // Assert
        radio.IsChecked.ShouldBeTrue();
        surface.ShouldHaveState(radio, VisualState.Disabled);
        surface.ShouldRender("◉ Locked");
        var mark = surface.Cell(default).Style.Foreground;
        mark.Kind.ShouldBe(ColorKind.Indexed);
        mark.Red.ShouldBe((byte) 8);
        var content = surface.Cell(new Point(2, 0)).Style.Foreground;
        content.Kind.ShouldBe(ColorKind.Indexed);
        content.Red.ShouldBe((byte) 8);

        // Act and assert restored availability
        await surface.UpdateAsync(() => radio.IsEnabled = true, "re-enable selected RadioButton");
        surface.Cell(default).Style.Foreground.ShouldBe(Color.Indexed(14));
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(Color.Indexed(14));
    }

    private static RadioButton Radio(
        string content,
        bool enabled = true,
        bool isChecked = false) => new()
        {
            Content = new ControlText(content),
            GroupName = "surface",
            IsChecked = isChecked,
            IsEnabled = enabled,
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
