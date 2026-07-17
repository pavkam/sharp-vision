// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;

/// <summary>Documents immutable application palettes, direct appearances, and ordinary CLR properties.</summary>
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

        ShowcasePanel placementPreview = new();
        left.Click += (_, _) => placementPreview.LabelPlacement = LabelPlacement.Left;
        right.Click += (_, _) => placementPreview.LabelPlacement = LabelPlacement.Right;
        above.Click += (_, _) => placementPreview.LabelPlacement = LabelPlacement.Above;
        below.Click += (_, _) => placementPreview.LabelPlacement = LabelPlacement.Below;
        var placement = Doc.Column(
            new Text("Label placement"),
            Doc.Row(left, right, above, below),
            placementPreview);

        Button baseline = new() { Content = new Text("Baseline theme"), HasShadow = false };
        Button typedPreview = new()
        {
            Content = new Text("Semantic accent"),
            Background = ColorRole.Accent,
            Foreground = ColorRole.Background,
            BorderGlyphs = Glyphs.Heavy,
            HasShadow = false,
        };
        Text typedReadout = new("Background: Accent · Border: Heavy · Shadow: Off");

        Button overridden = new() { Content = new Text("Only me"), Foreground = Color.Indexed(3), BorderGlyphs = Glyphs.Ascii };
        Button plain = new() { Content = new Text("Themed sibling") };

        var roleSwatches = BuildRoleSwatches();
        var glyphPreview = Doc.Column(
            new ProgressBar
            {
                Width = Length.Cells(18),
                Value = 0.5,
            },
            new ComboBox
            {
                Width = Length.Cells(18),
                Items = ["Theme glyphs"],
            },
            new Expander
            {
                Header = "Disclosure",
                IsExpanded = false,
            });

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
        var halfBlockChrome = new Dock
        {
            Width = Length.Cells(32),
            Height = Length.Cells(3),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.HalfBlock,
            Children = { new Text("Half-block border preset") },
        };

        return Doc.Page(
            Title,
            "Demonstrates immutable application color and glyph palettes, direct appearances, and ordinary control properties.",
            Doc.Section(
                "🎭",
                "Application theme",
                "The sidebar picker publishes one frozen application theme snapshot to every attached control.",
                Doc.Example(
                    "Live semantic roles",
                    "Change themes in the sidebar; the panel, twelve role swatches, progress cells, drop-down marker, and disclosure marker update through Application.Theme.",
                    Doc.Column(panel, roleSwatches, new Text("Theme-owned glyphs"), glyphPreview),
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
                "Direct appearance",
                "Controls own small deterministic appearance policies; direct values override only that instance.",
                Doc.Example(
                    "Semantic Button appearance",
                    "Compare the ordinary baseline with a semantic Accent surface, heavy border, and deliberately flat face. No indexed color or raw glyph record leaks into the UI.",
                    Doc.Column(Doc.Row(baseline, typedPreview), typedReadout),
                    "button.Background = ColorRole.Accent;\nbutton.BorderGlyphs = Glyphs.Heavy;"),
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
                    "The Docks own their heavy shadowed and sculpted half-block borders directly while each child remains an ordinary content node.",
                    Doc.Column(chrome, halfBlockChrome),
                    "surface.BorderGlyphs = Glyphs.HalfBlock;")),
            Doc.Section(
                "🎭",
                "Third-party controls",
                "Custom controls expose ordinary validated CLR properties and use the same palette and protected render seams as built-in controls.",
                Doc.Example(
                    "ShowcasePanel label placement",
                    "Choose all four placements; LabelPlacement is an ordinary validated control property, not a private theme mechanism.",
                    placement,
                    "panel.LabelPlacement = LabelPlacement.Left;")));
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
                Background = RoleColor(role),
            };
            rows[index] = Doc.Row(chip, new Text(role.ToString()));
        }

        return Doc.Column(rows);
    }

    private static ThemeColor RoleColor(ColorRole role) => ThemeColor.From(role);
}
