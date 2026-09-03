// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Globalization;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the DateInput control with live date-selection specimens.</summary>
internal sealed class DateInputPane: CompositeControlBase
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "DateInput";

    /// <summary>Initializes the retained interactive DateInput documentation page.</summary>
    internal DateInputPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        // Basic selection with live value tracking.
        var basicStatus = new Text("Value: (pick a date)");
        var basic = new DateInput
        {
            Culture = CultureInfo.InvariantCulture,
            DropDownHeight = Length.Percent(50)
        };
        basic.ValueChanged += (_, eventArgs) =>
            basicStatus.Content = $"Value: {FormatDate(eventArgs.Value)}";
        basicStatus.Content = $"Value: {FormatDate(basic.Value)}";

        // Bounded range with Minimum and Maximum.
        var boundsStatus = new Text("Value: (pick a date)");
        var minDate = new DateOnly(2026, 1, 1);
        var maxDate = new DateOnly(2026, 12, 31);
        var bounded = new DateInput
        {
            Culture = CultureInfo.InvariantCulture,
            Minimum = minDate,
            Maximum = maxDate
        };
        bounded.ValueChanged += (_, eventArgs) =>
            boundsStatus.Content = $"Value: {FormatDate(eventArgs.Value)}";
        boundsStatus.Content = $"Value: {FormatDate(bounded.Value)} (2026 only)";

        // Nullable with AllowNull=true.
        var nullableStatus = new Text("Value: (pick a date)");
        var nullable = new DateInput
        {
            Culture = CultureInfo.InvariantCulture,
            AllowNull = true
        };
        nullable.ValueChanged += (_, eventArgs) =>
            nullableStatus.Content = $"Value: {FormatDate(eventArgs.Value)}";
        nullableStatus.Content = $"Value: {FormatDate(nullable.Value)}";

        // Localized culture.
        var statusCulture = new Text("Value: (pick a date)");
        var inputCulture = new DateInput { Culture = new CultureInfo("de-DE") };
        inputCulture.ValueChanged += (_, eventArgs) =>
            statusCulture.Content = $"Value: {FormatDate(eventArgs.Value)}";
        statusCulture.Content = $"Value: {FormatDate(inputCulture.Value)}";

        // Custom format.
        var statusFormat = new Text("Value: (pick a date)");
        var inputFormat = new DateInput { Format = "yyyy/MM/dd" };
        inputFormat.ValueChanged += (_, eventArgs) =>
            statusFormat.Content = $"Value: {FormatDate(eventArgs.Value)}";
        statusFormat.Content = $"Value: {FormatDate(inputFormat.Value)}";

        // StartAffix reserves a fixed cell for an application-owned deadline marker.
        var deadlineStatus = new Text("Value: (pick a date)");
        var deadline = new DateInput
        {
            Culture = CultureInfo.InvariantCulture,
            StartAffix = new Affix("⏰", "!", SemanticColor.Warning)
        };
        deadline.ValueChanged += (_, eventArgs) =>
            deadlineStatus.Content = $"Value: {FormatDate(eventArgs.Value)}";
        deadlineStatus.Content = $"Value: {FormatDate(deadline.Value)}";

        return new DocPage(
            Title,
            "<info>DateInput</info> displays a formatted date with inline segment editing and an optional Calendar popup.",
            new DocSection(
                "📅",
                "Basic selection",
                "Edit date segments inline with <reverse>Up</reverse>/<reverse>Down</reverse> arrows, or open the Calendar popup with <reverse>Alt+Down</reverse> or <reverse>F4</reverse>.",
                new DocExample(
                    "Pick a date",
                    "<reverse>Left</reverse>/<reverse>Right</reverse> move between segments. The Calendar uses half the usable placement-side height and responds to terminal resize. Click or press <reverse>Enter</reverse> in the popup to commit.",
                    new DocColumn(basic, basicStatus),
                    "var dateInput = new DateInput\n{\n    DropDownHeight = Length.Percent(50)\n};\ndateInput.ValueChanged += (_, e) =>\n    Console.Write(e.Value);")),
            new DocSection(
                "📐",
                "Bounds and validation",
                "Constrain the selectable range with <info>Minimum</info> and <info>Maximum</info>. Values outside the range are clamped automatically.",
                new DocExample(
                    "Bounded to a single year",
                    "Segments that would push the date outside the permitted range are clamped, and the Calendar popup greys out blocked months.",
                    new DocColumn(bounded, boundsStatus),
                    "dateInput.Minimum = new DateOnly(2026, 1, 1);\ndateInput.Maximum = new DateOnly(2026, 12, 31);")),
            new DocSection(
                "🚫",
                "Nullable",
                "With <info>AllowNull</info> set, pressing <reverse>Delete</reverse> clears the committed value to null, displaying placeholder dashes.",
                new DocExample(
                    "Clearable date field",
                    "Press <reverse>Delete</reverse> to clear. The Calendar popup and inline editing restore a value on the next selection.",
                    new DocColumn(nullable, nullableStatus),
                    "dateInput.AllowNull = true;\n// Value is null when cleared")),
            new DocSection(
                "🌍",
                "Localized culture",
                "<info>Culture</info> localizes the Calendar popup and the typed field's date segment order, separators, and digits. The setter accepts only cultures whose calendar is Gregorian, throwing <info>ArgumentException</info> otherwise.",
                new DocExample(
                    "German date field",
                    "German renders day before month with a period separator (\"dd.MM.yyyy\") instead of the invariant month-day-year slash order.",
                    new DocColumn(inputCulture, statusCulture),
                    "dateInput.Culture = new CultureInfo(\"de-DE\");")),
            new DocSection(
                "🧩",
                "Custom format",
                "<info>Format</info> overrides the derived date pattern entirely, taking precedence over the short date pattern that <info>Culture</info> would otherwise select.",
                new DocExample(
                    "Year-first date field",
                    "The pattern's own tokens select the segment order and separators, overriding whatever <info>Culture</info> would otherwise produce.",
                    new DocColumn(inputFormat, statusFormat),
                    "dateInput.Format = \"yyyy/MM/dd\";")),
            new DocSection(
                "⏰",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell beside the date segments for application-owned, data-driven decoration that a theme never authors.",
                new DocExample(
                    "Deadline glyph before the date",
                    "The alarm glyph sits in its own reserved cell and never shares space with the editable segments.",
                    new DocColumn(deadline, deadlineStatus),
                    "var dateInput = new DateInput\n{\n    StartAffix = new Affix(\"⏰\", \"!\", SemanticColor.Warning)\n};")));
    }

    private static string FormatDate(DateOnly? date) =>
        date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "(none)";
}
