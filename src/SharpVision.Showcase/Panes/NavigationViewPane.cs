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
        basic.Items.Add(new NavigationViewItem { Header = "Dashboard", Glyph = "📊" });
        basic.Items.Add(new NavigationViewItem { Header = "Reports", Glyph = "📈" });
        basic.Items.Add(new NavigationViewItem { Header = "Settings", Glyph = "⚙" });
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

        return Doc.Page(Title, "Provides a sidebar navigation control with typed items, collapsible groups, and pinned footer.",
            Doc.Section("🧭", "Basic sidebar", "Up/Down arrows navigate. Focus selects.",
                Doc.Example("App sidebar with glyphs", "Optional glyph prefix per item.", Doc.Column(basic, status), "nav.Items.Add(new NavigationViewItem { Header = \"Dashboard\", Glyph = \"📊\" });")),
            Doc.Section("🧭", "Groups and separators", "Groups create collapsible sections.",
                Doc.Example("Project browser", "Two groups with separator. Enter toggles.", grouped)),
            Doc.Section("🧭", "Footer items", "FooterItems pinned to bottom.",
                Doc.Example("Settings with footer", "About stays pinned.", footer)));
    }
}
