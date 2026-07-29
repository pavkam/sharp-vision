// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the NavigationView with sidebar items, groups, overflow scrolling, and footer.</summary>
internal sealed class NavigationViewPane: CompositeControl
{
    /// <summary>Initializes the retained NavigationView showcase content.</summary>
    internal NavigationViewPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "NavigationView";

    private static DocPage CreateContent()
    {
        var status = new Text("Selected: Dashboard");
        var basic = new NavigationView { Header = "&MY APP", Width = Length.Cells(24), Height = Length.Cells(12) };
        basic.Items.Add(new NavigationViewItem { Header = "&界 Dashboard" });
        basic.Items.Add(new NavigationViewItem { Header = "&Reports", Glyph = "📈" });
        basic.Items.Add(new NavigationViewItem { Header = "&Settings", Glyph = "⚙", IsEnabled = false });
        basic.SelectionChanged += (_, _) => status.Content = $"Selected: {basic.SelectedItem?.Header ?? "none"}";

        var grouped = new NavigationView { Header = "&PROJECT", Width = Length.Cells(24), Height = Length.Cells(16) };
        var core = new NavigationViewGroup { Header = "&Core" };
        core.AddItem(new NavigationViewItem { Header = "Mo&dels" });
        core.AddItem(new NavigationViewItem { Header = "&Services" });
        var tests = new NavigationViewGroup { Header = "&Tests" };
        tests.AddItem(new NavigationViewItem { Header = "&Unit" });
        tests.AddItem(new NavigationViewItem { Header = "&Integration" });
        grouped.Items.Add(core);
        grouped.Items.Add(new NavigationViewSeparator());
        grouped.Items.Add(tests);

        var footer = new NavigationView { Header = "S&ETTINGS", Width = Length.Cells(24), Height = Length.Cells(10) };
        footer.Items.Add(new NavigationViewItem { Header = "&General" });
        footer.Items.Add(new NavigationViewItem { Header = "&Appearance" });
        footer.FooterItems.Add(new NavigationViewSeparator());
        footer.FooterItems.Add(new NavigationViewItem { Header = "A&bout" });

        var overflow = new NavigationView { Header = "&LONG", Width = Length.Cells(24), Height = Length.Cells(8) };
        for (var index = 1; index <= 8; index++)
        {
            overflow.Items.Add(new NavigationViewItem { Header = $"Destination &{index}" });
        }

        return new DocPage(Title,
            "<info>NavigationView</info> provides sidebar navigation with typed items, collapsible groups, and a pinned footer.",
            new DocSection("🧭", "Basic sidebar",
                "The quiet background recedes while <reverse>Up</reverse>/<reverse>Down</reverse> moves the current entry and the sidebar retains focus.",
                new DocExample("App sidebar with glyphs",
                    "Unicode labels, optional glyph prefixes, and unavailable entries share one route. The Settings entry is disabled — keyboard navigation skips it but it remains visible.",
                    new DocColumn(basic, status),
                    "var nav = new NavigationView { Header = \"MY APP\" };\n" +
                    "nav.Items.Add(new NavigationViewItem { Header = \"界 Dashboard\" });\n" +
                    "nav.Items.Add(new NavigationViewItem { Header = \"Reports\", Glyph = \"📈\" });\n" +
                    "nav.Items.Add(new NavigationViewItem { Header = \"&Settings\", Glyph = \"⚙\", IsEnabled = false });"),
                new DocExample("Overflow navigation", "Eight destinations overflow the visible area and scroll automatically.",
                    overflow)),
            new DocSection("🧭", "Groups and separators",
                "Group headers participate in keyboard navigation without becoming selected items.",
                new DocExample("Project browser", "Use <reverse>Up</reverse>/<reverse>Down</reverse> to reach a group header; <reverse>Enter</reverse> or <reverse>Space</reverse> toggles it.",
                    grouped)),
            new DocSection("📌", "Footer items", "Items added to <info>FooterItems</info> remain anchored below the scrollable main region, useful for persistent destinations like About or Settings.",
                new DocExample("Settings with footer", "The About item stays visible at the bottom edge regardless of the main item count.", footer)));
    }
}
