// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the TabControl with live page switching, dynamic tabs, and disabled state.</summary>
internal sealed class TabControlPane: CompositeControlBase
{
    /// <summary>Initializes the retained TabControl showcase content.</summary>
    internal TabControlPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "TabControl";

    private static DocPage CreateContent()
    {
        var status = new Text("Selected: General");
        var tabs = new TabControl { Width = Length.Cells(50), Height = Length.Cells(8) };
        tabs.Items.Add(new TabItem
        {
            Header = "&General",
            Content = new Stack
            {
                Padding = new Thickness(1),
                Children =
                {
                    new Text("General settings."), new CheckBox { Text = "&Notifications" }
                }
            }
        });
        tabs.Items.Add(new TabItem
        {
            Header = "&Advanced",
            Content = new Stack
            {
                Padding = new Thickness(1),
                Children = { new CheckBox { Text = "&Debug mode" } }
            }
        });
        tabs.Items.Add(new TabItem
        {
            Header = "&About",
            Content = new Text("SharpVision v1.0") { Padding = new Thickness(1) }
        });
        tabs.SelectionChanged += (_, _) =>
            status.Content = $"Selected: {(tabs.SelectedIndex >= 0 ? tabs.Items[tabs.SelectedIndex].Header : "none")}";

        var dynStatus = new Text("Tabs: 2");
        var dyn = new TabControl { Width = Length.Cells(50), Height = Length.Cells(6) };
        dyn.Items.Add(new TabItem { Header = "&Tab 1", IsClosable = true, Content = new Text("First") { Padding = new Thickness(1) } });
        dyn.Items.Add(new TabItem { Header = "&Tab 2", Content = new Text("Second") { Padding = new Thickness(1) } });
        var addBtn = new Button { Text = "&Add tab" };
        var rmBtn = new Button { Text = "&Remove last" };
        var closeBtn = new Button { Text = "Close selected" };
        var counter = 2;
        var closeCancelled = false;
        dyn.CloseRequested += (_, args) =>
        {
            closeCancelled = false;
            if (args.Item.Header.Contains("Tab 1", StringComparison.Ordinal))
            {
                args.Cancel = true;
                closeCancelled = true;
                dynStatus.Content = "Close request cancelled for Tab 1.";
            }
        };
        addBtn.Click += (_, _) =>
        {
            counter++;
            dyn.Items.Add(new TabItem
            {
                Header = $"Tab {counter}",
                IsClosable = true,
                Content = new Text($"Content {counter}") { Padding = new Thickness(1) }
            });
            dynStatus.Content = $"Tabs: {dyn.Items.Count}";
        };
        closeBtn.Click += (_, _) =>
        {
            if (dyn.SelectedIndex >= 0)
            {
                closeCancelled = false;
                _ = dyn.RequestClose(dyn.Items[dyn.SelectedIndex]);
                if (!closeCancelled)
                {
                    dynStatus.Content = $"Tabs: {dyn.Items.Count}";
                }
            }
        };
        rmBtn.Click += (_, _) =>
        {
            if (dyn.Items.Count > 0)
            {
                _ = dyn.Items.Remove(dyn.Items[^1]);
                dynStatus.Content = $"Tabs: {dyn.Items.Count}";
            }
        };

        var disabledTabs = new TabControl { Width = Length.Cells(50), Height = Length.Cells(6) };
        disabledTabs.Items.Add(new TabItem
        {
            Header = "Acti&ve",
            Content = new Text("This tab is selectable.") { Padding = new Thickness(1) }
        });

        var overflowTabs = new TabControl
        {
            Width = Length.Cells(50),
            Height = Length.Cells(6),
            HeaderWidth = Length.Cells(10),
            HeaderOverflowPolicy = TabHeaderOverflowPolicy.Scroll,
        };
        foreach (var header in new[] { "One", "Two", "Three", "Four", "Five" })
        {
            overflowTabs.Items.Add(new TabItem
            {
                Header = header,
                Content = new Text($"Selected page: {header}") { Padding = new Thickness(1) },
            });
        }
        disabledTabs.Items.Add(new TabItem
        {
            Header = "&Unavailable",
            IsEnabled = false,
            Content = new Text("This tab cannot be reached.") { Padding = new Thickness(1) }
        });
        disabledTabs.Items.Add(new TabItem
        {
            Header = "&Other",
            Content = new Text("Keyboard navigation skips the disabled tab.") { Padding = new Thickness(1) }
        });

        return new DocPage(Title, "<info>TabControl</info> uses retained <info>TabItem</info> headers with hover, selection, and typed page content.",
            new DocSection("📑", "Basic tabs", "Click headers, use <reverse>Left</reverse>/<reverse>Right</reverse>, or jump with <reverse>Home</reverse>/<reverse>End</reverse>.",
                new DocExample("Settings panel", "Only the active header is highlighted; page content stays neutral.",
                    new DocColumn(tabs, status),
                    "tabs.Items.Add(new TabItem { Header = \"&General\" });")),
            new DocSection("🔄", "Dynamic tabs", "Add and remove tabs at runtime.",
                new DocExample("Add and remove", "Buttons modify the tab set.",
                    new DocColumn(dyn, new DocRow(addBtn, rmBtn, closeBtn), dynStatus))),
            new DocSection("🎨", "Styling",
                "<info>DividerColor</info>, <info>SelectionIndicatorColor</info>, <info>DividerGlyph</info>, and <info>UnderlineGlyph</info> customize the tab strip appearance.",
                new DocExample("Custom colors and glyphs",
                    "Override the divider and selection indicator for a distinct visual treatment.",
                    new TabControl
                    {
                        Width = Length.Cells(50),
                        Height = Length.Cells(6),
                        DividerColor = Color.Rgb(0x60, 0x60, 0x60),
                        SelectionIndicatorColor = Color.Rgb(0x40, 0xc0, 0x40),
                        Items =
                        {
                            new TabItem
                            {
                                Header = "&Profile",
                                Content = new Text("Custom styled tab strip.") { Padding = new Thickness(1) }
                            },
                            new TabItem
                            {
                                Header = "S&ettings",
                                Content = new Text("Second page content.") { Padding = new Thickness(1) }
                            }
                        }
                    },
                    "tabs.DividerColor = Color.Rgb(0x60, 0x60, 0x60);\ntabs.SelectionIndicatorColor = Color.Rgb(0x40, 0xC0, 0x40);")),
            new DocSection("🚫", "Disabled tabs", "A disabled <info>TabItem</info> remains visible but keyboard and pointer navigation skip it.",
                new DocExample("Unavailable tab", "Use <reverse>Left</reverse>/<reverse>Right</reverse> arrows — the middle tab is skipped.",
                    disabledTabs)),
            new DocSection("↔", "Narrow header strips", "Use <info>HeaderWidth</info> with the scroll policy when a fixed header size is useful; keyboard selection keeps the active page reachable.",
                new DocExample("Overflow scrolling", "The headers are wider than the visible strip; use <reverse>End</reverse> and <reverse>Home</reverse> to move through them.",
                    overflowTabs)));
    }
}
