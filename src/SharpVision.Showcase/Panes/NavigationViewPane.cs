// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents basic, grouped, Unicode, disabled, scrolling, and pinned-footer NavigationView specimens.</summary>
internal sealed class NavigationViewPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "NavigationView";

    /// <summary>Initializes the retained NavigationView documentation page.</summary>
    internal NavigationViewPane() => InitializeContent(CreateContent());

    private static Dock CreateContent()
    {
        var status = new Text("Selected: none");
        var basic = CreateView("MY APP", height: 7);
        basic.Items.Add(new NavigationViewItem { Header = "Dashboard", Glyph = "界" });
        basic.Items.Add(new NavigationViewItem { Header = "Reports", Glyph = "◆" });
        basic.Items.Add(new NavigationViewItem { Header = "Disabled", IsEnabled = false });
        basic.SelectionChanged += (_, _) => status.Content = $"Selected: {basic.SelectedItem?.Header ?? "none"}";

        var grouped = CreateView("PROJECT", height: 9);
        var core = new NavigationViewGroup { Header = "Core" };
        core.AddItem(new NavigationViewItem { Header = "Models" });
        core.AddItem(new NavigationViewItem { Header = "Services" });
        grouped.Items.Add(core);
        grouped.Items.Add(new NavigationViewSeparator());
        grouped.Items.Add(new NavigationViewItem { Header = "Overview" });

        var footer = CreateView("SETTINGS", height: 7);
        footer.Items.Add(new NavigationViewItem { Header = "General" });
        footer.Items.Add(new NavigationViewItem { Header = "Appearance" });
        footer.FooterItems.Add(new NavigationViewSeparator());
        footer.FooterItems.Add(new NavigationViewItem { Header = "About", Glyph = "界" });

        var overflow = CreateView("LONG", height: 6);

        foreach (var index in Enumerable.Range(1, 8))
        {
            overflow.Items.Add(new NavigationViewItem { Header = $"Page {index}" });
        }

        return Doc.Page(
            Title,
            "Provides a retained sidebar with typed entries, collapsible groups, one scrolling main region, and a pinned footer.",
            Doc.Section(
                "🧭",
                "Selection and availability",
                "Focus, pointer, and arrow navigation share one flat selected-item contract while unavailable entries are skipped.",
                Doc.Example(
                    "Application sidebar",
                    "Glyphs and Unicode labels use terminal-cell measurement; activation updates the retained status.",
                    Doc.Card(Doc.Column(basic, status)),
                    "nav.Items.Add(new NavigationViewItem { Header = \"Dashboard\", Glyph = \"界\" });")),
            Doc.Section(
                "▼",
                "Groups and separators",
                "Expanded group descendants participate in navigation while separators remain purely semantic dividers.",
                Doc.Example(
                    "Project browser",
                    "Focus a group and press Enter to collapse or expand its retained descendants.",
                    Doc.Card(grouped))),
            Doc.Section(
                "↧",
                "Footer and overflow",
                "The main region scrolls independently while footer entries stay pinned to the bottom edge.",
                Doc.Example(
                    "Pinned footer",
                    "Footer selection never changes the main scrolling offset.",
                    Doc.Card(footer)),
                Doc.Example(
                    "Long navigation",
                    "Arrow navigation brings main entries through the intrinsic Stack viewport.",
                    Doc.Card(overflow))));
    }

    private static NavigationView CreateView(string header, int height) => new()
    {
        Header = header,
        Width = Length.Cells(28),
        Height = Length.Cells(height),
        BorderThickness = new Thickness(1),
        BorderGlyphs = Glyphs.Rounded,
    };
}
