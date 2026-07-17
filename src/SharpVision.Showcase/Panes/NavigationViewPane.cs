// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

internal sealed class NavigationViewPane: CompositeControl
{
    internal NavigationViewPane() => InitializeContent(CreateContent());
    internal const string Title = "NavigationView";

    private static Dock CreateContent()
    {
        var status = new Text("Selected: Dashboard");
        var basic = new NavigationView { Header = "MY APP", Width = Length.Cells(24), Height = Length.Cells(12), BorderThickness = new Thickness(1), BorderGlyphs = Glyphs.Rounded };
        basic.Items.Add(new NavigationViewItem { Header = "界 Dashboard" });
        basic.Items.Add(new NavigationViewItem { Header = "Reports", Glyph = "📈" });
        basic.Items.Add(new NavigationViewItem { Header = "Settings", Glyph = "⚙", IsEnabled = false });
        basic.SelectionChanged += (_, _) => status.Content = $"Selected: {basic.SelectedItem?.Header ?? "none"}";

        var grouped = new NavigationView { Header = "PROJECT", Width = Length.Cells(24), Height = Length.Cells(16), BorderThickness = new Thickness(1), BorderGlyphs = Glyphs.Rounded };
        var core = new NavigationViewGroup { Header = "Core" };
        core.AddItem(new NavigationViewItem { Header = "Models" });
        core.AddItem(new NavigationViewItem { Header = "Services" });
        var tests = new NavigationViewGroup { Header = "Tests" };
        tests.AddItem(new NavigationViewItem { Header = "Unit" });
        tests.AddItem(new NavigationViewItem { Header = "Integration" });
        grouped.Items.Add(core);
        grouped.Items.Add(new NavigationViewSeparator());
        grouped.Items.Add(tests);

        var footer = new NavigationView { Header = "SETTINGS", Width = Length.Cells(24), Height = Length.Cells(10), BorderThickness = new Thickness(1), BorderGlyphs = Glyphs.Rounded };
        footer.Items.Add(new NavigationViewItem { Header = "General" });
        footer.Items.Add(new NavigationViewItem { Header = "Appearance" });
        footer.FooterItems.Add(new NavigationViewSeparator());
        footer.FooterItems.Add(new NavigationViewItem { Header = "About" });

        var overflow = new NavigationView { Header = "LONG", Width = Length.Cells(24), Height = Length.Cells(8), BorderThickness = new Thickness(1), BorderGlyphs = Glyphs.Rounded };
        for (var index = 1; index <= 8; index++)
        {
            overflow.Items.Add(new NavigationViewItem { Header = $"Destination {index}" });
        }

        return Doc.Page(Title, "Provides a sidebar navigation control with typed items, collapsible groups, and pinned footer.",
            Doc.Section("🧭", "Basic sidebar", "Up/Down arrows move the current entry while the sidebar retains focus.",
                Doc.Example("App sidebar with glyphs", "Unicode labels, optional glyph prefixes, and unavailable entries share one route.", Doc.Column(basic, status), "nav.Items.Add(new NavigationViewItem { Header = \"界 Dashboard\" });"),
                Doc.Example("Overflow navigation", "Eight destinations exercise the intrinsic scrolling stack.", overflow)),
            Doc.Section("🧭", "Groups and separators", "Group headers participate in keyboard navigation without becoming selected items.",
                Doc.Example("Project browser", "Use Up/Down to reach a group header; Enter or Space toggles it.", grouped)),
            Doc.Section("🧭", "Footer items", "FooterItems pinned to bottom.",
                Doc.Example("Settings with footer", "About stays pinned.", footer)));
    }
}
