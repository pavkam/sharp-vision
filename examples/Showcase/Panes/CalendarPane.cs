// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Globalization;

using Text = SharpVision.Controls.Display.Text;
using UiCalendar = Calendar;

/// <summary>Documents single-date, interval, blocked-date, and rich-face Calendar behavior.</summary>
internal sealed class CalendarPane: CompositeControl
{
    private static readonly DateOnly _month = new(2026, 7, 1);

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Calendar";

    /// <summary>Initializes the retained interactive Calendar documentation page.</summary>
    internal CalendarPane()
    {
        SelectCalendar = CreateCalendar();
        _ = SelectCalendar.Select(new DateOnly(2026, 7, 19));
        SelectStatus = new Text(FormatSelection("Selected", SelectCalendar.Selection));
        SelectCalendar.SelectionChanged += (_, eventArgs) =>
            SelectStatus.Content = FormatSelection("Selected", eventArgs.Selection);

        IntervalCalendar = CreateCalendar();
        IntervalCalendar.SelectionMode = CalendarSelectionMode.Interval;
        _ = IntervalCalendar.Select(new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 12));
        IntervalStatus = new Text(FormatSelection("Booked", IntervalCalendar.Selection));
        IntervalCalendar.SelectionChanged += (_, eventArgs) =>
            IntervalStatus.Content = FormatSelection("Booked", eventArgs.Selection);

        SpecialCalendar = CreateSpecialCalendar();
        InitializeContent(CreateContent());
    }

    /// <summary>Gets the live single-date specimen.</summary>
    internal UiCalendar SelectCalendar { get; }

    /// <summary>Gets the live single-date readout.</summary>
    internal Text SelectStatus { get; }

    /// <summary>Gets the live inclusive-interval specimen.</summary>
    internal UiCalendar IntervalCalendar { get; }

    /// <summary>Gets the live inclusive-interval readout.</summary>
    internal Text IntervalStatus { get; }

    /// <summary>Gets the blocked and authored-face specimen.</summary>
    internal UiCalendar SpecialCalendar { get; }

    private DocPage CreateContent() => new DocPage(
        Title,
        "<info>Calendar</info> selects one date or an inclusive interval in a compact, localized six-week grid.",
        new DocSection(
            "📅",
            "Select mode",
            "One activation commits one date. Adjacent-month faces remain selectable and bring their month into view.",
            new DocExample(
                "Pick one date",
                "Use arrows, <reverse>Home</reverse>/<reverse>End</reverse>, or <reverse>Page Up</reverse>/<reverse>Page Down</reverse>; press <reverse>Enter</reverse> or click to select. FirstDayOfWeek and GoToToday() customize basic navigation.",
                new DocColumn(SelectCalendar, SelectStatus),
                "var calendar = new Calendar { FirstDayOfWeek = DayOfWeek.Monday };\ncalendar.Select(new DateOnly(2026, 7, 19));\ncalendar.GoToToday();")),
        new DocSection(
            "↔️",
            "Interval mode",
            "The first activation anchors a live preview; the second commits ordered inclusive endpoints.",
            new DocExample(
                "Book a date interval",
                "After a completed booking, the next activation starts a fresh interval without rebuilding the control.",
                new DocColumn(IntervalCalendar, IntervalStatus),
                "calendar.SelectionMode = CalendarSelectionMode.Interval;\ncalendar.Select(start, end);")),
        new DocSection(
            "✨",
            "Blocked and marked dates",
            "Blocked ranges stay visible with disabled semantics. Sparse trusted markup can replace individual four-cell faces without disturbing geometry.",
            new DocExample(
                "Availability and events",
                "The maintenance window cannot be selected; starred and dotted dates are authored rich faces, and disabled state wins over authored color.",
                SpecialCalendar,
                "calendar.BlockedDates.Block(new DateInterval(start, end));\ncalendar.SetMarkup(eventDate, \"<accent><b>19★</b></accent>\");")),
        new DocSection(
            "⌨️",
            "Keyboard and pointer",
            "<reverse>Tab</reverse> enters one focus stop. Arrows move by day or week, <reverse>Home</reverse>/<reverse>End</reverse> stay within the week, paging changes month, wheel changes the displayed month, and blocked dates are skipped."));

    private static UiCalendar CreateCalendar() => new()
    {
        Culture = CultureInfo.InvariantCulture,
        DisplayMonth = _month
    };

    private static UiCalendar CreateSpecialCalendar()
    {
        var calendar = CreateCalendar();
        calendar.BlockedDates.Block(new DateInterval(
            new DateOnly(2026, 7, 12),
            new DateOnly(2026, 7, 14)));
        calendar.BlockedDates.Block(new DateOnly(2026, 7, 24));
        calendar.SetMarkup(new DateOnly(2026, 7, 4), "<accent><b> 4• </b></accent>");
        calendar.SetMarkup(new DateOnly(2026, 7, 13), "<error><b> X  </b></error>");
        calendar.SetMarkup(new DateOnly(2026, 7, 19), "<info><b>19★ </b></info>");
        return calendar;
    }

    private static string FormatSelection(string label, DateInterval? selection) => selection switch
    {
        null => $"{label}: choose a date",
        { Start: var start, End: var end } when start == end => $"{label}: {start:yyyy-MM-dd}",
        { Start: var start, End: var end } => $"{label}: {start:yyyy-MM-dd} → {end:yyyy-MM-dd}"
    };
}
