// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves TimeInput appearance and interaction through mounted terminal surfaces.</summary>
public sealed class TimeInputSurfaceTests
{
    /// <summary>Verifies a mounted TimeInput renders a bordered field with a formatted time.</summary>
    [Fact]
    public async Task Render_WhenTimeInputIsMounted_DrawsBorderedFieldWithTimeAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert — bordered field renders
        input.Bounds.Width.ShouldBeGreaterThan(0);
        input.Bounds.Height.ShouldBeGreaterThan(0);
        surface.Cell(default).Text.ShouldBe("┏");

        // Assert — hover and focus behavior
        await surface.Pointer.MoveToAsync(input);
        input.IsPointerOver.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Tab);
        input.IsFocused.ShouldBeTrue();
    }

    /// <summary>Verifies Up arrow increments the focused hour segment.</summary>
    [Fact]
    public async Task Keyboard_WhenUpIsPressed_IncrementsHourAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);

        // Act
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — hour increments from 14 to 15
        input.Value.ShouldBe(new TimeOnly(15, 30));
    }

    /// <summary>Verifies Down arrow decrements the focused hour segment.</summary>
    [Fact]
    public async Task Keyboard_WhenDownIsPressed_DecrementsHourAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert — hour decrements from 14 to 13
        input.Value.ShouldBe(new TimeOnly(13, 30));
    }

    /// <summary>Verifies Right then Up navigates to the minute segment and increments.</summary>
    [Fact]
    public async Task Keyboard_WhenRightThenUp_IncrementsMinuteAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — minute increments from 30 to 31
        input.Value.ShouldBe(new TimeOnly(14, 31));
    }

    /// <summary>Verifies a mounted minute segment honors the configured time step.</summary>
    [Fact]
    public async Task Keyboard_WhenConfiguredStepIsUsed_IncrementsMinuteByStepAsync()
    {
        var input = new TimeInput { Value = new TimeOnly(14, 30), TimeStep = TimeSpan.FromMinutes(15) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Up);

        input.Value.ShouldBe(new TimeOnly(14, 45));
    }

    /// <summary>Verifies Left arrow returns to the previous segment after navigating Right.</summary>
    [Fact]
    public async Task Keyboard_WhenRightThenLeft_ReturnsToHourAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — Right then Left returns to hour, Up increments hour
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — hour increments (proves we're back on hour segment)
        input.Value.ShouldBe(new TimeOnly(15, 30));
    }

    /// <summary>Verifies Home moves to the first segment.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeIsPressed_MovesToFirstSegmentAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — go Right to minute, then Home to return to hour
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — hour increments (proves we're on the first segment)
        input.Value.ShouldBe(new TimeOnly(15, 30));
    }

    /// <summary>Verifies End moves to the last segment.</summary>
    [Fact]
    public async Task Keyboard_WhenEndIsPressed_MovesToLastSegmentAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — End moves to last segment, Down decrements it
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert — minute decrements (last segment for 24h without seconds)
        input.Value.ShouldBe(new TimeOnly(14, 29));
    }

    /// <summary>Verifies Delete clears the value when AllowNull is set.</summary>
    [Fact]
    public async Task Keyboard_WhenDeleteIsPressed_ClearsValueAsync()
    {
        // Arrange
        var input = new TimeInput
        {
            Value = new TimeOnly(14, 30),
            AllowNull = true
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        input.Value.ShouldBeNull();
    }

    /// <summary>Verifies typing digits enters the hour value directly.</summary>
    [Fact]
    public async Task Keyboard_WhenDigitsAreTyped_SetsHourAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — type "09" for 09 on the hour segment
        await surface.Keyboard.CompleteCharacterAsync(new Rune('0'));
        await surface.Keyboard.CompleteCharacterAsync(new Rune('9'));

        // Assert — hour changes to 9
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Hour.ShouldBe(9);
    }

    /// <summary>Verifies navigating Right twice reaches the second segment when ShowSeconds is set.</summary>
    [Fact]
    public async Task Keyboard_WhenShowSecondsAndNavigatedToSecond_IncrementsSecondAsync()
    {
        // Arrange
        var input = new TimeInput
        {
            Value = new TimeOnly(14, 30, 45),
            ShowSeconds = true
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — Right twice to reach second segment
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — second increments from 45 to 46
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Second.ShouldBe(46);
    }

    /// <summary>Verifies Backspace clears the active segment value.</summary>
    [Fact]
    public async Task Keyboard_WhenBackspaceIsPressed_ClearsActiveSegmentAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — Backspace on hour segment
        await surface.Keyboard.PressAsync(Code.Backspace);

        // Assert — hour resets to 0 in 24h mode
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Hour.ShouldBe(0);
    }

    /// <summary>Verifies a TimeInput constructed without an explicit Value seeds its lazily
    /// resolved default from the dispatcher's own clock once mounted, instead of a clock latched
    /// at construction - proving the control observes a fake TimeProvider it is mounted under.</summary>
    [Fact]
    public async Task Surface_WhenMountedUnderFakeClock_SeedsValueFromFakeClockAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var input = new TimeInput();

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            clock,
            TestContext.Current.CancellationToken);

        // Assert
        var expected = TimeOnly.FromDateTime(clock.GetLocalNow().DateTime);
        input.Value.ShouldBe(expected);
    }

    /// <summary>Verifies a TimeInput constructed with AllowNull disabled before mounting still
    /// seeds its lazily resolved default from the dispatcher's fake clock, instead of eagerly
    /// latching the construction-time wall clock the moment AllowNull is set to false in an
    /// object initializer, which runs before the control is attached to any dispatcher.</summary>
    [Fact]
    public async Task Surface_WhenAllowNullIsDisabledBeforeMountingUnderFakeClock_SeedsValueFromFakeClockAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var input = new TimeInput { AllowNull = false };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            clock,
            TestContext.Current.CancellationToken);

        // Assert
        var expected = TimeOnly.FromDateTime(clock.GetLocalNow().DateTime);
        input.Value.ShouldBe(expected);
    }

    /// <summary>Verifies a mounted TimeInput proves direct disable, ancestor-inherited disable, and
    /// re-enable recovery on a real terminal surface.</summary>
    [Fact]
    public async Task Enabled_WhenToggledOnMountedTimeInput_AppliesDirectAndInheritedDisabledStateAsync()
    {
        // Arrange — direct disable
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable TimeInput directly");

        // Assert
        surface.ShouldHaveState(input, VisualState.Disabled);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => input.IsEnabled = true, "re-enable TimeInput");

        // Assert
        surface.ShouldHaveState(input, VisualState.Normal);

        // Arrange — ancestor-inherited disable
        var child = new TimeInput { Value = new TimeOnly(14, 30) };
        var ancestor = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            ancestor,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => ancestor.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);
    }

    /// <summary>Verifies a disabled TimeInput's arranged geometry after a genuine resize still
    /// matches an independently mounted, still-enabled TimeInput arranged at that same size,
    /// proving disabling does not perturb layout.</summary>
    [Fact]
    public async Task Layout_WhenDisabledTimeInputIsResized_MatchesEnabledGeometryAtNewSizeAsync()
    {
        // Arrange
        var mountSize = new Size(12, 3);
        var resizedSize = new Size(20, 5);
        var disabled = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabled,
            mountSize,
            TestContext.Current.CancellationToken);
        await disabledSurface.UpdateAsync(() => disabled.IsEnabled = false, "disable TimeInput before resize");

        // Act
        await disabledSurface.ResizeAsync(resizedSize);

        var enabled = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabled,
            resizedSize,
            TestContext.Current.CancellationToken);

        // Assert
        disabled.Bounds.ShouldBe(enabled.Bounds);
        disabled.DesiredSize.ShouldBe(enabled.DesiredSize);
    }

    /// <summary>Verifies a disabled TimeInput refuses Tab focus and leaves it on a focusable
    /// sibling instead.</summary>
    [Fact]
    public async Task Keyboard_WhenDisabledTimeInputIsTabbed_DoesNotReceiveFocusAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        var sibling = new TimeInput();
        var root = new Stack { Children = { input, sibling } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable TimeInput before Tab");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        input.IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(sibling);
    }

    /// <summary>Verifies both affixes render pinned beside the segment text, with the segment text
    /// unmoved from its own affix-less position - only the affix columns are new.</summary>
    [Fact]
    public async Task Render_WhenTimeInputHasBothAffixes_PinsThemBesideSegmentsAsync()
    {
        // Arrange
        var input = new TimeInput
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Value = new TimeOnly(14, 30),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(14, 3),
            TestContext.Current.CancellationToken);

        // Assert - border, then ">", one gap, then "14:30", one gap, then "<".
        surface.Cell(new Point(1, 1)).Text.ShouldBe(">");
        surface.Cell(new Point(3, 1)).Text.ShouldBe("1");
        surface.Cell(new Point(7, 1)).Text.ShouldBe("0");
        surface.Cell(new Point(9, 1)).Text.ShouldBe("<");
    }

    /// <summary>Verifies a click on a segment still activates the correct segment when a start
    /// affix shifts the segment layout rightward, proving pointer hit-testing accounts for the
    /// affix-deflated box instead of the raw, affix-unaware content box.</summary>
    [Fact]
    public async Task Pointer_WhenStartAffixIsSetAndMinuteSegmentIsClicked_ActivatesMinuteAsync()
    {
        // Arrange
        var input = new TimeInput
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Value = new TimeOnly(14, 30),
            StartAffix = new Affix(">")
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(14, 3),
            TestContext.Current.CancellationToken);

        // Act - column 7 (relative to Bounds) lands on the minute segment's "3" digit: border(1) +
        // affix(1) + gap(1) + "14:"(3) = column 6 is "3", column 7 is "0".
        await surface.Pointer.ClickAsync(input, new Point(6, 1));
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert - the minute segment incremented, proving the click landed on minute, not hour.
        input.Value.ShouldBe(new TimeOnly(14, 31));
    }
}
