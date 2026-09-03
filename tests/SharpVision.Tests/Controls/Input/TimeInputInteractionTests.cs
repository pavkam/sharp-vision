// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves TimeInput's complete segment-editing contract through mounted surfaces and
/// routed detached input: 12-hour digit entry and AM/PM selection, increment carry and bound
/// saturation, traversal and command keys that bubble when they cannot act, the null seed, custom
/// and localized formats, mid-entry culture and layout switches, pointer hit-testing over wide
/// designators, focus retention, and ValueChanged reentrancy.</summary>
public sealed class TimeInputInteractionTests
{
    #region Twelve-hour digit entry and AM/PM

    /// <summary>Verifies hour digit entry under a 12-hour layout keeps the current half of the day:
    /// a buffered pair commits and advances to Minute, a lone zero clamps to one o'clock and stays
    /// on Hour, and a first digit above the threshold commits immediately.</summary>
    [Theory]
    [InlineData(14, "11", 23, 31)]
    [InlineData(14, "12", 12, 31)]
    [InlineData(14, "05", 17, 31)]
    [InlineData(14, "2", 14, 31)]
    [InlineData(14, "0", 14, 30)]
    [InlineData(2, "11", 11, 31)]
    [InlineData(2, "12", 0, 31)]
    public async Task Keyboard_WhenTwelveHourDigitsAreTyped_KeepsHalfOfDayAndAdvancesAsync(
        int startHour,
        string digits,
        int expectedHour,
        int expectedMinute)
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(startHour, 30), Use24HourFormat = false };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(16, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act - the trailing Up shows whether entry advanced to Minute or stayed on Hour.
        await surface.Keyboard.TypeAsync(digits);

        if (digits == "0")
        {
            input.Value.ShouldBe(new TimeOnly(13, 30), "a lone zero clamps to one o'clock in the same half of the day");
        }

        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new TimeOnly(expectedHour, expectedMinute));
    }

    /// <summary>Verifies "p" selects PM and "a" selects AM rather than toggling: repeating the
    /// letter of the current half of the day changes nothing, yet it still moved the designator
    /// highlight, so the field consumes it instead of leaking it to an ancestor.</summary>
    [Fact]
    public void Keyboard_WhenAOrPIsTyped_SelectsHalfOfDayWithoutToggling()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(2, 30), Use24HourFormat = false };

        // Act and assert
        Type(control, 'p').IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new TimeOnly(14, 30));
        Type(control, 'P').IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new TimeOnly(14, 30));
        Type(control, 'a').IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new TimeOnly(2, 30));
        Type(control, 'A').IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new TimeOnly(2, 30));

        // The designator is now the active segment: Up flips the half of the day.
        _ = Press(control, Code.Up);
        control.Value.ShouldBe(new TimeOnly(14, 30));
    }

    /// <summary>Verifies the AM/PM letters are inert under a 24-hour layout and over a null value.</summary>
    [Fact]
    public void Keyboard_WhenAOrPIsTypedWithoutDesignatorOrValue_LeavesKeyUnhandled()
    {
        // Arrange
        using var twentyFourHour = new TimeInput { Value = new TimeOnly(2, 30) };
        using var empty = new TimeInput { Value = null, Use24HourFormat = false };

        // Act and assert
        Type(twentyFourHour, 'p').IsHandled.ShouldBeFalse();
        twentyFourHour.Value.ShouldBe(new TimeOnly(2, 30));
        Type(empty, 'p').IsHandled.ShouldBeFalse();
        empty.Value.ShouldBeNull();
    }

    /// <summary>Verifies stepping the designator with Up or Down flips the half of the day and
    /// wraps within the same day in both directions.</summary>
    [Fact]
    public async Task Keyboard_WhenDesignatorIsStepped_FlipsHalfOfDayBothWaysAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(2, 30), Use24HourFormat = false };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(16, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(new TimeOnly(14, 30));
        surface.Cell(new Point(7, 1)).Text.ShouldBe("P");
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(new TimeOnly(2, 30));
        await surface.Keyboard.PressAsync(Code.Down);
        input.Value.ShouldBe(new TimeOnly(14, 30));
        await surface.Keyboard.PressAsync(Code.Down);
        input.Value.ShouldBe(new TimeOnly(2, 30));
        surface.Cell(new Point(7, 1)).Text.ShouldBe("A");
    }

    #endregion

    #region Increment carry, bounds, and seconds

    /// <summary>Verifies stepping Minute past 59 carries into the next hour and stepping back
    /// returns across the same boundary.</summary>
    [Fact]
    public async Task Keyboard_WhenMinuteStepsPastHour_CarriesIntoHourAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(10, 59) };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(12, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(new TimeOnly(11, 0));
        await surface.Keyboard.PressAsync(Code.Down);
        input.Value.ShouldBe(new TimeOnly(10, 59));
    }

    /// <summary>Verifies a step that cannot leave the inclusive bounds changes nothing and is still
    /// consumed, exactly as DateInput does, so a bounded field never leaks the arrow to an ancestor.</summary>
    [Theory]
    [InlineData(Code.Up)]
    [InlineData(Code.Down)]
    public void Keyboard_WhenStepWouldLeaveBounds_LeavesValueAndConsumesKey(Code code)
    {
        // Arrange
        var bound = new TimeOnly(12, 0);
        using var control = new TimeInput { Value = bound, Minimum = bound, Maximum = bound };

        // Act
        var key = Press(control, code);

        // Assert
        control.Value.ShouldBe(bound);
        key.IsHandled.ShouldBeTrue();
    }

    /// <summary>Verifies a bounded field inside a scrolling host owns its arrow keys on a mounted
    /// surface: Up at the upper bound neither changes the value nor scrolls the host behind it.</summary>
    [Fact]
    public async Task Keyboard_WhenBoundedFieldSitsInScrollingHost_UpAtBoundDoesNotScrollHostAsync()
    {
        // Arrange
        var bound = new TimeOnly(12, 0);
        var input = new TimeInput { Value = bound, Minimum = bound, Maximum = bound };
        var host = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { new ControlText("above") { Height = Length.Cells(1) }, input }
        };

        for (var index = 0; index < 12; index++)
        {
            host.Children.Add(new ControlText($"row {index}") { Height = Length.Cells(1) });
        }

        await using var surface = await ComponentSurface.MountAsync(host, new Size(20, 8), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(input).ShouldBeTrue(), "focus the bounded field");
        await surface.UpdateAsync(() => host.VerticalOffset = 1, "scroll the host by one line");
        surface.Cell(new Point(0, 0)).Text.ShouldNotBe("a", "the 'above' row scrolled out of view");

        // Act
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        input.Value.ShouldBe(bound);
        host.VerticalOffset.ShouldBe(1, "the field consumed both arrows instead of scrolling the host");
        surface.ShouldHaveFocus(input);
    }

    /// <summary>Verifies the seconds segment is reachable with End once shown, steps by one second,
    /// and Backspace resets it - and Minute - to zero.</summary>
    [Fact]
    public async Task Keyboard_WhenSecondsAreShown_EndReachesSecondsAndBackspaceClearsThemAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(10, 30, 45), ShowSeconds = true };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(14, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(new TimeOnly(10, 30, 46));
        await surface.Keyboard.PressAsync(Code.Backspace);
        input.Value.ShouldBe(new TimeOnly(10, 30, 0));
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Backspace);
        input.Value.ShouldBe(new TimeOnly(10, 0, 0));
    }

    /// <summary>Verifies hiding the seconds segment while it is active clamps the active segment
    /// back to Minute, so the next step edits Minute instead of a vanished segment.</summary>
    [Fact]
    public async Task ShowSeconds_WhenDisabledWhileSecondsAreActive_ClampsActiveSegmentToMinuteAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(10, 30, 45), ShowSeconds = true };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(14, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);

        // Act
        await surface.UpdateAsync(() => input.ShowSeconds = false, "hide seconds while active");
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new TimeOnly(10, 31, 45));
        (surface.Cell(new Point(4, 1)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);
    }

    /// <summary>Verifies switching to the 24-hour layout while the designator is active clamps the
    /// active segment to Minute.</summary>
    [Fact]
    public async Task Use24HourFormat_WhenEnabledWhileDesignatorIsActive_ClampsActiveSegmentToMinuteAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30), Use24HourFormat = false };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(16, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);

        // Act
        await surface.UpdateAsync(() => input.Use24HourFormat = true, "switch to 24-hour while designator is active");
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new TimeOnly(14, 31));
    }

    #endregion

    #region Traversal and command keys

    /// <summary>Verifies Left at the first segment and Right at the last segment stay put without
    /// wrapping, yet are still consumed by the field.</summary>
    [Fact]
    public void Keyboard_WhenTraversalHitsEitherEnd_StaysWithoutWrappingAndConsumesKey()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(10, 30) };

        // Act and assert
        Press(control, Code.Left).IsHandled.ShouldBeTrue();
        Press(control, Code.Home).IsHandled.ShouldBeTrue();
        _ = Press(control, Code.Up);
        control.Value.ShouldBe(new TimeOnly(11, 30));
        _ = Press(control, Code.End);
        Press(control, Code.Right).IsHandled.ShouldBeTrue();
        Press(control, Code.End).IsHandled.ShouldBeTrue();
        _ = Press(control, Code.Up);
        control.Value.ShouldBe(new TimeOnly(11, 31));
    }

    /// <summary>Verifies keys outside the segment command set - including Enter and Escape, since
    /// TimeInput has no press activation or popup - stay unhandled and change nothing.</summary>
    [Theory]
    [InlineData(Code.Enter)]
    [InlineData(Code.Escape)]
    [InlineData(Code.PageUp)]
    [InlineData(Code.PageDown)]
    [InlineData(Code.F4)]
    public void Keyboard_WhenNonCommandKeyIsPressed_LeavesKeyUnhandled(Code code)
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(10, 30) };

        // Act
        var key = Press(control, code);

        // Assert
        key.IsHandled.ShouldBeFalse();
        control.Value.ShouldBe(new TimeOnly(10, 30));
    }

    /// <summary>Verifies Delete and Backspace over an empty value, and Delete under AllowNull =
    /// false, change nothing and are still consumed by the field.</summary>
    [Fact]
    public void Keyboard_WhenClearingCannotChangeValue_ConsumesKeyWithoutChange()
    {
        // Arrange
        using var empty = new TimeInput { Value = null };
        using var nonNullable = new TimeInput { Value = new TimeOnly(10, 30), AllowNull = false };

        // Act and assert
        Press(empty, Code.Delete).IsHandled.ShouldBeTrue();
        Press(empty, Code.Backspace).IsHandled.ShouldBeTrue();
        empty.Value.ShouldBeNull();
        Press(nonNullable, Code.Delete).IsHandled.ShouldBeTrue();
        nonNullable.Value.ShouldBe(new TimeOnly(10, 30));
    }

    /// <summary>Verifies a hidden field routes nothing: keys change neither the value nor the
    /// active segment, a pointer press neither focuses nor activates a segment, and the field
    /// resumes editing exactly where it was once shown again.</summary>
    [Fact]
    public async Task Visibility_WhenHidden_IgnoresKeysAndPointerUntilShownAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(10, 30) };
        var sibling = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var root = new Stack { Children = { input, sibling } };
        var changes = 0;
        input.ValueChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(root, new Size(20, 8), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.UpdateAsync(() => input.Visibility = Visibility.Hidden, "hide the focused field");
        surface.ShouldHaveFocus(null);

        // Act
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.TypeAsync("5");
        await surface.Pointer.MoveToAsync(new Point(1, 1));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        input.Value.ShouldBe(new TimeOnly(10, 30));
        changes.ShouldBe(0);
        input.IsFocused.ShouldBeFalse();
        surface.Cell(new Point(1, 1)).Text.ShouldBe(" ", "a hidden field paints nothing");

        // Act - showing it again resumes on the segment that was active before hiding.
        await surface.UpdateAsync(() => input.Visibility = Visibility.Visible, "show the field again");
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(input).ShouldBeTrue(), "refocus the field");
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new TimeOnly(10, 31));
        changes.ShouldBe(1);
    }

    /// <summary>Verifies a culture whose AM/PM designator formats to nothing still renders an
    /// editable, visible designator: the invariant "AM"/"PM" stands in, End reaches it, Up flips
    /// the half of the day, and the flipped text is what the user sees.</summary>
    [Fact]
    public async Task Culture_WhenDesignatorIsEmpty_RendersInvariantDesignatorThatStaysEditableAsync()
    {
        // Arrange
        var culture = (CultureInfo) CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.AMDesignator = string.Empty;
        culture.DateTimeFormat.PMDesignator = string.Empty;
        var input = new TimeInput { Value = new TimeOnly(9, 15), Use24HourFormat = false, Culture = culture };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(16, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        ReadRow(surface, 1, 16).ShouldContain("09:15 AM");

        // Act
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new TimeOnly(21, 15));
        ReadRow(surface, 1, 16).ShouldContain("09:15 PM");
        var designator = ReadRow(surface, 1, 16).IndexOf("PM", StringComparison.Ordinal);
        (surface.Cell(new Point(designator, 1)).Style.Attributes & TerminalAttributes.Reverse)
            .ShouldBe(TerminalAttributes.Reverse, "the active designator is highlighted");

        // Act - the designator is also hit-testable by pointer.
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Pointer.MoveToAsync(new Point(designator, 1));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        input.Value.ShouldBe(new TimeOnly(9, 15));
        ReadRow(surface, 1, 16).ShouldContain("09:15 AM");
    }

    #endregion

    #region Null value

    /// <summary>Verifies a digit typed over an empty value seeds midnight - not the clock - before
    /// applying, and that seed is clamped into the bounds first.</summary>
    [Fact]
    public async Task Keyboard_WhenDigitIsTypedOnNullValue_SeedsMidnightClampedIntoBoundsAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        clock.Advance(new DateTimeOffset(2026, 6, 10, 12, 34, 0, TimeSpan.Zero) - DateTimeOffset.UnixEpoch);
        var unbounded = new TimeInput { Value = null };
        var bounded = new TimeInput { Value = null, Minimum = new TimeOnly(8, 0) };
        var root = new Stack { Children = { unbounded, bounded } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(20, 6), clock, TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        ReadRow(surface, 1, 7).ShouldBe("┃--:--┃");

        // Act
        await surface.Keyboard.TypeAsync("5");
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("5");

        // Assert
        unbounded.Value.ShouldBe(new TimeOnly(5, 0));
        bounded.Value.ShouldBe(new TimeOnly(8, 0));
    }

    /// <summary>Verifies Down on an empty value seeds the dispatcher clock's current time rather
    /// than decrementing from it.</summary>
    [Fact]
    public async Task Keyboard_WhenDownIsPressedOnNullValue_SeedsClockTimeAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        clock.Advance(new DateTimeOffset(2026, 6, 10, 12, 34, 56, TimeSpan.Zero) - DateTimeOffset.UnixEpoch);
        var input = new TimeInput { Value = null };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(12, 3), clock, TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        input.Value.ShouldBe(TimeOnly.FromDateTime(clock.GetLocalNow().DateTime));
    }

    #endregion

    #region Formats and culture

    /// <summary>Verifies assigning a culture while a custom Format is set validates the pattern
    /// under that culture and re-renders its separator localized.</summary>
    [Fact]
    public void Culture_WhenAssignedWithCustomFormat_RendersLocalizedSeparator()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(14, 30), Format = "HH:mm" };

        // Act
        control.Culture = new CultureInfo("fi-FI");

        // Assert
        RenderRow(control, new Size(12, 3), 1).ShouldContain("14.30");
        control.Format.ShouldBe("HH:mm");
    }

    /// <summary>Verifies a non-padded 12-hour format widens on the mounted surface when the hour
    /// steps from one digit to two, so the field is remeasured rather than clipped.</summary>
    [Fact]
    public async Task Format_WhenNonPaddedHourWidens_RemeasuresMountedFieldAsync()
    {
        // Arrange
        var input = new TimeInput
        {
            Value = new TimeOnly(9, 5),
            Format = "h:mm tt",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(16, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        var narrowWidth = input.Bounds.Width;
        ReadRow(surface, 1, narrowWidth).ShouldBe("┃9:05 AM┃");

        // Act
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new TimeOnly(10, 5));
        input.Bounds.Width.ShouldBe(narrowWidth + 1);
        ReadRow(surface, 1, input.Bounds.Width).ShouldBe("┃10:05 AM┃");
    }

    /// <summary>Verifies escaped and quoted literals in a time format render literally while
    /// unquoted separators localize.</summary>
    [Fact]
    public void Format_WhenLiteralsAreEscapedOrQuoted_RendersThemVerbatim()
    {
        // Arrange
        using var control = new TimeInput
        {
            Value = new TimeOnly(14, 30),
            Format = @"HH\:mm 'hrs'",
            Culture = new CultureInfo("fi-FI")
        };

        // Act
        var row = RenderRow(control, new Size(24, 3), 1);
        _ = Press(control, Code.Right);
        _ = Press(control, Code.Up);

        // Assert
        row.ShouldContain("14:30 hrs");
        control.Value.ShouldBe(new TimeOnly(14, 31));
    }

    /// <summary>Verifies formats TimeOnly cannot render are rejected without replacing the current
    /// derived layout.</summary>
    [Theory]
    [InlineData("yyyy")]
    [InlineData("d")]
    [InlineData("HH%")]
    public void Format_WhenPatternCannotBeRendered_ThrowsAndPreservesLayout(string format)
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(14, 30) };

        // Act
        var exception = Should.Throw<ArgumentException>(() => control.Format = format);

        // Assert
        exception.ParamName.ShouldBe("value");
        control.Format.ShouldBeNull();
        RenderRow(control, new Size(12, 3), 1).ShouldContain("14:30");
    }

    /// <summary>Verifies a Culture switch after a buffered first digit discards it, so the next
    /// digit starts a fresh entry.</summary>
    [Fact]
    public async Task Culture_WhenSwitchedMidEntry_DiscardsPartialDigitAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(14, 30) };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(12, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("1");
        input.Value.ShouldBe(new TimeOnly(1, 30));

        // Act
        await surface.UpdateAsync(() => input.Culture = new CultureInfo("fi-FI"), "switch culture mid-entry");
        await surface.Keyboard.TypeAsync("5");

        // Assert
        input.Value.ShouldBe(new TimeOnly(5, 30));
        surface.Cell(new Point(3, 1)).Text.ShouldBe(".");
    }

    #endregion

    #region Pointer

    /// <summary>Verifies pointer hit-testing over a wide non-Latin designator resolves the clicked
    /// designator column, and a separator column resolves to the segment following it.</summary>
    [Fact]
    public async Task Pointer_WhenWideDesignatorOrSeparatorIsClicked_ActivatesResolvedSegmentAsync()
    {
        // Arrange - ja-JP renders "02:30 午後"; each kanji occupies two cells starting at column 7.
        var input = new TimeInput
        {
            Value = new TimeOnly(14, 30),
            Use24HourFormat = false,
            Culture = new CultureInfo("ja-JP"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(16, 3), TestContext.Current.CancellationToken);
        surface.Cell(new Point(7, 1)).Text.ShouldBe("午");

        // Act and assert - the designator flips, then the separator click lands on Minute.
        await surface.Pointer.ClickAsync(input, new Point(9, 1));
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(new TimeOnly(2, 30));
        await surface.Pointer.ClickAsync(input, new Point(3, 1));
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(new TimeOnly(2, 31));
    }

    /// <summary>Verifies a secondary-button click, a wheel, and a plain move over the field leave
    /// the active segment and the value alone.</summary>
    [Fact]
    public async Task Pointer_WhenRightClickWheelOrMoveHitsField_ChangesNothingAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(10, 30) };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(12, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Pointer.RightClickAsync(input, new Point(5, 1));
        await surface.Pointer.WheelAsync(input, new Point(5, 1), wheelY: 1);
        await surface.Pointer.MoveToAsync(input, new Point(5, 1));
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert - Hour is still the active segment.
        input.Value.ShouldBe(new TimeOnly(11, 30));
    }

    #endregion

    #region Focus

    /// <summary>Verifies losing focus discards a buffered digit and removes the highlight, while
    /// regaining focus keeps the previously active segment.</summary>
    [Fact]
    public async Task Focus_WhenLostAndRegained_DiscardsPartialDigitAndKeepsSegmentAsync()
    {
        // Arrange
        var first = new TimeInput { Value = new TimeOnly(14, 30) };
        var second = new TimeInput { Value = new TimeOnly(8, 0) };
        var root = new Stack { Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(root, new Size(20, 6), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.TypeAsync("1");
        first.Value.ShouldBe(new TimeOnly(14, 1));
        (surface.Cell(new Point(4, 1)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);

        // Act
        await surface.Pointer.ClickAsync(second, new Point(1, 1));
        surface.ShouldHaveFocus(second);
        var highlightedWhileUnfocused = (surface.Cell(new Point(4, 1)).Style.Attributes & TerminalAttributes.Reverse) != 0;
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        await surface.Keyboard.TypeAsync("5");

        // Assert - "5" starts a fresh minute entry on the still-active Minute segment.
        highlightedWhileUnfocused.ShouldBeFalse();
        surface.ShouldHaveFocus(first);
        first.Value.ShouldBe(new TimeOnly(14, 5));
        second.Value.ShouldBe(new TimeOnly(8, 0));
    }

    /// <summary>Verifies a programmatic Value assignment while focused on Minute keeps Minute active.</summary>
    [Fact]
    public async Task Value_WhenAssignedWhileFocusedOnMinute_KeepsMinuteSegmentActiveAsync()
    {
        // Arrange
        var input = new TimeInput { Value = new TimeOnly(10, 30) };
        await using var surface = await ComponentSurface.MountAsync(input, new Size(12, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);

        // Act
        await surface.UpdateAsync(() => input.Value = new TimeOnly(18, 45), "assign while focused");
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(new TimeOnly(18, 46));
    }

    #endregion

    #region Layout

    /// <summary>Verifies a field narrower than its text clips at the content box and a field with
    /// no content columns draws only its border, neither throwing.</summary>
    [Theory]
    [InlineData(4, "┃14┃")]
    [InlineData(2, "┃┃")]
    public async Task Layout_WhenWidthIsTiny_ClipsWithoutEscapingAsync(int width, string expectedRow)
    {
        // Arrange
        var input = new TimeInput
        {
            Value = new TimeOnly(14, 30),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = Length.Cells(width)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(input, new Size(12, 3), TestContext.Current.CancellationToken);

        // Assert
        input.Bounds.Width.ShouldBe(width);
        ReadRow(surface, 1, width).ShouldBe(expectedRow);
        surface.Cell(new Point(width, 1)).Text.ShouldBe(" ");
    }

    #endregion

    #region ValueChanged

    /// <summary>Verifies a ValueChanged subscriber that assigns a newer value sees its own nested
    /// transition published in order, with no stale replay of the interrupted one.</summary>
    [Fact]
    public void ValueChanged_WhenSubscriberAssignsNewerValue_PublishesNestedTransitionInOrder()
    {
        // Arrange
        var start = new TimeOnly(10, 30);
        var first = new TimeOnly(11, 0);
        var second = new TimeOnly(12, 0);
        using var control = new TimeInput { Value = start };
        var observed = new List<(TimeOnly? Previous, TimeOnly? Current)>();
        control.ValueChanged += (_, eventArgs) =>
        {
            observed.Add((eventArgs.PreviousValue, eventArgs.Value));

            if (eventArgs.Value == first)
            {
                control.Value = second;
            }
        };

        // Act - through the keyboard path, so the nested assignment interrupts a routed edit.
        _ = Press(control, Code.Up);
        var handledStart = observed.Count;
        control.Value = first;

        // Assert
        handledStart.ShouldBe(1);
        control.Value.ShouldBe(second);
        observed.ShouldBe([(start, new TimeOnly(11, 30)), (new TimeOnly(11, 30), first), (first, second)]);
    }

    /// <summary>Verifies a throwing ValueChanged subscriber does not roll back a keyboard commit.</summary>
    [Fact]
    public void ValueChanged_WhenSubscriberThrowsDuringKeyboardEdit_CommitsValueBeforePropagating()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(10, 30) };
        control.ValueChanged += (_, _) => throw new InvalidOperationException("observer failed");

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => Press(control, Code.Up));

        // Assert
        exception.Message.ShouldBe("observer failed");
        control.Value.ShouldBe(new TimeOnly(11, 30));
    }

    #endregion

    #region Helpers

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
