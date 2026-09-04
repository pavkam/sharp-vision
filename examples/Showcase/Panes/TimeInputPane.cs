// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Globalization;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the TimeInput control with live time-editing specimens.</summary>
internal sealed class TimeInputPane: CompositeControlBase
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "TimeInput";

    /// <summary>Initializes the retained interactive TimeInput documentation page.</summary>
    internal TimeInputPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        // 24-hour format (default).
        var status24 = CreateStatus();
        var input24 = new TimeInput
        {
            TimeStep = TimeSpan.FromMinutes(15),
            Value = new TimeOnly(14, 30)
        };
        input24.ValueChanged += (_, eventArgs) =>
            status24.Content = $"Value: {FormatTime(eventArgs.Value)}";
        status24.Content = $"Value: {FormatTime(input24.Value)}";

        // 12-hour format with AM/PM.
        var status12 = CreateStatus();
        var input12 = new TimeInput
        {
            Use24HourFormat = false,
            Value = new TimeOnly(9, 45)
        };
        input12.ValueChanged += (_, eventArgs) =>
            status12.Content = $"Value: {FormatTime(eventArgs.Value)}";
        status12.Content = $"Value: {FormatTime(input12.Value)}";

        // With seconds visible.
        var statusSec = CreateStatus();
        var inputSec = new TimeInput
        {
            ShowSeconds = true,
            Value = new TimeOnly(14, 30, 5)
        };
        inputSec.ValueChanged += (_, eventArgs) =>
            statusSec.Content = $"Value: {FormatTime(eventArgs.Value, showSeconds: true)}";
        statusSec.Content = $"Value: {FormatTime(inputSec.Value, showSeconds: true)}";

        // Nullable.
        var statusNull = CreateStatus();
        var inputNull = new TimeInput { AllowNull = true, Value = null };
        inputNull.ValueChanged += (_, eventArgs) =>
            statusNull.Content = $"Value: {FormatTime(eventArgs.Value)}";
        statusNull.Content = $"Value: {FormatTime(inputNull.Value)}";

        // Localized culture.
        var statusCulture = CreateStatus();
        var inputCulture = new TimeInput
        {
            Culture = new CultureInfo("fi-FI"),
            Use24HourFormat = false,
            Value = new TimeOnly(14, 30)
        };
        inputCulture.ValueChanged += (_, eventArgs) =>
            statusCulture.Content = $"Value: {FormatTime(eventArgs.Value)}";
        statusCulture.Content = $"Value: {FormatTime(inputCulture.Value)}";

        // Custom format.
        var statusFormat = CreateStatus();
        var inputFormat = new TimeInput
        {
            Format = "HH:mm:ss.fff",
            Value = new TimeOnly(14, 30, 5, 123)
        };
        inputFormat.ValueChanged += (_, eventArgs) =>
            statusFormat.Content = $"Value: {FormatTime(eventArgs.Value, showFraction: true)}";
        statusFormat.Content = $"Value: {FormatTime(inputFormat.Value, showFraction: true)}";

        // StartAffix reserves a fixed cell for an application-owned pinned-reminder marker.
        var statusPinned = CreateStatus();
        var inputPinned = new TimeInput
        {
            StartAffix = new Affix("📌", "*", SemanticColor.Accent),
            Value = new TimeOnly(18, 45)
        };
        inputPinned.ValueChanged += (_, eventArgs) =>
            statusPinned.Content = $"Value: {FormatTime(eventArgs.Value)}";
        statusPinned.Content = $"Value: {FormatTime(inputPinned.Value)}";

        return new DocPage(
            Title,
            "<info>TimeInput</info> edits a time value with inline segment navigation and optional 12-hour or seconds display.",
            new DocSection(
                "🕐",
                "24-hour format",
                "The default display uses a 24-hour clock. <reverse>Up</reverse>/<reverse>Down</reverse> arrows adjust the focused segment; this specimen uses a 15-minute TimeStep for the minute segment.",
                new DocExample(
                    "Default time field",
                    "<reverse>Left</reverse>/<reverse>Right</reverse> navigate between hour and minute segments. <reverse>Up</reverse>/<reverse>Down</reverse> increment or decrement.",
                    new DocColumn(input24, status24),
                    "var timeInput = new TimeInput { TimeStep = TimeSpan.FromMinutes(15) };\ntimeInput.ValueChanged += (_, e) =>\n    Console.Write(e.Value);")),
            new DocSection(
                "🕑",
                "12-hour format",
                "Set <info>Use24HourFormat</info> to false for a 12-hour clock with an AM/PM segment.",
                new DocExample(
                    "AM/PM time field",
                    "The AM/PM segment toggles with <reverse>Up</reverse>/<reverse>Down</reverse> or by typing <reverse>A</reverse> or <reverse>P</reverse>.",
                    new DocColumn(input12, status12),
                    "timeInput.Use24HourFormat = false;")),
            new DocSection(
                "⏱️",
                "With seconds",
                "Enable <info>ShowSeconds</info> to add a third editable segment for second-level precision.",
                new DocExample(
                    "Seconds-precision field",
                    "The seconds segment behaves identically to hour and minute: arrow keys, digit entry, and segment navigation.",
                    new DocColumn(inputSec, statusSec),
                    "timeInput.ShowSeconds = true;")),
            new DocSection(
                "🚫",
                "Nullable",
                "With <info>AllowNull</info> set, pressing <reverse>Delete</reverse> clears the committed value to null, displaying placeholder dashes.",
                new DocExample(
                    "Clearable time field",
                    "Press <reverse>Delete</reverse> to clear. Any subsequent edit restores a concrete time value.",
                    new DocColumn(inputNull, statusNull),
                    "timeInput.AllowNull = true;\n// Value is null when cleared")),
            new DocSection(
                "🌍",
                "Localized culture",
                "<info>Culture</info> localizes the time separator and the AM/PM designator text. It defaults to invariant, so out-of-the-box rendering never depends on the host locale.",
                new DocExample(
                    "Finnish time field",
                    "Finnish uses a period time separator and \"ap.\"/\"ip.\" designators instead of a colon and \"AM\"/\"PM\".",
                    new DocColumn(inputCulture, statusCulture),
                    "timeInput.Culture = new CultureInfo(\"fi-FI\");\ntimeInput.Use24HourFormat = false;")),
            new DocSection(
                "🧩",
                "Custom format",
                "<info>Format</info> overrides the derived segment order entirely with a custom pattern, taking precedence over <info>Use24HourFormat</info> and <info>ShowSeconds</info>.",
                new DocExample(
                    "Fractional-second field",
                    "One to seven <info>f</info> or <info>F</info> tokens create an editable fractional-second segment at that precision, but a literal \"/\" or \":\" in the pattern still resolves to the active culture's date or time separator.",
                    new DocColumn(inputFormat, statusFormat),
                    "timeInput.Format = \"HH:mm:ss.fff\";")),
            new DocSection(
                "📌",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell beside the time segments for application-owned, data-driven decoration that a theme never authors.",
                new DocExample(
                    "Pinned-reminder glyph before the time",
                    "The pin glyph sits in its own reserved cell and never shares space with the editable segments.",
                    new DocColumn(inputPinned, statusPinned),
                    "var timeInput = new TimeInput\n{\n    StartAffix = new Affix(\"📌\", \"*\", SemanticColor.Accent)\n};")));
    }

    private static string FormatTime(
        TimeOnly? time,
        bool showSeconds = false,
        bool showFraction = false) =>
        time.HasValue
            ? time.Value.ToString(
                showFraction ? "HH:mm:ss.fff" : showSeconds ? "HH:mm:ss" : "HH:mm",
                CultureInfo.InvariantCulture)
            : "(none)";

    private static Text CreateStatus() => new("Value: (none)") { Width = Length.Cells(28) };
}
