// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Globalization;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the CurrencyInput control with live invariant, localized, bounded, nullable,
/// precision, grouping, and currency-identity specimens.</summary>
internal sealed class CurrencyInputPane: CompositeControlBase
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "CurrencyInput";

    /// <summary>Initializes the retained interactive CurrencyInput documentation page.</summary>
    internal CurrencyInputPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        // Invariant formatting.
        var invariantStatus = new Text("Value: 0.00");
        var invariant = new CurrencyInput { Value = 0m };
        invariant.ValueChanged += (_, eventArgs) =>
            invariantStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // Localized culture with its own separators, sign, and symbol placement.
        var localizedStatus = new Text("Value: 1234.56");
        var localized = new CurrencyInput { Culture = new CultureInfo("de-DE"), Value = 1234.56m };
        localized.ValueChanged += (_, eventArgs) =>
            localizedStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // Bounded range with a custom Step.
        var boundedStatus = new Text("Value: 50.00");
        var bounded = new CurrencyInput { Minimum = 0m, Maximum = 100m, Step = 5m, Value = 50m };
        bounded.ValueChanged += (_, eventArgs) =>
            boundedStatus.Content = $"Value: {FormatValue(eventArgs.Value)} (0-100 by 5)";

        // Nullable with AllowNull=true.
        var nullableStatus = new Text("Value: (none)");
        var nullable = new CurrencyInput { AllowNull = true };
        nullable.ValueChanged += (_, eventArgs) =>
            nullableStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // Explicit precision and rounding, overriding the culture-derived decimal places.
        var precisionStatus = new Text("Value: 12.345");
        var precision = new CurrencyInput
        {
            DecimalPlaces = 3,
            RoundingMode = MidpointRounding.AwayFromZero,
            Value = 12.345m
        };
        precision.ValueChanged += (_, eventArgs) =>
            precisionStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // Grouped display for large magnitudes.
        var groupedStatus = new Text("Value: 1234567.00");
        var grouped = new CurrencyInput { AllowGrouping = true, Value = 1234567m };
        grouped.ValueChanged += (_, eventArgs) =>
            groupedStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // Currency identity override: ISO code display mode.
        var identityStatus = new Text("Value: 99.99");
        var identity = new CurrencyInput
        {
            Culture = new CultureInfo("en-US"),
            DisplayMode = CurrencyDisplayMode.IsoCode,
            Value = 99.99m
        };
        identity.ValueChanged += (_, eventArgs) =>
            identityStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        // StartAffix reserves a fixed cell for an application-owned receipt-backed marker.
        var receiptStatus = new Text("Value: 42.50");
        var receipt = new CurrencyInput
        {
            Value = 42.50m,
            StartAffix = new Affix("🧾")
        };
        receipt.ValueChanged += (_, eventArgs) =>
            receiptStatus.Content = $"Value: {FormatValue(eventArgs.Value)}";

        return new DocPage(
            Title,
            "<info>CurrencyInput</info> edits a nullable monetary value through a transient typed buffer, formatted and parsed against a culture's currency-specific globalization data.",
            new DocSection(
                "💵",
                "Invariant formatting",
                "The default <info>Culture</info> is invariant, so out-of-the-box rendering never depends on the host locale.",
                new DocExample(
                    "Invariant currency field",
                    "Type digits, then press <reverse>Enter</reverse> or move focus away to commit. <reverse>Up</reverse>/<reverse>Down</reverse> step by <info>Step</info> immediately.",
                    new DocColumn(invariant, invariantStatus),
                    "var currencyInput = new CurrencyInput();")),
            new DocSection(
                "🌍",
                "Localized culture",
                "<info>Culture</info> supplies the currency-specific decimal separator, group separator, group sizes, sign, and positive/negative layout pattern used for both display and parsing.",
                new DocExample(
                    "German currency field",
                    "German uses a comma as the decimal separator, a period to group digits, and places the euro symbol after the number.",
                    new DocColumn(localized, localizedStatus),
                    "currencyInput.Culture = new CultureInfo(\"de-DE\");")),
            new DocSection(
                "📐",
                "Bounds and stepping",
                "<info>Minimum</info> and <info>Maximum</info> clamp the committed value. <info>Step</info> sets the Up/Down increment, and <reverse>Home</reverse>/<reverse>End</reverse> jump straight to the bounds.",
                new DocExample(
                    "Bounded, stepped field",
                    "<reverse>Up</reverse>/<reverse>Down</reverse> adjust by 5 within 0-100; <reverse>Home</reverse>/<reverse>End</reverse> jump to the bounds directly.",
                    new DocColumn(bounded, boundedStatus),
                    "currencyInput.Minimum = 0m;\ncurrencyInput.Maximum = 100m;\ncurrencyInput.Step = 5m;")),
            new DocSection(
                "🚫",
                "Nullable",
                "With <info>AllowNull</info> set, an empty committed buffer clears the value to null instead of reverting to the last amount.",
                new DocExample(
                    "Clearable currency field",
                    "Clear all typed digits and press <reverse>Enter</reverse> to commit null.",
                    new DocColumn(nullable, nullableStatus),
                    "currencyInput.AllowNull = true;\n// Value is null when the buffer commits empty")),
            new DocSection(
                "🎯",
                "Precision and rounding",
                "<info>DecimalPlaces</info> overrides the culture-derived fractional digit count, and <info>RoundingMode</info> governs how a typed value rounds at commit.",
                new DocExample(
                    "Three-decimal field",
                    "An explicit <info>DecimalPlaces</info> of 3 keeps a sub-cent precision the culture's own currency data would not derive on its own.",
                    new DocColumn(precision, precisionStatus),
                    "currencyInput.DecimalPlaces = 3;\ncurrencyInput.RoundingMode = MidpointRounding.AwayFromZero;")),
            new DocSection(
                "📊",
                "Grouped display",
                "<info>AllowGrouping</info> groups digits under <info>Culture</info>'s currency-specific group separator and sizes for the idle and freshly focused display.",
                new DocExample(
                    "Grouped large value",
                    "The idle display groups digits under the culture's currency group sizes; a typed group separator is always accepted and stripped at commit.",
                    new DocColumn(grouped, groupedStatus),
                    "currencyInput.AllowGrouping = true;")),
            new DocSection(
                "🔤",
                "Currency identity",
                "<info>DisplayMode</info> chooses how the currency identity is resolved and composed around the formatted number: symbol, ISO code, culture-native name, or a caller-supplied override.",
                new DocExample(
                    "ISO code display",
                    "<info>DisplayMode</info> set to <info>IsoCode</info> renders the three-letter ISO 4217 code instead of the culture's currency symbol.",
                    new DocColumn(identity, identityStatus),
                    "currencyInput.DisplayMode = CurrencyDisplayMode.IsoCode;")),
            new DocSection(
                "🧾",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell beside the value for application-owned, data-driven decoration that a theme never authors.",
                new DocExample(
                    "Receipt glyph before the amount",
                    "The receipt glyph marks an expense entered from a scanned receipt; it sits in its own reserved cell and never participates in parsing.",
                    new DocColumn(receipt, receiptStatus),
                    "currencyInput.StartAffix = new Affix(\"🧾\");")));
    }

    private static string FormatValue(decimal? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "(none)";
}
