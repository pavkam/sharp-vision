// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using UiCalendar = SharpVision.Controls.Input.Calendar;

/// <summary>Proves the detached public Calendar contract and date geometry.</summary>
public sealed class CalendarTests
{
    #region Interaction and rendering

    /// <summary>Verifies Space press activates the active date while release remains inert.</summary>
    [ComponentUnitEvidence(typeof(UiCalendar))]
    [Fact]
    public void Dispatch_WhenSpaceIsPressed_ActivatesOnlyPress()
    {
        // Arrange
        var active = new DateOnly(2026, 7, 19);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.Selection = null;

        // Act
        var pressed = CharacterKey(calendar, new Rune(' '), KeyAction.Press);
        var released = CharacterKey(calendar, new Rune(' '), KeyAction.Release);

        // Assert
        pressed.Handled.ShouldBeTrue();
        released.Handled.ShouldBeFalse();
        calendar.Selection.ShouldBe(new DateInterval(active, active));
    }

    /// <summary>Verifies keys outside the calendar command set remain available to routed input.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsUnhandled_RaisesInheritedKeyDownWithoutConsumingIt()
    {
        // Arrange
        using var calendar = new UiCalendar();
        var raised = 0;
        calendar.KeyDown += (_, _) => raised++;

        // Act
        var eventArgs = Key(calendar, Code.F1);

        // Assert
        eventArgs.Handled.ShouldBeFalse();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies paging from a long month clamps to the adjacent month's last day.</summary>
    [Fact]
    public void Dispatch_WhenPagingIntoShortMonth_ClampsActiveDay()
    {
        // Arrange
        var initial = new DateOnly(2025, 1, 31);
        using var calendar = new UiCalendar { Selection = new DateInterval(initial, initial) };
        calendar.Selection = null;

        // Act
        var eventArgs = Key(calendar, Code.PageDown);

        // Assert
        eventArgs.Handled.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(new DateOnly(2025, 2, 28));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2025, 2, 1));
    }

    /// <summary>Verifies header and wheel month navigation share bounded month arithmetic.</summary>
    [Fact]
    public void Dispatch_WhenHeaderAndWheelNavigate_ChangesDisplayedMonthOnly()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1)
        };
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        var active = calendar.ActiveDate;
        var inset = calendar.ActualStyle.ContentInset;
        var header = new PointerEventArgs(Pointer(
            new Point(calendar.ContentBounds.Right - inset.Right - 1, calendar.ContentBounds.Y + inset.Top),
            PointerAction.Press));

        // Act
        _ = Router.Route(calendar, Events.Pointer, header);
        var wheel = new PointerEventArgs(Wheel(1));
        _ = Router.Route(calendar, Events.Pointer, wheel);

        // Assert
        header.Handled.ShouldBeTrue();
        wheel.Handled.ShouldBeTrue();
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 7, 1));
        calendar.ActiveDate.ShouldBe(active);
    }

    /// <summary>Verifies zero and tiny allocations clip without escaping the frame.</summary>
    [Fact]
    public void Render_WhenBoundsAreTiny_DoesNotEscape()
    {
        // Arrange
        using var calendar = new UiCalendar { Bounds = default };
        using Frame empty = new(new Size(1, 1));

        // Act and assert zero
        Should.NotThrow(() => calendar.Render(empty.Canvas));

        // Arrange tiny
        calendar.Bounds = new Rect(0, 0, 3, 3);
        using Frame tiny = new(new Size(3, 3));

        // Act and assert tiny
        Should.NotThrow(() => calendar.Render(tiny.Canvas));
        FrameOracle.Get(tiny, default).ShouldBe("┏");
    }

    /// <summary>Verifies a wide grapheme that cannot wholly fit is omitted rather than split.</summary>
    [Fact]
    public void SetMarkup_WhenWideGraphemeCrossesFaceEdge_ClipsWholeGrapheme()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1)
        };
        calendar.SetMarkup(new DateOnly(2026, 7, 19), "abc界");
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        using Frame frame = new(new Size(32, 10));

        // Act
        calendar.Render(frame.Canvas);

        // Assert
        Row(frame, 6).ShouldStartWith("┃ abc ");
        frame.GetCell(new Point(5, 6)).IsContinuation.ShouldBeFalse();
    }

    /// <summary>Verifies the final supported month renders without date arithmetic overflow.</summary>
    [Fact]
    public void Render_WhenMaximumMonthIsDisplayed_DoesNotOverflow()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = DateOnly.MaxValue
        };
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        using Frame frame = new(new Size(32, 10));

        // Act and assert
        Should.NotThrow(() => calendar.Render(frame.Canvas));
        Row(frame, 1).ShouldContain("December 9999");
    }

    /// <summary>Verifies convenience methods preserve the same atomic selection contract.</summary>
    [Fact]
    public void Select_WhenIntervalModeIsActive_CommitsAndClearsAtomically()
    {
        // Arrange
        using var calendar = new UiCalendar { SelectionMode = CalendarSelectionMode.Interval };
        var start = new DateOnly(2026, 7, 10);
        var end = new DateOnly(2026, 7, 19);

        // Act
        var selected = calendar.Select(start, end);
        var cleared = calendar.ClearSelection();

        // Assert
        selected.ShouldBeTrue();
        cleared.ShouldBeTrue();
        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies a programmatic commit replaces any pending interval gesture.</summary>
    [Fact]
    public void Select_WhenIntervalAnchorIsPending_ClearsAnchorBeforeRaisingEvent()
    {
        // Arrange
        using var calendar = new UiCalendar { SelectionMode = CalendarSelectionMode.Interval };
        var anchor = new DateOnly(2026, 7, 10);
        var start = new DateOnly(2026, 7, 14);
        var end = new DateOnly(2026, 7, 19);
        _ = calendar.ActivateDate(anchor);
        DateOnly? observedAnchor = anchor;
        calendar.SelectionChanged += (_, _) => observedAnchor = calendar.IntervalAnchor;

        // Act
        _ = calendar.Select(start, end);

        // Assert
        calendar.IntervalAnchor.ShouldBeNull();
        observedAnchor.ShouldBeNull();
    }

    /// <summary>Verifies a primary day press uses the same select-mode activation path.</summary>
    [Fact]
    public void Dispatch_WhenDateIsPressed_SelectsMappedDay()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1)
        };
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        var inset = calendar.ActualStyle.ContentInset;
        var eventArgs = new PointerEventArgs(Pointer(
            new Point(calendar.ContentBounds.X + inset.Left, calendar.ContentBounds.Y + inset.Top + 5),
            PointerAction.Press));

        // Act
        _ = Router.Route(calendar, Events.Pointer, eventArgs);

        // Assert
        eventArgs.Handled.ShouldBeTrue();
        calendar.Selection.ShouldBe(new DateInterval(
            new DateOnly(2026, 7, 19),
            new DateOnly(2026, 7, 19)));
    }

    /// <summary>Verifies Home stays inside the current week when its first date is blocked.</summary>
    [Fact]
    public void Dispatch_WhenWeekStartIsBlocked_MovesHomeInward()
    {
        // Arrange
        var active = new DateOnly(2026, 7, 15);
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            Selection = new DateInterval(active, active)
        };
        calendar.Selection = null;
        calendar.BlockedDates.Block(new DateOnly(2026, 7, 12));

        // Act
        var eventArgs = Key(calendar, Code.Home);

        // Assert
        eventArgs.Handled.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 13));
    }

    /// <summary>Verifies directional movement skips blocked dates and follows the active month.</summary>
    [Fact]
    public void Dispatch_WhenRightMovesAcrossMonth_SkipsBlockedDate()
    {
        // Arrange
        var initial = new DateOnly(2026, 7, 31);
        using var calendar = new UiCalendar { Selection = new DateInterval(initial, initial) };
        calendar.Selection = null;
        calendar.BlockedDates.Block(new DateOnly(2026, 8, 1));

        // Act
        var eventArgs = Key(calendar, Code.Right);

        // Assert
        eventArgs.Handled.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 8, 2));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 8, 1));
        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies the configured week start changes the rendered weekday order.</summary>
    [Fact]
    public void Render_WhenFirstDayOfWeekIsConfigured_UsesConfiguredColumnOrder()
    {
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            FirstDayOfWeek = DayOfWeek.Monday,
            DisplayMonth = new DateOnly(2026, 7, 1)
        };
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        using Frame frame = new(new Size(32, 10));

        calendar.Render(frame.Canvas);

        Row(frame, 2).ShouldBe("┃  Mo  Tu  We  Th  Fr  Sa  Su  ┃");
    }

    /// <summary>Verifies GoToToday moves the active date and display when today is selectable.</summary>
    [Fact]
    public void GoToToday_WhenTodayIsSelectable_MovesActiveDateAndMonth()
    {
        using var calendar = new UiCalendar
        {
            DisplayMonth = new DateOnly(2000, 1, 1),
            Selection = null
        };

        var expected = DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
        var moved = calendar.GoToToday();

        moved.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(expected);
        calendar.DisplayMonth.ShouldBe(new DateOnly(expected.Year, expected.Month, 1));
    }

    /// <summary>Verifies sparse markup replaces one date face without changing grid geometry.</summary>
    [Fact]
    public void SetMarkup_WhenDateIsVisible_ReplacesCompleteDayFace()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1)
        };
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        using Frame frame = new(new Size(32, 10));

        // Act
        calendar.SetMarkup(new DateOnly(2026, 7, 19), "<fg=brightcyan><b>VIP</b></fg>");
        calendar.Render(frame.Canvas);

        // Assert
        Row(frame, 6).ShouldStartWith("┃ VIP ");
        calendar.DesiredSize.ShouldBe(new Size(32, 10));
    }

    /// <summary>Verifies markup with no visible face is rejected before replacing prior content.</summary>
    [Fact]
    public void SetMarkup_WhenVisibleFaceIsEmpty_ThrowsBeforeMutation()
    {
        // Arrange
        var date = new DateOnly(2026, 7, 19);
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1)
        };
        calendar.SetMarkup(date, "VIP");

        // Act
        var exception = Should.Throw<ArgumentException>(() => calendar.SetMarkup(date, "<b></b>"));

        // Assert
        exception.ParamName.ShouldBe("markup");
        calendar.GetMarkup(date).ShouldBe("VIP");
    }

    /// <summary>Verifies the default chrome contains a complete fixed six-week month grid.</summary>
    [Fact]
    public void Render_WhenMonthIsDisplayed_WritesExactCalendarRows()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1)
        };
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        using Frame frame = new(new Size(32, 10));

        // Act
        calendar.Render(frame.Canvas);

        // Assert
        calendar.DesiredSize.ShouldBe(new Size(32, 10));
        Row(frame, 1).ShouldBe("┃ <        July 2026         > ┃");
        Row(frame, 2).ShouldBe("┃  Su  Mo  Tu  We  Th  Fr  Sa  ┃");
        Row(frame, 3).ShouldBe("┃  28  29  30   1   2   3   4  ┃");
        Row(frame, 8).ShouldBe("┃   2   3   4   5   6   7   8  ┃");
    }

    #endregion

    #region Style surface

    /// <summary>Verifies a default unstyled Calendar falls back to the semantic input profile.</summary>
    [Fact]
    public void Constructor_WhenStyleIsUnset_FallsBackToDefaultCalendarStyle()
    {
        // Arrange and act
        var theme = new Theme();
        theme.Freeze();
        using var calendar = new UiCalendar();
        calendar.SetTheme(theme);

        // Assert
        calendar.Style.ShouldBeNull();
        calendar.ActualStyle.ShouldBe(CalendarStyle.Default);
        calendar.ActualStyle.SelectedDayColor.ShouldBe((ColorValue) ThemeColor.SelectedText);
        calendar.ActualStyle.TodayMarkerColor.ShouldBe((ColorValue) ThemeColor.ActiveText);
        calendar.ActualStyle.OutOfMonthDayColor.ShouldBe((ColorValue) ThemeColor.Muted);
        calendar.ActualStyle.WeekdayHeaderColor.ShouldBe((ColorValue) ThemeColor.Muted);
        calendar.ActualStyle.DisabledDayColor.ShouldBe((ColorValue) ThemeColor.DisabledText);
        calendar.ActualStyle.ContentInset.ShouldBe(new Thickness(horizontal: 1, vertical: 0));
        calendar.Padding.ShouldBe(default);
    }

    /// <summary>Verifies assigning a local style propagates to ActualStyle and reports one notification,
    /// while a no-op reassignment of an equal style raises no duplicate ActualStyle notification.</summary>
    [Fact]
    public void Style_WhenAssigned_PropagatesOnceWithoutDuplicateOnNoOpReassignment()
    {
        // Arrange
        var style = CalendarStyle.Default.With(selectedDayColor: Color.Rgb(10, 20, 30));
        using var calendar = new UiCalendar();
        var names = new List<string?>();
        calendar.PropertyChanged += (_, eventArgs) => names.Add(eventArgs.PropertyName);

        // Act
        calendar.Style = style;

        // Assert
        calendar.Style.ShouldBe(style);
        calendar.ActualStyle.ShouldBe(style);
        names.ShouldContain(nameof(UiCalendar.Style));
        names.Count(name => name == nameof(UiCalendar.ActualStyle)).ShouldBe(1);

        // Act no-op reassignment
        names.Clear();
        calendar.Style = style;

        // Assert no duplicate notification
        names.ShouldBeEmpty();
    }

    /// <summary>Verifies restyling the content inset remeasures without overwriting caller padding.</summary>
    [Fact]
    public void Style_WhenContentInsetChanges_RemeasuresAndUpdatesPadding()
    {
        // Arrange
        using var calendar = new UiCalendar();
        calendar.Padding = new Thickness(3);
        calendar.Clear(Invalidation.All);
        var restyled = CalendarStyle.Default.With(contentInset: new Thickness(horizontal: 2, vertical: 1));

        // Act
        calendar.Style = restyled;

        // Assert
        calendar.Padding.ShouldBe(new Thickness(3));
        calendar.Pending.ShouldBe(Invalidation.All);
    }

    #endregion

    #region State and blocked ranges

    /// <summary>Verifies an attached blocked-range collection rejects off-dispatcher mutation.</summary>
    [Fact]
    public async Task Block_WhenAttachedOwnerIsMutatedOffDispatcher_ThrowsBeforeMutationAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var calendar = await dispatcher.InvokeAsync(() =>
        {
            var created = new UiCalendar();
            created.Attach(dispatcher);
            return created;
        }, TestContext.Current.CancellationToken);

        try
        {
            // Act
            var exception = await Should.ThrowAsync<InvalidOperationException>(() => Task.Run(() =>
                calendar.BlockedDates.Block(new DateOnly(2026, 7, 19))));

            // Assert
            exception.Message.ShouldNotBeNullOrWhiteSpace();
            calendar.BlockedDates.Count.ShouldBe(0);
        }
        finally
        {
            await dispatcher.InvokeAsync(calendar.Dispose, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies select mode rejects a multi-day value without losing prior state.</summary>
    [Fact]
    public void Selection_WhenSelectModeReceivesInterval_ThrowsBeforeMutation()
    {
        // Arrange
        var existing = new DateInterval(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 10));
        using var calendar = new UiCalendar { Selection = existing };
        var requested = new DateInterval(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14));

        // Act
        var exception = Should.Throw<ArgumentException>(() => calendar.Selection = requested);

        // Assert
        exception.ParamName.ShouldBe("value");
        calendar.Selection.ShouldBe(existing);
    }

    /// <summary>Verifies a new blocked range clears an intersecting selection exactly once.</summary>
    [Fact]
    public void Block_WhenRangeIntersectsSelection_ClearsSelection()
    {
        // Arrange
        var date = new DateOnly(2026, 7, 19);
        using var calendar = new UiCalendar { Selection = new DateInterval(date, date) };
        var changes = 0;
        calendar.SelectionChanged += (_, eventArgs) =>
        {
            eventArgs.PreviousSelection.ShouldBe(new DateInterval(date, date));
            eventArgs.Selection.ShouldBeNull();
            changes++;
        };

        // Act
        calendar.BlockedDates.Block(date);

        // Assert
        calendar.Selection.ShouldBeNull();
        changes.ShouldBe(1);
    }

    /// <summary>Verifies a selection outside calendar bounds is rejected before mutation.</summary>
    [Fact]
    public void Selection_WhenOutsideBounds_ThrowsBeforeMutation()
    {
        // Arrange
        var existing = new DateInterval(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 12));
        using var calendar = new UiCalendar
        {
            MinimumDate = new DateOnly(2026, 7, 10),
            MaximumDate = new DateOnly(2026, 7, 20),
            Selection = existing
        };

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            calendar.Selection = new DateInterval(new DateOnly(2026, 7, 9), new DateOnly(2026, 7, 9)));

        // Assert
        exception.ParamName.ShouldBe("value");
        calendar.Selection.ShouldBe(existing);
    }

    /// <summary>Verifies non-Gregorian display cultures cannot produce false month geometry.</summary>
    [Fact]
    public void Culture_WhenCalendarIsNonGregorian_ThrowsBeforeMutation()
    {
        // Arrange
        using var calendar = new UiCalendar { Culture = CultureInfo.InvariantCulture };
        var requested = new CultureInfo("ar-SA");

        // Act
        var exception = Should.Throw<ArgumentException>(() => calendar.Culture = requested);

        // Assert
        exception.ParamName.ShouldBe("value");
        calendar.Culture.ShouldBe(CultureInfo.InvariantCulture);
    }

    /// <summary>Verifies interval mode orders reverse activations and commits once complete.</summary>
    [Fact]
    public void ActivateDate_WhenIntervalModeReceivesTwoDates_CommitsOrderedInterval()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            DisplayMonth = new DateOnly(2026, 7, 1),
            SelectionMode = CalendarSelectionMode.Interval
        };
        var changes = 0;
        calendar.SelectionChanged += (_, _) => changes++;

        // Act
        var anchored = calendar.ActivateDate(new DateOnly(2026, 7, 19));
        var committed = calendar.ActivateDate(new DateOnly(2026, 7, 10));

        // Assert
        anchored.ShouldBeTrue();
        committed.ShouldBeTrue();
        calendar.Selection.ShouldBe(new DateInterval(
            new DateOnly(2026, 7, 10),
            new DateOnly(2026, 7, 19)));
        calendar.IntervalAnchor.ShouldBeNull();
        changes.ShouldBe(1);
    }

    /// <summary>Verifies an unavailable interior date prevents an interval commit.</summary>
    [Fact]
    public void ActivateDate_WhenIntervalCrossesBlockedDate_PreservesAnchor()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            DisplayMonth = new DateOnly(2026, 7, 1),
            SelectionMode = CalendarSelectionMode.Interval
        };
        var anchor = new DateOnly(2026, 7, 10);
        calendar.BlockedDates.Block(new DateOnly(2026, 7, 12));
        _ = calendar.ActivateDate(anchor);

        // Act
        var committed = calendar.ActivateDate(new DateOnly(2026, 7, 14));

        // Assert
        committed.ShouldBeFalse();
        calendar.Selection.ShouldBeNull();
        calendar.IntervalAnchor.ShouldBe(anchor);
    }

    /// <summary>Verifies changing interaction modes clears incompatible state deterministically.</summary>
    [Fact]
    public void SelectionMode_WhenChanged_ClearsSelectionAndAnchor()
    {
        // Arrange
        using var calendar = new UiCalendar { Selection = new DateInterval(new DateOnly(2026, 7, 19), new DateOnly(2026, 7, 19)) };

        // Act
        calendar.SelectionMode = CalendarSelectionMode.Interval;
        _ = calendar.ActivateDate(new DateOnly(2026, 7, 20));
        calendar.SelectionMode = CalendarSelectionMode.Select;

        // Assert
        calendar.Selection.ShouldBeNull();
        calendar.IntervalAnchor.ShouldBeNull();
    }

    /// <summary>Verifies select mode commits one date before publishing its change.</summary>
    [Fact]
    public void Selection_WhenSelectModeDateIsAssigned_CommitsBeforeEvent()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            DisplayMonth = new DateOnly(2026, 7, 19)
        };
        DateInterval? observed = null;
        calendar.SelectionChanged += (_, eventArgs) =>
        {
            calendar.Selection.ShouldBe(eventArgs.Selection);
            observed = eventArgs.Selection;
        };
        var date = new DateOnly(2026, 7, 19);

        // Act
        calendar.Selection = new DateInterval(date, date);

        // Assert
        calendar.SelectionMode.ShouldBe(CalendarSelectionMode.Select);
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 7, 1));
        calendar.ActiveDate.ShouldBe(date);
        observed.ShouldBe(new DateInterval(date, date));
    }

    /// <summary>Verifies touching ranges become one bounded lookup interval.</summary>
    [Fact]
    public void Block_WhenRangesOverlapOrTouch_CoalescesThem()
    {
        // Arrange
        using var calendar = new UiCalendar();
        calendar.BlockedDates.Block(new DateInterval(
            new DateOnly(2026, 7, 10),
            new DateOnly(2026, 7, 12)));

        // Act
        calendar.BlockedDates.Block(new DateInterval(
            new DateOnly(2026, 7, 13),
            new DateOnly(2026, 7, 15)));

        // Assert
        calendar.BlockedDates.ShouldBe(
        [
            new DateInterval(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 15))
        ]);
    }

    /// <summary>Verifies removing an interior date preserves both remaining sides.</summary>
    [Fact]
    public void Unblock_WhenDateBisectsRange_SplitsIt()
    {
        // Arrange
        using var calendar = new UiCalendar();
        calendar.BlockedDates.Block(new DateInterval(
            new DateOnly(2026, 7, 10),
            new DateOnly(2026, 7, 12)));

        // Act
        calendar.BlockedDates.Unblock(new DateOnly(2026, 7, 11));

        // Assert
        calendar.BlockedDates.Contains(new DateOnly(2026, 7, 10)).ShouldBeTrue();
        calendar.BlockedDates.Contains(new DateOnly(2026, 7, 11)).ShouldBeFalse();
        calendar.BlockedDates.Contains(new DateOnly(2026, 7, 12)).ShouldBeTrue();
    }

    /// <summary>Verifies range normalization does not overflow at DateOnly endpoints.</summary>
    [Fact]
    public void Block_WhenRangesTouchDateLimits_DoesNotOverflow()
    {
        // Arrange
        using var calendar = new UiCalendar();

        // Act
        calendar.BlockedDates.Block(DateOnly.MinValue);
        calendar.BlockedDates.Block(DateOnly.MinValue.AddDays(1));
        calendar.BlockedDates.Block(DateOnly.MaxValue.AddDays(-1));
        calendar.BlockedDates.Block(DateOnly.MaxValue);

        // Assert
        calendar.BlockedDates.Count.ShouldBe(2);
        calendar.BlockedDates.Contains(DateOnly.MinValue).ShouldBeTrue();
        calendar.BlockedDates.Contains(DateOnly.MaxValue).ShouldBeTrue();
    }

    /// <summary>Verifies an interval rejects reversed endpoints before a value exists.</summary>
    [Fact]
    public void Constructor_WhenEndPrecedesStart_Throws()
    {
        // Arrange
        var start = new DateOnly(2026, 7, 19);
        var end = start.AddDays(-1);

        // Act
        Action action = () => _ = new DateInterval(start, end);

        // Assert
        var exception = action.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("end");
    }

    /// <summary>Verifies both inclusive endpoints participate in an interval.</summary>
    [Fact]
    public void Contains_WhenDateIsAtEitherEndpoint_ReturnsTrue()
    {
        // Arrange
        var interval = new DateInterval(
            new DateOnly(2026, 7, 10),
            new DateOnly(2026, 7, 19));

        // Act
        var containsStart = interval.Contains(interval.Start);
        var containsEnd = interval.Contains(interval.End);

        // Assert
        containsStart.ShouldBeTrue();
        containsEnd.ShouldBeTrue();
    }

    #endregion

    #region Helpers

    private static string Row(Frame frame, int y)
    {
        var result = new StringBuilder(frame.Size.Width);

        for (var x = 0; x < frame.Size.Width; x++)
        {
            var text = FrameOracle.Get(frame, new Point(x, y));
            _ = result.Append(text.Length == 0 ? " " : text);
        }

        return result.ToString();
    }

    private static KeyEventArgs Key(
        UiCalendar calendar,
        Code code,
        KeyAction action = KeyAction.Press)
    {
        var eventArgs = new KeyEventArgs(new Stroke(
            code,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            action));
        _ = Router.Route(calendar, Events.Key, eventArgs);
        return eventArgs;
    }

    private static KeyEventArgs CharacterKey(UiCalendar calendar, Rune character, KeyAction action)
    {
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Character,
            character,
            nativeCode: 0,
            Modifiers.None,
            action));
        _ = Router.Route(calendar, Events.Key, eventArgs);
        return eventArgs;
    }

    private static Pointer Pointer(Point cells, PointerAction action) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);

    private static Pointer Wheel(int wheelY) => new(
        cells: default,
        pixels: null,
        Buttons.None,
        PointerAction.Wheel,
        wheelX: 0,
        wheelY,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);

    #endregion
}
