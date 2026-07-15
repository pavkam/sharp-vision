// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;

/// <summary>Documents application theming, type-keyed styles, local overrides, and third-party style properties.</summary>
internal sealed class ThemingPane: CompositeControl
{

    internal ThemingPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Theming";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        ShowcasePanel panel = new();

        Button left = new() { Content = new Text("Left") };
        Button right = new() { Content = new Text("Right") };
        Button above = new() { Content = new Text("Above") };
        Button below = new() { Content = new Text("Below") };
        left.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Left;
        right.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Right;
        above.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Above;
        below.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Below;

        var placement = Doc.Column(
            new Text("Label placement"),
            Doc.Row(left, right, above, below));

        // A scratch theme, never installed as the application theme, holds one semantic style keyed
        // to Button. The preview borrows that style locally so it can be compared with a baseline.
        ControlStyle<Button> typedStyle = new();
        typedStyle.Set(BackgroundProperty, State.Normal, ThemeColors.Accent);
        typedStyle.Set(ForegroundProperty, State.Normal, ThemeColors.Background);
        typedStyle.Set(BorderGlyphsProperty, State.Normal, Glyphs.Heavy);
        typedStyle.Set(HasShadowProperty, State.Normal, false);
        Theme spotlight = new();
        spotlight.SetStyle(typedStyle);

        Button baseline = new() { Content = new Text("Baseline theme"), HasShadow = false };
        Button typedPreview = new()
        {
            Content = new Text("Semantic accent"),
            Style = spotlight.GetStyle<Button>(),
        };
        Text typedReadout = new("Background: Accent · Border: Heavy · Shadow: Off");

        // A local override attaches a ControlStyle directly to one instance, skipping any theme.
        ControlStyle<Button> localStyle = new();
        localStyle.Set(ForegroundProperty, State.Normal, Color.Indexed(3));
        localStyle.Set(BorderGlyphsProperty, State.Normal, Glyphs.Ascii);
        Button overridden = new() { Content = new Text("Only me"), Style = localStyle };
        Button plain = new() { Content = new Text("Themed sibling") };

        var roleSwatches = BuildRoleSwatches();

        var catalogEntry = ThemeCatalog.Default.Entries[0];
        var catalog = new Text(
            $"Catalog entry: {Text.Escape(catalogEntry.Name)}\n" +
            $"Slug: {Text.Escape(catalogEntry.Slug)} · Scheme: {catalogEntry.ColorScheme}\n" +
            $"Author: {Text.Escape(catalogEntry.Author)} · License: {Text.Escape(catalogEntry.License)}")
        {
            Overflow = Overflow.Wrap,
        };

        var stateMatrix = Doc.Column(
            new Button { Content = new Text("Hover or focus me") },
            new Button { Content = new Text("Disabled"), IsEnabled = false },
            new CheckBox { Content = new Text("Checked state"), IsChecked = true },
            new CheckBox { Content = new Text("Indeterminate state"), IsThreeState = true, IsChecked = null });

        var chrome = new Dock
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Heavy,
            HasShadow = true,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowGlyph = new Rune('░'),
            Padding = new Thickness(1, 0),
            Children = { new Text("Intrinsic themed border and shadow") },
        };

        return Doc.Page(
            Title,
            "Demonstrates application themes, type-keyed styles, local overrides, and third-party style properties.",
            Doc.Section(
                "🎭",
                "Application theme",
                "The sidebar picker publishes one frozen application theme snapshot to every attached control.",
                Doc.Example(
                    "Live semantic roles",
                    "Change themes in the sidebar and both the custom panel and all twelve role swatches update through Application.Theme.",
                    Doc.Column(panel, roleSwatches),
                    "application.Theme = ThemeCatalog.Default.Load(\"default-dark\");")),
            Doc.Section(
                "🎭",
                "Catalog",
                "ThemeCatalog exposes stable metadata before an application chooses to load the immutable theme payload.",
                Doc.Example(
                    "Attribution and scheme metadata",
                    "Display name, slug, dark/light scheme, author, and license support discoverable theme pickers and attribution.",
                    catalog)),
            Doc.Section(
                "🎭",
                "Type and local styles",
                "Type-keyed theme recipes apply broadly; a Control.Style override resolves later and affects only that instance.",
                Doc.Example(
                    "Type-keyed Button style",
                    "Compare the ordinary baseline with a semantic Accent surface, heavy border, and deliberately flat face. No indexed color or raw glyph record leaks into the UI.",
                    Doc.Column(Doc.Row(baseline, typedPreview), typedReadout),
                    "style.Set(Control.BackgroundProperty, State.Normal, ThemeColors.Accent);\nstyle.Set(Control.BorderGlyphsProperty, State.Normal, Glyphs.Heavy);"),
                Doc.Example(
                    "Per-instance override",
                    "Only me owns the ASCII/yellow override; the sibling continues following the application theme.",
                    Doc.Row(overridden, plain))),
            Doc.Section(
                "🎭",
                "Visual states",
                "Normal, hovered, focused, pressed, checked, indeterminate, and disabled values resolve through deterministic state precedence.",
                Doc.Example(
                    "Interactive state matrix",
                    "Move focus and pointer across the controls; checked, indeterminate, and disabled states remain visible in combination.",
                    stateMatrix)),
            Doc.Section(
                "🎭",
                "Shared chrome",
                "Border and shadow are intrinsic style properties on ordinary controls rather than wrapper control types.",
                Doc.Example(
                    "Themeable surface chrome",
                    "The Dock owns its border and block shadow directly while its child remains an ordinary content node.",
                    chrome)),
            Doc.Section(
                "🎭",
                "Third-party controls",
                "Custom controls register StyleProperty metadata and resolve it through the same theme/local cascade as built-in chrome.",
                Doc.Example(
                    "ShowcasePanel label placement",
                    "Choose all four placements; LabelPlacement is a custom style property, not a private theme mechanism.",
                    placement,
                    "var property = StyleProperty<LabelPlacement>.Register<ShowcasePanel>(\n    \"label-placement\",\n    LabelPlacement.Left,\n    Impact.Measure);")));
    }

    private static Stack BuildRoleSwatches()
    {
        var roles = Enum.GetValues<ColorRole>();
        var rows = new Control[roles.Length];

        for (var index = 0; index < roles.Length; index++)
        {
            var role = roles[index];
            var chip = new Dock()
            {
                Width = Length.Cells(6),
                Height = Length.Cells(1),
                FillMode = FillMode.Opaque,
                Background = RoleColor(role),
            };
            rows[index] = Doc.Row(chip, new Text(role.ToString()));
        }

        return Doc.Column(rows);
    }

    private static Color RoleColor(ColorRole role) => role switch
    {
        ColorRole.Foreground => ThemeColors.Foreground,
        ColorRole.Background => ThemeColors.Background,
        ColorRole.Surface => ThemeColors.Surface,
        ColorRole.Border => ThemeColors.Border,
        ColorRole.Accent => ThemeColors.Accent,
        ColorRole.Muted => ThemeColors.Muted,
        ColorRole.SelectionBackground => ThemeColors.SelectionBackground,
        ColorRole.SelectionForeground => ThemeColors.SelectionForeground,
        ColorRole.Error => ThemeColors.Error,
        ColorRole.Warning => ThemeColors.Warning,
        ColorRole.Success => ThemeColors.Success,
        ColorRole.Info => ThemeColors.Info,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The color role is unknown."),
    };
}
