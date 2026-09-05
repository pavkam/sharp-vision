// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the NavigationView with sidebar items, groups, overflow scrolling, and footer.</summary>
internal sealed class NavigationViewPane: CompositeControlBase
{
    /// <summary>Initializes the retained NavigationView showcase content.</summary>
    internal NavigationViewPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "NavigationView";

    private static DocPage CreateContent()
    {
        var status = new Text("Selected: 界 Dashboard");
        var basic = new NavigationView { Header = "&MY APP", Width = Length.Cells(24), Height = Length.Cells(4) };
        basic.Items.Add(new NavigationViewItem { Text = "&界 Dashboard" });
        basic.Items.Add(new NavigationViewItem { Text = "&Reports", Glyph = "📈" });
        basic.Items.Add(new NavigationViewItem { Text = "&Settings", Glyph = "⚙", IsEnabled = false });
        basic.SelectItem((NavigationViewItem) basic.Items[0]);
        basic.SelectionChanged += (_, eventArgs) =>
            status.Content = $"Selected: {DocCaption.PlainCaption(basic.SelectedItem?.Text ?? "none")} ({eventArgs.Cause})";

        var grouped = new NavigationView { Header = "&PROJECT", Width = Length.Cells(24), Height = Length.Cells(8) };
        var core = new NavigationViewGroup { Header = "&Core", IsExpanded = false };
        core.Items.Add(new NavigationViewItem { Text = "Mo&dels" });
        core.Items.Add(new NavigationViewItem { Text = "&Services" });
        var tests = new NavigationViewGroup { Header = "&Tests" };
        tests.Items.Add(new NavigationViewItem { Text = "&Unit" });
        tests.Items.Add(new NavigationViewItem { Text = "&Integration" });
        grouped.Items.Add(core);
        grouped.Items.Add(new NavigationViewSeparator());
        grouped.Items.Add(tests);
        var groupedStatus = new Text("Right expands/enters · Left returns/collapses");
        grouped.SelectionChanged += (_, eventArgs) => groupedStatus.Content =
            $"Selected: {DocCaption.PlainCaption(grouped.SelectedItem?.Text ?? "group header")} ({eventArgs.Cause})";

        var footer = new NavigationView { Header = "S&ETTINGS", Width = Length.Cells(24), Height = Length.Cells(10) };
        footer.Items.Add(new NavigationViewItem { Text = "&General" });
        footer.Items.Add(new NavigationViewItem { Text = "&Appearance" });
        footer.FooterItems.Add(new NavigationViewSeparator());
        footer.FooterItems.Add(new NavigationViewItem { Text = "A&bout" });

        var overflow = new NavigationView
        {
            Header = "&LONG",
            Width = Length.Cells(24),
            Height = Length.Cells(8),
            WrapNavigation = true
        };
        for (var index = 1; index <= 8; index++)
        {
            overflow.Items.Add(new NavigationViewItem { Text = $"Destination &{index}" });
        }

        // EndAffix reserves a fixed cell for an application-owned unread-count badge.
        var badgeStatus = new Text("Selected: none");
        var badges = new NavigationView { Header = "INB&OX", Width = Length.Cells(24), Height = Length.Cells(8) };
        badges.Items.Add(new NavigationViewItem
        {
            Text = "U&nread",
            EndAffix = new Affix("●", "*", SemanticColor.Error)
        });
        badges.Items.Add(new NavigationViewItem { Text = "Arc&hive" });
        badges.SelectionChanged += (_, eventArgs) =>
            badgeStatus.Content = $"Selected: {DocCaption.PlainCaption(badges.SelectedItem?.Text ?? "none")} ({eventArgs.Cause})";

        var styled = new NavigationView { Header = "C&USTOM", Width = Length.Cells(24), Height = Length.Cells(7) };
        var styledGroup = new NavigationViewGroup
        {
            Header = "&Workspace",
            Style = NavigationViewGroupStyle.Default with
            {
                CollapsedGlyph = new Rune('+'),
                ExpandedGlyph = new Rune('-'),
                ItemIndent = 4
            }
        };
        styledGroup.Items.Add(new NavigationViewItem
        {
            Text = "&Changes",
            Style = NavigationViewItemStyle.Default with
            {
                IdleMarker = new Rune('·'),
                CurrentMarker = new Rune('›')
            }
        });
        styledGroup.Items.Add(new NavigationViewItem { Text = "&History" });
        styled.Items.Add(styledGroup);
        styled.Items.Add(new NavigationViewSeparator
        {
            Style = NavigationViewSeparatorStyle.Default with { Glyph = new Rune('═') }
        });

        return new DocPage(Title,
            "<info>NavigationView</info> provides sidebar navigation with typed items, collapsible groups, and a pinned footer.",
            new DocSection("🧭", "Basic sidebar",
                "The quiet background recedes while Up/Down, Home/End, and paging move the current entry and the sidebar retains focus.",
                new DocExample("App sidebar with glyphs",
                    "Unicode labels, optional glyph prefixes, and unavailable entries share one route. The Settings entry is disabled — keyboard navigation skips it but it remains visible.",
                    new DocColumn(basic, status),
                    "var nav = new NavigationView { Header = \"&MY APP\" };\n" +
                    "nav.Items.Add(new NavigationViewItem { Text = \"&界 Dashboard\" });\n" +
                    "nav.Items.Add(new NavigationViewItem { Text = \"&Reports\", Glyph = \"📈\" });\n" +
                    "nav.Items.Add(new NavigationViewItem { Text = \"&Settings\", Glyph = \"⚙\", IsEnabled = false });"),
                new DocExample("Overflow navigation", "Eight destinations overflow the visible area, scroll automatically, and wrap from last to first with Up/Down.",
                    overflow)),
            new DocSection("🧭", "Groups and separators",
                "Group headers participate in keyboard navigation without becoming selected items.",
                new DocExample("Project browser", "Right expands a collapsed group or enters its first child; Left returns to the group or collapses it. Enter and Space also toggle a current group.",
                    new DocColumn(grouped, groupedStatus))),
            new DocSection("📌", "Footer items", "Items added to <info>FooterItems</info> remain anchored below the scrollable main region, useful for persistent destinations like About or Settings.",
                new DocExample("Settings with footer", "The About item stays visible at the bottom edge regardless of the main item count.", footer)),
            new DocSection("🔔", "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell beside an item's text for application-owned, data-driven decoration that a theme never authors.",
                new DocExample("Unread-count badge",
                    "The badge dot sits in its own reserved cell after the label and never shares space with the caption.",
                    new DocColumn(badges, badgeStatus),
                    "nav.Items.Add(new NavigationViewItem\n{\n    Text = \"U&nread\",\n    EndAffix = new Affix(\"●\", \"*\", SemanticColor.Error)\n});")),
            new DocSection("🎨", "Entry customization",
                "Items, groups, separators, affixes, and the generated scrollbar each expose a typed local style without exposing the retained presentation tree.",
                new DocExample("Custom markers, disclosure, indent, and separator",
                    "The first child uses custom idle/current markers; the group uses +/- disclosure with a four-cell indent, and the separator uses a double rule.",
                    styled)));
    }
}
