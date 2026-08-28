// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;


/// <summary>Proves the detached public Calendar contract and date geometry.</summary>
public sealed class CalendarTests
{
    /// <summary>Verifies culture-derived weekday state follows the newest reentrant culture.</summary>
    [Fact]
    public void Culture_WhenPropertyObserverCommitsNewerCulture_DerivesNewestFirstDayOfWeek()
    {
        var outer = CultureInfo.GetCultureInfo("de-DE");
        var nested = CultureInfo.GetCultureInfo("en-US");
        using var calendar = new UiCalendar();
        calendar.Culture = CultureInfo.InvariantCulture;
        calendar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(UiCalendar.Culture) && calendar.Culture.Equals(outer))
            {
                calendar.Culture = nested;
            }
        };

        calendar.Culture = outer;

        calendar.Culture.ShouldBe(nested);
        calendar.FirstDayOfWeek.ShouldBe(nested.DateTimeFormat.FirstDayOfWeek);
    }
    /// <summary>Verifies ActiveDate publication cannot overwrite a newer nested selection.</summary>
    [Fact]
    public void Selection_WhenActiveDateObserverSelectsNewerDate_PreservesNewerTransaction()
    {
        var first = new DateOnly(2026, 8, 10);
        var second = new DateOnly(2026, 8, 20);
        using var calendar = new UiCalendar();
        var observations = new List<(DateInterval? EventValue, DateInterval? LiveValue)>();
        calendar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(UiCalendar.ActiveDate) && calendar.ActiveDate == first)
            {
                calendar.Selection = new DateInterval(second, second);
            }
        };
        calendar.SelectionChanged += (_, eventArgs) =>
            observations.Add((eventArgs.Selection, calendar.Selection));

        calendar.Selection = new DateInterval(first, first);

        calendar.Selection.ShouldBe(new DateInterval(second, second));
        calendar.ActiveDate.ShouldBe(second);
        observations.ShouldBe([(new DateInterval(second, second), new DateInterval(second, second))]);
    }

    /// <summary>Verifies the same transaction ownership applies to multi-day intervals spanning
    /// display months, keeping navigation and selection aligned with the nested commit.</summary>
    [Fact]
    public void Selection_WhenActiveDateObserverSelectsNewerInterval_PreservesNavigationAndEventOrder()
    {
        var outer = new DateInterval(new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 2));
        var nested = new DateInterval(new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 9));
        using var calendar = new UiCalendar { SelectionMode = CalendarSelectionMode.Interval };
        var observations = new List<(DateInterval? Selection, DateOnly ActiveDate, DateOnly DisplayMonth)>();
        calendar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(UiCalendar.ActiveDate) && calendar.ActiveDate == outer.Start)
            {
                calendar.Selection = nested;
            }
        };
        calendar.SelectionChanged += (_, eventArgs) =>
            observations.Add((eventArgs.Selection, calendar.ActiveDate, calendar.DisplayMonth));

        calendar.Selection = outer;

        calendar.Selection.ShouldBe(nested);
        calendar.ActiveDate.ShouldBe(nested.Start);
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 10, 1));
        observations.ShouldBe([(nested, nested.Start, new DateOnly(2026, 10, 1))]);
    }

    #region Interaction and rendering

    /// <summary>Verifies Space press activates the active date while release remains inert.</summary>
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
        pressed.IsHandled.ShouldBeTrue();
        released.IsHandled.ShouldBeFalse();
        calendar.Selection.ShouldBe(new DateInterval(active, active));
    }

    /// <summary>Verifies a disabled Calendar refuses Space activation and leaves selection unchanged.</summary>
    [Fact]
    public void Dispatch_WhenDisabled_RefusesSpaceActivation()
    {
        // Arrange
        var active = new DateOnly(2026, 7, 19);
        using var calendar = new UiCalendar
        {
            Selection = new DateInterval(active, active),
            IsEnabled = false
        };
        calendar.Selection = null;

        // Act
        var pressed = CharacterKey(calendar, new Rune(' '), KeyAction.Press);

        // Assert
        pressed.IsHandled.ShouldBeFalse();
        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies an incidental Control modifier on Space does not activate the active date,
    /// and leaves the stroke unhandled so a shortcut bound to the modified combination still sees it.</summary>
    [Fact]
    public void Dispatch_WhenSpaceHasControlModifier_DoesNotActivateAndLeavesUnhandled()
    {
        var active = new DateOnly(2026, 7, 19);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.Selection = null;

        var pressed = CharacterKey(calendar, new Rune(' '), KeyAction.Press, Modifiers.Control);

        pressed.IsHandled.ShouldBeFalse();
        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies Shift-held Space (a common terminal chord) still activates the active date.</summary>
    [Fact]
    public void Dispatch_WhenSpaceHasShiftModifier_StillActivates()
    {
        var active = new DateOnly(2026, 7, 19);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.Selection = null;

        var pressed = CharacterKey(calendar, new Rune(' '), KeyAction.Press, Modifiers.Shift);

        pressed.IsHandled.ShouldBeTrue();
        calendar.Selection.ShouldBe(new DateInterval(active, active));
    }

    /// <summary>Verifies an incidental Control modifier on Enter does not activate the active date.</summary>
    [Fact]
    public void Dispatch_WhenEnterHasControlModifier_DoesNotActivateAndLeavesUnhandled()
    {
        var active = new DateOnly(2026, 7, 19);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.Selection = null;

        var pressed = Key(calendar, Code.Enter, modifiers: Modifiers.Control);

        pressed.IsHandled.ShouldBeFalse();
        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) still activates the active date.</summary>
    [Fact]
    public void Dispatch_WhenEnterHasShiftModifier_StillActivates()
    {
        var active = new DateOnly(2026, 7, 19);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.Selection = null;

        var pressed = Key(calendar, Code.Enter, modifiers: Modifiers.Shift);

        pressed.IsHandled.ShouldBeTrue();
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
        eventArgs.IsHandled.ShouldBeFalse();
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
        eventArgs.IsHandled.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(new DateOnly(2025, 2, 28));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2025, 2, 1));
    }

    /// <summary>Verifies delegated initial and repeated movement has the same modifier and
    /// navigation behavior as the Calendar's ordinarily routed key path.</summary>
    [Fact]
    public void HandleNavigationKey_WhenDelegated_MatchesOrdinarilyRoutedMovement()
    {
        var initial = new DateOnly(2026, 7, 19);
        using var ordinary = CreateNavigationCalendar(initial);
        using var delegated = CreateNavigationCalendar(initial);
        var ordinaryInitial = Key(ordinary, Code.Right, KeyAction.Press);
        var delegatedInitial = NavigationKey(Code.Right, KeyAction.Press, Modifiers.None);
        var ordinaryRepeated = Key(ordinary, Code.Right, KeyAction.Repeat);
        var delegatedRepeated = NavigationKey(Code.Right, KeyAction.Repeat, Modifiers.None);
        var ordinaryRejected = Key(ordinary, Code.Right, KeyAction.Press, Modifiers.Control);
        var delegatedRejected = NavigationKey(Code.Right, KeyAction.Press, Modifiers.Control);

        var initialHandled = delegated.HandleNavigationKey(delegatedInitial);
        var repeatedHandled = delegated.HandleNavigationKey(delegatedRepeated);
        var rejectedHandled = delegated.HandleNavigationKey(delegatedRejected);

        initialHandled.ShouldBe(ordinaryInitial.IsHandled);
        repeatedHandled.ShouldBe(ordinaryRepeated.IsHandled);
        rejectedHandled.ShouldBe(ordinaryRejected.IsHandled);
        delegatedInitial.IsHandled.ShouldBeFalse();
        delegatedRepeated.IsHandled.ShouldBeFalse();
        delegatedRejected.IsHandled.ShouldBeFalse();
        delegated.ActiveDate.ShouldBe(ordinary.ActiveDate);
        delegated.DisplayMonth.ShouldBe(ordinary.DisplayMonth);
        delegated.Selection.ShouldBe(ordinary.Selection);
    }

    /// <summary>Verifies delegated activation preserves ordinary routed initial, repeat, and
    /// modifier behavior for both Calendar activation keys.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HandleNavigationKey_WhenActivationIsDelegated_MatchesOrdinarilyRoutedCalendar(
        bool useSpace)
    {
        var initial = new DateOnly(2026, 7, 19);
        using var ordinary = CreateNavigationCalendar(initial);
        using var delegated = CreateNavigationCalendar(initial);
        List<DateInterval?> ordinaryChanges = [];
        List<DateInterval?> delegatedChanges = [];
        ordinary.SelectionChanged += (_, eventArgs) => ordinaryChanges.Add(eventArgs.Selection);
        delegated.SelectionChanged += (_, eventArgs) => delegatedChanges.Add(eventArgs.Selection);
        var ordinaryInitial = RoutedActivationKey(ordinary, useSpace, KeyAction.Press, Modifiers.None);
        var delegatedInitial = NavigationActivationKey(useSpace, KeyAction.Press, Modifiers.None);
        var ordinaryRepeated = RoutedActivationKey(ordinary, useSpace, KeyAction.Repeat, Modifiers.None);
        var delegatedRepeated = NavigationActivationKey(useSpace, KeyAction.Repeat, Modifiers.None);
        var ordinaryRejected = RoutedActivationKey(ordinary, useSpace, KeyAction.Press, Modifiers.Control);
        var delegatedRejected = NavigationActivationKey(useSpace, KeyAction.Press, Modifiers.Control);

        var initialHandled = delegated.HandleNavigationKey(delegatedInitial);
        var repeatedHandled = delegated.HandleNavigationKey(delegatedRepeated);
        var rejectedHandled = delegated.HandleNavigationKey(delegatedRejected);

        initialHandled.ShouldBe(ordinaryInitial.IsHandled);
        repeatedHandled.ShouldBe(ordinaryRepeated.IsHandled);
        rejectedHandled.ShouldBe(ordinaryRejected.IsHandled);
        delegated.Selection.ShouldBe(ordinary.Selection);
        delegated.ActiveDate.ShouldBe(ordinary.ActiveDate);
        delegatedChanges.ShouldBe(ordinaryChanges);

        ordinary.Selection = null;
        delegated.Selection = null;
        ordinaryChanges.Clear();
        delegatedChanges.Clear();
        var ordinaryShift = RoutedActivationKey(ordinary, useSpace, KeyAction.Press, Modifiers.Shift);
        var delegatedShift = NavigationActivationKey(useSpace, KeyAction.Press, Modifiers.Shift);

        var shiftHandled = delegated.HandleNavigationKey(delegatedShift);

        shiftHandled.ShouldBe(ordinaryShift.IsHandled);
        delegated.Selection.ShouldBe(ordinary.Selection);
        delegated.ActiveDate.ShouldBe(ordinary.ActiveDate);
        delegatedChanges.ShouldBe(ordinaryChanges);
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
        header.IsHandled.ShouldBeTrue();
        wheel.IsHandled.ShouldBeTrue();
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
        frame.GetCell(new Point(5, 6)).Continuation.ShouldBeFalse();
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
        eventArgs.IsHandled.ShouldBeTrue();
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
        eventArgs.IsHandled.ShouldBeTrue();
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
        eventArgs.IsHandled.ShouldBeTrue();
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
        calendar.ActualStyle.SelectedDayColor.ShouldBe((ControlColor) SemanticColor.SelectedText);
        calendar.ActualStyle.TodayMarkerColor.ShouldBe((ControlColor) SemanticColor.ActiveText);
        calendar.ActualStyle.OutOfMonthDayColor.ShouldBe((ControlColor) SemanticColor.Muted);
        calendar.ActualStyle.WeekdayHeaderColor.ShouldBe((ControlColor) SemanticColor.Muted);
        calendar.ActualStyle.DisabledDayColor.ShouldBe((ControlColor) SemanticColor.DisabledText);
        calendar.ActualStyle.ContentInset.ShouldBe(new Thickness(horizontal: 1, vertical: 0));
        calendar.Padding.ShouldBe(default);
    }

    /// <summary>Verifies assigning a local style propagates to ActualStyle and reports one notification,
    /// while a no-op reassignment of an equal style raises no duplicate ActualStyle notification.</summary>
    [Fact]
    public void Style_WhenAssigned_PropagatesOnceWithoutDuplicateOnNoOpReassignment()
    {
        // Arrange
        var style = CalendarStyle.Default with { SelectedDayColor = Color.Rgb(10, 20, 30) };
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
        var restyled = CalendarStyle.Default with { ContentInset = new Thickness(horizontal: 2, vertical: 1) };

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

    /// <summary>Verifies the single-date Select overload commits the exact one-day interval its
    /// documentation promises.</summary>
    [Fact]
    public void Select_WhenGivenOneDate_CommitsOneDayInterval()
    {
        // Arrange
        using var calendar = new UiCalendar { Selection = null };
        var date = new DateOnly(2026, 7, 19);

        // Act
        var selected = calendar.Select(date);

        // Assert
        selected.ShouldBeTrue();
        calendar.Selection.ShouldBe(new DateInterval(date, date));
    }

    /// <summary>Verifies the single-date Select overload rejects a blocked date and leaves the
    /// prior selection untouched.</summary>
    [Fact]
    public void Select_WhenDateIsBlocked_ThrowsAndPreservesSelection()
    {
        // Arrange
        var existing = new DateOnly(2026, 7, 10);
        using var calendar = new UiCalendar { Selection = new DateInterval(existing, existing) };
        var blocked = new DateOnly(2026, 7, 19);
        calendar.BlockedDates.Block(blocked);

        // Act
        _ = Should.Throw<ArgumentException>(() => calendar.Select(blocked));

        // Assert
        calendar.Selection.ShouldBe(new DateInterval(existing, existing));
    }

    /// <summary>Verifies the two-date Select overload rejects an end that precedes its start
    /// before touching the committed selection, matching DateInterval's own documented
    /// validation.</summary>
    [Fact]
    public void Select_WhenEndPrecedesStart_ThrowsAndPreservesSelection()
    {
        // Arrange
        var existing = new DateOnly(2026, 7, 10);
        using var calendar = new UiCalendar
        {
            SelectionMode = CalendarSelectionMode.Interval,
            Selection = new DateInterval(existing, existing)
        };

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
            calendar.Select(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 15)));

        // Assert
        exception.ParamName.ShouldBe("end");
        calendar.Selection.ShouldBe(new DateInterval(existing, existing));
    }

    /// <summary>Verifies RemoveMarkup removes an authored face and reports true, and reports false
    /// without invalidating when no markup was authored for the date.</summary>
    [Fact]
    public void RemoveMarkup_WhenAuthoredFaceExists_RemovesAndReportsTrue()
    {
        // Arrange
        using var calendar = new UiCalendar();
        var date = new DateOnly(2026, 7, 19);
        calendar.SetMarkup(date, "VIP");
        calendar.Clear(Invalidation.All);

        // Act
        var removed = calendar.RemoveMarkup(date);

        // Assert
        removed.ShouldBeTrue();
        calendar.GetMarkup(date).ShouldBeNull();
        calendar.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies RemoveMarkup on a date with no authored face reports false and does not
    /// invalidate rendering.</summary>
    [Fact]
    public void RemoveMarkup_WhenNoAuthoredFaceExists_ReportsFalseWithoutInvalidating()
    {
        // Arrange
        using var calendar = new UiCalendar();
        calendar.Clear(Invalidation.All);

        // Act
        var removed = calendar.RemoveMarkup(new DateOnly(2026, 7, 19));

        // Assert
        removed.ShouldBeFalse();
        calendar.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies ClearMarkup removes every authored face and invalidates rendering.</summary>
    [Fact]
    public void ClearMarkup_WhenFacesAreAuthored_RemovesAllAndInvalidatesRender()
    {
        // Arrange
        using var calendar = new UiCalendar();
        calendar.SetMarkup(new DateOnly(2026, 7, 10), "A");
        calendar.SetMarkup(new DateOnly(2026, 7, 19), "B");
        calendar.Clear(Invalidation.All);

        // Act
        calendar.ClearMarkup();

        // Assert
        calendar.GetMarkup(new DateOnly(2026, 7, 10)).ShouldBeNull();
        calendar.GetMarkup(new DateOnly(2026, 7, 19)).ShouldBeNull();
        calendar.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies ClearMarkup on a calendar with no authored faces is a no-op that does not
    /// invalidate rendering.</summary>
    [Fact]
    public void ClearMarkup_WhenNoFacesAreAuthored_IsNoOp()
    {
        // Arrange
        using var calendar = new UiCalendar();
        calendar.Clear(Invalidation.All);

        // Act
        calendar.ClearMarkup();

        // Assert
        calendar.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies GoToToday reports false and leaves ActiveDate and DisplayMonth untouched
    /// when today falls outside the calendar's selectable bounds.</summary>
    [Fact]
    public void GoToToday_WhenTodayIsOutsideBounds_ReturnsFalseWithoutMoving()
    {
        // Arrange
        var today = DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
        using var calendar = new UiCalendar
        {
            MinimumDate = today.AddDays(1),
            MaximumDate = DateOnly.MaxValue,
            DisplayMonth = new DateOnly(2000, 1, 1)
        };
        var previousActive = calendar.ActiveDate;
        var previousMonth = calendar.DisplayMonth;

        // Act
        var moved = calendar.GoToToday();

        // Assert
        moved.ShouldBeFalse();
        calendar.ActiveDate.ShouldBe(previousActive);
        calendar.DisplayMonth.ShouldBe(previousMonth);
    }

    /// <summary>Verifies GoToToday reports false and leaves state untouched when today is blocked.</summary>
    [Fact]
    public void GoToToday_WhenTodayIsBlocked_ReturnsFalseWithoutMoving()
    {
        // Arrange
        var today = DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
        using var calendar = new UiCalendar { DisplayMonth = new DateOnly(2000, 1, 1) };
        calendar.BlockedDates.Block(today);
        var previousActive = calendar.ActiveDate;
        var previousMonth = calendar.DisplayMonth;

        // Act
        var moved = calendar.GoToToday();

        // Assert
        moved.ShouldBeFalse();
        calendar.ActiveDate.ShouldBe(previousActive);
        calendar.DisplayMonth.ShouldBe(previousMonth);
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

    /// <summary>Verifies semantic activation is observable even when the selected date is already
    /// committed and the selection assignment is consequently a no-op.</summary>
    [Fact]
    public void ActivateDate_WhenSelectedDateIsActivated_RaisesDateActivatedOnce()
    {
        // Arrange
        var date = new DateOnly(2026, 7, 19);
        using var calendar = new UiCalendar
        {
            SelectionMode = CalendarSelectionMode.Select,
            Selection = new DateInterval(date, date)
        };
        List<DateOnly> activations = [];
        calendar.DateActivated += activations.Add;

        // Act
        var changed = calendar.ActivateDate(date);

        // Assert
        changed.ShouldBeFalse();
        activations.ShouldBe([date]);
    }

    /// <summary>Verifies held navigation repeats while Enter and Space establish an interval
    /// anchor only once for one physical key hold.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispatch_WhenActivationKeyRepeats_ActivatesOnlyInitialPressWhileNavigationRepeats(
        bool useSpace)
    {
        // Arrange
        using var calendar = new UiCalendar { SelectionMode = CalendarSelectionMode.Interval };
        var beforeMove = calendar.ActiveDate;

        // Act - navigation remains repeatable.
        _ = Key(calendar, Code.Right, KeyAction.Repeat);
        var activationDate = calendar.ActiveDate;

        if (useSpace)
        {
            _ = CharacterKey(calendar, new Rune(' '), KeyAction.Press);
            _ = CharacterKey(calendar, new Rune(' '), KeyAction.Repeat);
            _ = CharacterKey(calendar, new Rune(' '), KeyAction.Repeat);
        }
        else
        {
            _ = Key(calendar, Code.Enter);
            _ = Key(calendar, Code.Enter, KeyAction.Repeat);
            _ = Key(calendar, Code.Enter, KeyAction.Repeat);
        }

        // Assert
        activationDate.ShouldBe(beforeMove.AddDays(1));
        calendar.IntervalAnchor.ShouldBe(activationDate);
        calendar.Selection.ShouldBeNull();
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

    /// <summary>Verifies blocking an unrelated date does not repage a caller-set DisplayMonth
    /// when the active date does not move.</summary>
    [Fact]
    public void Block_WhenActiveDateDoesNotMove_PreservesCallerSetDisplayMonth()
    {
        // Arrange
        var active = new DateOnly(2026, 8, 4);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.DisplayMonth = new DateOnly(2026, 1, 1);

        // Act
        calendar.BlockedDates.Block(new DateOnly(2030, 6, 6));

        // Assert
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 1, 1));
        calendar.ActiveDate.ShouldBe(active);
    }

    /// <summary>Verifies widening MinimumDate or MaximumDate, which repairs nothing, does not
    /// repage a caller-set DisplayMonth.</summary>
    [Fact]
    public void Bounds_WhenWidenedAndActiveDateDoesNotMove_PreservesCallerSetDisplayMonth()
    {
        // Arrange
        var active = new DateOnly(2026, 8, 4);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.DisplayMonth = new DateOnly(2026, 1, 1);

        // Act
        calendar.MinimumDate = new DateOnly(1900, 1, 1);
        calendar.MaximumDate = new DateOnly(2999, 12, 31);

        // Assert
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 1, 1));
        calendar.ActiveDate.ShouldBe(active);
    }

    /// <summary>Verifies MinimumDate and MaximumDate default to DateOnly.MinValue and
    /// DateOnly.MaxValue.</summary>
    [Fact]
    public void Bounds_WhenConstructed_DefaultToFullDateOnlyRange()
    {
        // Arrange
        using var calendar = new UiCalendar();

        // Assert
        calendar.MinimumDate.ShouldBe(DateOnly.MinValue);
        calendar.MaximumDate.ShouldBe(DateOnly.MaxValue);
    }

    /// <summary>Verifies MinimumDate rejects a value that exceeds MaximumDate.</summary>
    [Fact]
    public void MinimumDate_WhenExceedsMaximumDate_ThrowsBeforeMutation()
    {
        // Arrange
        using var calendar = new UiCalendar { MaximumDate = new DateOnly(2026, 7, 20) };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => calendar.MinimumDate = new DateOnly(2026, 7, 21));
        calendar.MinimumDate.ShouldBe(DateOnly.MinValue);
    }

    /// <summary>Verifies MaximumDate rejects a value that precedes MinimumDate.</summary>
    [Fact]
    public void MaximumDate_WhenPrecedesMinimumDate_ThrowsBeforeMutation()
    {
        // Arrange
        using var calendar = new UiCalendar { MinimumDate = new DateOnly(2026, 7, 20) };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => calendar.MaximumDate = new DateOnly(2026, 7, 19));
        calendar.MaximumDate.ShouldBe(DateOnly.MaxValue);
    }

    /// <summary>Verifies narrowing MaximumDate below a committed selection clears that selection,
    /// the documented repair MinimumDate and MaximumDate share.</summary>
    [Fact]
    public void MaximumDate_WhenNarrowedBelowSelection_ClearsSelection()
    {
        // Arrange
        var selected = new DateOnly(2026, 7, 25);
        using var calendar = new UiCalendar { Selection = new DateInterval(selected, selected) };

        // Act
        calendar.MaximumDate = new DateOnly(2026, 7, 20);

        // Assert
        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies narrowing MinimumDate above the active date clamps it back inside bounds.</summary>
    [Fact]
    public void MinimumDate_WhenRaisedAboveActiveDate_ClampsActiveDate()
    {
        // Arrange
        var active = new DateOnly(2026, 7, 10);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };

        // Act
        calendar.MinimumDate = new DateOnly(2026, 7, 15);

        // Assert
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 15));
    }

    /// <summary>Verifies tightening either bound completes selection and active-date repair after
    /// the bound's property notification throws.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bounds_WhenPropertyObserverThrows_StillRepairDependentState(bool minimum)
    {
        // Arrange
        var selected = minimum ? new DateOnly(2026, 7, 10) : new DateOnly(2026, 7, 20);
        var bound = minimum ? new DateOnly(2026, 7, 12) : new DateOnly(2026, 7, 18);
        using var calendar = new UiCalendar { Selection = new DateInterval(selected, selected) };
        var propertyName = minimum ? nameof(UiCalendar.MinimumDate) : nameof(UiCalendar.MaximumDate);
        calendar.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == propertyName)
            {
                throw new InvalidOperationException("observer failure");
            }
        };

        // Act
        _ = Should.Throw<InvalidOperationException>(() =>
        {
            if (minimum)
            {
                calendar.MinimumDate = bound;
            }
            else
            {
                calendar.MaximumDate = bound;
            }
        });

        // Assert
        calendar.Selection.ShouldBeNull();
        calendar.ActiveDate.ShouldBe(bound);
        calendar.DisplayMonth.ShouldBe(new DateOnly(bound.Year, bound.Month, 1));
    }

    /// <summary>Verifies changing selection policy clears its obsolete interval state after a
    /// throwing property observer.</summary>
    [Fact]
    public void SelectionMode_WhenPropertyObserverThrows_StillClearsPendingState()
    {
        // Arrange
        using var calendar = new UiCalendar { SelectionMode = CalendarSelectionMode.Interval };
        _ = calendar.ActivateDate(new DateOnly(2026, 7, 12));
        calendar.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UiCalendar.SelectionMode))
            {
                throw new InvalidOperationException("observer failure");
            }
        };

        // Act
        _ = Should.Throw<InvalidOperationException>(() => calendar.SelectionMode = CalendarSelectionMode.Select);

        // Assert
        calendar.IntervalAnchor.ShouldBeNull();
        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies blocked-date repair reaches the active date after a throwing selection callback.</summary>
    [Fact]
    public void Block_WhenSelectionObserverThrows_StillRepairsActiveDate()
    {
        // Arrange
        var date = new DateOnly(2026, 7, 12);
        using var calendar = new UiCalendar { Selection = new DateInterval(date, date) };
        calendar.SelectionChanged += (_, _) => throw new InvalidOperationException("observer failure");

        // Act
        _ = Should.Throw<InvalidOperationException>(() => calendar.BlockedDates.Block(date));

        // Assert
        calendar.Selection.ShouldBeNull();
        calendar.BlockedDates.Contains(calendar.ActiveDate).ShouldBeFalse();
    }

    /// <summary>Verifies blocked-date repair reaches the active date after a throwing anchor callback.</summary>
    [Fact]
    public void Block_WhenIntervalAnchorObserverThrows_StillRepairsActiveDate()
    {
        // Arrange
        var date = new DateOnly(2026, 7, 12);
        using var calendar = new UiCalendar { SelectionMode = CalendarSelectionMode.Interval };
        _ = calendar.ActivateDate(date);
        calendar.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UiCalendar.IntervalAnchor))
            {
                throw new InvalidOperationException("observer failure");
            }
        };

        // Act
        _ = Should.Throw<InvalidOperationException>(() => calendar.BlockedDates.Block(date));

        // Assert
        calendar.IntervalAnchor.ShouldBeNull();
        calendar.BlockedDates.Contains(calendar.ActiveDate).ShouldBeFalse();
    }

    /// <summary>Verifies Culture rejects a null assignment.</summary>
    [Fact]
    public void Culture_WhenAssignedNull_ThrowsBeforeMutation()
    {
        // Arrange
        using var calendar = new UiCalendar { Culture = CultureInfo.InvariantCulture };

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => calendar.Culture = null!);
        calendar.Culture.ShouldBe(CultureInfo.InvariantCulture);
    }

    /// <summary>Verifies FirstDayOfWeek rejects an undefined value.</summary>
    [Fact]
    public void FirstDayOfWeek_WhenUndefined_ThrowsBeforeMutation()
    {
        // Arrange
        using var calendar = new UiCalendar { FirstDayOfWeek = DayOfWeek.Monday };

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => calendar.FirstDayOfWeek = (DayOfWeek) 99);
        calendar.FirstDayOfWeek.ShouldBe(DayOfWeek.Monday);
    }

    /// <summary>Verifies unblocking a date does not repage a caller-set DisplayMonth when the
    /// active date does not move.</summary>
    [Fact]
    public void Unblock_WhenActiveDateDoesNotMove_PreservesCallerSetDisplayMonth()
    {
        // Arrange
        var active = new DateOnly(2026, 8, 4);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.BlockedDates.Block(new DateOnly(2030, 6, 6));
        calendar.DisplayMonth = new DateOnly(2026, 1, 1);

        // Act
        calendar.BlockedDates.Unblock(new DateOnly(2030, 6, 6));

        // Assert
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 1, 1));
        calendar.ActiveDate.ShouldBe(active);
    }

    /// <summary>Verifies clearing an already-populated blocked collection does not repage a
    /// caller-set DisplayMonth when the active date does not move.</summary>
    [Fact]
    public void Clear_WhenActiveDateDoesNotMove_PreservesCallerSetDisplayMonth()
    {
        // Arrange
        var active = new DateOnly(2026, 8, 4);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.BlockedDates.Block(new DateOnly(2030, 6, 6));
        calendar.DisplayMonth = new DateOnly(2026, 1, 1);

        // Act
        calendar.BlockedDates.Clear();

        // Assert
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 1, 1));
        calendar.ActiveDate.ShouldBe(active);
    }

    /// <summary>Verifies the documented example (calendar.md) sets its DisplayMonth before
    /// blocking a range and the block does not silently repage it.</summary>
    [Fact]
    public void Block_WhenFollowingDocumentedExample_KeepsAuthoredDisplayMonth()
    {
        // Arrange
        using var booking = new UiCalendar
        {
            SelectionMode = CalendarSelectionMode.Interval,
            DisplayMonth = new DateOnly(2026, 7, 1)
        };

        // Act
        booking.BlockedDates.Block(
            new DateInterval(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14)));

        // Assert
        booking.DisplayMonth.ShouldBe(new DateOnly(2026, 7, 1));
    }

    /// <summary>Verifies a rejected interval endpoint - one whose span crosses a blocked date -
    /// leaves ActiveDate and DisplayMonth untouched, rather than committing the anchor move
    /// before the rejection is discovered.</summary>
    [Fact]
    public void ActivateDate_WhenIntervalCrossesBlockedDate_LeavesActiveDateAndDisplayMonthUntouched()
    {
        // Arrange
        var anchor = new DateOnly(2026, 7, 10);
        using var calendar = new UiCalendar
        {
            SelectionMode = CalendarSelectionMode.Interval,
            DisplayMonth = new DateOnly(2026, 7, 1)
        };
        calendar.BlockedDates.Block(new DateOnly(2026, 7, 15));
        _ = calendar.ActivateDate(anchor);
        var activeBeforeReject = calendar.ActiveDate;
        var displayBeforeReject = calendar.DisplayMonth;

        // Act
        var accepted = calendar.ActivateDate(new DateOnly(2026, 8, 2));

        // Assert
        accepted.ShouldBeFalse();
        calendar.ActiveDate.ShouldBe(activeBeforeReject);
        calendar.DisplayMonth.ShouldBe(displayBeforeReject);
        calendar.IntervalAnchor.ShouldBe(anchor);
    }

    /// <summary>Verifies re-assigning the identical Selection value - the shape of an idempotent
    /// binding refresh - does not repage a caller-browsed DisplayMonth. CommitSelection is the
    /// one SetActiveDate caller that other tests did not cover, since it moved the active date before
    /// discovering the selection had not actually changed.</summary>
    [Fact]
    public void Selection_WhenReassignedTheIdenticalValue_PreservesCallerBrowsedDisplayMonth()
    {
        // Arrange
        var selected = new DateOnly(2024, 1, 15);
        using var calendar = new UiCalendar { Selection = new DateInterval(selected, selected) };
        calendar.DisplayMonth = new DateOnly(2024, 6, 1);

        // Act
        calendar.Selection = new DateInterval(selected, selected);

        // Assert
        calendar.DisplayMonth.ShouldBe(new DateOnly(2024, 6, 1));
        calendar.ActiveDate.ShouldBe(selected);
    }

    /// <summary>Verifies assigning DisplayMonth before ActiveDate is ever established seeds the
    /// active date into the assigned month, rather than leaving it to resolve to today on first
    /// read.</summary>
    [Fact]
    public void DisplayMonth_WhenAssignedBeforeActiveDateIsEstablished_SeedsActiveDateIntoAssignedMonth()
    {
        // Arrange and Act
        using var calendar = new UiCalendar { DisplayMonth = new DateOnly(2026, 1, 1) };

        // Assert
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 1, 1));
    }

    /// <summary>Verifies reassigning DisplayMonth after the active date is already established
    /// only browses the display, leaving the active date untouched.</summary>
    [Fact]
    public void DisplayMonth_WhenReassignedAfterActiveDateIsEstablished_DoesNotMoveActiveDate()
    {
        // Arrange
        var active = new DateOnly(2026, 8, 4);
        using var calendar = new UiCalendar { Selection = new DateInterval(active, active) };
        calendar.Selection = null;

        // Act
        calendar.DisplayMonth = new DateOnly(2020, 1, 1);

        // Assert
        calendar.ActiveDate.ShouldBe(active);
        calendar.DisplayMonth.ShouldBe(new DateOnly(2020, 1, 1));
    }

    /// <summary>Verifies every documented public editing method on Calendar and its owned
    /// BlockedDates collection rejects use after disposal with the documented
    /// ObjectDisposedException.</summary>
    [Fact]
    public void Methods_WhenCalendarIsDisposed_ThrowObjectDisposedException()
    {
        // Arrange
        var calendar = new UiCalendar();
        calendar.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => calendar.Select(new DateOnly(2026, 7, 19)));
        _ = Should.Throw<ObjectDisposedException>(() =>
            calendar.Select(new DateOnly(2026, 7, 19), new DateOnly(2026, 7, 19)));
        _ = Should.Throw<ObjectDisposedException>(() => calendar.ClearSelection());
        _ = Should.Throw<ObjectDisposedException>(() => calendar.SetMarkup(new DateOnly(2026, 7, 19), "x"));
        _ = Should.Throw<ObjectDisposedException>(() => calendar.RemoveMarkup(new DateOnly(2026, 7, 19)));
        _ = Should.Throw<ObjectDisposedException>(calendar.ClearMarkup);
        _ = Should.Throw<ObjectDisposedException>(() => calendar.GoToToday());
        _ = Should.Throw<ObjectDisposedException>(() => calendar.BlockedDates.Block(new DateOnly(2026, 7, 19)));
        _ = Should.Throw<ObjectDisposedException>(() => calendar.BlockedDates.Unblock(new DateOnly(2026, 7, 19)));
        _ = Should.Throw<ObjectDisposedException>(calendar.BlockedDates.Clear);
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
        KeyAction action = KeyAction.Press,
        Modifiers modifiers = Modifiers.None)
    {
        var eventArgs = new KeyEventArgs(new Stroke(
            code,
            character: null,
            nativeCode: 0,
            modifiers,
            action));
        _ = Router.Route(calendar, Events.Key, eventArgs);
        return eventArgs;
    }

    private static UiCalendar CreateNavigationCalendar(DateOnly initial)
    {
        var calendar = new UiCalendar { Selection = new DateInterval(initial, initial) };
        calendar.Selection = null;
        return calendar;
    }

    private static KeyEventArgs NavigationKey(Code code, KeyAction action, Modifiers modifiers) =>
        new(new Stroke(code, character: null, nativeCode: 0, modifiers, action));

    private static KeyEventArgs NavigationActivationKey(bool useSpace, KeyAction action, Modifiers modifiers) =>
        new(new Stroke(
            useSpace ? Code.Character : Code.Enter,
            useSpace ? new Rune(' ') : null,
            nativeCode: 0,
            modifiers,
            action));

    private static KeyEventArgs RoutedActivationKey(
        UiCalendar calendar,
        bool useSpace,
        KeyAction action,
        Modifiers modifiers)
    {
        var eventArgs = NavigationActivationKey(useSpace, action, modifiers);
        _ = Router.Route(calendar, Events.Key, eventArgs);
        return eventArgs;
    }

    private static KeyEventArgs CharacterKey(
        UiCalendar calendar,
        Rune character,
        KeyAction action,
        Modifiers modifiers = Modifiers.None)
    {
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Character,
            character,
            nativeCode: 0,
            modifiers,
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
