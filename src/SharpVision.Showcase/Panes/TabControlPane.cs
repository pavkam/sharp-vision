// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

internal sealed class TabControlPane: CompositeControl
{
    internal TabControlPane() => InitializeContent(CreateContent());
    internal const string Title = "TabControl";

    private static Dock CreateContent()
    {
        var status = new Text("Selected: General");
        var tabs = new TabControl { Width = Length.Cells(50), Height = Length.Cells(8) };
        tabs.Items.Add(new TabItem { Header = "General", Content = new Stack { Padding = new Thickness(1), Children = { new Text("General settings."), new CheckBox { Content = new Text("Notifications") } } } });
        tabs.Items.Add(new TabItem { Header = "Advanced", Content = new Stack { Padding = new Thickness(1), Children = { new CheckBox { Content = new Text("Debug mode") } } } });
        tabs.Items.Add(new TabItem { Header = "About", Content = new Text("SharpVision v1.0") { Padding = new Thickness(1) } });
        tabs.SelectionChanged += (_, _) => status.Content = $"Selected: {(tabs.SelectedIndex >= 0 ? tabs.Items[tabs.SelectedIndex].Header : "none")}";

        var dynStatus = new Text("Tabs: 2");
        var dyn = new TabControl { Width = Length.Cells(50), Height = Length.Cells(6) };
        dyn.Items.Add(new TabItem { Header = "Tab 1", Content = new Text("First") { Padding = new Thickness(1) } });
        dyn.Items.Add(new TabItem { Header = "Tab 2", Content = new Text("Second") { Padding = new Thickness(1) } });
        var addBtn = new Button { Content = new Text("Add tab") };
        var rmBtn = new Button { Content = new Text("Remove last") };
        var counter = 2;
        addBtn.Click += (_, _) => { counter++; dyn.Items.Add(new TabItem { Header = $"Tab {counter}", Content = new Text($"Content {counter}") { Padding = new Thickness(1) } }); dynStatus.Content = $"Tabs: {dyn.Items.Count}"; };
        rmBtn.Click += (_, _) => { if (dyn.Items.Count > 0) { _ = dyn.Items.Remove(dyn.Items[^1]); dynStatus.Content = $"Tabs: {dyn.Items.Count}"; } };

        return Doc.Page(Title, "Arranges typed tab pages with a header bar and keyboard-driven page switching.",
            Doc.Section("📑", "Basic tabs", "Left/Right arrows switch tabs.",
                Doc.Example("Settings panel", "Click a tab or use arrows.", Doc.Column(tabs, status), "tabs.Items.Add(new TabItem { Header = \"General\" });")),
            Doc.Section("📑", "Dynamic tabs", "Add and remove tabs at runtime.",
                Doc.Example("Add and remove", "Buttons modify the tab set.", Doc.Column(dyn, Doc.Row(addBtn, rmBtn), dynStatus))));
    }
}
