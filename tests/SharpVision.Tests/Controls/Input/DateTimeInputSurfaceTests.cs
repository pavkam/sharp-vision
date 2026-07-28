// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves DateTimeInput appearance and interaction through mounted terminal surfaces.</summary>
public sealed class DateTimeInputSurfaceTests
{
    /// <summary>Verifies a mounted DateTimeInput renders a bordered field with date and time.</summary>
    [ComponentBehaviorEvidence(
        typeof(DateTimeInput),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Render_WhenDateTimeInputIsMounted_DrawsBorderedFieldAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
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

    /// <summary>Verifies Up arrow increments the focused month segment.</summary>
    [Fact]
    public async Task Keyboard_WhenUpIsPressed_IncrementsMonthAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);

        // Act
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — month increments from 3 to 4
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Month.ShouldBe(4);
        input.Value.Value.Day.ShouldBe(15);
    }

    /// <summary>Verifies Down arrow decrements the focused month segment.</summary>
    [Fact]
    public async Task Keyboard_WhenDownIsPressed_DecrementsMonthAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert — month decrements from 3 to 2
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Month.ShouldBe(2);
    }

    /// <summary>Verifies Right navigates through date segments to the day.</summary>
    [Fact]
    public async Task Keyboard_WhenRightThenUp_IncrementsDayAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — day increments from 15 to 16
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Day.ShouldBe(16);
    }

    /// <summary>Verifies navigating Right twice then Up increments the year.</summary>
    [Fact]
    public async Task Keyboard_WhenNavigatedToYear_IncrementsYearAsync()
    {
        // Arrange — Right twice: month → day → year
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — year increments from 2026 to 2027
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Year.ShouldBe(2027);
    }

    /// <summary>Verifies navigating Right three times reaches the hour segment.</summary>
    [Fact]
    public async Task Keyboard_WhenNavigatedToHour_IncrementsHourAsync()
    {
        // Arrange — Right three times: month → day → year → hour
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — hour increments from 14 to 15
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Hour.ShouldBe(15);
    }

    /// <summary>Verifies navigating Right four times reaches the minute segment.</summary>
    [Fact]
    public async Task Keyboard_WhenNavigatedToMinute_IncrementsMinuteAsync()
    {
        // Arrange — Right four times: month → day → year → hour → minute
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — minute increments from 30 to 31
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Minute.ShouldBe(31);
    }

    /// <summary>Verifies a mounted date-time minute segment honors the configured time step.</summary>
    [Fact]
    public async Task Keyboard_WhenConfiguredStepIsUsed_IncrementsMinuteByStepAsync()
    {
        var input = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 14, 30, 0),
            TimeStep = TimeSpan.FromMinutes(15)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        for (var i = 0; i < 4; i++)
        {
            await surface.Keyboard.PressAsync(Code.Right);
        }

        await surface.Keyboard.PressAsync(Code.Up);

        input.Value.Value.Minute.ShouldBe(45);
    }

    /// <summary>Verifies Left returns to the previous segment.</summary>
    [Fact]
    public async Task Keyboard_WhenRightThenLeft_ReturnsToMonthAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — month increments (proves we returned to month)
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Month.ShouldBe(4);
    }

    /// <summary>Verifies Delete clears the value when AllowNull is set.</summary>
    [Fact]
    public async Task Keyboard_WhenDeleteIsPressed_ClearsValueAsync()
    {
        // Arrange
        var input = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 14, 30, 0),
            AllowNull = true
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        input.Value.ShouldBeNull();
    }

    /// <summary>Verifies Escape closes an open popup.</summary>
    [Fact]
    public async Task Keyboard_WhenEscapeIsPressedWhileOpen_ClosesPopupAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(() => input.IsOpen = true, "open popup");
        input.IsOpen.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies typing digits on the month segment sets the month directly.</summary>
    [Fact]
    public async Task Keyboard_WhenDigitsAreTyped_SetsMonthAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — type "09" for September
        await surface.Keyboard.CompleteCharacterAsync(new Rune('0'));
        await surface.Keyboard.CompleteCharacterAsync(new Rune('9'));

        // Assert — month changes to 9
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Month.ShouldBe(9);
    }

    /// <summary>Verifies Backspace clears the active segment to its minimum.</summary>
    [Fact]
    public async Task Keyboard_WhenBackspaceIsPressed_ClearsActiveSegmentAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 7, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — Backspace on month segment
        await surface.Keyboard.PressAsync(Code.Backspace);

        // Assert — month resets to 1
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Month.ShouldBe(1);
    }

    /// <summary>Verifies Home moves to the first segment.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeIsPressed_MovesToFirstSegmentAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — navigate Right, then Home to return to first segment
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — month increments (proves we're on the first segment)
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Month.ShouldBe(4);
    }

    /// <summary>Verifies End moves to the last segment.</summary>
    [Fact]
    public async Task Keyboard_WhenEndIsPressed_MovesToLastSegmentAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — End moves to last segment, Down decrements
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert — minute decrements (last segment in 24h without seconds)
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Minute.ShouldBe(29);
    }
}
