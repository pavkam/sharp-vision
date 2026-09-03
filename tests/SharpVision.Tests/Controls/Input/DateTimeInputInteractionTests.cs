// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves DateTimeInput's combined date-and-time segment contract through mounted surfaces
/// and routed detached input: 12-hour digit entry and AM/PM selection, carry across every
/// component boundary, saturation at the DateTime range, per-segment Backspace, DateTimeKind
/// preservation, designator-less hour normalization, and the Calendar popup's boundary-day
/// acceptance, traversal, browsing, and disposal while open.</summary>
public sealed class DateTimeInputInteractionTests
{
    private static readonly DateTime _march15 = new(2026, 3, 15, 14, 30, 0);

    #region Twelve-hour digit entry and AM/PM

    /// <summary>Verifies hour digit entry under a 12-hour layout keeps the current half of the day
    /// and advances to Minute after a commit.</summary>
    [Theory]
    [InlineData("11", 23, 31)]
    [InlineData("12", 12, 31)]
    [InlineData("05", 17, 31)]
    [InlineData("2", 14, 31)]
    public async Task Keyboard_WhenTwelveHourDigitsAreTyped_KeepsHalfOfDayAndAdvancesAsync(
        string digits,
        int expectedHour,
        int expectedMinute)
    {
        // Arrange
        var input = Create(_march15);
        input.Use24HourFormat = false;
        await using var surface = await ComponentSurface.MountAsync(input, new Size(28, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);

        // Act
        await surface.Keyboard.TypeAsync(digits);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new DateTime(2026, 3, 15, expectedHour, expectedMinute, 0));
    }

