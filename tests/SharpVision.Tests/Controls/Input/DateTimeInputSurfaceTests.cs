// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves DateTimeInput appearance and interaction through mounted terminal surfaces.</summary>
public sealed class DateTimeInputSurfaceTests
{
    /// <summary>Verifies a mounted DateTimeInput renders a bordered field with date and time.</summary>
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
        await surface.UpdateAsync(() => input.IsOpen = true, "open Calendar popup with overridden popup style");

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

    /// <summary>Verifies a designator-less lowercase hour token renders the same 24-hour digits
    /// that segment editing accepts and commits.</summary>
    [Fact]
    public async Task Keyboard_WhenLowercaseHourHasNoDesignator_RendersCommittedDigitsAsync()
    {
        // Arrange
        var input = new DateTimeInput
        {
            Format = "yyyy-MM-dd hh:mm",
            Value = new DateTime(2026, 7, 19, 0, 0, 0)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(30, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);

        // Act
        await surface.Keyboard.TypeAsync("15");

        // Assert
        input.Value.ShouldNotBeNull().Hour.ShouldBe(15);
        surface.Cell(new Point(12, 1)).Text.ShouldBe("1");
        surface.Cell(new Point(13, 1)).Text.ShouldBe("5");
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

    /// <summary>
    /// Verifies opening the Calendar popup leaves the Calendar itself genuinely focused, not just
    /// functionally reachable through DateTimeInput's own key-forwarding. Calendar's own
    /// keyboard-cursor underline (Calendar.ResolveDateStyle) is gated on Calendar.IsFocused, so a
    /// user watching the popup needs real focus on the Calendar to see the cursor move as arrow
    /// keys are forwarded into it.
    /// </summary>
    [Fact]
    public async Task Keyboard_WhenPopupOpens_FocusesTheCalendarAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.UpdateAsync(() => input.IsOpen = true, "open popup");

        // Assert
        calendar.IsFocused.ShouldBeTrue();
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
    /// Calendar's Selection being mistaken for a semantic Calendar activation, which would both
    /// commit a second time and set IsOpen = false out from under the caller.</summary>
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
        await surface.UpdateAsync(() => input.IsOpen = true, "open the Calendar popup");
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
        input.IsOpen.ShouldBeTrue();
        calendar.Selection.ShouldBe(new DateInterval(new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 15)));
    }

    /// <summary>Verifies a mounted DateTimeInput proves direct disable, ancestor-inherited disable,
    /// and re-enable recovery on a real terminal surface.</summary>
    [Fact]
    public async Task Enabled_WhenToggledOnMountedDateTimeInput_AppliesDirectAndInheritedDisabledStateAsync()
    {
        // Arrange — direct disable
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable DateTimeInput directly");

        // Assert
        surface.ShouldHaveState(input, VisualState.Disabled);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => input.IsEnabled = true, "re-enable DateTimeInput");

        // Assert
        surface.ShouldHaveState(input, VisualState.Normal);

