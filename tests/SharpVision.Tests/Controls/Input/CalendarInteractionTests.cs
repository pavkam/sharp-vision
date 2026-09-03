// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves Calendar's remaining interaction and condition contract: pointer-driven interval
/// selection with hover preview, hover clearing on leave, header and wheel browsing at both ends
/// of the representable range, week-edge and week movement around blocked dates, bounds that span
/// months, blocked-range normalization, authored-face precedence, localized weekday order, and the
/// repair paths that keep the active date and interval anchor inside a shrinking domain.</summary>
public sealed class CalendarInteractionTests
{
    private static readonly DateOnly _july2026 = new(2026, 7, 1);

    #region Pointer interval selection and hover

    /// <summary>Verifies a two-click interval selection: the first click pends an anchor with no
    /// committed selection, hovering previews the provisional span, the second click commits the
    /// ordered interval once, and a third click clears it and pends a fresh anchor.</summary>
    [Fact]
    public async Task Pointer_WhenIntervalIsClickedInTwoSteps_PreviewsThenCommitsOrderedSpanAsync()
    {
        // Arrange - July 2026, Sunday-first: July 6 is Monday on the second grid row.
        var calendar = CreateJuly(CalendarSelectionMode.Interval);
        var events = new List<(DateInterval? Previous, DateInterval? Current)>();
        calendar.SelectionChanged += (_, eventArgs) => events.Add((eventArgs.PreviousSelection, eventArgs.Selection));
        await using var surface = await ComponentSurface.MountAsync(calendar, new Size(32, 10), TestContext.Current.CancellationToken);
        var preview = Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark));

        // Act - anchor July 9, hover July 6 (an earlier date), then click it.
        await surface.Pointer.ClickAsync(calendar, CellFor(new DateOnly(2026, 7, 9)));
        calendar.IntervalAnchor.ShouldBe(new DateOnly(2026, 7, 9));
        calendar.Selection.ShouldBeNull();
        await surface.Pointer.MoveToAsync(calendar, CellFor(new DateOnly(2026, 7, 6)));

        // Assert the preview covers the span between hover and anchor without committing.
        surface.Cell(CellFor(new DateOnly(2026, 7, 7))).Style.Foreground.ShouldBe(preview);
        surface.Cell(CellFor(new DateOnly(2026, 7, 10))).Style.Foreground.ShouldNotBe(preview);
        calendar.Selection.ShouldBeNull();
        events.ShouldBeEmpty();

        await surface.Pointer.ClickAsync(calendar, CellFor(new DateOnly(2026, 7, 6)));
        var committed = new DateInterval(new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 9));
        calendar.Selection.ShouldBe(committed);
        calendar.IntervalAnchor.ShouldBeNull();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 6));
        events.ShouldBe([(null, committed)]);

        // A third click clears the interval and pends a new anchor.
        await surface.Pointer.ClickAsync(calendar, CellFor(new DateOnly(2026, 7, 20)));
        calendar.Selection.ShouldBeNull();
        calendar.IntervalAnchor.ShouldBe(new DateOnly(2026, 7, 20));
        events.ShouldBe([(null, committed), (committed, null)]);
    }

    /// <summary>Verifies a pointer endpoint whose span crosses a blocked date is ignored, keeping
    /// the anchor, the active date, and the displayed month untouched.</summary>
    [Fact]
    public async Task Pointer_WhenIntervalEndpointCrossesBlockedDate_IgnoresClickAndKeepsAnchorAsync()
    {
        // Arrange
        var calendar = CreateJuly(CalendarSelectionMode.Interval);
        calendar.BlockedDates.Block(new DateOnly(2026, 7, 8));
        await using var surface = await ComponentSurface.MountAsync(calendar, new Size(32, 10), TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(calendar, CellFor(new DateOnly(2026, 7, 6)));

        // Act
        await surface.Pointer.ClickAsync(calendar, CellFor(new DateOnly(2026, 7, 10)));

        // Assert
        calendar.IntervalAnchor.ShouldBe(new DateOnly(2026, 7, 6));
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 6));
        calendar.Selection.ShouldBeNull();
        calendar.DisplayMonth.ShouldBe(_july2026);
    }

    /// <summary>Verifies a routed pointer Leave clears the hovered date and its highlight. Leave
    /// reports exist only in SGR pixel mode, so the surface is mounted with pixel coordinates.</summary>
    [Fact]
    public async Task Pointer_WhenPointerLeaves_ClearsHoveredDateAndHighlightAsync()
    {
        // Arrange
        var calendar = CreateJuly(CalendarSelectionMode.Select);
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(32, 10),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        var hover = Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark));
        var cell = CellFor(new DateOnly(2026, 7, 13));
        await surface.Pointer.MoveToAsync(calendar, cell);
        calendar.HoveredDate.ShouldBe(new DateOnly(2026, 7, 13));
        surface.Cell(cell).Style.Foreground.ShouldBe(hover);

        // Act
        await surface.Pointer.LeaveAsync();

        // Assert
        calendar.HoveredDate.ShouldBeNull();
        surface.Cell(cell).Style.Foreground.ShouldNotBe(hover);
    }

    /// <summary>Verifies the previous-month header arrow browses the display back one month without
    /// moving the active date, and a press on the border outside the grid does nothing.</summary>
    [Fact]
    public async Task Pointer_WhenPreviousArrowOrBorderIsClicked_BrowsesOrIgnoresAsync()
    {
        // Arrange
        var calendar = CreateJuly(CalendarSelectionMode.Select);
        await using var surface = await ComponentSurface.MountAsync(calendar, new Size(32, 10), TestContext.Current.CancellationToken);
        var active = calendar.ActiveDate;

        // Act
        await surface.Pointer.ClickAsync(calendar, new Point(2, 1));
        var afterArrow = calendar.DisplayMonth;
        await surface.Pointer.ClickAsync(calendar, new Point(0, 0));

        // Assert
        afterArrow.ShouldBe(new DateOnly(2026, 6, 1));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 6, 1));
        calendar.ActiveDate.ShouldBe(active);
        calendar.Selection.ShouldBeNull();
    }

    #endregion

    #region Keyboard movement

    /// <summary>Verifies Up and Down keep walking past a blocked landing date in the direction of
    /// travel instead of stopping on it or refusing to move.</summary>
    [Fact]
    public void Keyboard_WhenWeekMoveLandsOnBlockedDate_ContinuesPastIt()
    {
        // Arrange
        using var calendar = CreateJuly(CalendarSelectionMode.Select);
        _ = calendar.Select(new DateOnly(2026, 7, 19));
        calendar.BlockedDates.Block(new DateOnly(2026, 7, 12));

        // Act and assert
        Press(calendar, Code.Up).IsHandled.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 11));
        _ = calendar.Select(new DateOnly(2026, 7, 5));
        Press(calendar, Code.Down).IsHandled.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 13));
    }

    /// <summary>Verifies Home and End are left unhandled when the active date already sits on the
    /// requested week edge, so an ancestor can respond.</summary>
    [Fact]
    public void Keyboard_WhenAlreadyAtWeekEdge_LeavesHomeAndEndUnhandled()
    {
        // Arrange - July 5, 2026 is a Sunday and July 11 a Saturday under the invariant week.
        using var calendar = CreateJuly(CalendarSelectionMode.Select);
        _ = calendar.Select(new DateOnly(2026, 7, 5));

        // Act and assert
        Press(calendar, Code.Home).IsHandled.ShouldBeFalse();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 5));
        Press(calendar, Code.End).IsHandled.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 11));
        Press(calendar, Code.End).IsHandled.ShouldBeFalse();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 11));
    }

    /// <summary>Verifies month paging, wheel browsing, and the header arrows are all left unhandled
    /// at the first representable month, where nothing earlier exists to show.</summary>
    [Fact]
    public void Navigation_WhenAtFirstRepresentableMonth_LeavesBackwardCommandsUnhandled()
    {
        // Arrange
        using var calendar = new UiCalendar { Culture = CultureInfo.InvariantCulture, DisplayMonth = DateOnly.MinValue };
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        calendar.ActiveDate.ShouldBe(DateOnly.MinValue);

        // Act and assert
        Press(calendar, Code.PageUp).IsHandled.ShouldBeFalse();
        Route(calendar, Wheel(1)).IsHandled.ShouldBeFalse();
        Route(calendar, Pointer(new Point(calendar.ContentBounds.X + 1, calendar.ContentBounds.Y), PointerAction.Press))
            .IsHandled.ShouldBeFalse();
        calendar.DisplayMonth.ShouldBe(DateOnly.MinValue);
        calendar.ActiveDate.ShouldBe(DateOnly.MinValue);
    }

    /// <summary>Verifies month paging, wheel browsing, and the header arrows are all left unhandled
    /// at the last representable month.</summary>
    [Fact]
    public void Navigation_WhenAtLastRepresentableMonth_LeavesForwardCommandsUnhandled()
    {
        // Arrange
        using var calendar = new UiCalendar { Culture = CultureInfo.InvariantCulture, DisplayMonth = DateOnly.MaxValue };
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        var lastMonth = new DateOnly(9999, 12, 1);

        // Act and assert
        Press(calendar, Code.PageDown).IsHandled.ShouldBeFalse();
        Route(calendar, Wheel(-1)).IsHandled.ShouldBeFalse();
        Route(calendar, Pointer(new Point(calendar.ContentBounds.Right - 2, calendar.ContentBounds.Y), PointerAction.Press))
            .IsHandled.ShouldBeFalse();
        calendar.DisplayMonth.ShouldBe(lastMonth);
        calendar.ActiveDate.ShouldBe(lastMonth);
    }

    /// <summary>Verifies bounds spanning two months: movement crosses the month boundary and
    /// repages the display, stops at both bounds, and paging clamps into the bounded window.</summary>
    [Fact]
    public void Keyboard_WhenBoundsSpanMonths_MovesAcrossBoundaryAndStopsAtBounds()
    {
        // Arrange
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            MinimumDate = new DateOnly(2026, 3, 25),
            MaximumDate = new DateOnly(2026, 4, 5)
        };
        _ = calendar.Select(new DateOnly(2026, 3, 31));

        // Act and assert - Right crosses into April and repages the display.
        Press(calendar, Code.Right).IsHandled.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 4, 1));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 4, 1));

        // A week or month jump whose landing day lies outside the bounds is refused outright:
        // only Home and End clamp their landing day into range.
        Press(calendar, Code.Down).IsHandled.ShouldBeFalse();
        Press(calendar, Code.PageDown).IsHandled.ShouldBeFalse();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 4, 1));

        for (var day = 2; day <= 5; day++)
        {
            Press(calendar, Code.Right).IsHandled.ShouldBeTrue();
        }

        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 4, 5));
        Press(calendar, Code.Right).IsHandled.ShouldBeFalse();
        Press(calendar, Code.PageUp).IsHandled.ShouldBeFalse();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 4, 5));

        // Up crosses back into March and repages; a second Up would land before Minimum.
        Press(calendar, Code.Up).IsHandled.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 3, 29));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 3, 1));
        Press(calendar, Code.Up).IsHandled.ShouldBeFalse();

        for (var day = 28; day >= 25; day--)
        {
            Press(calendar, Code.Left).IsHandled.ShouldBeTrue();
        }

        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 3, 25));
        Press(calendar, Code.Left).IsHandled.ShouldBeFalse();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 3, 25));
    }

    #endregion

    #region Culture and rendering

    /// <summary>Verifies a Monday-first culture renders its localized weekday row and month title
    /// in that order.</summary>
    [Fact]
    public async Task Render_WhenCultureStartsWeekOnMonday_LocalizesHeaderAndWeekdayOrderAsync()
    {
        // Arrange
        var calendar = new UiCalendar { Culture = new CultureInfo("de-DE"), DisplayMonth = _july2026 };

        // Act
        await using var surface = await ComponentSurface.MountAsync(calendar, new Size(32, 10), TestContext.Current.CancellationToken);

        // Assert
        calendar.FirstDayOfWeek.ShouldBe(DayOfWeek.Monday);
        ReadRow(surface, 1).ShouldContain("Juli 2026");
        ReadRow(surface, 2).ShouldBe("┃  Mo  Di  Mi  Do  Fr  Sa  So  ┃");
        ReadRow(surface, 3).ShouldBe("┃  29  30   1   2   3   4   5  ┃");
    }

    /// <summary>Verifies a short allocation draws the header alone at one content row and adds the
    /// weekday row at two, without any date rows escaping the bounds.</summary>
    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void Render_WhenHeightIsShort_DrawsOnlyRowsThatFit(int height, bool expectsWeekdays)
    {
        // Arrange
        using var calendar = CreateJuly(CalendarSelectionMode.Select);
        var size = new Size(32, height);
        new LayoutEngine().Layout(calendar, size);
        using Frame frame = new(size);

        // Act
        calendar.Render(frame.Canvas);

        // Assert
        var header = Row(frame, 1);
        header.ShouldStartWith("┃ <");
        header.ShouldContain("July 2026");
        header.ShouldEndWith("> ┃");

        if (expectsWeekdays)
        {
            Row(frame, 2).ShouldBe("┃  Su  Mo  Tu  We  Th  Fr  Sa  ┃");
            Row(frame, 3).ShouldStartWith("┗");
        }
        else
        {
            Row(frame, 2).ShouldStartWith("┗");
        }
    }

    /// <summary>Verifies an authored face whose styled span covers only part of the text leaves the
    /// unspanned graphemes on the inherited style.</summary>
    [Fact]
    public async Task SetMarkup_WhenSpanCoversPartOfFace_LeavesRemainderInheritedAsync()
    {
        // Arrange
        var calendar = CreateJuly(CalendarSelectionMode.Select);
        await using var surface = await ComponentSurface.MountAsync(calendar, new Size(32, 10), TestContext.Current.CancellationToken);
        var cell = CellFor(new DateOnly(2026, 7, 15));

        // Act
        await surface.UpdateAsync(() => calendar.SetMarkup(new DateOnly(2026, 7, 15), "<b>1</b>5x"), "author a partially styled face");

        // Assert
        surface.Cell(cell).Text.ShouldBe("1");
        (surface.Cell(cell).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        surface.Cell(new Point(cell.X + 1, cell.Y)).Text.ShouldBe("5");
        (surface.Cell(new Point(cell.X + 1, cell.Y)).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.None);
        surface.Cell(new Point(cell.X + 2, cell.Y)).Text.ShouldBe("x");
    }

    #endregion

    #region Blocked ranges

    /// <summary>Verifies blocking earlier and adjacent ranges keeps the collection sorted and
    /// coalesced, re-blocking an already blocked date is a silent no-op, and unblocking a date
    /// outside every range changes nothing.</summary>
    [Fact]
    public void BlockedDates_WhenRangesAreAddedOutOfOrder_StaySortedAndCoalesced()
    {
        // Arrange
        using var calendar = CreateJuly(CalendarSelectionMode.Select);
        var blocked = calendar.BlockedDates;
        var invalidations = 0;
        calendar.PropertyChanged += (_, _) => invalidations++;

        // Act
        blocked.Block(new DateInterval(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 25)));
        blocked.Block(new DateInterval(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5)));
        blocked.Block(new DateOnly(2026, 7, 3));
        blocked.Unblock(new DateOnly(2026, 7, 10));
        blocked.Block(new DateOnly(2026, 7, 6));

        // Assert
        blocked.Count.ShouldBe(2);
        blocked.ToArray().ShouldBe(
        [
            new DateInterval(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 6)),
            new DateInterval(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 25))
        ]);
        var nonGeneric = new List<DateInterval>();
        var enumerator = ((System.Collections.IEnumerable) blocked).GetEnumerator();

        while (enumerator.MoveNext())
        {
            nonGeneric.Add((DateInterval) enumerator.Current);
        }

        nonGeneric.ShouldBe(blocked.ToArray());
        blocked.Contains(new DateOnly(2026, 7, 6)).ShouldBeTrue();
        blocked.Contains(new DateOnly(2026, 7, 7)).ShouldBeFalse();
        invalidations.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies clearing an empty collection is a silent no-op and clearing a populated one
    /// makes every date selectable again.</summary>
    [Fact]
    public void BlockedDates_WhenCleared_RemovesEveryRangeAndTolleratesEmptyClear()
    {
        // Arrange
        using var calendar = CreateJuly(CalendarSelectionMode.Select);
        var blocked = calendar.BlockedDates;

        // Act and assert
        Should.NotThrow(blocked.Clear);
        blocked.Count.ShouldBe(0);
        blocked.Block(new DateOnly(2026, 7, 3));
        blocked.Clear();
        blocked.Count.ShouldBe(0);
        blocked.Contains(new DateOnly(2026, 7, 3)).ShouldBeFalse();
        _ = calendar.Select(new DateOnly(2026, 7, 3));
        calendar.Selection.ShouldBe(new DateInterval(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 3)));
    }

    #endregion

    #region Repair

    /// <summary>Verifies blocking the active date moves it to the next selectable day, or to the
    /// previous one when the maximum leaves no room ahead.</summary>
    [Fact]
    public void BlockedDates_WhenActiveDateIsBlocked_MovesToNearestSelectableDay()
    {
        // Arrange
        using var forward = CreateJuly(CalendarSelectionMode.Select);
        _ = forward.Select(new DateOnly(2026, 7, 19));
        using var backward = CreateJuly(CalendarSelectionMode.Select);
        _ = backward.Select(new DateOnly(2026, 7, 19));
        backward.MaximumDate = new DateOnly(2026, 7, 19);

        // Act
        forward.BlockedDates.Block(new DateOnly(2026, 7, 19));
        backward.BlockedDates.Block(new DateOnly(2026, 7, 19));

        // Assert
        forward.ActiveDate.ShouldBe(new DateOnly(2026, 7, 20));
        forward.Selection.ShouldBeNull();
        backward.ActiveDate.ShouldBe(new DateOnly(2026, 7, 18));
        backward.Selection.ShouldBeNull();
    }

    /// <summary>Verifies raising the minimum above a pending interval anchor clears the anchor and
    /// moves the active date into range.</summary>
    [Fact]
    public void MinimumDate_WhenRaisedAbovePendingAnchor_ClearsAnchorAndRepairsActiveDate()
    {
        // Arrange
        using var calendar = CreateJuly(CalendarSelectionMode.Interval);
        _ = calendar.Select(new DateOnly(2026, 7, 6));
        calendar.Selection = null;
        _ = Press(calendar, Code.Enter);
        calendar.IntervalAnchor.ShouldBe(new DateOnly(2026, 7, 6));

        // Act
        calendar.MinimumDate = new DateOnly(2026, 7, 10);

        // Assert
        calendar.IntervalAnchor.ShouldBeNull();
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 10));
        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies an IntervalAnchor observer that commits a newer selection while an older
    /// selection is mid-commit wins: the older commit reports success without overwriting the newer
    /// interval, and only the newer interval is published.</summary>
    [Fact]
    public void Selection_WhenAnchorObserverCommitsNewerSelection_PreservesNewerTransaction()
    {
        // Arrange
        using var calendar = CreateJuly(CalendarSelectionMode.Interval);
        _ = calendar.Select(new DateOnly(2026, 7, 6));
        calendar.Selection = null;
        _ = Press(calendar, Code.Enter);
        var older = new DateInterval(new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 15));
        var newer = new DateInterval(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 22));
        var published = new List<DateInterval?>();
        var reentered = false;
        calendar.SelectionChanged += (_, eventArgs) => published.Add(eventArgs.Selection);
        calendar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == "IntervalAnchor" && calendar.IntervalAnchor is null && !reentered)
            {
                reentered = true;
                calendar.Selection = newer;
            }
        };

        // Act
        var changed = calendar.Select(older.Start, older.End);

        // Assert
        changed.ShouldBeTrue();
        calendar.Selection.ShouldBe(newer);
        calendar.ActiveDate.ShouldBe(newer.Start);
        published.ShouldBe([newer]);
    }

    #endregion

    #region Helpers

    private static UiCalendar CreateJuly(CalendarSelectionMode mode)
    {
        var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = _july2026,
            SelectionMode = mode
        };
        return calendar;
    }

    /// <summary>Resolves the calendar-relative cell for one July 2026 date inside the six-week grid
    /// rendered with the invariant Sunday-first week.</summary>
    private static Point CellFor(DateOnly date)
    {
        var offset = ((int) _july2026.DayOfWeek + 7) % 7;
        var index = date.DayNumber - (_july2026.DayNumber - offset);
        return new Point(2 + (index % 7 * 4), 3 + (index / 7));
    }

    private static KeyEventArgs Press(ControlBase control, Code code)
    {
        var eventArgs = new KeyEventArgs(new Stroke(code, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press));
        _ = Router.Route(control, Events.Key, eventArgs);
        return eventArgs;
    }

    private static PointerEventArgs Route(ControlBase control, Pointer pointer)
    {
        var eventArgs = new PointerEventArgs(pointer);
        _ = Router.Route(control, Events.Pointer, eventArgs);
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

    private static string ReadRow(ComponentSurface surface, int y) =>
        string.Concat(Enumerable.Range(0, 32).Select(x => surface.Cell(new Point(x, y)).Text));

    private static Color Project(Color color) => TerminalPalette.Project(color, ColorDepth.Basic16);

    #endregion
}
