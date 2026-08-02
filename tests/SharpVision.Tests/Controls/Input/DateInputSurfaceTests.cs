// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves DateInput appearance and interaction through mounted terminal surfaces.</summary>
public sealed class DateInputSurfaceTests
{
    /// <summary>Verifies a mounted DateInput renders a bordered field with a formatted date.</summary>
    [ComponentBehaviorEvidence(
        typeof(DateInput),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Render_WhenDateInputIsMounted_DrawsBorderedFieldWithDateAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            Format = "MM/dd/yyyy"
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
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
    public async Task Keyboard_WhenUpIsPressed_IncrementsMonthSegmentAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);

        // Act
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — month increments from 3 to 4
        input.Value.ShouldBe(new DateOnly(2026, 4, 15));
    }

    /// <summary>Verifies Down arrow decrements the focused month segment.</summary>
    [Fact]
    public async Task Keyboard_WhenDownIsPressed_DecrementsMonthSegmentAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert — month decrements from 3 to 2
        input.Value.ShouldBe(new DateOnly(2026, 2, 15));
    }

    /// <summary>Verifies Right then Up navigates to the day segment and increments.</summary>
    [Fact]
    public async Task Keyboard_WhenRightThenUp_IncrementsDaySegmentAsync()
    {
        // Arrange — invariant culture is MM/dd/yyyy so Right moves to day
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — day increments from 15 to 16
        input.Value.ShouldBe(new DateOnly(2026, 3, 16));
    }

    /// <summary>Verifies Left arrow returns to the previous segment after navigating Right.</summary>
    [Fact]
    public async Task Keyboard_WhenRightThenLeft_ReturnsToMonthSegmentAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — Right then Left returns to month, Up increments month
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — month increments (proves we're back on the first segment)
        input.Value.ShouldBe(new DateOnly(2026, 4, 15));
    }

    /// <summary>Verifies Home returns to the first segment from anywhere.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeIsPressed_MovesToFirstSegmentAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — go Right twice to year, then Home to return to month
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — month increments (proves we're on the first segment)
        input.Value.ShouldBe(new DateOnly(2026, 4, 15));
    }

    /// <summary>Verifies End moves to the last segment.</summary>
    [Fact]
    public async Task Keyboard_WhenEndIsPressed_MovesToLastSegmentAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — End moves to last segment (year for invariant MM/dd/yyyy)
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — year increments (proves we're on the last segment)
        input.Value.ShouldBe(new DateOnly(2027, 3, 15));
    }

    /// <summary>Verifies navigating Right twice reaches the year segment.</summary>
    [Fact]
    public async Task Keyboard_WhenNavigatedToYear_IncrementsYearAsync()
    {
        // Arrange — Right twice to get to year (MM → dd → yyyy)
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — year increments from 2026 to 2027
        input.Value.ShouldBe(new DateOnly(2027, 3, 15));
    }

    /// <summary>Verifies Delete clears the value when AllowNull is set.</summary>
    [Fact]
    public async Task Keyboard_WhenDeleteIsPressed_ClearsValueAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            AllowNull = true
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
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
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
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

    /// <summary>Verifies typing digits enters the month value directly.</summary>
    [Fact]
    public async Task Keyboard_WhenDigitsAreTyped_SetsMonthSegmentAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — type "09" for September on the month segment
        await surface.Keyboard.CompleteCharacterAsync(new Rune('0'));
        await surface.Keyboard.CompleteCharacterAsync(new Rune('9'));

        // Assert — month changes to 9
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Month.ShouldBe(9);
    }

    /// <summary>Verifies the short date format 'd' resolves segment kinds correctly.</summary>
    [Fact]
    public async Task Keyboard_WhenShortFormatIsUsed_ResolvesSegmentsCorrectlyAsync()
    {
        // Arrange — "d" is the default format
        var input = new DateInput
        {
            Value = new DateOnly(2026, 6, 20),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — segment 0 should be month, increment it
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — month changes from 6 to 7
        input.Value.ShouldBe(new DateOnly(2026, 7, 20));
    }

    /// <summary>Verifies Backspace clears the active segment to its minimum.</summary>
    [Fact]
    public async Task Keyboard_WhenBackspaceIsPressed_ClearsActiveSegmentAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 7, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act — Backspace on month segment
        await surface.Keyboard.PressAsync(Code.Backspace);

        // Assert — month resets to 1
        _ = input.Value.ShouldNotBeNull();
        input.Value.Value.Month.ShouldBe(1);
    }
}
