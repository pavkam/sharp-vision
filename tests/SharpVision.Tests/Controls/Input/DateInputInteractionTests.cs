// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using System.Text.Json;

/// <summary>Proves DateInput's complete segment-editing, formatting, focus, pointer, and Calendar
/// popup contract through mounted surfaces and routed detached input: digit overtyping and
/// auto-advance, increment carry and clamping, traversal at both ends, the null placeholder,
/// every standard and escaped format shape, culture switches mid-entry, and the popup session's
/// keys, pointer gestures, restyling, resizing, and disposal while open.</summary>
public sealed class DateInputInteractionTests
{
    private static readonly DateOnly _march15 = new(2026, 3, 15);

    #region Digit entry

    /// <summary>Verifies month digit entry buffers a first digit at or below the auto-commit
    /// threshold, clamps an overtyped pair to December, commits a first digit above the threshold
    /// at once, and advances to the Day segment after either commit.</summary>
    [Theory]
    [InlineData("09", 9)]
    [InlineData("13", 12)]
    [InlineData("2", 2)]
    [InlineData("12", 12)]
    public async Task Keyboard_WhenMonthDigitsAreTyped_ClampsOvertypeAndAdvancesToDayAsync(string digits, int expectedMonth)
    {
        // Arrange
        var input = Create(_march15);
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act - the trailing Up proves the active segment moved on to Day.
        await surface.Keyboard.TypeAsync(digits);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new DateOnly(2026, expectedMonth, 16));
    }

    /// <summary>Verifies a two-digit day beyond the month's length clamps to the last day, and a
    /// first day digit above the threshold commits immediately, both advancing to Year.</summary>
    [Theory]
    [InlineData("31", 2, 28)]
    [InlineData("4", 2, 4)]
    [InlineData("00", 2, 1)]
    public async Task Keyboard_WhenDayDigitsAreTyped_ClampsToMonthLengthAndAdvancesToYearAsync(string digits, int month, int expectedDay)
    {
        // Arrange
        var input = Create(new DateOnly(2026, month, 10));
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);

        // Act - the trailing Up proves the active segment moved on to Year.
        await surface.Keyboard.TypeAsync(digits);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new DateOnly(2027, month, expectedDay));
    }

    /// <summary>Verifies a four-digit year of zeros clamps to year one, and the Year segment - the
    /// last editable segment - keeps accepting a fresh four-digit entry instead of advancing.</summary>
    [Fact]
    public async Task Keyboard_WhenYearIsTypedAsZerosThenRetyped_ClampsToYearOneAndStaysOnYearAsync()
    {
        // Arrange
        var input = Create(_march15);
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);

        // Act
        await surface.Keyboard.TypeAsync("0000");
        var afterZeros = input.Value;
        await surface.Keyboard.TypeAsync("2031");

        // Assert
        afterZeros.ShouldBe(new DateOnly(1, 3, 15));
        input.Value.ShouldBe(new DateOnly(2031, 3, 15));
        surface.Cell(new Point(7, 1)).Text.ShouldBe("2");
        surface.Cell(new Point(10, 1)).Text.ShouldBe("1");
    }

    #endregion

    #region Increment carry and clamping

    /// <summary>Verifies stepping the Day segment past the end of the month carries into the next
    /// month and stepping back returns across the same boundary.</summary>
    [Fact]
    public async Task Keyboard_WhenDayStepsPastMonthEnd_CarriesAcrossMonthBoundaryAsync()
    {
        // Arrange
        var input = Create(new DateOnly(2026, 3, 31));
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(new DateOnly(2026, 4, 1));
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        input.Value.ShouldBe(new DateOnly(2026, 3, 30));
    }

    /// <summary>Verifies stepping Month past December carries into the next year and a month step
    /// onto a shorter month clamps the day instead of throwing.</summary>
    [Fact]
    public async Task Keyboard_WhenMonthStepsAcrossYearOrOntoShorterMonth_CarriesAndClampsDayAsync()
    {
        // Arrange
        var input = Create(new DateOnly(2026, 12, 31));
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert - December 31 + 1 month = January 31 of the next year.
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(new DateOnly(2027, 1, 31));

        // January 31 + 1 month clamps to February 28; back down from March 31 also clamps.
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(new DateOnly(2027, 2, 28));
        await surface.UpdateAsync(() => input.Value = new DateOnly(2027, 3, 31), "reset to March 31");
        await surface.Keyboard.PressAsync(Code.Down);
        input.Value.ShouldBe(new DateOnly(2027, 2, 28));
    }

    /// <summary>Verifies a step that would leave the inclusive bounds is clamped away to no change
    /// and still consumed, so a bounded field never leaks the arrow to an ancestor.</summary>
    [Theory]
    [InlineData(Code.Up)]
    [InlineData(Code.Down)]
    public void Keyboard_WhenStepWouldLeaveBounds_LeavesValueAndConsumesKey(Code code)
    {
        // Arrange
        using var control = Create(_march15);
        control.Minimum = _march15;
        control.Maximum = _march15;
        _ = Press(control, Code.Right);

        // Act
        var key = Press(control, code);

        // Assert
        control.Value.ShouldBe(_march15);
        key.IsHandled.ShouldBeTrue();
    }

    /// <summary>Verifies stepping Day or Month at <see cref="DateOnly.MaxValue"/> swallows the
    /// out-of-range arithmetic instead of throwing, leaving the value untouched and the key consumed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Keyboard_WhenStepExceedsDateOnlyRange_LeavesValueAndConsumesKey(int segment)
    {
        // Arrange
        using var control = Create(DateOnly.MaxValue);

        for (var index = 0; index < segment; index++)
        {
            _ = Press(control, Code.Right);
        }

        // Act
        var key = Press(control, Code.Up);

        // Assert
        control.Value.ShouldBe(DateOnly.MaxValue);
        key.IsHandled.ShouldBeTrue();
    }

    #endregion

    #region Traversal and command keys

    /// <summary>Verifies Left at the first segment and Right at the last segment stay put without
    /// wrapping, yet are still consumed by the field.</summary>
    [Fact]
    public void Keyboard_WhenTraversalHitsEitherEnd_StaysWithoutWrappingAndConsumesKey()
    {
        // Arrange
        using var control = Create(_march15);

        // Act - Left at Month, then Up proves Month is still active.
        var left = Press(control, Code.Left);
        _ = Press(control, Code.Up);

        // Assert
        left.IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new DateOnly(2026, 4, 15));

        // Act - Right past Year, then Up proves Year is still active.
        _ = Press(control, Code.End);
        var right = Press(control, Code.Right);
        _ = Press(control, Code.Up);

        // Assert
        right.IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new DateOnly(2027, 4, 15));
    }

    /// <summary>Verifies keys outside the segment command set stay unhandled while the popup is
    /// closed, so an ancestor can respond, and never change the value.</summary>
    [Theory]
    [InlineData(Code.PageUp)]
    [InlineData(Code.PageDown)]
    [InlineData(Code.Escape)]
    [InlineData(Code.Insert)]
    public void Keyboard_WhenNonCommandKeyIsPressedWhileClosed_LeavesKeyUnhandled(Code code)
    {
        // Arrange
        using var control = Create(_march15);

        // Act
        var key = Press(control, code);

        // Assert
        key.IsHandled.ShouldBeFalse();
        control.Value.ShouldBe(_march15);
        control.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies Enter and Space on the closed field are press-activation keys that do not
    /// open the popup - only Alt+Down, F4, and the pointer do - and leave the value alone.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterOrSpaceIsPressedWhileClosed_DoesNotOpenPopupAsync()
    {
        // Arrange
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(30, 15), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);
        var afterEnter = input.IsOpen;
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        afterEnter.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(_march15);
        surface.ShouldHaveFocus(input);
    }

    /// <summary>Verifies Delete and Backspace on an already-empty field, and Delete under
    /// AllowNull = false, are consumed without an observable change.</summary>
    [Fact]
    public void Keyboard_WhenClearingCannotChangeValue_ConsumesKeyWithoutChange()
    {
        // Arrange
        using var empty = Create(null);
        using var nonNullable = Create(_march15);
        nonNullable.AllowNull = false;

        // Act
        var deleteOnEmpty = Press(empty, Code.Delete);
        var backspaceOnEmpty = Press(empty, Code.Backspace);
        var deleteOnNonNullable = Press(nonNullable, Code.Delete);

        // Assert
        deleteOnEmpty.IsHandled.ShouldBeTrue();
        backspaceOnEmpty.IsHandled.ShouldBeTrue();
        empty.Value.ShouldBeNull();
        deleteOnNonNullable.IsHandled.ShouldBeTrue();
        nonNullable.Value.ShouldBe(_march15);
    }

    /// <summary>Verifies Delete remains a live clearing command under a format with no editable
    /// segments, where segment-editing keys are otherwise left unhandled: it clears the value once,
    /// then - with nothing left to clear and no segment to recognize it - bubbles like the rest.</summary>
    [Fact]
    public void Keyboard_WhenFormatHasNoEditableSegments_DeleteStillClearsValue()
    {
        // Arrange
        using var control = Create(_march15, "'choose date'");

        // Act
        var backspace = Press(control, Code.Backspace);
        var firstDelete = Press(control, Code.Delete);
        var secondDelete = Press(control, Code.Delete);

        // Assert
        backspace.IsHandled.ShouldBeFalse();
        firstDelete.IsHandled.ShouldBeTrue();
        control.Value.ShouldBeNull();
        secondDelete.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies the popup command stays available under a literal-only format.</summary>
    [Fact]
    public void Keyboard_WhenFormatHasNoEditableSegments_AltDownStillOpensPopup()
    {
        // Arrange
        using var control = Create(_march15, "'choose date'");

        // Act
        var key = Press(control, Code.Down, Modifiers.Alt);

        // Assert
        key.IsHandled.ShouldBeTrue();
        control.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies F4 opens the popup only as a plain chord: a Shift-modified F4 stays
    /// unhandled and closed for a host shortcut.</summary>
    [Fact]
    public void Keyboard_WhenF4IsPressed_OpensOnlyWithoutCommandModifiers()
    {
        // Arrange
        using var control = Create(_march15);

        // Act
        var shifted = Press(control, Code.F4, Modifiers.Shift);
        var closedAfterShift = control.IsOpen;
        var plain = Press(control, Code.F4);

        // Assert
        shifted.IsHandled.ShouldBeFalse();
        closedAfterShift.ShouldBeFalse();
        plain.IsHandled.ShouldBeTrue();
        control.IsOpen.ShouldBeTrue();
    }

    #endregion

    #region Null value

    /// <summary>Verifies a null value renders the dim placeholder pattern with the active segment
    /// reversed, and every other placeholder segment dimmed but not reversed.</summary>
    [Fact]
    public async Task Render_WhenValueIsNullAndFocused_DrawsDimPlaceholderWithReversedActiveSegmentAsync()
    {
        // Arrange
        var input = Create(null);
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        var row = string.Concat(Enumerable.Range(1, 10).Select(x => surface.Cell(new Point(x, 1)).Text));
        row.ShouldBe("--/--/----");
        var active = surface.Cell(new Point(1, 1)).Style.Attributes;
        var inactive = surface.Cell(new Point(4, 1)).Style.Attributes;
        (active & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);
        (active & TerminalAttributes.Dim).ShouldBe(TerminalAttributes.None);
        (inactive & TerminalAttributes.Dim).ShouldBe(TerminalAttributes.Dim);
        (inactive & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies Down on an empty field seeds the dispatcher clock's current date rather
    /// than decrementing from it or refusing to act.</summary>
    [Fact]
    public async Task Keyboard_WhenDownIsPressedOnNullValue_SeedsTodayFromDispatcherClockAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        clock.Advance(new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero) - DateTimeOffset.UnixEpoch);
        var input = Create(null);
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), clock, TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        input.Value.ShouldBeNull();

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        input.Value.ShouldBe(DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
    }

    /// <summary>Verifies cancelling a popup that was opened over an empty value leaves the Calendar
    /// without a selection instead of resurrecting the browsed date as a selection.</summary>
    [Fact]
    public async Task Popup_WhenOpenedOverNullValueAndCancelled_RestoresEmptySelectionAsync()
    {
        // Arrange
        var input = Create(null);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(30, 15), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        await surface.UpdateAsync(() => input.IsOpen = true, "open over an empty value");
        var openingActive = calendar.ActiveDate;
        await surface.Keyboard.PressAsync(Code.Right);
        calendar.ActiveDate.ShouldBe(openingActive.AddDays(1));

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBeNull();
        calendar.Selection.ShouldBeNull();
        calendar.ActiveDate.ShouldBe(openingActive);
    }

    #endregion

    #region Formats

    /// <summary>Verifies every single-letter standard specifier DateOnly supports resolves to a
    /// concrete pattern whose name runs are inert and whose first numeric run is the first editable
    /// segment.</summary>
    [Theory]
    [InlineData("o", "2026-03-15", 2027, 3, 15)]
    [InlineData("O", "2026-03-15", 2027, 3, 15)]
    [InlineData("r", "Sun, 15 Mar 2026", 2026, 3, 16)]
    [InlineData("R", "Sun, 15 Mar 2026", 2026, 3, 16)]
    [InlineData("D", "Sunday, 15 March 2026", 2026, 3, 16)]
    [InlineData("M", "March 15", 2026, 3, 16)]
    [InlineData("Y", "2026 March", 2027, 3, 15)]
    public void Format_WhenStandardSpecifierIsUsed_RendersPatternAndEditsFirstNumericRun(
        string format,
        string expectedText,
        int yearAfterUp,
        int monthAfterUp,
        int dayAfterUp)
    {
        // Arrange
        using var control = Create(_march15, format);

        // Act
        var row = RenderRow(control, new Size(30, 3), 1);
        _ = Press(control, Code.Up);

        // Assert
        row.ShouldContain(expectedText);
        control.Value.ShouldBe(new DateOnly(yearAfterUp, monthAfterUp, dayAfterUp));
    }

    /// <summary>Verifies percent-prefixed tokens render unpadded, and a value transition that widens
    /// the formatted text remeasures the mounted field so the wider text is fully visible.</summary>
    [Fact]
    public async Task Format_WhenPercentPrefixedTokensWiden_RemeasuresMountedFieldAsync()
    {
        // Arrange
        var input = Create(new DateOnly(2026, 9, 1), "%M/%d/yyyy");
        input.HorizontalAlignment = HorizontalAlignment.Left;
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        var narrowWidth = input.Bounds.Width;
        ReadRow(surface, 1, narrowWidth).ShouldBe("┃9/1/2026 ▼┃");

        // Act
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new DateOnly(2026, 10, 1));
        input.Bounds.Width.ShouldBe(narrowWidth + 1);
        ReadRow(surface, 1, input.Bounds.Width).ShouldBe("┃10/1/2026 ▼┃");
    }

    /// <summary>Verifies a backslash-escaped separator renders literally while an unquoted
    /// separator resolves to the culture's own date separator.</summary>
    [Fact]
    public void Format_WhenSeparatorIsEscaped_RendersLiteralInsteadOfCultureSeparator()
    {
        // Arrange
        using var escaped = Create(_march15, @"dd\/MM\/yyyy");
        escaped.Culture = new CultureInfo("de-DE");
        using var unquoted = Create(_march15, "dd/MM/yyyy");
        unquoted.Culture = new CultureInfo("de-DE");

        // Act and assert
        RenderRow(escaped, new Size(20, 3), 1).ShouldContain("15/03/2026");
        RenderRow(unquoted, new Size(20, 3), 1).ShouldContain("15.03.2026");
    }

    /// <summary>Verifies formats DateOnly cannot render - time tokens, unsupported single
    /// specifiers, a dangling percent, and a backslash-escaped quote inside a quoted literal (which
    /// only DateTime's formatter understands) - are rejected without replacing the current format.</summary>
    [Theory]
    [InlineData("HH:mm")]
    [InlineData("T")]
    [InlineData("t")]
    [InlineData("dd%")]
    [InlineData(@"'It\'s' dd")]
    public void Format_WhenPatternCannotBeRendered_ThrowsAndPreservesFormat(string format)
    {
        // Arrange
        using var control = Create(_march15);

        // Act
        var exception = Should.Throw<ArgumentException>(() => control.Format = format);

        // Assert
        exception.ParamName.ShouldBe("value");
        control.Format.ShouldBe("d");
        RenderRow(control, new Size(20, 3), 1).ShouldContain("03/15/2026");
    }

    #endregion

    #region Culture and programmatic value

    /// <summary>Verifies a Culture switch after a buffered first digit discards that digit, so the
    /// next digit starts a fresh entry on the reordered first segment, and the same culture reaches
    /// the owned Calendar's weekday ordering.</summary>
    [Fact]
    public async Task Culture_WhenSwitchedMidEntry_DiscardsPartialDigitAndReordersCalendarAsync()
    {
        // Arrange
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(30, 15), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("1");
        input.Value.ShouldBe(new DateOnly(2026, 1, 15));

        // Act - de-DE orders Day first, so "2" must become day 2 rather than month 12.
        await surface.UpdateAsync(() => input.Culture = new CultureInfo("de-DE"), "switch culture mid-entry");
        await surface.Keyboard.TypeAsync("2");
        await surface.UpdateAsync(() => input.IsOpen = true, "open the German calendar");

        // Assert
        input.Value.ShouldBe(new DateOnly(2026, 1, 2));
        var calendar = input.OwnedCalendar;
        calendar.Culture.Name.ShouldBe("de-DE");
        calendar.FirstDayOfWeek.ShouldBe(DayOfWeek.Monday);
        surface.Cell(new Point(calendar.Bounds.X + 3, calendar.Bounds.Y + 2)).Text.ShouldBe("M");
    }

    /// <summary>Verifies a programmatic Value assignment while the field is focused on a later
    /// segment keeps that segment active, so the next step edits the new value's same component.</summary>
    [Fact]
    public async Task Value_WhenAssignedWhileFocusedOnDay_KeepsDaySegmentActiveAsync()
    {
        // Arrange
        var input = Create(_march15);
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);

        // Act
        await surface.UpdateAsync(() => input.Value = new DateOnly(2030, 7, 20), "assign while focused");
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new DateOnly(2030, 7, 21));
        (surface.Cell(new Point(4, 1)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);
        (surface.Cell(new Point(1, 1)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies a field too narrow for its text clips the segments while keeping the
    /// border and the drop-down indicator in place.</summary>
    [Fact]
    public async Task Layout_WhenWidthIsTiny_ClipsSegmentsAndKeepsIndicatorAsync()
    {
        // Arrange
        var input = Create(_march15);
        input.HorizontalAlignment = HorizontalAlignment.Left;
        input.Width = Length.Cells(6);

        // Act
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);

        // Assert
        input.Bounds.Width.ShouldBe(6);
        ReadRow(surface, 1, 6).ShouldBe("┃03 ▼┃");
        surface.Cell(new Point(6, 1)).Text.ShouldBe(" ");
    }

    #endregion

    #region Pointer

    /// <summary>Verifies pressing a literal separator column activates the editable segment that
    /// follows it, matching the segment the hit test resolves for a literal.</summary>
    [Fact]
    public async Task Pointer_WhenSeparatorIsClicked_ActivatesFollowingSegmentAsync()
    {
        // Arrange
        var input = Create(_march15);
        await using var surface = await ComponentSurface.MountAsync(input, new Size(20, 3), TestContext.Current.CancellationToken);

        // Act - column 3 is the "/" between Month and Day.
        await surface.Pointer.ClickAsync(input, new Point(3, 1));
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        surface.ShouldHaveFocus(input);
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(new DateOnly(2026, 3, 16));
    }

    /// <summary>Verifies a secondary-button click neither activates the segment under it nor
    /// toggles the popup, and a wheel or a plain move over the field changes nothing.</summary>
    [Fact]
    public async Task Pointer_WhenRightClickWheelOrMoveHitsField_ChangesNothingAsync()
    {
        // Arrange
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(30, 15), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Pointer.RightClickAsync(input, new Point(5, 1));
        await surface.Pointer.WheelAsync(input, new Point(5, 1), wheelY: 1);
        await surface.Pointer.MoveToAsync(input, new Point(8, 1));
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert - Month is still the active segment and the popup never opened.
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(new DateOnly(2026, 4, 15));
    }

    /// <summary>Verifies a click on the drop-down indicator opens the popup, and a click back on
    /// the anchoring field while it is open is neither a light dismissal nor a second toggle: the
    /// anchor sits inside the popup's dismissal surface, so the session simply stays open.</summary>
    [Fact]
    public async Task Pointer_WhenIndicatorIsClicked_OpensAndAnchorClickKeepsSessionOpenAsync()
    {
        // Arrange
        var input = Create(_march15);
        input.HorizontalAlignment = HorizontalAlignment.Left;
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(30, 15), TestContext.Current.CancellationToken);
        var opened = 0;
        var closed = 0;
        input.DropDownOpened += (_, _) => opened++;
        input.DropDownClosed += (_, _) => closed++;

        // Act - the indicator sits one cell inside the right border.
        await surface.Pointer.ClickAsync(input, new Point(input.Bounds.Width - 2, 1));
        var openAfterIndicator = input.IsOpen;
        await surface.Pointer.ClickAsync(input, new Point(2, 1));

        // Assert
        openAfterIndicator.ShouldBeTrue();
        input.IsOpen.ShouldBeTrue();
        opened.ShouldBe(1);
        closed.ShouldBe(0);
        input.Value.ShouldBe(_march15);
    }

    /// <summary>Verifies a disabled field ignores both the indicator click and Alt+Down, staying
    /// closed and unfocused.</summary>
    [Fact]
    public async Task Pointer_WhenDisabledFieldIsClicked_DoesNotOpenOrFocusAsync()
    {
        // Arrange
        var input = Create(_march15);
        input.HorizontalAlignment = HorizontalAlignment.Left;
        input.IsEnabled = false;
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(30, 15), TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(input, new Point(input.Bounds.Width - 2, 1));
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Alt);

        // Assert
        input.IsOpen.ShouldBeFalse();
        input.IsFocused.ShouldBeFalse();
        input.Value.ShouldBe(_march15);
    }

    #endregion

    #region Focus

    /// <summary>Verifies losing focus removes the reversed segment highlight, and regaining focus
    /// returns the field to its first segment regardless of where it was before.</summary>
    [Fact]
    public async Task Focus_WhenLostAndRegained_ClearsHighlightAndReturnsToFirstSegmentAsync()
    {
        // Arrange
        var first = Create(_march15);
        var second = new TimeInput { Value = new TimeOnly(10, 30) };
        var root = new Stack { Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(40, 6), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);
        (surface.Cell(new Point(7, 1)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(second);
        var highlightedWhileUnfocused = (surface.Cell(new Point(7, 1)).Style.Attributes & TerminalAttributes.Reverse) != 0;
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        highlightedWhileUnfocused.ShouldBeFalse();
        surface.ShouldHaveFocus(first);
        first.Value.ShouldBe(new DateOnly(2026, 4, 15));
        second.Value.ShouldBe(new TimeOnly(10, 30));
    }

    #endregion

    #region ValueChanged

    /// <summary>Verifies a ValueChanged subscriber that assigns a newer value sees its own nested
    /// transition published in order, with no stale replay of the interrupted one.</summary>
    [Fact]
    public void ValueChanged_WhenSubscriberAssignsNewerValue_PublishesNestedTransitionInOrder()
    {
        // Arrange
        using var control = Create(_march15);
        var first = new DateOnly(2026, 4, 1);
        var second = new DateOnly(2026, 5, 1);
        var observed = new List<(DateOnly? Previous, DateOnly? Current)>();
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

    /// <summary>Verifies a throwing ValueChanged subscriber does not roll back the committed value:
    /// the exception surfaces from the setter while Value already reports the new date.</summary>
    [Fact]
    public void ValueChanged_WhenSubscriberThrows_CommitsValueBeforePropagating()
    {
        // Arrange
        using var control = Create(_march15);
        control.ValueChanged += (_, _) => throw new InvalidOperationException("observer failed");

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => control.Value = new DateOnly(2026, 4, 1));

        // Assert
        exception.Message.ShouldBe("observer failed");
        control.Value.ShouldBe(new DateOnly(2026, 4, 1));
    }

    #endregion

    #region Popup session

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
        await surface.Keyboard.PressAsync(Code.Right);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        input.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(next);
        input.Value.ShouldBe(_march15);
        calendar.ActiveDate.ShouldBe(_march15);
    }

    /// <summary>Verifies Home inside the open popup clamps to the Minimum date when the week start
    /// lies before it, and Enter then accepts that clamped date with exactly one ValueChanged.</summary>
    [Fact]
    public async Task Popup_WhenHomeLandsBeforeMinimum_ClampsIntoRangeBeforeAcceptingAsync()
    {
        // Arrange - March 11, 2026 is a Wednesday; the active Friday's week starts on Sunday March 8.
        var minimum = new DateOnly(2026, 3, 11);
        var input = Create(new DateOnly(2026, 3, 13));
        input.Minimum = minimum;
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(30, 15), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        var changes = 0;
        input.ValueChanged += (_, _) => changes++;
        await surface.UpdateAsync(() => input.IsOpen = true, "open the bounded calendar");

        // Act
        await surface.Keyboard.PressAsync(Code.Home);
        var activeAfterHome = calendar.ActiveDate;
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        activeAfterHome.ShouldBe(minimum);
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(minimum);
        changes.ShouldBe(1);
    }

    /// <summary>Verifies Page Up/Page Down, End, and Up/Down navigate the open Calendar by month,
    /// week edge, and week, each remaining provisional until accepted.</summary>
    [Fact]
    public async Task Popup_WhenPageWeekAndEdgeKeysArePressed_MoveActiveDateProvisionallyAsync()
    {
        // Arrange
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(30, 15), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        await surface.UpdateAsync(() => input.IsOpen = true, "open the calendar");

        // Act and assert - March 15, 2026 is a Sunday.
        await surface.Keyboard.PressAsync(Code.PageDown);
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 4, 15));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 4, 1));
        await surface.Keyboard.PressAsync(Code.PageUp);
        calendar.ActiveDate.ShouldBe(_march15);
        await surface.Keyboard.PressAsync(Code.End);
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 3, 21));
        await surface.Keyboard.PressAsync(Code.Down);
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 3, 28));
        await surface.Keyboard.PressAsync(Code.Up);
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 3, 21));
        input.Value.ShouldBe(_march15);
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies the header arrows and the wheel browse the open Calendar's displayed month
    /// without closing the popup or touching the value, and cancellation returns to the opening month.</summary>
    [Fact]
    public async Task Popup_WhenHeaderArrowOrWheelBrowses_ChangesDisplayedMonthWithoutClosingAsync()
    {
        // Arrange
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        await surface.UpdateAsync(() => input.IsOpen = true, "open the calendar");

        // Act and assert - previous-month arrow, then wheel forward twice.
        await surface.Pointer.ClickAsync(calendar, new Point(2, 1));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 2, 1));
        input.IsOpen.ShouldBeTrue();
        await surface.Pointer.WheelAsync(calendar, new Point(15, 4), wheelY: -1);
        await surface.Pointer.WheelAsync(calendar, new Point(15, 4), wheelY: -1);
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 4, 1));
        await surface.Pointer.ClickAsync(calendar, new Point(calendar.Bounds.Width - 3, 1));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 5, 1));
        input.IsOpen.ShouldBeTrue();
        input.Value.ShouldBe(_march15);

        // Cancelling and reopening shows the committed value's month again, not the browsed one.
        await surface.Keyboard.PressAsync(Code.Escape);
        input.IsOpen.ShouldBeFalse();
        await surface.UpdateAsync(() => input.IsOpen = true, "reopen after browsing");
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 3, 1));
        calendar.ActiveDate.ShouldBe(_march15);
    }

    /// <summary>Verifies days past Maximum render disabled inside the open popup and Right at the
    /// Maximum day cannot move the active date.</summary>
    [Fact]
    public async Task Popup_WhenMaximumFallsInsideDisplayedMonth_DisablesLaterDaysAndStopsNavigationAsync()
    {
        // Arrange - March 20, 2026 is a Friday on the third grid row (Sunday-first, March 1 is a Sunday).
        var maximum = new DateOnly(2026, 3, 20);
        var input = Create(maximum);
        input.Maximum = maximum;
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        await surface.UpdateAsync(() => input.IsOpen = true, "open the bounded calendar");

        // Act
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        calendar.ActiveDate.ShouldBe(maximum);
        var disabled = Project(ThemeColorHelper.DisabledForeground(ThemeCatalog.Dark));
        var origin = calendar.Bounds;
        surface.Cell(new Point(origin.X + 2 + (6 * 4), origin.Y + 3 + 2)).Style.Foreground.ShouldBe(disabled);
        surface.Cell(new Point(origin.X + 2 + (5 * 4), origin.Y + 3 + 2)).Style.Foreground.ShouldNotBe(disabled);
    }

    /// <summary>Verifies the open popup marks the dispatcher clock's current date inside the
    /// displayed month with the today-marker color.</summary>
    [Fact]
    public async Task Popup_WhenOpenedUnderFakeClock_MarksTodayInsideDisplayedMonthAsync()
    {
        // Arrange - today is Tuesday March 10, 2026: row 1, column 2 of a Sunday-first March grid.
        var clock = new ManualTimeProvider();
        clock.Advance(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero) - DateTimeOffset.UnixEpoch);
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), clock, TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;

        // Act
        await surface.UpdateAsync(() => input.IsOpen = true, "open under the fake clock");

        // Assert
        var today = Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark));
        var origin = calendar.Bounds;
        surface.Cell(new Point(origin.X + 2 + (2 * 4) + 2, origin.Y + 3 + 1)).Text.ShouldBe("0");
        surface.Cell(new Point(origin.X + 2 + (2 * 4) + 2, origin.Y + 3 + 1)).Style.Foreground.ShouldBe(today);
        surface.Cell(new Point(origin.X + 2 + (3 * 4) + 2, origin.Y + 3 + 1)).Style.Foreground.ShouldNotBe(today);
    }

    /// <summary>Verifies restyling the owned Calendar while the popup is open repaints its header
    /// arrow and is reflected by ActualCalendarStyle.</summary>
    [Fact]
    public async Task Popup_WhenCalendarStyleChangesWhileOpen_RepaintsHeaderAsync()
    {
        // Arrange
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        await surface.UpdateAsync(() => input.IsOpen = true, "open the calendar");
        var arrow = new Point(calendar.Bounds.X + 2, calendar.Bounds.Y + 1);
        surface.Cell(arrow).Text.ShouldBe("<");
        var before = surface.Cell(arrow).Style.Foreground;
        var style = CalendarStyle.Default with { NavigationColor = Color.Rgb(200, 30, 30) };

        // Act
        await surface.UpdateAsync(() => input.CalendarStyle = style, "restyle the open calendar");

        // Assert
        input.ActualCalendarStyle.ShouldBe(style);
        surface.Cell(arrow).Style.Foreground.ShouldBe(Project(Color.Rgb(200, 30, 30)));
        surface.Cell(arrow).Style.Foreground.ShouldNotBe(before);
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies an application theme swap while the popup is open forwards the owned
    /// Calendar's resolved style through ActualCalendarStyle and repaints the open header.</summary>
    [Fact]
    public async Task Popup_WhenThemeSwapsWhileOpen_ForwardsActualCalendarStyleAndRepaintsAsync()
    {
        // Arrange
        var themeA = WithAccent(Color.Rgb(10, 20, 30));
        var themeB = WithAccent(Color.Rgb(200, 210, 220));
        var input = Create(_march15);
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), themeA, TestContext.Current.CancellationToken);
        var calendar = input.OwnedCalendar;
        await surface.UpdateAsync(() => input.IsOpen = true, "open the calendar");
        var arrow = new Point(calendar.Bounds.X + 2, calendar.Bounds.Y + 1);
        surface.Cell(arrow).Style.Foreground.ShouldBe(Project(Color.Rgb(10, 20, 30)));
        var notifications = new List<string?>();
        input.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap the theme while open");

        // Assert
        notifications.ShouldContain(nameof(DateInput.ActualCalendarStyle));
        input.ActualCalendarStyle.ShouldBe(calendar.ActualStyle);
        surface.Cell(arrow).Style.Foreground.ShouldBe(Project(Color.Rgb(200, 210, 220)));
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a root resize while the popup is open keeps it open and re-places it
    /// directly beneath the field.</summary>
    [Fact]
    public async Task Popup_WhenRootResizesWhileOpen_StaysOpenBeneathFieldAsync()
    {
        // Arrange
        var input = Create(_march15);
        input.Height = Length.Cells(3);
        input.VerticalAlignment = VerticalAlignment.Top;
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(34, 16), TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open the calendar");
        popup.SurfaceBounds.Y.ShouldBe(input.Bounds.Bottom);

        // Act
        await surface.ResizeAsync(new Size(50, 24));

        // Assert
        input.IsOpen.ShouldBeTrue();
        popup.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.Y.ShouldBe(input.Bounds.Bottom);
        var calendar = input.OwnedCalendar;
        calendar.Bounds.Y.ShouldBeGreaterThanOrEqualTo(input.Bounds.Bottom);
        surface.Cell(new Point(calendar.Bounds.X + 2, calendar.Bounds.Y + 1)).Text.ShouldBe("<");
    }

    /// <summary>Verifies disposing the field while its popup is open tears the popup down silently
    /// - disposal detaches every subscriber, so no DropDownClosed fires - and leaves the surface
    /// responsive.</summary>
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
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.IsDisposed.ShouldBeTrue();
        popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(0);
        surface.ShouldHaveFocus(sibling);
        sibling.Value.ShouldBe(new TimeOnly(11, 30));
    }

    #endregion

    #region Helpers

    private static DateInput Create(DateOnly? value, string? format = null)
    {
        var input = new DateInput { Culture = CultureInfo.InvariantCulture };

        if (format is not null)
        {
            input.Format = format;
        }

        input.Value = value;
        return input;
    }

    private static KeyEventArgs Press(ControlBase control, Code code, Modifiers modifiers = Modifiers.None)
    {
        var eventArgs = new KeyEventArgs(new Stroke(code, character: null, nativeCode: 0, modifiers, KeyAction.Press));
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

    private static Color Project(Color color) => TerminalPalette.Project(color, ColorDepth.Basic16);

    private static Theme WithAccent(Color accent)
    {
        var source = ThemeCatalog.Dark;
        var theme = new Theme(
            source.Palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);

        foreach (var color in Enum.GetValues<SemanticColor>())
        {
            theme.SetColor(color, color == SemanticColor.Accent ? accent : source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<SemanticDecoration>())
        {
            theme.SetAttributes(decoration, source.ResolveAttributes(decoration));
        }

        theme.SetStyleSections(new Dictionary<string, JsonElement>(source.StyleSections));
        theme.Freeze();
        return theme;
    }

    #endregion
}
