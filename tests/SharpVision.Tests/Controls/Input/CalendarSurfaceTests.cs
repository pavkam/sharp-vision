// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using UiCalendar = SharpVision.Controls.Input.Calendar;

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
        (surface.Cell(new Point(22, 7)).Style.Attributes & Attributes.Underline)
            .ShouldBe(Attributes.Underline);

        // Act and assert Space activation
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        calendar.Selection.ShouldBe(new DateInterval(active, active));
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
        var preview = Project(ThemeColorHelper.HoveredForeground(Themes.Dark));
        surface.Cell(new Point(26, 4)).Style.Foreground.ShouldBe(preview);
        surface.Cell(new Point(6, 5)).Style.Foreground.ShouldNotBe(preview);

        // Act commit
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert committed interval
        calendar.IntervalAnchor.ShouldBeNull();
        calendar.Selection.ShouldBe(new DateInterval(start, new DateOnly(2026, 7, 12)));
        surface.Cell(new Point(26, 4)).Style.Background.ShouldBe(
            Project(ThemeColorHelper.SelectionBackground(Themes.Dark)));

        // Act and assert pointer interval parity
        await surface.Pointer.ClickAsync(calendar, new Point(10, 5));
        calendar.IntervalAnchor.ShouldBe(new DateOnly(2026, 7, 14));
        calendar.Selection.ShouldBeNull();
        await surface.Pointer.ClickAsync(calendar, new Point(18, 5));
        calendar.Selection.ShouldBe(new DateInterval(
            new DateOnly(2026, 7, 14),
            new DateOnly(2026, 7, 16)));
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
            Project(ThemeColorHelper.HoveredBackground(Themes.Dark)));
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
            Project(ThemeColorHelper.Accent(Themes.Dark)));
        (surface.Cell(new Point(26, 3)).Style.Attributes & Attributes.Bold).ShouldBe(Attributes.Bold);
        surface.Cell(new Point(22, 4)).Style.Foreground.ShouldBe(
            Project(ThemeColorHelper.SelectionForeground(Themes.Dark)));
        surface.Cell(new Point(2, 5)).Style.Foreground.ShouldBe(
            Project(ThemeColorHelper.DisabledForeground(Themes.Dark)));

        // Act unavailable cleanup
        await surface.Pointer.MoveToAsync(calendar, new Point(6, 5));
        await surface.UpdateAsync(() => calendar.IsEnabled = false, "disable hovered Calendar");

        // Assert cleanup
        calendar.HoveredDate.ShouldBeNull();
        surface.ShouldHaveFocus(null);
        surface.Cell(new Point(22, 4)).Style.Foreground.ShouldBe(
            Project(ThemeColorHelper.DisabledForeground(Themes.Dark)));
    }

    private static Color Project(Color color) =>
        Palette.Project(color, ColorDepth.Basic16);
}
