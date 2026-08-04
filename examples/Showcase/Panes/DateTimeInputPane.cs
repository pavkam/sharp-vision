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
        var combinedStatus = new Text("Value: (pick a date and time)");
        var combined = new DateTimeInput { TimeStep = TimeSpan.FromMinutes(15) };
        combined.ValueChanged += (_, eventArgs) =>
            combinedStatus.Content = $"Value: {FormatDateTime(eventArgs.Value)}";
        combinedStatus.Content = $"Value: {FormatDateTime(combined.Value)}";

        // 12-hour format with AM/PM.
        var status12 = new Text("Value: (pick a date and time)");
        var input12 = new DateTimeInput { Use24HourFormat = false };
        input12.ValueChanged += (_, eventArgs) =>
            status12.Content = $"Value: {FormatDateTime(eventArgs.Value, use12Hour: true)}";
        status12.Content = $"Value: {FormatDateTime(input12.Value, use12Hour: true)}";

        // Nullable.
        var statusNull = new Text("Value: (pick a date and time)");
        var inputNull = new DateTimeInput { AllowNull = true };
        inputNull.ValueChanged += (_, eventArgs) =>
            statusNull.Content = $"Value: {FormatDateTime(eventArgs.Value)}";
        statusNull.Content = $"Value: {FormatDateTime(inputNull.Value)}";

        // Localized culture.
        var statusCulture = new Text("Value: (pick a date and time)");
        var inputCulture = new DateTimeInput { Culture = new CultureInfo("de-DE") };
        inputCulture.ValueChanged += (_, eventArgs) =>
            statusCulture.Content = $"Value: {FormatDateTime(eventArgs.Value)}";
        statusCulture.Content = $"Value: {FormatDateTime(inputCulture.Value)}";

        return new DocPage(
            Title,
            "<info>DateTimeInput</info> combines date and time editing in one bordered field with a Calendar popup for date segments and inline editing for time segments.",
            new DocSection(
                "📅",
                "Combined input",
                "Date segments open a Calendar popup on activation. Time segments edit inline; this specimen advances minutes in 15-minute steps. <reverse>Alt+Down</reverse> opens the popup from any segment.",
                new DocExample(
                    "Default date-time field",
                    "<reverse>Left</reverse>/<reverse>Right</reverse> navigate all segments. The Calendar popup commits a date while preserving the current time portion.",
                    new DocColumn(combined, combinedStatus),
                    "var dateTime = new DateTimeInput { TimeStep = TimeSpan.FromMinutes(15) };\ndateTime.ValueChanged += (_, e) =>\n    Console.Write(e.Value);")),
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
                    "dateTime.Culture = new CultureInfo(\"de-DE\");")));
    }

    private static string FormatDateTime(DateTime? dateTime, bool use12Hour = false) =>
        dateTime.HasValue
            ? dateTime.Value.ToString(
                use12Hour ? "yyyy-MM-dd hh:mm tt" : "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture)
            : "(none)";
}