    /// <summary>Verifies "p" selects PM and "a" selects AM without toggling on repeat, that a
    /// repeated letter is still consumed because it moved the designator highlight, and that the
    /// designator is the active segment afterwards.</summary>
    [Fact]
    public void Keyboard_WhenAOrPIsTyped_SelectsHalfOfDayWithoutToggling()
    {
        // Arrange
        using var control = Create(new DateTime(2026, 3, 15, 2, 30, 0));
        control.Use24HourFormat = false;

        // Act and assert
        Type(control, 'p').IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new DateTime(2026, 3, 15, 14, 30, 0));
        Type(control, 'p').IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new DateTime(2026, 3, 15, 14, 30, 0));
        Type(control, 'A').IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new DateTime(2026, 3, 15, 2, 30, 0));
        Type(control, 'a').IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new DateTime(2026, 3, 15, 2, 30, 0));

        // The designator is now the active segment: Up flips the half of the day.
        _ = Press(control, Code.Up);
        control.Value.ShouldBe(new DateTime(2026, 3, 15, 14, 30, 0));
    }

    /// <summary>Verifies stepping the designator flips the half of the day within the same date
    /// in both directions, even at the last hour of the year.</summary>
    [Fact]
    public void Keyboard_WhenDesignatorIsStepped_FlipsHalfOfDayWithinSameDate()
    {
        // Arrange
        using var control = Create(new DateTime(2026, 12, 31, 23, 30, 0));
        control.Use24HourFormat = false;
        _ = Press(control, Code.End);

        // Act and assert
        _ = Press(control, Code.Up);
        control.Value.ShouldBe(new DateTime(2026, 12, 31, 11, 30, 0));
        _ = Press(control, Code.Up);
        control.Value.ShouldBe(new DateTime(2026, 12, 31, 23, 30, 0));
        _ = Press(control, Code.Down);
        control.Value.ShouldBe(new DateTime(2026, 12, 31, 11, 30, 0));
    }

    #endregion

    #region Carry, saturation, and per-segment clearing

    /// <summary>Verifies stepping each component past its range carries into the next larger
    /// component: minute into hour, hour into day, day into month, month into year.</summary>
    [Theory]
    [InlineData(4, 2026, 12, 31, 23, 59, 2027, 1, 1, 0, 0)]
    [InlineData(3, 2026, 12, 31, 23, 59, 2027, 1, 1, 0, 59)]
    [InlineData(1, 2026, 12, 31, 23, 59, 2027, 1, 1, 23, 59)]
    [InlineData(0, 2026, 12, 31, 23, 59, 2027, 1, 31, 23, 59)]
    public void Keyboard_WhenComponentStepsPastRange_CarriesIntoLargerComponent(
        int segment,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        int expectedHour,
        int expectedMinute)
    {
        // Arrange
        using var control = Create(new DateTime(year, month, day, hour, minute, 0));
        MoveTo(control, segment);

        // Act
        var key = Press(control, Code.Up);

        // Assert
        key.IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new DateTime(expectedYear, expectedMonth, expectedDay, expectedHour, expectedMinute, 0));
    }

    /// <summary>Verifies stepping any component at <see cref="DateTime.MaxValue"/> swallows the
    /// out-of-range arithmetic, leaving the value untouched and the key consumed by the field.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Keyboard_WhenStepExceedsDateTimeRange_LeavesValueAndConsumesKey(int segment)
    {
        // Arrange
        using var control = Create(DateTime.MaxValue);
        MoveTo(control, segment);

        // Act
        var key = Press(control, Code.Up);

        // Assert
        control.Value.ShouldBe(DateTime.MaxValue);
        key.IsHandled.ShouldBeTrue();
    }

    /// <summary>Verifies Backspace resets each segment to its lowest value - day and year to one,
    /// hour, minute, and second to zero - while leaving every other component alone.</summary>
    [Theory]
    [InlineData(0, 2026, 1, 15, 14, 30, 45)]
    [InlineData(1, 2026, 3, 1, 14, 30, 45)]
    [InlineData(2, 1, 3, 15, 14, 30, 45)]
    [InlineData(3, 2026, 3, 15, 0, 30, 45)]
    [InlineData(4, 2026, 3, 15, 14, 0, 45)]
    [InlineData(5, 2026, 3, 15, 14, 30, 0)]
    public void Keyboard_WhenBackspaceClearsSegment_ResetsOnlyThatComponent(
        int segment,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        int expectedHour,
        int expectedMinute,
        int expectedSecond)
    {
        // Arrange
        using var control = Create(new DateTime(2026, 3, 15, 14, 30, 45));
        control.ShowSeconds = true;
        MoveTo(control, segment);

        // Act
        var key = Press(control, Code.Backspace);

        // Assert
        key.IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new DateTime(expectedYear, expectedMonth, expectedDay, expectedHour, expectedMinute, expectedSecond));
    }

    /// <summary>Verifies the seconds segment steps by one second once shown.</summary>
    [Fact]
    public void Keyboard_WhenSecondsAreShown_EndReachesSecondsAndUpStepsThem()
    {
        // Arrange
        using var control = Create(new DateTime(2026, 3, 15, 14, 30, 59));
        control.ShowSeconds = true;

        // Act
        _ = Press(control, Code.End);
        _ = Press(control, Code.Up);

        // Assert
        control.Value.ShouldBe(new DateTime(2026, 3, 15, 14, 31, 0));
    }

    /// <summary>Verifies digit entry, stepping, and Backspace all preserve the value's
    /// <see cref="DateTimeKind"/>.</summary>
    [Fact]
    public void Keyboard_WhenValueIsEdited_PreservesDateTimeKind()
    {
        // Arrange
        using var control = Create(new DateTime(2026, 3, 15, 14, 30, 0, DateTimeKind.Utc));

        // Act
        _ = Type(control, '1');
        _ = Type(control, '2');
        _ = Press(control, Code.End);
        _ = Press(control, Code.Up);
        _ = Press(control, Code.Backspace);

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Kind.ShouldBe(DateTimeKind.Utc);
        value.ShouldBe(new DateTime(2026, 12, 15, 14, 0, 0, DateTimeKind.Utc));
    }

    #endregion

    #region Traversal and command keys

    /// <summary>Verifies Left at the first segment and Right at the last segment stay put without
    /// wrapping and are still consumed by the field.</summary>
    [Fact]
    public void Keyboard_WhenTraversalHitsEitherEnd_StaysWithoutWrappingAndConsumesKey()
    {
        // Arrange
        using var control = Create(_march15);

        // Act and assert
        Press(control, Code.Left).IsHandled.ShouldBeTrue();
        _ = Press(control, Code.Up);
        control.Value.ShouldBe(new DateTime(2026, 4, 15, 14, 30, 0));
        _ = Press(control, Code.End);
        Press(control, Code.Right).IsHandled.ShouldBeTrue();
        _ = Press(control, Code.Up);
        control.Value.ShouldBe(new DateTime(2026, 4, 15, 14, 31, 0));
    }

    /// <summary>Verifies keys outside the command set stay unhandled while closed, and F4 - a
    /// DateInput-only opening chord - neither opens nor is consumed here.</summary>
    [Theory]
    [InlineData(Code.PageUp)]
    [InlineData(Code.PageDown)]
    [InlineData(Code.Escape)]
    [InlineData(Code.F4)]
    public void Keyboard_WhenNonCommandKeyIsPressedWhileClosed_LeavesKeyUnhandled(Code code)
    {
        // Arrange
        using var control = Create(_march15);

        // Act
        var key = Press(control, code);

        // Assert
        key.IsHandled.ShouldBeFalse();
        control.IsOpen.ShouldBeFalse();
        control.Value.ShouldBe(_march15);
    }

    /// <summary>Verifies Enter on the closed field is consumed by press activation without opening
    /// the popup, while a pointer click on the indicator does open it.</summary>
    [Fact]
    public async Task Activation_WhenEnterIsPressedOrIndicatorIsClicked_OpensOnlyForPointerAsync()
    {
        // Arrange
        var input = Create(_march15);
        input.HorizontalAlignment = HorizontalAlignment.Left;
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);
        var openAfterEnter = input.IsOpen;
        await surface.Pointer.ClickAsync(input, new Point(input.Bounds.Width - 2, 1));

        // Assert
        openAfterEnter.ShouldBeFalse();
        input.IsOpen.ShouldBeTrue();
        input.Value.ShouldBe(_march15);
    }

    /// <summary>Verifies Delete and Backspace over an empty value, and Delete under AllowNull =
    /// false, change nothing and are still consumed by the field.</summary>
    [Fact]
    public void Keyboard_WhenClearingCannotChangeValue_ConsumesKeyWithoutChange()
    {
        // Arrange
        using var empty = Create(null);
        using var nonNullable = Create(_march15);
        nonNullable.AllowNull = false;

        // Act and assert
        Press(empty, Code.Delete).IsHandled.ShouldBeTrue();
        Press(empty, Code.Backspace).IsHandled.ShouldBeTrue();
        empty.Value.ShouldBeNull();
        Press(nonNullable, Code.Delete).IsHandled.ShouldBeTrue();
        nonNullable.Value.ShouldBe(_march15);
    }

    /// <summary>Verifies keys that edit the closed field - digits, Backspace, and the AM/PM letters
    /// - are inert while the Calendar popup is open: the value, the active segment, and the popup
    /// all stay as they were, and Space then accepts the browsed day as the keyboard table promises.</summary>
    [Fact]
    public async Task Popup_WhenEditingKeysArriveWhileOpen_AreInertAndSpaceAcceptsAsync()
    {
        // Arrange
        var input = Create(_march15);
        input.Use24HourFormat = false;
        input.HorizontalAlignment = HorizontalAlignment.Left;
        var root = new Overlay { Children = { input } };
        var changes = 0;
        input.ValueChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(root, new Size(36, 16), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Alt);
        input.IsOpen.ShouldBeTrue();
        changes.ShouldBe(0);

        // Act
        await surface.Keyboard.TypeAsync("7");
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.TypeAsync("a");
        await surface.Keyboard.TypeAsync("p");

        // Assert
        input.IsOpen.ShouldBeTrue();
        input.Value.ShouldBe(_march15);
        changes.ShouldBe(0);

        // Act - browse one day and accept with Space.
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(new DateTime(2026, 3, 16, 14, 30, 0));
        changes.ShouldBe(1);
        surface.ShouldHaveFocus(input);

        // Act - the first segment is active again after the accept: Up steps the month.
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new DateTime(2026, 4, 16, 14, 30, 0));
    }

    /// <summary>Verifies a hidden field routes nothing: keys change neither the value nor open the
    /// popup, a pointer press on the indicator neither focuses nor opens, and the field resumes
    /// normally once shown.</summary>
    [Fact]
    public async Task Visibility_WhenHidden_IgnoresKeysAndPointerUntilShownAsync()
    {
        // Arrange
        var input = Create(_march15);
        input.HorizontalAlignment = HorizontalAlignment.Left;
        var root = new Overlay { Children = { input } };
        var changes = 0;
        input.ValueChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);
        var indicator = new Point(input.Bounds.Width - 2, 1);
        await surface.UpdateAsync(() => input.Visibility = Visibility.Hidden, "hide the focused field");
        surface.ShouldHaveFocus(null);

        // Act
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Alt);
        await surface.Pointer.MoveToAsync(indicator);
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(_march15);
        changes.ShouldBe(0);
        input.IsFocused.ShouldBeFalse();
        surface.Cell(indicator).Text.ShouldBe(" ", "a hidden field paints nothing");

        // Act
        await surface.UpdateAsync(() => input.Visibility = Visibility.Visible, "show the field again");
        await surface.Pointer.MoveToAsync(indicator);
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        input.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(input);
    }

    #endregion

    #region Null value

    /// <summary>Verifies the null placeholder renders every segment, with a four-dash year, and Down
    /// seeds the dispatcher clock's current date and time.</summary>
    [Fact]
    public async Task Keyboard_WhenDownIsPressedOnNullValue_SeedsClockDateTimeAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        clock.Advance(new DateTimeOffset(2026, 6, 10, 12, 34, 0, TimeSpan.Zero) - DateTimeOffset.UnixEpoch);
        var input = Create(null);
        await using var surface = await ComponentSurface.MountAsync(input, new Size(28, 3), clock, TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        ReadRow(surface, 1, 17).ShouldBe("┃--/--/---- --:--");
        (surface.Cell(new Point(1, 1)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        input.Value.ShouldBe(clock.GetLocalNow().DateTime);
    }

    #endregion

    #region Formats and culture

    /// <summary>Verifies a designator-less lowercase hour token renders as 24-hour while the same
    /// letter inside quotes or behind a backslash stays literal text.</summary>
    [Theory]
    [InlineData("'h' h:mm")]
    [InlineData("\"h\" h:mm")]
    [InlineData(@"\h h:mm")]
    public void Format_WhenHourLetterIsQuotedOrEscaped_NormalizesOnlyTheToken(string format)
    {
        // Arrange
        using var control = Create(new DateTime(2026, 3, 15, 14, 5, 0));
        control.Format = format;

        // Act
        var row = RenderRow(control, new Size(20, 3), 1);
        _ = Press(control, Code.Up);

        // Assert
        row.ShouldContain("h 14:05");
        control.Value.ShouldBe(new DateTime(2026, 3, 15, 15, 5, 0));
    }

    /// <summary>Verifies a backslash inside a quoted literal escapes an embedded quote - a shape only
    /// DateTime's formatter accepts - so the apostrophe renders and the following run stays editable.</summary>
    [Fact]
    public void Format_WhenQuotedLiteralEscapesQuote_RendersApostropheAndKeepsDayEditable()
    {
        // Arrange
        using var control = Create(_march15);
        control.Format = @"'It\'s' dd HH:mm";

        // Act
        var row = RenderRow(control, new Size(24, 3), 1);
        _ = Press(control, Code.Up);

        // Assert
        row.ShouldContain("It's 15 14:30");
        control.Value.ShouldBe(new DateTime(2026, 3, 16, 14, 30, 0));
    }

    /// <summary>Verifies assigning a culture while a custom Format is set validates the pattern
    /// under that culture and localizes its unquoted separators.</summary>
    [Fact]
    public void Culture_WhenAssignedWithCustomFormat_RendersLocalizedSeparators()
    {
        // Arrange
        using var control = Create(new DateTime(2026, 3, 15, 14, 5, 0));
        control.Format = "HH:mm dd/MM/yyyy";

        // Act
        control.Culture = new CultureInfo("de-DE");

        // Assert
        RenderRow(control, new Size(24, 3), 1).ShouldContain("14:05 15.03.2026");
        control.OwnedCalendar.Culture.Name.ShouldBe("de-DE");
    }

    /// <summary>Verifies a Culture switch after a buffered first digit discards it, so the next
    /// digit starts a fresh entry on the reordered first segment.</summary>
    [Fact]
    public async Task Culture_WhenSwitchedMidEntry_DiscardsPartialDigitAsync()
    {
        // Arrange
        var input = Create(_march15);
        await using var surface = await ComponentSurface.MountAsync(input, new Size(28, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("1");
        input.Value.ShouldBe(new DateTime(2026, 1, 15, 14, 30, 0));

        // Act - de-DE orders Day first, so "2" becomes day 2 rather than month 12.
        await surface.UpdateAsync(() => input.Culture = new CultureInfo("de-DE"), "switch culture mid-entry");
        await surface.Keyboard.TypeAsync("2");

        // Assert
        input.Value.ShouldBe(new DateTime(2026, 1, 2, 14, 30, 0));
    }

    /// <summary>Verifies hiding seconds while the seconds segment is active clamps the active
    /// segment back to Minute.</summary>
    [Fact]
    public void ShowSeconds_WhenDisabledWhileSecondsAreActive_ClampsActiveSegmentToMinute()
    {
        // Arrange
        using var control = Create(new DateTime(2026, 3, 15, 14, 30, 45));
        control.ShowSeconds = true;
        _ = Press(control, Code.End);

        // Act
        control.ShowSeconds = false;
        _ = Press(control, Code.Up);

        // Assert
        control.Value.ShouldBe(new DateTime(2026, 3, 15, 14, 31, 45));
    }

    #endregion

    #region Focus and programmatic value

    /// <summary>Verifies losing focus discards a buffered digit and removes the highlight, while
    /// regaining focus keeps the previously active segment.</summary>
    [Fact]
    public async Task Focus_WhenLostAndRegained_DiscardsPartialDigitAndKeepsSegmentAsync()
    {
        // Arrange
        var first = Create(_march15);
        var second = new TimeInput { Value = new TimeOnly(8, 0) };
        var root = new Stack { Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(40, 6), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.TypeAsync("1");
        first.Value.ShouldBe(new DateTime(2026, 3, 15, 14, 1, 0));
        (surface.Cell(new Point(15, 1)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(second);
        var highlightedWhileUnfocused = (surface.Cell(new Point(15, 1)).Style.Attributes & TerminalAttributes.Reverse) != 0;
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        await surface.Keyboard.TypeAsync("5");

        // Assert
        highlightedWhileUnfocused.ShouldBeFalse();
        surface.ShouldHaveFocus(first);
        first.Value.ShouldBe(new DateTime(2026, 3, 15, 14, 5, 0));
    }

    /// <summary>Verifies a programmatic Value assignment while focused on Hour keeps Hour active.</summary>
    [Fact]
    public async Task Value_WhenAssignedWhileFocusedOnHour_KeepsHourSegmentActiveAsync()
    {
        // Arrange
        var input = Create(_march15);
        await using var surface = await ComponentSurface.MountAsync(input, new Size(28, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);

        // Act
        await surface.UpdateAsync(() => input.Value = new DateTime(2030, 7, 20, 8, 0, 0), "assign while focused");
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new DateTime(2030, 7, 20, 9, 0, 0));
    }

    /// <summary>Verifies a field narrower than its text clips the segments while keeping the
    /// border and the drop-down indicator in place.</summary>
    [Fact]
    public async Task Layout_WhenWidthIsTiny_ClipsSegmentsAndKeepsIndicatorAsync()
    {
        // Arrange
        var input = Create(_march15);
        input.HorizontalAlignment = HorizontalAlignment.Left;
        input.Width = Length.Cells(8);

        // Act
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);

        // Assert
        input.Bounds.Width.ShouldBe(8);
        ReadRow(surface, 1, 8).ShouldBe("┃03/1 ▼┃");
    }

    /// <summary>Verifies a secondary-button click, a wheel, and a plain move over the field leave
    /// the active segment, the value, and the popup alone.</summary>
    [Fact]
    public async Task Pointer_WhenRightClickWheelOrMoveHitsField_ChangesNothingAsync()
    {
        // Arrange
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Pointer.RightClickAsync(input, new Point(12, 1));
        await surface.Pointer.WheelAsync(input, new Point(12, 1), wheelY: 1);
        await surface.Pointer.MoveToAsync(input, new Point(12, 1));
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(new DateTime(2026, 4, 15, 14, 30, 0));
    }

    #endregion

    #region ValueChanged

    /// <summary>Verifies a ValueChanged subscriber that assigns a newer value sees its own nested
    /// transition published in order, with no stale replay of the interrupted one.</summary>
    [Fact]
    public void ValueChanged_WhenSubscriberAssignsNewerValue_PublishesNestedTransitionInOrder()
    {
        // Arrange
        var first = new DateTime(2026, 4, 1, 0, 0, 0);
        var second = new DateTime(2026, 5, 1, 0, 0, 0);
        using var control = Create(_march15);
        var observed = new List<(DateTime? Previous, DateTime? Current)>();
        control.ValueChanged += (_, eventArgs) =>
        {
            observed.Add((eventArgs.PreviousValue, eventArgs.Value));

            if (eventArgs.Value == first)
            {
                control.Value = second;
            }
        };

        // Act
        control.Value = first;

        // Assert
        control.Value.ShouldBe(second);
        observed.ShouldBe([(_march15, first), (first, second)]);
    }

    /// <summary>Verifies a throwing ValueChanged subscriber does not roll back the committed value.</summary>
    [Fact]
    public void ValueChanged_WhenSubscriberThrows_CommitsValueBeforePropagating()
    {
        // Arrange
        using var control = Create(_march15);
        control.ValueChanged += (_, _) => throw new InvalidOperationException("observer failed");

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => Press(control, Code.Up));

        // Assert
        exception.Message.ShouldBe("observer failed");
        control.Value.ShouldBe(new DateTime(2026, 4, 15, 14, 30, 0));
    }

    #endregion

    #region Popup session

    /// <summary>Verifies a popup click on the Minimum boundary day, whose preserved time-of-day
    /// would fall before Minimum, accepts the next day with the time intact instead of clamping.</summary>
    [Fact]
    public async Task Popup_WhenBoundaryDayIsClickedBelowMinimumTime_AdvancesDateAndPreservesTimeAsync()
    {
        // Arrange - March 10, 2026 is a Tuesday on the second grid row of a Sunday-first March.
        var input = Create(new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc));
        input.Minimum = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        var changes = 0;
        input.ValueChanged += (_, _) => changes++;
        await surface.UpdateAsync(() => input.IsOpen = true, "open the bounded calendar");

        // Act
        await surface.Pointer.ClickAsync(calendar, new Point(2 + (2 * 4), 3 + 1));

        // Assert
        input.IsOpen.ShouldBeFalse();
        var value = input.Value.ShouldNotBeNull();
        value.ShouldBe(new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc));
        value.Kind.ShouldBe(DateTimeKind.Utc);
        changes.ShouldBe(1);
    }

    /// <summary>Verifies Home inside the open popup clamps to the Minimum day before the selectable
    /// walk, and accepting that boundary day still preserves the time by nudging the date.</summary>
    [Fact]
    public async Task Popup_WhenHomeLandsBeforeMinimum_ClampsThenAcceptsWithPreservedTimeAsync()
    {
        // Arrange - March 11, 2026 is a Wednesday; the active Friday's week starts on Sunday March 8.
        var input = Create(new DateTime(2026, 3, 13, 9, 0, 0));
        input.Minimum = new DateTime(2026, 3, 11, 12, 0, 0);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        await surface.UpdateAsync(() => input.IsOpen = true, "open the bounded calendar");

        // Act
        await surface.Keyboard.PressAsync(Code.Home);
        var activeAfterHome = calendar.ActiveDate;
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        activeAfterHome.ShouldBe(new DateOnly(2026, 3, 11));
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(new DateTime(2026, 3, 12, 9, 0, 0));
    }

    /// <summary>Verifies a plain Tab while the popup is open cancels the session and continues
    /// traversal to the next control without changing the value.</summary>
    [Fact]
    public async Task Popup_WhenTabIsPressedWhileOpen_CancelsAndMovesFocusOnAsync()
    {
        // Arrange
        var input = Create(_march15);
        var next = new TimeInput { Value = new TimeOnly(10, 30) };
        var root = new Stack { Children = { input, next } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(40, 20), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Alt);
        surface.ShouldHaveFocus(calendar);
        await surface.Keyboard.PressAsync(Code.PageDown);
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 4, 15));

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        input.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(next);
        input.Value.ShouldBe(_march15);
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 3, 15));
    }

    /// <summary>Verifies the header arrows and the wheel browse the open Calendar without closing
    /// it, and restyling the Calendar while open repaints the header.</summary>
    [Fact]
    public async Task Popup_WhenBrowsedAndRestyledWhileOpen_StaysOpenAndRepaintsAsync()
    {
        // Arrange
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        await surface.UpdateAsync(() => input.IsOpen = true, "open the calendar");
        var arrow = new Point(calendar.Bounds.X + 2, calendar.Bounds.Y + 1);
        var before = surface.Cell(arrow).Style.Foreground;

        // Act and assert
        await surface.Pointer.ClickAsync(calendar, new Point(2, 1));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 2, 1));
        await surface.Pointer.WheelAsync(calendar, new Point(15, 4), wheelY: -1);
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 3, 1));
        input.IsOpen.ShouldBeTrue();
        input.Value.ShouldBe(_march15);

        var style = CalendarStyle.Default with { NavigationColor = Color.Rgb(200, 30, 30) };
        await surface.UpdateAsync(() => input.CalendarStyle = style, "restyle the open calendar");
        input.ActualCalendarStyle.ShouldBe(style);
        surface.Cell(arrow).Style.Foreground.ShouldBe(TerminalPalette.Project(Color.Rgb(200, 30, 30), ColorDepth.Basic16));
        surface.Cell(arrow).Style.Foreground.ShouldNotBe(before);
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies disposing the field while its popup is open tears the popup down, leaves
    /// the surface responsive, and tolerates a repeated Dispose.</summary>
    [Fact]
    public async Task Popup_WhenFieldIsDisposedWhileOpen_ClosesPopupWithoutFaultAsync()
    {
        // Arrange
        var input = Create(_march15);
        var sibling = new TimeInput { Value = new TimeOnly(10, 30) };
        var root = new Stack { Children = { input, sibling } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(40, 20), TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open the calendar");
        var closed = 0;
        input.DropDownClosed += (_, _) => closed++;

        // Act
        await surface.UpdateAsync(input.Dispose, "dispose the field while open");
        await surface.UpdateAsync(input.Dispose, "dispose the field again");
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert - disposal detaches every subscriber first, so no DropDownClosed fires.
        input.IsDisposed.ShouldBeTrue();
        popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(0);
        surface.ShouldHaveFocus(sibling);
        sibling.Value.ShouldBe(new TimeOnly(11, 30));
    }

    #endregion

    #region Helpers

    private static DateTimeInput Create(DateTime? value) => new() { Value = value };

    private static void MoveTo(ControlBase control, int segment)
    {
        _ = Press(control, Code.Home);

        for (var index = 0; index < segment; index++)
        {
            _ = Press(control, Code.Right);
        }
    }

    private static KeyEventArgs Press(ControlBase control, Code code, Modifiers modifiers = Modifiers.None)
    {
        var eventArgs = new KeyEventArgs(new Stroke(code, character: null, nativeCode: 0, modifiers, KeyAction.Press));
        _ = Router.Route(control, Events.Key, eventArgs);
        return eventArgs;
    }

    private static KeyEventArgs Type(ControlBase control, char character)
    {
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(character),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));
        _ = Router.Route(control, Events.Key, eventArgs);
        return eventArgs;
    }

    private static string RenderRow(ControlBase control, Size size, int y)
    {
        new LayoutEngine().Layout(control, size);
        using Frame frame = new(size);
        control.Render(frame.Canvas);
        var result = new StringBuilder(size.Width);

        for (var x = 0; x < size.Width; x++)
        {
            var text = FrameOracle.Get(frame, new Point(x, y));
            _ = result.Append(text.Length == 0 ? " " : text);
        }

        return result.ToString();
    }

    private static string ReadRow(ComponentSurface surface, int y, int width) =>
        string.Concat(Enumerable.Range(0, width).Select(x => surface.Cell(new Point(x, y)).Text));

    #endregion
}
