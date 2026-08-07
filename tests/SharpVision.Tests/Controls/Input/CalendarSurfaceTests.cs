// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using System.Text.Json;

using SharpVision.Tests.Input;


/// <summary>Proves Calendar behavior through mounted terminal input and semantic output.</summary>
public sealed class CalendarSurfaceTests
{
    /// <summary>Verifies mounted paging preserves month geometry and Space activates the focused date.</summary>
    [Fact]
    public async Task Surface_WhenPagedAndSpacePressed_SelectsFocusedDateAsync()
    {
        // Arrange
        var active = new DateOnly(2026, 7, 31);
        var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = active,
            Selection = new DateInterval(active, active)
        };
        calendar.Selection = null;
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(32, 10),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Keyboard.PressAsync(Code.PageUp);

        // Assert focused paging
        calendar.ActiveDate.ShouldBe(active);
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 7, 1));
        (surface.Cell(new Point(22, 7)).Style.Attributes & TerminalAttributes.Underline)
            .ShouldBe(TerminalAttributes.Underline);

        // Act and assert Space activation
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        calendar.Selection.ShouldBe(new DateInterval(active, active));
    }

    /// <summary>Verifies a theme swap confined to the directly-resolved Accent role still repaints
    /// the header arrow, instead of the base role-profile comparison alone under-invalidating it.</summary>
    [Fact]
    public async Task Surface_WhenThemeSwapChangesOnlyAccent_RepaintsHeaderArrowAsync()
    {
        // Arrange
        var themeA = WithAccent(Color.Rgb(10, 20, 30));
        var themeB = WithAccent(Color.Rgb(200, 210, 220));
        var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2000, 1, 1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(32, 10),
            themeA,
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("<");
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(Project(Color.Rgb(10, 20, 30)));

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap Accent-only theme");

        // Assert
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(Project(Color.Rgb(200, 210, 220)));
    }

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

    /// <summary>Verifies a mounted calendar renders the configured week start and can jump to today.</summary>
    [Fact]
    public async Task Surface_WhenWeekStartAndTodayAreConfigured_UsesBothBasicsAsync()
    {
        var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            FirstDayOfWeek = DayOfWeek.Monday,
            DisplayMonth = new DateOnly(2000, 1, 1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(32, 10),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(3, 2)).Text.ShouldBe("M");
        await surface.UpdateAsync(() => calendar.GoToToday(), "go to today");

        var today = DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
        calendar.ActiveDate.ShouldBe(today);
        calendar.DisplayMonth.ShouldBe(new DateOnly(today.Year, today.Month, 1));
    }

    /// <summary>Verifies interval preview stops at the active date before committing ordered endpoints.</summary>
    [Fact]
    public async Task Surface_WhenIntervalIsKeyboardSelected_PreviewsAndCommitsExactSpanAsync()
    {
        // Arrange
        var start = new DateOnly(2026, 7, 10);
        var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1),
            SelectionMode = CalendarSelectionMode.Interval,
            Selection = new DateInterval(start, start)
        };
        calendar.Selection = null;
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(32, 10),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act pending interval
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert exact preview
        calendar.IntervalAnchor.ShouldBe(start);
        calendar.ActiveDate.ShouldBe(new DateOnly(2026, 7, 12));
        calendar.Selection.ShouldBeNull();
        var theme = calendar.Theme.ShouldNotBeNull();
        var preview = Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark));
        surface.Cell(new Point(26, 4)).Style.Foreground.ShouldBe(preview);
        surface.Cell(new Point(6, 5)).Style.Foreground.ShouldNotBe(preview);

        // Act commit
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert committed interval
        calendar.IntervalAnchor.ShouldBeNull();
        calendar.Selection.ShouldBe(new DateInterval(start, new DateOnly(2026, 7, 12)));
        surface.Cell(new Point(26, 4)).Style.Background.ShouldBe(
            Project(ThemeColorHelper.SelectionBackground(ThemeCatalog.Dark)));

        // Act and assert pointer interval parity
        await surface.Pointer.ClickAsync(calendar, new Point(10, 5));
        calendar.IntervalAnchor.ShouldBe(new DateOnly(2026, 7, 14));
        calendar.Selection.ShouldBeNull();
        await surface.Pointer.ClickAsync(calendar, new Point(18, 5));
        calendar.Selection.ShouldBe(new DateInterval(
            new DateOnly(2026, 7, 14),
            new DateOnly(2026, 7, 16)));
    }

    /// <summary>
    /// Verifies a click past the last date column, in the dead space between the seven-column
    /// grid's right edge and the arranged content bounds' actual right edge, selects nothing
    /// instead of silently resolving to a date in the grid. A wider-than-natural arrangement
    /// (matching how <c>Popup</c> stretches its content to at least the anchor's width) leaves
    /// that dead space inside <c>CalendarBounds</c> but outside the seven-column grid; the hit
    /// test's column index was never checked against the column count the way its row index
    /// already is.
    /// </summary>
    [Fact]
    public async Task Pointer_WhenClickIsPastTheLastDateColumn_SelectsNothingAsync()
    {
        // Arrange: stretched wider than the natural 32-cell frame (mirroring how Popup stretches
        // its content to at least the anchor's width), so CalendarBounds extends past the
        // seven-column, 28-cell date grid, leaving dead space before the right border.
        var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
            ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
            ┃ <            July 2026             > ┃
            ┃  Su  Mo  Tu  We  Th  Fr  Sa          ┃
            ┃  28  29  30   1   2   3   4          ┃
            ┃   5   6   7   8   9  10  11          ┃
            ┃  12  13  14  15  16  17  18          ┃
            ┃  19  20  21  22  23  24  25          ┃
            ┃  26  27  28  29  30  31   1          ┃
            ┃   2   3   4   5   6   7   8          ┃
            ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
            """);

        // Act: the seven-column grid occupies absolute columns 1..28 (28 cells wide, 4 per
        // column); column 35 is inside the arranged bounds (border at 0 and 39) but well past
        // the grid's last column.
        await surface.Pointer.ClickAsync(calendar, new Point(35, 3));

        // Assert
        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies exact rendering, hover, focus, keyboard, pointer, markup, and unavailable cleanup.</summary>
    [Fact]
    [ComponentBehaviorEvidence(
        typeof(UiCalendar),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.UnavailableCleanup)]
    public async Task Surface_WhenSelectInputIsDispatched_ExposesCompleteCalendarBehaviorAsync()
    {
        // Arrange
        var initial = new DateOnly(2026, 7, 19);
        var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1),
            Selection = new DateInterval(initial, initial)
        };
        calendar.Selection = null;
        calendar.BlockedDates.Block(new DateOnly(2026, 7, 12));
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(32, 10),
            TestContext.Current.CancellationToken);

        // Assert initial complete surface
        surface.ShouldRender("""
            ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
            ┃ <        July 2026         > ┃
            ┃  Su  Mo  Tu  We  Th  Fr  Sa  ┃
            ┃  28  29  30   1   2   3   4  ┃
            ┃   5   6   7   8   9  10  11  ┃
            ┃  12  13  14  15  16  17  18  ┃
            ┃  19  20  21  22  23  24  25  ┃
            ┃  26  27  28  29  30  31   1  ┃
            ┃   2   3   4   5   6   7   8  ┃
            ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
            """);

        // Act mounted wheel and header navigation
        await surface.Pointer.WheelAsync(calendar, new Point(15, 1), wheelY: 1);
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 6, 1));
        await surface.Pointer.ClickAsync(calendar, new Point(29, 1));
        calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 7, 1));

        // Act hover and keyboard selection
        await surface.Pointer.MoveToAsync(calendar, new Point(6, 5));
        calendar.HoveredDate.ShouldBe(new DateOnly(2026, 7, 13));
        var theme = calendar.Theme.ShouldNotBeNull();
        surface.Cell(new Point(6, 5)).Style.Background.ShouldBe(
            Project(ThemeColorHelper.HoveredBackground(ThemeCatalog.Dark)));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert keyboard route
        surface.ShouldHaveFocus(calendar);
        calendar.Selection.ShouldBe(new DateInterval(
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 20)));

        // Act pointer route and blocked cell
        await surface.Pointer.ClickAsync(calendar, new Point(22, 4));
        calendar.Selection.ShouldBe(new DateInterval(
            new DateOnly(2026, 7, 10),
            new DateOnly(2026, 7, 10)));
        await surface.Pointer.ClickAsync(calendar, new Point(2, 5));

        // Assert pointer and authored-state precedence
        calendar.Selection.ShouldBe(new DateInterval(
            new DateOnly(2026, 7, 10),
            new DateOnly(2026, 7, 10)));
        await surface.UpdateAsync(
            () =>
            {
                calendar.SetMarkup(new DateOnly(2026, 7, 4), "<fg=brightcyan><b>VIP</b></fg>");
                calendar.SetMarkup(
                    new DateOnly(2026, 7, 10),
                    "<u=curly><fg=brightred><b>BAD</b></fg></u>");
                calendar.SetMarkup(new DateOnly(2026, 7, 12), "<fg=brightred><b> X </b></fg>");
            },
            "author marked calendar dates");
        surface.Cell(new Point(26, 3)).Text.ShouldBe("V");
        surface.Cell(new Point(26, 3)).Style.Foreground.ShouldBe(
            Project(ThemeColorHelper.Accent(ThemeCatalog.Dark)));
        (surface.Cell(new Point(26, 3)).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        surface.Cell(new Point(22, 4)).Style.Foreground.ShouldBe(
            Project(ThemeColorHelper.SelectionForeground(ThemeCatalog.Dark)));
        surface.Cell(new Point(2, 5)).Style.Foreground.ShouldBe(
            Project(ThemeColorHelper.DisabledForeground(ThemeCatalog.Dark)));

        // Act unavailable cleanup
        await surface.Pointer.MoveToAsync(calendar, new Point(6, 5));
        await surface.UpdateAsync(() => calendar.Enabled = false, "disable hovered Calendar");

        // Assert cleanup
        calendar.HoveredDate.ShouldBeNull();
        surface.ShouldHaveFocus(null);
        surface.Cell(new Point(22, 4)).Style.Foreground.ShouldBe(
            Project(ThemeColorHelper.DisabledForeground(ThemeCatalog.Dark)));
    }

    /// <summary>Verifies a local CalendarStyle's selected-day and out-of-month-day colors reach the
    /// rendered cells, proving the day-grid render path reads ActualStyle instead of a hardcoded
    /// theme role.</summary>
    [Fact]
    public async Task Surface_WhenStyleReplacesSelectedAndOutOfMonthColors_RendersStyledCellsAsync()
    {
        // Arrange
        var selected = new DateOnly(2026, 7, 19);
        var style = CalendarStyle.Default with
        {
            SelectedDayColor = Color.Rgb(200, 30, 30),
            OutOfMonthDayColor = Color.Rgb(30, 30, 200)
        };
        var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1),
            Style = style,
            Selection = new DateInterval(selected, selected)
        };
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(32, 10),
            TestContext.Current.CancellationToken);

        // Assert selected-day foreground (19 July falls in week 3, Sunday column)
        surface.Cell(new Point(2, 6)).Style.Foreground.ShouldBe(Project(Color.Rgb(200, 30, 30)));

        // Assert out-of-month-day foreground (28 July preceding the displayed month, first grid cell)
        surface.Cell(new Point(2, 3)).Style.Foreground.ShouldBe(Project(Color.Rgb(30, 30, 200)));
    }

    /// <summary>Verifies GoToToday under a mounted fake clock moves to that clock's date rather
    /// than the real system clock, proving the call site reads the dispatcher's TimeProvider - the
    /// assertion would fail against the real system date if the call site reverted to
    /// <c>TimeProvider.System</c>.</summary>
    [Fact]
    public async Task Surface_WhenGoToTodayIsCalledUnderFakeClock_MovesToFakeDateAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var calendar = new UiCalendar
        {
            DisplayMonth = new DateOnly(2000, 1, 1),
            Selection = null
        };
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(32, 10),
            clock,
            TestContext.Current.CancellationToken);
        var expected = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        // Act
        var moved = false;
        await surface.UpdateAsync(() => moved = calendar.GoToToday(), "go to today under fake clock");

        // Assert
        moved.ShouldBeTrue();
        calendar.ActiveDate.ShouldBe(expected);
        calendar.DisplayMonth.ShouldBe(new DateOnly(expected.Year, expected.Month, 1));
    }

    /// <summary>Verifies today's cell renders with TodayMarkerColor while a neighboring cell does
    /// not, and that a committed selection covering today overrides the marker so a selected today
    /// still reads as selected. The calendar is mounted under a fake clock so the "today" under
    /// test is deterministic rather than depending on the real wall-clock date.</summary>
    [Fact]
    public async Task Surface_WhenTodayIsRendered_AppliesTodayMarkerUnlessSelectedAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var displayMonth = new DateOnly(today.Year, today.Month, 1);
        var neighbor = today.Day > 1 ? today.AddDays(-1) : today.AddDays(1);
        var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = displayMonth,
            Selection = null
        };
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(32, 10),
            clock,
            TestContext.Current.CancellationToken);
        var todayCell = CellFor(today, displayMonth, calendar.FirstDayOfWeek);
        var neighborCell = CellFor(neighbor, displayMonth, calendar.FirstDayOfWeek);
        var todayColor = Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark));

        // Assert unselected today carries the marker while its neighbor does not
        surface.Cell(todayCell).Style.Foreground.ShouldBe(todayColor);
        surface.Cell(neighborCell).Style.Foreground.ShouldNotBe(todayColor);

        // Act — select today
        await surface.UpdateAsync(
            () => calendar.Selection = new DateInterval(today, today),
            "select today");

        // Assert a selected today reads as selected, not as today
        surface.Cell(todayCell).Style.Foreground.ShouldBe(
            Project(ThemeColorHelper.SelectionForeground(ThemeCatalog.Dark)));
    }

    /// <summary>Resolves the absolute surface cell for one date inside a calendar's six-week grid,
    /// mirroring Calendar's own internal grid geometry (a four-cell-wide column inside a
    /// one-cell border plus one-cell horizontal padding, with the date grid beginning two rows
    /// below the content top).</summary>
    private static Point CellFor(DateOnly date, DateOnly displayMonth, DayOfWeek firstDayOfWeek)
    {
        var first = (int) firstDayOfWeek;
        var offset = ((int) displayMonth.DayOfWeek - first + 7) % 7;
        var firstDayNumber = displayMonth.DayNumber - offset;
        var index = date.DayNumber - firstDayNumber;
        var row = index / 7;
        var column = index % 7;
        return new Point(2 + (column * 4), 3 + row);
    }

    private static Color Project(Color color) =>
        TerminalPalette.Project(color, ColorDepth.Basic16);
}
