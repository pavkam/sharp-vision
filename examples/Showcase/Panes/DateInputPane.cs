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
            Culture = CultureInfo.InvariantCulture
        };
        basic.ValueChanged += (_, eventArgs) =>
            basicStatus.Content = $"Value: {FormatDate(eventArgs.Value)}";
        basicStatus.Content = $"Value: {FormatDate(basic.Value)}";

        // Bounded range with MinimumDate and MaximumDate.
        var boundsStatus = new Text("Value: (pick a date)");
        var minDate = new DateOnly(2026, 1, 1);
        var maxDate = new DateOnly(2026, 12, 31);
        var bounded = new DateInput
        {
            Culture = CultureInfo.InvariantCulture,
            MinimumDate = minDate,
            MaximumDate = maxDate
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

        return new DocPage(
            Title,
            "<info>DateInput</info> displays a formatted date with inline segment editing and an optional Calendar popup.",
            new DocSection(
                "📅",
                "Basic selection",
                "Edit date segments inline with <reverse>Up</reverse>/<reverse>Down</reverse> arrows, or open the Calendar popup with <reverse>Alt+Down</reverse> or <reverse>F4</reverse>.",
                new DocExample(
                    "Pick a date",
                    "<reverse>Left</reverse>/<reverse>Right</reverse> move between segments. <reverse>Up</reverse>/<reverse>Down</reverse> adjust the focused segment. Click or press <reverse>Enter</reverse> in the popup to commit.",
                    new DocColumn(basic, basicStatus),
                    "var dateInput = new DateInput();\ndateInput.ValueChanged += (_, e) =>\n    Console.Write(e.Value);")),
            new DocSection(
                "📐",
                "Bounds and validation",
                "Constrain the selectable range with <info>MinimumDate</info> and <info>MaximumDate</info>. Values outside the range are clamped automatically.",
                new DocExample(
                    "Bounded to a single year",
                    "Segments that would push the date outside the permitted range are clamped, and the Calendar popup greys out blocked months.",
                    new DocColumn(bounded, boundsStatus),
                    "dateInput.MinimumDate = new DateOnly(2026, 1, 1);\ndateInput.MaximumDate = new DateOnly(2026, 12, 31);")),
            new DocSection(
                "🚫",
                "Nullable",
                "With <info>AllowNull</info> set, pressing <reverse>Delete</reverse> clears the committed value to null, displaying placeholder dashes.",
                new DocExample(
                    "Clearable date field",
                    "Press <reverse>Delete</reverse> to clear. The Calendar popup and inline editing restore a value on the next selection.",
                    new DocColumn(nullable, nullableStatus),
                    "dateInput.AllowNull = true;\n// Value is null when cleared")));
    }

    private static string FormatDate(DateOnly? date) =>
        date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "(none)";
}
