// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves DateInput appearance and interaction through mounted terminal surfaces.</summary>
public sealed class DateInputSurfaceTests
{
    /// <summary>Verifies a mounted DateInput renders a bordered field with a formatted date.</summary>
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
        await surface.UpdateAsync(() => input.IsOpen = true, "open Calendar popup with overridden popup style");

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

        await surface.UpdateAsync(() => input.IsOpen = true, "open the Calendar popup");
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 1, 1));
        calendar.Selection.ShouldBe(new DateInterval(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1)));

        // Act: change Value while the popup remains open.
        await surface.UpdateAsync(() => input.Value = new DateOnly(2026, 6, 15), "change value while open");

        // Assert
        input.IsOpen.ShouldBeTrue();
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
        await surface.UpdateAsync(() => input.IsOpen = true, "open popup");
        input.IsOpen.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        input.IsOpen.ShouldBeFalse();
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
        await surface.UpdateAsync(() => input.IsOpen = true, "open popup");

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(new DateOnly(2026, 3, 16));
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies opening the Calendar popup leaves the Calendar itself genuinely focused, not just
    /// functionally reachable through shared owner-preview delegation. PopupModalTracker.Enter used
    /// to always re-commit focus to the popup's owner as the modal scope's initial focus, silently
    /// discarding whatever Popup.FocusOnOpen (true for DateInput) had just placed on the Calendar a
    /// moment earlier in the very same call. The regression was invisible to
    /// <see cref="Keyboard_WhenRightAndEnterArePressedWhileOpen_SelectsNextDateAsync"/> above,
    /// which only proves the committed value ends up correct: the coordinator delegates the owner
    /// preview stroke to Calendar.HandleNavigationKey regardless of real focus, but Calendar's own
    /// keyboard-cursor underline (Calendar.ResolveDateStyle) is
    /// gated on Calendar.IsFocused, so a user watching the popup saw no cursor move at all.
    /// </summary>
    [Fact]
    public async Task Keyboard_WhenPopupOpens_FocusesTheCalendarAsync()
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
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.UpdateAsync(() => input.IsOpen = true, "open popup");

        // Assert
        calendar.IsFocused.ShouldBeTrue();
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
        input.IsOpen.ShouldBeFalse();
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
        input.IsOpen.ShouldBeFalse();
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

    /// <summary>Verifies a mounted DateInput proves direct disable, ancestor-inherited disable, and
    /// re-enable recovery on a real terminal surface.</summary>
    [Fact]
    public async Task Enabled_WhenToggledOnMountedDateInput_AppliesDirectAndInheritedDisabledStateAsync()
    {
        // Arrange — direct disable
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable DateInput directly");

        // Assert
        surface.ShouldHaveState(input, VisualState.Disabled);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => input.IsEnabled = true, "re-enable DateInput");

        // Assert
        surface.ShouldHaveState(input, VisualState.Normal);

        // Arrange — ancestor-inherited disable
        var child = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        var ancestor = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            ancestor,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => ancestor.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);
    }

    /// <summary>Verifies a disabled DateInput's arranged geometry after a genuine resize still
    /// matches an independently mounted, still-enabled DateInput arranged at that same size,
    /// proving disabling does not perturb layout.</summary>
    [Fact]
    public async Task Layout_WhenDisabledDateInputIsResized_MatchesEnabledGeometryAtNewSizeAsync()
    {
        // Arrange
        var mountSize = new Size(20, 3);
        var resizedSize = new Size(30, 5);
        var disabled = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabled,
            mountSize,
            TestContext.Current.CancellationToken);
        await disabledSurface.UpdateAsync(() => disabled.IsEnabled = false, "disable DateInput before resize");

        // Act
        await disabledSurface.ResizeAsync(resizedSize);

        var enabled = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabled,
            resizedSize,
            TestContext.Current.CancellationToken);

        // Assert
        disabled.Bounds.ShouldBe(enabled.Bounds);
        disabled.DesiredSize.ShouldBe(enabled.DesiredSize);
    }

    /// <summary>Verifies a disabled DateInput refuses Tab focus and leaves it on a focusable
    /// sibling instead, reusing the same Stack-of-two idiom the segment-carry evidence above mounts
    /// for Tab navigation between two DateInput instances.</summary>
    [Fact]
    public async Task Keyboard_WhenDisabledDateInputIsTabbedPast_LeavesFocusOnSiblingAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        var sibling = new DateInput { Culture = CultureInfo.InvariantCulture };
        var root = new Stack { Children = { input, sibling } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 3),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable DateInput before Tab");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        input.IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(sibling);
    }

    /// <summary>Verifies both affixes render pinned inside the field box beside the formatted
    /// date, strictly inboard of the drop-down indicator: a start affix, a one-cell gap, the date
    /// text, a one-cell gap, the end affix, then the indicator's own separating gap and glyph.</summary>
    [Fact]
    public async Task Render_WhenDateInputHasBothAffixes_PinsThemInsideFieldBoxAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            Format = "MM/dd/yyyy",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        // Assert - border, ">", gap, "03/15/2026", gap, "<", gap, "▼".
        surface.Cell(new Point(1, 1)).Text.ShouldBe(">");
        surface.Cell(new Point(3, 1)).Text.ShouldBe("0");
        surface.Cell(new Point(12, 1)).Text.ShouldBe("6");
        surface.Cell(new Point(14, 1)).Text.ShouldBe("<");
        surface.Cell(new Point(16, 1)).Text.ShouldBe("▼");
    }

    /// <summary>Verifies the drop-down indicator keeps the same offset from the field's own right
    /// edge whether or not affixes are set, proving an affix is reserved strictly inboard of the
    /// indicator and never shifts or overlaps it.</summary>
    [Fact]
    public async Task Render_WhenAffixesAreSet_KeepsDropDownIndicatorOffsetFromRightEdgeAsync()
    {
        // Arrange
        var bare = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            Format = "MM/dd/yyyy"
        };
        var affixed = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            Format = "MM/dd/yyyy",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        await using var bareSurface = await ComponentSurface.MountAsync(
            bare,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await using var affixedSurface = await ComponentSurface.MountAsync(
            affixed,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        // Assert
        bareSurface.Cell(new Point(bare.Bounds.Right - 2, 1)).Text.ShouldBe("▼");
        affixedSurface.Cell(new Point(affixed.Bounds.Right - 2, 1)).Text.ShouldBe("▼");
    }

    /// <summary>Verifies a click on a date segment still activates the correct segment when a
    /// start affix shifts the segment layout rightward, proving pointer hit-testing accounts for
    /// the affix-deflated field box instead of the raw, affix-unaware content box.</summary>
    [Fact]
    public async Task Pointer_WhenStartAffixIsSetAndDaySegmentIsClicked_ActivatesDaySegmentAsync()
    {
        // Arrange
        var input = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            Format = "MM/dd/yyyy",
            StartAffix = new Affix(">")
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        // Act - column 6 lands on the first digit of the day segment "15": border(1) + affix(1) +
        // gap(1) + "03/"(3) = 6.
        await surface.Pointer.ClickAsync(input, new Point(6, 1));
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert - the day incremented, proving the click landed on the day segment, not month.
        input.Value.ShouldBe(new DateOnly(2026, 3, 16));
    }

    /// <summary>Verifies real terminal input reaches the Calendar-focused popup route exactly
    /// once and Escape, direct close, and owner unavailability restore the opening date.</summary>
    [Theory]
    [InlineData("escape")]
    [InlineData("direct")]
    [InlineData("popup-direct")]
    [InlineData("unavailable")]
    public async Task Keyboard_WhenPopupNavigationUsesMountedCalendarRoute_MovesOnceAndRollsBackAsync(
        string closePath)
    {
        // Arrange
        var openingDate = new DateOnly(2026, 3, 15);
        var input = new DateInput
        {
            Value = openingDate,
            Culture = CultureInfo.InvariantCulture
        };
        var closed = 0;
        input.DropDownClosed += (_, _) => closed++;
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        var popup = OwnedTree.Find<Popup>(input).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Alt);
        surface.ShouldHaveFocus(calendar);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.RepeatAsync(Code.Right);

        // Assert provisional exact-once movement, then cancellation rollback.
        calendar.ActiveDate.ShouldBe(openingDate.AddDays(2));
        input.Value.ShouldBe(openingDate);

        if (closePath == "escape")
        {
            await surface.Keyboard.PressAsync(Code.Escape);
        }
        else if (closePath == "direct")
        {
            await surface.UpdateAsync(() => input.IsOpen = false, "close DateInput directly");
        }
        else if (closePath == "popup-direct")
        {
            await surface.UpdateAsync(() => popup.IsOpen = false, "close the owned DateInput popup directly");
        }
        else
        {
            await surface.UpdateAsync(() => input.IsEnabled = false, "make DateInput unavailable");
        }

        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(openingDate);
        calendar.ActiveDate.ShouldBe(openingDate);
        closed.ShouldBe(1);
    }

    /// <summary>Verifies tightening bounds while a session is open supersedes an opening cursor
    /// that is no longer valid and cancellation preserves the repaired committed value.</summary>
    [Fact]
    public async Task Keyboard_WhenMinimumTightensDuringOpenSession_EscapePreservesRepairedDateAsync()
    {
        var openingDate = new DateOnly(2026, 3, 15);
        var repairedDate = new DateOnly(2026, 3, 20);
        var input = new DateInput
        {
            Value = openingDate,
            Culture = CultureInfo.InvariantCulture
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Alt);
        await surface.Keyboard.PressAsync(Code.Right);

        await surface.UpdateAsync(() => input.Minimum = repairedDate, "tighten DateInput minimum while open");
        await surface.Keyboard.PressAsync(Code.Escape);

        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(repairedDate);
        calendar.ActiveDate.ShouldBe(repairedDate);
    }

    /// <summary>Verifies a committed value mutation while the Calendar is open supersedes the
    /// opening cursor snapshot instead of leaving the calendar on stale state after Escape.</summary>
    [Fact]
    public async Task Keyboard_WhenValueChangesExternallyDuringOpenSession_EscapePreservesNewDateAsync()
    {
        var openingDate = new DateOnly(2026, 3, 15);
        var replacementDate = new DateOnly(2026, 4, 20);
        var input = new DateInput
        {
            Value = openingDate,
            Culture = CultureInfo.InvariantCulture
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Alt);
        await surface.Keyboard.PressAsync(Code.Right);

        await surface.UpdateAsync(() => input.Value = replacementDate, "replace DateInput value while open");
        await surface.Keyboard.PressAsync(Code.Escape);

        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(replacementDate);
        calendar.ActiveDate.ShouldBe(replacementDate);
    }

    /// <summary>Verifies Enter and Space accept an unchanged provisional date and close the
    /// Calendar-focused session.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Keyboard_WhenEnterOrSpaceAcceptsOpeningDate_ClosesCalendarSessionAsync(bool useSpace)
    {
        // Arrange
        var openingDate = new DateOnly(2026, 3, 15);
        var input = new DateInput
        {
            Value = openingDate,
            Culture = CultureInfo.InvariantCulture
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Alt);
        surface.ShouldHaveFocus(calendar);

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
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(openingDate);
    }

    /// <summary>Verifies light dismissal restores the opening Calendar cursor without changing
    /// the committed date.</summary>
    [Fact]
    public async Task Pointer_WhenPopupIsLightDismissedAfterBrowsing_RestoresOpeningDateAsync()
    {
        // Arrange
        var openingDate = new DateOnly(2026, 3, 15);
        var input = new DateInput
        {
            Value = openingDate,
            Culture = CultureInfo.InvariantCulture
        };
        var outside = new ControlText("x");
        Overlay.SetTop(outside, Length.Cells(19));
        Overlay.SetLeft(outside, Length.Cells(29));
        var root = new Overlay { Children = { input, outside } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 20),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateInput popup");
        await surface.Keyboard.PressAsync(Code.Right);
        calendar.ActiveDate.ShouldBe(openingDate.AddDays(1));

        // Act
        await surface.Pointer.ClickAsync(outside);

        // Assert
        input.IsOpen.ShouldBeFalse();
        input.Value.ShouldBe(openingDate);
        calendar.ActiveDate.ShouldBe(openingDate);
    }

    /// <summary>Verifies a Calendar pointer activation commits the clicked date before explicitly
    /// accepting and closing the popup session.</summary>
    [Fact]
    public async Task Pointer_WhenCalendarDateIsClicked_AcceptsDateAndClosesAsync()
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
            new Size(32, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateInput popup");

        // Act - March 16 is the Monday cell on the third displayed week row.
        await surface.Pointer.ClickAsync(calendar, new Point(6, 5));

        // Assert
        input.Value.ShouldBe(new DateOnly(2026, 3, 16));
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies clicking the already-selected Calendar date still accepts the semantic
    /// activation and closes even though the Calendar selection does not mutate.</summary>
    [Fact]
    public async Task Pointer_WhenSelectedCalendarDateIsClicked_AcceptsAndClosesAsync()
    {
        // Arrange
        var openingDate = new DateOnly(2026, 3, 15);
        var input = new DateInput
        {
            Value = openingDate,
            Culture = CultureInfo.InvariantCulture
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(32, 15),
            TestContext.Current.CancellationToken);
        var calendar = OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull();
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateInput popup");

        // Act - March 15 is the Sunday cell on the third displayed week row.
        await surface.Pointer.ClickAsync(calendar, new Point(2, 5));

        // Assert
        input.Value.ShouldBe(openingDate);
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a close callback that reopens after keyboard acceptance establishes a
    /// replacement session that the completed activation cannot close again.</summary>
    [Fact]
    public async Task Keyboard_WhenAcceptedCloseReopens_PreservesReplacementSessionAsync()
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
        await surface.UpdateAsync(() => input.IsOpen = true, "open DateInput popup");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        closed.ShouldBe(1);
        input.IsOpen.ShouldBeTrue();
    }
}
