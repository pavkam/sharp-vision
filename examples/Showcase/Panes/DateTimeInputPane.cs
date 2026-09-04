// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Globalization;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the DateTimeInput control with live combined date-time specimens.</summary>
internal sealed class DateTimeInputPane: CompositeControlBase
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "DateTimeInput";

    /// <summary>Initializes the retained interactive DateTimeInput documentation page.</summary>
    internal DateTimeInputPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        // Combined date-time input with live value tracking.
        var combinedStatus = CreateStatus();
        var combined = new DateTimeInput
        {
            TimeStep = TimeSpan.FromMinutes(15),
            DropDownHeight = Length.Percent(50),
            ShowSeconds = true,
            Value = new DateTime(2026, 9, 3, 14, 30, 5, DateTimeKind.Utc),
            CalendarStyle = CalendarStyle.Default with { SelectedDayColor = Color.Rgb(0x77, 0xaa, 0xff) },
            PopupChrome = new PopupChrome
            {
                Border = new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Heavy,
                    Color.Rgb(0x77, 0xaa, 0xff),
                    Color.Transparent,
                    TerminalAttributes.None)
            }
        };
        combined.ValueChanged += (_, eventArgs) =>
            combinedStatus.Content = $"Value: {FormatDateTime(eventArgs.Value)}";
        combinedStatus.Content = $"Value: {FormatDateTime(combined.Value)}";

        // 12-hour format with AM/PM.
        var status12 = CreateStatus();
        var input12 = new DateTimeInput
        {
            Use24HourFormat = false,
            Value = new DateTime(2026, 9, 3, 9, 45, 0, DateTimeKind.Utc)
        };
        input12.ValueChanged += (_, eventArgs) =>
            status12.Content = $"Value: {FormatDateTime(eventArgs.Value, use12Hour: true)}";
        status12.Content = $"Value: {FormatDateTime(input12.Value, use12Hour: true)}";

        // Nullable.
        var statusNull = CreateStatus();
        var inputNull = new DateTimeInput { AllowNull = true, Value = null };
        inputNull.ValueChanged += (_, eventArgs) =>
            statusNull.Content = $"Value: {FormatDateTime(eventArgs.Value)}";
        statusNull.Content = $"Value: {FormatDateTime(inputNull.Value)}";

        // Localized culture.
        var statusCulture = CreateStatus();
        var inputCulture = new DateTimeInput
        {
            Culture = new CultureInfo("de-DE"),
            Value = new DateTime(2026, 9, 3, 14, 30, 0, DateTimeKind.Utc)
        };
        inputCulture.ValueChanged += (_, eventArgs) =>
            statusCulture.Content = $"Value: {FormatDateTime(eventArgs.Value)}";
        statusCulture.Content = $"Value: {FormatDateTime(inputCulture.Value)}";

        // Custom format.
        var statusFormat = CreateStatus();
        var inputFormat = new DateTimeInput
        {
            Format = "yyyy/MM/dd HH:mm:ss.fff",
            Value = new DateTime(2026, 9, 3, 14, 30, 5, 123, DateTimeKind.Utc)
        };
        inputFormat.ValueChanged += (_, eventArgs) =>
            statusFormat.Content = $"Value: {FormatDateTime(eventArgs.Value, showFraction: true)}";
        statusFormat.Content = $"Value: {FormatDateTime(inputFormat.Value, showFraction: true)}";

        // EndAffix reserves a fixed cell for an application-owned reminder marker.
        var statusReminder = CreateStatus();
        var inputReminder = new DateTimeInput
        {
            EndAffix = new Affix("🔔", "!", SemanticColor.Info),
            Value = new DateTime(2026, 9, 3, 18, 45, 0, DateTimeKind.Utc)
        };
        inputReminder.ValueChanged += (_, eventArgs) =>
            statusReminder.Content = $"Value: {FormatDateTime(eventArgs.Value)}";
        statusReminder.Content = $"Value: {FormatDateTime(inputReminder.Value)}";

        return new DocPage(
            Title,
            "<info>DateTimeInput</info> combines date and time editing in one bordered field with a Calendar popup for date segments and inline editing for time segments.",
            new DocSection(
                "📅",
                "Combined input",
                "All segments edit inline; this specimen advances minutes in 15-minute steps. The disclosure, <reverse>Alt+Down</reverse>, or <reverse>F4</reverse> opens the Calendar popup from any segment.",
                new DocExample(
                    "Responsive date-time field",
                    "<reverse>Left</reverse>/<reverse>Right</reverse> navigate all segments. The styled Calendar popup uses half the usable placement-side height and preserves the current time portion.",
                    new DocColumn(combined, combinedStatus),
                    "var dateTime = new DateTimeInput\n{\n    Value = new DateTime(2026, 9, 3, 14, 30, 5, DateTimeKind.Utc),\n    ShowSeconds = true,\n    TimeStep = TimeSpan.FromMinutes(15),\n    DropDownHeight = Length.Percent(50),\n    CalendarStyle = CalendarStyle.Default with\n    {\n        SelectedDayColor = Color.Rgb(0x77, 0xaa, 0xff)\n    },\n    PopupChrome = new PopupChrome\n    {\n        Border = new Border(BorderSide.All, BorderGlyphStyle.Heavy,\n            Color.Rgb(0x77, 0xaa, 0xff), Color.Transparent, TerminalAttributes.None)\n    }\n};\ndateTime.ValueChanged += (_, e) =>\n    Console.Write(e.Value);")),
            new DocSection(
                "🕐",
                "12-hour format",
                "Set <info>Use24HourFormat</info> to false for a 12-hour clock with an AM/PM segment appended after the time.",
                new DocExample(
                    "AM/PM combined field",
                    "The AM/PM segment toggles with <reverse>Up</reverse>/<reverse>Down</reverse> or by typing <reverse>A</reverse> or <reverse>P</reverse>.",
                    new DocColumn(input12, status12),
                    "dateTime.Use24HourFormat = false;")),
            new DocSection(
                "🚫",
                "Nullable",
                "With <info>AllowNull</info> set, pressing <reverse>Delete</reverse> clears the value to null, displaying placeholder dashes for all segments.",
                new DocExample(
                    "Clearable date-time field",
                    "Press <reverse>Delete</reverse> to clear. The Calendar popup or inline editing restores a concrete value on the next interaction.",
                    new DocColumn(inputNull, statusNull),
                    "dateTime.AllowNull = true;\n// Value is null when cleared")),
            new DocSection(
                "🌍",
                "Localized culture",
                "<info>Culture</info> localizes both the popup calendar and the typed field's date segment order, separators, and digits. It defaults to invariant, so out-of-the-box rendering never depends on the host locale.",
                new DocExample(
                    "German date-time field",
                    "German renders day before month with a period separator (\"dd.MM.yyyy\") instead of the invariant month-day-year slash order.",
                    new DocColumn(inputCulture, statusCulture),
                    "dateTime.Culture = new CultureInfo(\"de-DE\");")),
            new DocSection(
                "🧩",
                "Custom format",
                "<info>Format</info> overrides the derived combined pattern entirely, taking precedence over <info>Culture</info>'s date pattern and <info>Use24HourFormat</info>/<info>ShowSeconds</info>.",
                new DocExample(
                    "Year-first fractional field",
                    "The pattern's own tokens select the date segment order and expose an editable millisecond segment, but a literal \"/\" or \":\" in the pattern still resolves to the active culture's date or time separator.",
                    new DocColumn(inputFormat, statusFormat),
                    "dateTime.Format = \"yyyy/MM/dd HH:mm:ss.fff\";")),
            new DocSection(
                "🔔",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell beside the combined segments for application-owned, data-driven decoration that a theme never authors.",
                new DocExample(
                    "Reminder glyph after the date and time",
                    "The bell glyph sits in its own reserved cell after the last segment and never shares space with the editable field.",
                    new DocColumn(inputReminder, statusReminder),
                    "var dateTime = new DateTimeInput\n{\n    EndAffix = new Affix(\"🔔\", \"!\", SemanticColor.Info)\n};")));
    }

    private static string FormatDateTime(
        DateTime? dateTime,
        bool use12Hour = false,
        bool showFraction = false) =>
        dateTime.HasValue
            ? dateTime.Value.ToString(
                showFraction
                    ? "yyyy-MM-dd HH:mm:ss.fff"
                    : use12Hour ? "yyyy-MM-dd hh:mm tt" : "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture)
            : "(none)";

    private static Text CreateStatus() => new("Value: (none)") { Width = Length.Cells(36) };
}