        // Arrange — ancestor-inherited disable
        var child = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        var ancestor = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            ancestor,
            new Size(28, 3),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => ancestor.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);
    }

    /// <summary>Verifies a disabled DateTimeInput's arranged geometry after a genuine resize still
    /// matches an independently mounted, still-enabled DateTimeInput arranged at that same size,
    /// proving disabling does not perturb layout.</summary>
    [Fact]
    public async Task Layout_WhenDisabledDateTimeInputIsResized_MatchesEnabledGeometryAtNewSizeAsync()
    {
        // Arrange
        var mountSize = new Size(28, 3);
        var resizedSize = new Size(36, 5);
        var disabled = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabled,
            mountSize,
            TestContext.Current.CancellationToken);
        await disabledSurface.UpdateAsync(() => disabled.IsEnabled = false, "disable DateTimeInput before resize");

        // Act
        await disabledSurface.ResizeAsync(resizedSize);

        var enabled = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabled,
            resizedSize,
            TestContext.Current.CancellationToken);

        // Assert
        disabled.Bounds.ShouldBe(enabled.Bounds);
        disabled.DesiredSize.ShouldBe(enabled.DesiredSize);
    }

    /// <summary>Verifies a disabled DateTimeInput refuses Tab focus and leaves it on a focusable
    /// sibling instead.</summary>
    [Fact]
    public async Task Keyboard_WhenDisabledDateTimeInputIsTabbed_DoesNotReceiveFocusAsync()
    {
        // Arrange
        var input = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        var sibling = new DateTimeInput();
        var root = new Stack { Children = { input, sibling } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(28, 6),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable DateTimeInput before Tab");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        input.IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(sibling);
    }

    /// <summary>Verifies both affixes render pinned inside the field box beside the formatted
    /// date-time, strictly inboard of the drop-down indicator: a start affix, a one-cell gap, the
    /// date-time text, a one-cell gap, the end affix, then the indicator's own separating gap and
    /// glyph.</summary>
    [Fact]
    public async Task Render_WhenDateTimeInputHasBothAffixes_PinsThemInsideFieldBoxAsync()
    {
        // Arrange
        var input = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 14, 30, 0),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);

        // Assert - border, ">", gap, "03/15/2026 14:30", gap, "<", gap, "▼".
        surface.Cell(new Point(1, 1)).Text.ShouldBe(">");
        surface.Cell(new Point(3, 1)).Text.ShouldBe("0");
        surface.Cell(new Point(18, 1)).Text.ShouldBe("0");
        surface.Cell(new Point(20, 1)).Text.ShouldBe("<");
        surface.Cell(new Point(22, 1)).Text.ShouldBe("▼");
    }

    /// <summary>Verifies the drop-down indicator keeps the same offset from the field's own right
    /// edge whether or not affixes are set, proving an affix is reserved strictly inboard of the
    /// indicator and never shifts or overlaps it.</summary>
    [Fact]
    public async Task Render_WhenAffixesAreSet_KeepsDropDownIndicatorOffsetFromRightEdgeAsync()
    {
        // Arrange
        var bare = new DateTimeInput { Value = new DateTime(2026, 3, 15, 14, 30, 0) };
        var affixed = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 14, 30, 0),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        await using var bareSurface = await ComponentSurface.MountAsync(
            bare,
            new Size(28, 3),
            TestContext.Current.CancellationToken);
        await using var affixedSurface = await ComponentSurface.MountAsync(
            affixed,
            new Size(28, 3),
            TestContext.Current.CancellationToken);

        // Assert
        bareSurface.Cell(new Point(bare.Bounds.Right - 2, 1)).Text.ShouldBe("▼");
        affixedSurface.Cell(new Point(affixed.Bounds.Right - 2, 1)).Text.ShouldBe("▼");
    }

    /// <summary>Verifies a click on the hour segment still activates it when a start affix shifts
    /// the segment layout rightward, proving pointer hit-testing accounts for the affix-deflated
    /// field box instead of the raw, affix-unaware content box.</summary>
    [Fact]
    public async Task Pointer_WhenStartAffixIsSetAndHourSegmentIsClicked_ActivatesHourSegmentAsync()
    {
        // Arrange
        var input = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 14, 30, 0),
            StartAffix = new Affix(">")
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(28, 3),
            TestContext.Current.CancellationToken);

        // Act - column 14 lands on the first digit of the hour segment "14": border(1) + affix(1)
        // + gap(1) + "03/15/2026 "(11) = 14.
        await surface.Pointer.ClickAsync(input, new Point(14, 1));
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert - the hour incremented, proving the click landed on the hour segment.
        input.Value.ShouldNotBeNull().Hour.ShouldBe(15);
    }

    /// <summary>Verifies owner- and Calendar-focused routes deliver each initial and repeated
    /// navigation stroke exactly once while cancellation restores the opening active date and
    /// leaves the complete date-time value untouched.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Keyboard_WhenPopupNavigationUsesEitherFocusRoute_MovesOnceAndProgrammaticCloseRollsBackAsync(
        bool focusCalendar)
    {
        // Arrange
        var openingValue = new DateTime(2026, 3, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(6789);
        var input = new DateTimeInput { Value = openingValue };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateTimeInput popup");
        var target = focusCalendar ? (ControlBase) calendar : input;
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(target).ShouldBeTrue(),
            "choose popup navigation focus route");
        await surface.UpdateAsync(
            () => _ = Router.Route(
                target,
                Events.Key,
                new KeyEventArgs(new Stroke(Code.Right, default, 0, Modifiers.None, KeyAction.Press))),
            "route initial popup navigation");
        await surface.UpdateAsync(
            () => _ = Router.Route(
                target,
                Events.Key,
                new KeyEventArgs(new Stroke(Code.Right, default, 0, Modifiers.None, KeyAction.Repeat))),
            "route repeated popup navigation");
        calendar.ActiveDate.ShouldBe(DateOnly.FromDateTime(openingValue).AddDays(2));
        input.Value.ShouldBe(openingValue);

        // Act
        await surface.UpdateAsync(() => input.IsOpen = false, "programmatically close DateTimeInput popup");

        // Assert
        input.Value.ShouldBe(openingValue);
        calendar.ActiveDate.ShouldBe(DateOnly.FromDateTime(openingValue));
    }

    /// <summary>Verifies accepting a changed Calendar date preserves every time tick and the
    /// original <see cref="DateTimeKind"/>.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Keyboard_WhenPopupDateIsAccepted_PreservesTimeTicksAndKindAsync(
        bool focusCalendar,
        bool useSpace)
    {
        // Arrange
        var openingValue = new DateTime(2026, 3, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(6789);
        var input = new DateTimeInput { Value = openingValue };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateTimeInput popup");
        var target = focusCalendar ? (ControlBase) calendar : input;
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(target).ShouldBeTrue(),
            "choose popup acceptance focus route");
        await surface.Keyboard.PressAsync(Code.Right);

        // Act
        if (useSpace)
        {
            await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        }
        else
        {
            await surface.Keyboard.PressAsync(Code.Enter);
        }

        // Assert
        var accepted = input.Value.ShouldNotBeNull();
        accepted.ShouldBe(openingValue.AddDays(1));
        accepted.Kind.ShouldBe(DateTimeKind.Utc);
        accepted.TimeOfDay.Ticks.ShouldBe(openingValue.TimeOfDay.Ticks);
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies disabling an open input cancels provisional Calendar navigation and
    /// restores the opening active date before releasing the popup.</summary>
    [Fact]
    public async Task Availability_WhenDisabledAfterPopupBrowsing_RestoresOpeningDateAsync()
    {
        // Arrange
        var openingValue = new DateTime(2026, 3, 15, 14, 30, 45, DateTimeKind.Local).AddTicks(6789);
        var input = new DateTimeInput { Value = openingValue };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateTimeInput popup");
        await surface.Keyboard.PressAsync(Code.Right);
        calendar.ActiveDate.ShouldBe(DateOnly.FromDateTime(openingValue).AddDays(1));

        // Act
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable DateTimeInput while popup is open");

        // Assert
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(openingValue);
        calendar.ActiveDate.ShouldBe(DateOnly.FromDateTime(openingValue));
    }

    /// <summary>Verifies Calendar pointer acceptance changes only the date while preserving every
    /// time tick and the original <see cref="DateTimeKind"/>.</summary>
    [Fact]
    public async Task Pointer_WhenCalendarDateIsClicked_AcceptsDateAndPreservesTimeAsync()
    {
        // Arrange
        var openingValue = new DateTime(2026, 3, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(6789);
        var input = new DateTimeInput { Value = openingValue };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(32, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateTimeInput popup");

        // Act - March 16 is the Monday cell on the third displayed week row.
        await surface.Pointer.ClickAsync(calendar, new Point(6, 5));

        // Assert
        var accepted = input.Value.ShouldNotBeNull();
        accepted.ShouldBe(openingValue.AddDays(1));
        accepted.Kind.ShouldBe(DateTimeKind.Utc);
        accepted.TimeOfDay.Ticks.ShouldBe(openingValue.TimeOfDay.Ticks);
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies clicking the already-selected Calendar date accepts and closes without
    /// requiring a selection mutation or disturbing time ticks and <see cref="DateTimeKind"/>.</summary>
    [Fact]
    public async Task Pointer_WhenSelectedCalendarDateIsClicked_AcceptsAndPreservesTimeAsync()
    {
        // Arrange
        var openingValue = new DateTime(2026, 3, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(6789);
        var input = new DateTimeInput { Value = openingValue };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(32, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateTimeInput popup");

        // Act - March 15 is the Sunday cell on the third displayed week row.
        await surface.Pointer.ClickAsync(calendar, new Point(2, 5));

        // Assert
        input.Value.ShouldBe(openingValue);
        input.Value.ShouldNotBeNull().Kind.ShouldBe(DateTimeKind.Utc);
        input.Value.ShouldNotBeNull().TimeOfDay.Ticks.ShouldBe(openingValue.TimeOfDay.Ticks);
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a close callback that reopens after keyboard acceptance establishes a
    /// replacement session that the completed activation cannot close again.</summary>
    [Fact]
    public async Task Keyboard_WhenAcceptedCloseReopens_PreservesReplacementSessionAsync()
    {
        // Arrange
        var input = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(6789)
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(32, 15),
            TestContext.Current.CancellationToken);
        var closed = 0;
        input.DropDownClosed += (_, _) =>
        {
            closed++;

            if (closed == 1)
            {
                input.IsOpen = true;
            }
        };
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateTimeInput popup");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        closed.ShouldBe(1);
        input.IsOpen.ShouldBeTrue();
    }
}
