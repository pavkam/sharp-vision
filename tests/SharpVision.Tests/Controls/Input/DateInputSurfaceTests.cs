// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Input;

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
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
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

    /// <summary>
    /// Verifies a programmatic <see cref="DateInput.Value"/> change while the Calendar popup is
    /// already open re-syncs the popup's displayed month and selection to the new value, instead
    /// of leaving it showing the previous value's month with the previous date highlighted.
    /// <see cref="DateTimeInput"/>'s equivalent commit path already re-pushes the calendar's
    /// selection on every committed change regardless of whether its popup is open; DateInput's
    /// Value setter only did so when the popup transitioned from closed to open.
    /// </summary>
    [Fact]
    public async Task Value_WhenChangedWhilePopupIsOpen_ResyncsCalendarAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 1, 1),
            Culture = CultureInfo.InvariantCulture
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();

        await surface.UpdateAsync(() => input.Opened = true, "open the Calendar popup");
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 1, 1));
        calendar.Selection.ShouldBe(new DateInterval(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1)));

        // Act: change Value while the popup remains open.
        await surface.UpdateAsync(() => input.Value = new DateOnly(2026, 6, 15), "change value while open");

        // Assert
        input.Opened.ShouldBeTrue();
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 6, 1));
        calendar.Selection.ShouldBe(new DateInterval(new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 15)));
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

    /// <summary>Verifies returning focus starts a new digit entry sequence.</summary>
    [Fact]
    public async Task Keyboard_WhenFocusLeavesAndReturns_DoesNotCarryPreviousDigitAsync()
    {
        // Arrange
        var first = new DateInput
        {
            Value = new DateOnly(2026, 8, 15),
            Culture = CultureInfo.InvariantCulture
        };
        var second = new DateInput { Culture = CultureInfo.InvariantCulture };
        var root = new Stack { Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("1");
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        await surface.Keyboard.TypeAsync("2");

        // Assert
        first.Value.ShouldBe(new DateOnly(2026, 2, 15));
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
        await surface.UpdateAsync(() => input.Opened = true, "open popup");
        input.Opened.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        input.Opened.ShouldBeFalse();
    }

    /// <summary>Verifies an open date popup routes navigation and selection keys to its calendar.</summary>
    [Fact]
    public async Task Keyboard_WhenRightAndEnterArePressedWhileOpen_SelectsNextDateAsync()
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
        await surface.UpdateAsync(() => input.Opened = true, "open popup");

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(new DateOnly(2026, 3, 16));
        input.Opened.ShouldBeFalse();
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

    /// <summary>Verifies clicking a specific segment column activates that segment instead of
    /// opening the popup: TimeInput and DateTimeInput both map a clicked column to a segment via
    /// pointer hit-testing, but DateInput fell through to the shared PressBehavior for every
    /// click, opening the Calendar popup regardless of which column was pressed.</summary>
    [Fact]
    public async Task Pointer_WhenDaySegmentColumnIsClicked_ActivatesSegmentInsteadOfOpeningPopupAsync()
    {
        // Arrange — borderless chrome keeps content columns aligned with control-relative
        // coordinates; InvariantCulture's short date pattern is MM/dd/yyyy, so the Day segment
        // starts at column 3 (past "MM/").
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act — click the Day segment, then increment the active segment.
        await surface.Pointer.ClickAsync(input, new Point(3, 0));
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — the popup stayed closed and the day incremented, proving the click activated
        // the Day segment instead of toggling the drop-down.
        input.Opened.ShouldBeFalse();
        input.Value.ShouldBe(new DateOnly(2026, 3, 16));
    }

    /// <summary>Verifies clicking the Month segment column while focus is elsewhere on the Year
    /// segment moves the active segment back to Month, proving the hit-test resolves each column
    /// to its own segment rather than always landing on the same one.</summary>
    [Fact]
    public async Task Pointer_WhenMonthSegmentColumnIsClicked_MovesActiveSegmentBackToMonthAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);

        // Act — click back on the Month segment (column 0) and increment.
        await surface.Pointer.ClickAsync(input, new Point(0, 0));
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert — month incremented, proving the click moved the active segment off Year.
        input.Opened.ShouldBeFalse();
        input.Value.ShouldBe(new DateOnly(2026, 4, 15));
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

    /// <summary>Verifies a DateInput constructed without an explicit Value seeds its lazily
    /// resolved default from the dispatcher's own clock once mounted, instead of a clock latched
    /// at construction - proving the control observes a fake TimeProvider it is mounted under.</summary>
    [Fact]
    public async Task Surface_WhenMountedUnderFakeClock_SeedsValueFromFakeClockAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var input = new DateInput();

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            clock,
            TestContext.Current.CancellationToken);

        // Assert
        var expected = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        input.Value.ShouldBe(expected);
    }

    /// <summary>Verifies a DateInput constructed with AllowNull disabled before mounting still
    /// seeds its lazily resolved default from the dispatcher's fake clock, instead of eagerly
    /// latching the construction-time wall clock the moment AllowNull is set to false in an
    /// object initializer, which runs before the control is attached to any dispatcher.</summary>
    [Fact]
    public async Task Surface_WhenAllowNullIsDisabledBeforeMountingUnderFakeClock_SeedsValueFromFakeClockAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var input = new DateInput { AllowNull = false };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            clock,
            TestContext.Current.CancellationToken);

        // Assert
        var expected = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        input.Value.ShouldBe(expected);
    }
}
