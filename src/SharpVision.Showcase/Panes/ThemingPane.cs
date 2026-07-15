// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;



/// <summary>Documents application theming, type-keyed styles, local overrides, and third-party style properties.</summary>
internal sealed class ThemingPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Theming";

    /// <summary>Initializes the retained Theming documentation page.</summary>
    internal ThemingPane() => InitializeContent(CreateContent());

    private static Stack CreateContent()
    {
        var panel = new ShowcasePanel();

        var left = new Button() { Content = new Text("Left") };
        var right = new Button() { Content = new Text("Right") };
        var above = new Button() { Content = new Text("Above") };
        var below = new Button() { Content = new Text("Below") };
        left.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Left;
        right.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Right;
        above.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Above;
        below.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Below;

        var placement = Doc.Column(
            new Text("Label placement"),
            Doc.Row(left, right, above, below));

        // A scratch theme, never installed as the application theme, holds one style keyed to the
        // Button type. ThemeResolver's design-time overload reads it back by type alone, with no live
        // control involved, proving the association Theme.SetStyle<Button> stored.
        var typedStyle = new ControlStyle<Button>();
        typedStyle.Set(BackgroundProperty, State.Normal, Color.Indexed(4));
        typedStyle.Set(BorderGlyphsProperty, State.Normal, Glyphs.Heavy);
        var spotlight = new Theme();
        spotlight.SetStyle(typedStyle);

        var resolvedBackground = ThemeResolver.Resolve(
            spotlight, typeof(Button), BackgroundProperty, State.Normal);
        var resolvedGlyphs = ThemeResolver.Resolve(
            spotlight, typeof(Button), BorderGlyphsProperty, State.Normal);

        var typedPreview = new Button()
        {
            Content = new Text("Every Button"),
            Style = spotlight.GetStyle<Button>(),
        };
        var typedReadout = new Text(
            $"ThemeResolver.Resolve(theme, typeof(Button), ...) reports background set: {resolvedBackground.HasValue}, border glyphs: {resolvedGlyphs}. The preview button borrows the same style object as a local override so the values are visible here.");

        // A local override attaches a ControlStyle directly to one instance, skipping any theme.
        var localStyle = new ControlStyle<Button>();
        localStyle.Set(ForegroundProperty, State.Normal, Color.Indexed(3));
        localStyle.Set(BorderGlyphsProperty, State.Normal, Glyphs.Ascii);
        var overridden = new Button() { Content = new Text("Only me"), Style = localStyle };
        var plain = new Button() { Content = new Text("Themed sibling") };

        var roleSwatches = BuildRoleSwatches();

        return Doc.Page(
            Title,
            "Demonstrates application themes, type-keyed styles, local overrides, and third-party style properties.",
            Doc.Example(
                "Application theme",
                "Use the theme picker in the sidebar footer. Application.Theme publishes a frozen snapshot to every attached control without ancestor-style inheritance.",
                panel),
            Doc.Example(
                "Type-keyed style",
                "Theme.SetStyle<Button> associates one style with every Button resolved under that theme. This scratch theme is never installed application-wide, so the readout below queries it directly by type.",
                Doc.Column(typedPreview, typedReadout)),
            Doc.Example(
                "Local override",
                "A control's Style property attaches a ControlStyle to that single instance, resolved after the theme cascade so it always wins. Only the first button below carries an override; its themed sibling still follows the application theme.",
                Doc.Row(overridden, plain)),
            Doc.Example(
                "Third-party style property",
                "ShowcasePanel registers LabelPlacement through StyleProperty metadata. Themes and local values resolve it with the same cascade as built-in chrome. All four placements are reachable below.",
                placement),
            Doc.Example(
                "Theme roles",
                "Themes are JSON palette files loaded through ThemeCatalog and ThemeFile. Every theme defines these 12 semantic ColorRole values; the swatches below show the active application theme and update live when you change the theme in the sidebar picker.",
                roleSwatches));
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
