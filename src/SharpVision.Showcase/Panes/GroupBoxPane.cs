// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the GroupBox control with titled-border framing specimens.</summary>
internal sealed class GroupBoxPane: CompositeControl
{

    internal GroupBoxPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "GroupBox";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        var settingsStatus = new Text("Settings: waiting");
        var autoSave = new CheckBox { Content = new Text("Auto-save"), IsChecked = true };
        var lineNumbers = new CheckBox { Content = new Text("Line numbers") };
        var wordWrap = new CheckBox { Content = new Text("Word wrap"), IsChecked = true };
        autoSave.StateChanged += (_, eventArgs) =>
            settingsStatus.Content = $"Settings: Auto-save → {eventArgs.Current}";
        lineNumbers.StateChanged += (_, eventArgs) =>
            settingsStatus.Content = $"Settings: Line numbers → {eventArgs.Current}";
        wordWrap.StateChanged += (_, eventArgs) =>
            settingsStatus.Content = $"Settings: Word wrap → {eventArgs.Current}";
        var settingsGroup = new GroupBox
        {
            Header = "Settings",
            Content = Doc.Column(autoSave, lineNumbers, wordWrap, settingsStatus),
        };

        var personalGroup = new GroupBox
        {
            Header = "Personal",
            Width = Length.Cells(28),
            Content = Doc.Column(
                new Text("Name") { Attributes = TerminalAttributes.Dim },
                new TextInput { Text = "Jane", Width = Length.Cells(22) },
                new Text("Email") { Attributes = TerminalAttributes.Dim },
                new TextInput { Text = "jane@example.com", Width = Length.Cells(22) }),
        };
        var preferencesGroup = new GroupBox
        {
            Header = "Preferences",
            Width = Length.Cells(28),
            Content = Doc.Column(
                new RadioButton { Content = new Text("Light theme"), GroupName = "theme" },
                new RadioButton { Content = new Text("Dark theme"), GroupName = "theme", IsChecked = true },
                new RadioButton { Content = new Text("System default"), GroupName = "theme" }),
        };

        var innerGroup = new GroupBox
        {
            Header = "Network",
            Content = Doc.Column(
                new CheckBox { Content = new Text("Use proxy"), IsChecked = true },
                new CheckBox { Content = new Text("Verify certificates") }),
        };
        var outerGroup = new GroupBox
        {
            Header = "Connection",
            Content = Doc.Column(
                new Text("Timeout: 30s"),
                innerGroup),
        };

        var roundedGroup = new GroupBox
        {
            Header = "Rounded",
            Glyphs = Glyphs.Rounded,
            Content = new Stack
            {
                Padding = new Thickness(1, 0),
                Children = { new Text("Default glyph family"), new Text("with smooth corners.") { Attributes = TerminalAttributes.Dim } },
            },
        };
        var lightGroup = new GroupBox
        {
            Header = "Light",
            Glyphs = Glyphs.Light,
            Content = new Stack
            {
                Padding = new Thickness(1, 0),
                Children = { new Text("Thin line borders"), new Text("for subtle framing.") { Attributes = TerminalAttributes.Dim } },
            },
        };
        var heavyGroup = new GroupBox
        {
            Header = "Heavy",
            Glyphs = Glyphs.Heavy,
            Content = new Stack
            {
                Padding = new Thickness(1, 0),
                Children = { new Text("Thick line borders"), new Text("for strong emphasis.") { Attributes = TerminalAttributes.Dim } },
            },
        };
        var emptyGroup = new GroupBox { Content = new Text("Untitled content") };
        var unicodeGroup = new GroupBox
        {
            Header = "界 Tools",
            Content = new Text("Wide header geometry"),
        };
        var asciiGroup = new GroupBox
        {
            Header = "ASCII",
            Glyphs = Glyphs.Ascii,
            Content = new Text("Fallback frame"),
        };
        var tinyGroup = new GroupBox
        {
            Header = "T",
            Width = Length.Cells(5),
            Height = Length.Cells(2),
        };

        return Doc.Page(
            Title,
            "Frames one content control with a titled border for visual grouping.",
            Doc.Section(
                "🗂️",
                "Basic group",
                "Use a GroupBox to frame related options with a descriptive header label.",
                Doc.Example(
                    "Settings checkboxes",
                    "Toggle any option and the status reflects the last changed setting.",
                    settingsGroup,
                    "var group = new GroupBox\n{\n    Header = \"Settings\",\n    Content = new Stack { ... },\n};")),
            Doc.Section(
                "🗂️",
                "Multiple groups",
                "Place groups side by side for a form layout that separates distinct concerns.",
                Doc.Example(
                    "Personal and preferences",
                    "Personal contains text inputs for identity. Preferences uses radio buttons for theme selection.",
                    Doc.Row(personalGroup, preferencesGroup))),
            Doc.Section(
                "🗂️",
                "Nested groups",
                "GroupBoxes nest cleanly when one logical section owns a subordinate grouping.",
                Doc.Example(
                    "Connection containing network",
                    "The outer Connection group frames a timeout label alongside the nested Network group.",
                    outerGroup)),
            Doc.Section(
                "🗂️",
                "Glyph styles",
                "The Glyphs property selects the Unicode box-drawing family for the border frame.",
                Doc.Example(
                    "Rounded, light, and heavy",
                    "Each GroupBox uses a different glyph family while sharing the same layout and header placement.",
                    Doc.Row(roundedGroup, lightGroup, heavyGroup),
                    "group.Glyphs = Glyphs.Heavy;"),
                Doc.Example(
                    "Empty, Unicode, ASCII, and tiny",
                    "Edge specimens prove omitted titles, wide header cells, fallback glyphs, and clipped frames.",
                    Doc.Row(emptyGroup, unicodeGroup, asciiGroup, tinyGroup))));
    }
}
