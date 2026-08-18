// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Globalization;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the NumberInput control with live integer, decimal, bounded, and nullable
/// specimens.</summary>
internal sealed class NumberInputPane: CompositeControlBase
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "NumberInput";

    /// <summary>Initializes the retained interactive NumberInput documentation page.</summary>
    internal NumberInputPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        // Whole-number editing.
        var integerStatus = new Text("Value: 0");
        var integer = new NumberInput { Mode = NumberInputMode.Integer, Value = 0m };
        integer.ValueChanged += (_, eventArgs) =>
            integerStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // Decimal editing with two fractional places.
        var decimalStatus = new Text("Value: 0.00");
        var @decimal = new NumberInput { DecimalPlaces = 2, Value = 0m };
        @decimal.ValueChanged += (_, eventArgs) =>
            decimalStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // Bounded range with a custom Step.
        var boundedStatus = new Text("Value: 50");
        var bounded = new NumberInput { Minimum = 0m, Maximum = 100m, Step = 5m, Value = 50m };
        bounded.ValueChanged += (_, eventArgs) =>
            boundedStatus.Content = $"Value: {FormatValue(eventArgs.Value)} (0-100 by 5)";

        // Nullable with AllowNull=true.
        var nullableStatus = new Text("Value: (none)");
        var nullable = new NumberInput { AllowNull = true };
        nullable.ValueChanged += (_, eventArgs) =>
            nullableStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // Grouped display for large magnitudes.
        var groupedStatus = new Text("Value: 1,234,567");
        var grouped = new NumberInput { AllowGrouping = true, DecimalPlaces = 0, Value = 1234567m };
        grouped.ValueChanged += (_, eventArgs) =>
            groupedStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // Non-invariant culture.
        var cultureStatus = new Text("Value: 1234,56");
        var culture = new NumberInput { Culture = new CultureInfo("de-DE"), DecimalPlaces = 2, Value = 1234.56m };
        culture.ValueChanged += (_, eventArgs) =>
            cultureStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // EndAffix reserves a fixed cell for an application-owned unit suffix.
        var percentStatus = new Text("Value: 50");
        var percent = new NumberInput
        {
            Minimum = 0m,
            Maximum = 100m,
            Value = 50m,
            EndAffix = new Affix("%")
        };
        percent.ValueChanged += (_, eventArgs) =>
            percentStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        return new DocPage(
            Title,
            "<info>NumberInput</info> edits an integer or decimal value through a transient typed buffer committed on Enter or focus loss.",
            new DocSection(
                "🔢",
                "Integer editing",
                "<info>Mode</info> set to <info>Integer</info> rejects the decimal-separator keystroke outright and always commits a whole number.",
                new DocExample(
                    "Whole-number field",
                    "Type digits, then press <reverse>Enter</reverse> or move focus away to commit. <reverse>Up</reverse>/<reverse>Down</reverse> step by <info>Step</info> immediately.",
                    new DocColumn(integer, integerStatus),
                    "var numberInput = new NumberInput { Mode = NumberInputMode.Integer };")),
            new DocSection(
                "💧",
                "Decimal editing",
                "The default <info>Mode</info> is <info>Decimal</info>. <info>DecimalPlaces</info> and <info>RoundingMode</info> govern how a typed value rounds at commit.",
                new DocExample(
                    "Two-decimal field",
                    "Typing \"2.5\" and pressing <reverse>Enter</reverse> commits the parsed and rounded value.",
                    new DocColumn(@decimal, decimalStatus),
                    "var numberInput = new NumberInput { DecimalPlaces = 2 };")),
            new DocSection(
                "📐",
                "Bounds and stepping",
                "<info>Minimum</info> and <info>Maximum</info> clamp the committed value. <info>Step</info> sets the Up/Down increment, and <reverse>Home</reverse>/<reverse>End</reverse> jump straight to the bounds.",
                new DocExample(
                    "Bounded, stepped field",
                    "<reverse>Up</reverse>/<reverse>Down</reverse> adjust by 5 within 0-100; <reverse>Home</reverse>/<reverse>End</reverse> jump to the bounds directly.",
                    new DocColumn(bounded, boundedStatus),
                    "numberInput.Minimum = 0m;\nnumberInput.Maximum = 100m;\nnumberInput.Step = 5m;")),
            new DocSection(
                "🚫",
                "Nullable",
                "With <info>AllowNull</info> set, an empty committed buffer clears the value to null instead of reverting to the last number.",
                new DocExample(
                    "Clearable number field",
                    "Clear all typed digits and press <reverse>Enter</reverse> to commit null.",
                    new DocColumn(nullable, nullableStatus),
                    "numberInput.AllowNull = true;\n// Value is null when the buffer commits empty")),
            new DocSection(
                "📊",
                "Grouped display",
                "<info>AllowGrouping</info> groups digits under <info>Culture</info> for the idle and freshly focused display; a typed group separator always parses regardless of the setting.",
                new DocExample(
                    "Grouped large value",
                    "The idle display groups digits in threes; typing a group separator during editing is accepted and stripped at commit.",
                    new DocColumn(grouped, groupedStatus),
                    "numberInput.AllowGrouping = true;")),
            new DocSection(
                "🌍",
                "Localized culture",
                "<info>Culture</info> supplies the decimal separator, group separator, sign, and grouping used for both display and parsing. It defaults to invariant, so out-of-the-box rendering never depends on the host locale.",
                new DocExample(
                    "German number field",
                    "German uses a comma as the decimal separator and a period to group digits, the inverse of the invariant convention.",
                    new DocColumn(culture, cultureStatus),
                    "numberInput.Culture = new CultureInfo(\"de-DE\");")),
            new DocSection(
                "🏷️",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell beside the value for an application-owned unit or symbol that a theme never authors.",
                new DocExample(
                    "Percent suffix",
                    "The percent sign sits in its own reserved cell after the value and never participates in parsing.",
                    new DocColumn(percent, percentStatus),
                    "numberInput.EndAffix = new Affix(\"%\");")));
    }

    private static string FormatValue(decimal? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "(none)";
}
