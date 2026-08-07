// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Input;

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
        input.PointerOver.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Tab);
        input.Focused.ShouldBeTrue();
    }

    /// <summary>Verifies PopupChrome's border override reaches the rendered open Calendar frame, not
    /// just the property value.</summary>
    [Fact]
    public async Task PopupStyle_WhenSetAndOpen_RendersOverriddenBorderGlyphAsync()
    {
        // Arrange
        var input = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 14, 30, 0),
            PopupChrome = new PopupChrome
            {
                Border = new Border(BorderSide.All, BorderGlyphStyle.Ascii, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None)
            }
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(input).ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(() => input.Opened = true, "open Calendar popup with overridden popup style");

        // Assert
        // ConnectsToAnchor omits the popup's top edge, so the bottom-left corner is checked instead.
        surface.Cell(new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Bottom - 1)).Text.ShouldBe("+");
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
        await surface.UpdateAsync(() => input.Opened = true, "open popup");
        input.Opened.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        input.Opened.ShouldBeFalse();
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

    /// <summary>Verifies a DateTimeInput constructed without an explicit Value seeds its lazily
    /// resolved default from the dispatcher's own clock once mounted, instead of a clock latched
    /// at construction - proving the control observes a fake TimeProvider it is mounted under.</summary>
    [Fact]
    public async Task Surface_WhenMountedUnderFakeClock_SeedsValueFromFakeClockAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var input = new DateTimeInput();

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(24, 3),
            clock,
            TestContext.Current.CancellationToken);

        // Assert
        var expected = clock.GetLocalNow().DateTime;
        input.Value.ShouldBe(expected);
    }

    /// <summary>Verifies a DateTimeInput constructed with AllowNull disabled before mounting still
    /// seeds its lazily resolved default from the dispatcher's fake clock, instead of eagerly
    /// latching the construction-time wall clock the moment AllowNull is set to false in an
    /// object initializer, which runs before the control is attached to any dispatcher.</summary>
    [Fact]
    public async Task Surface_WhenAllowNullIsDisabledBeforeMountingUnderFakeClock_SeedsValueFromFakeClockAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var input = new DateTimeInput { AllowNull = false };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(24, 3),
            clock,
            TestContext.Current.CancellationToken);

        // Assert
        var expected = clock.GetLocalNow().DateTime;
        input.Value.ShouldBe(expected);
    }

    /// <summary>Verifies a programmatic Value change while the Calendar popup is already open
    /// commits exactly once and leaves the popup open, instead of the push into the owned
    /// Calendar's Selection re-entering OnCalendarSelectionChanged as if it were a user pick -
    /// which would both commit a second time and set Opened = false out from under the caller.
    /// Sibling DateInput guards this same reentrancy with a _synchronizingCalendar flag; this
    /// proves DateTimeInput's equivalent guard covers the same hazard on every programmatic push,
    /// not just the lazy seeding path.</summary>
    [Fact]
    public async Task Value_WhenChangedWhilePopupIsOpen_CommitsOnceAndKeepsPopupOpenAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 1, 1, 8, 0, 0) };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.Opened = true, "open the Calendar popup");
        calendar.Selection.ShouldBe(new DateInterval(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1)));
        var changeCount = 0;
        input.ValueChanged += (_, _) => changeCount++;

        // Act — change Value, while the popup remains open, to a date the calendar does not
        // already show selected.
        await surface.UpdateAsync(
            () => input.Value = new DateTime(2026, 6, 15, 9, 30, 0),
            "change value while open");

        // Assert
        changeCount.ShouldBe(1);
        input.Opened.ShouldBeTrue();
        calendar.Selection.ShouldBe(new DateInterval(new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 15)));
    }
}
