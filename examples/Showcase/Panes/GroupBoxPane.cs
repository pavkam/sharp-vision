// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the GroupBox control with titled-border framing specimens.</summary>
internal sealed class GroupBoxPane: CompositeControlBase
{
    internal GroupBoxPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "GroupBox";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var settingsStatus = new Text("Settings: waiting");
        var autoSave = new CheckBox { Text = "Aut&o-save", IsChecked = true };
        var lineNumbers = new CheckBox { Text = "&Line numbers" };
        var wordWrap = new CheckBox { Text = "&Word wrap", IsChecked = true };
        autoSave.StateChanged += (_, eventArgs) =>
            settingsStatus.Content = $"Settings: Auto-save → {eventArgs.Current}";
        lineNumbers.StateChanged += (_, eventArgs) =>
            settingsStatus.Content = $"Settings: Line numbers → {eventArgs.Current}";
        wordWrap.StateChanged += (_, eventArgs) =>
            settingsStatus.Content = $"Settings: Word wrap → {eventArgs.Current}";
        var settingsGroup = new GroupBox
        {
            HeaderText = "&Settings",
            Content = new DocColumn(autoSave, lineNumbers, wordWrap, settingsStatus)
        };

        var personalGroup = new GroupBox
        {
            HeaderText = "&Personal",
            Width = Length.Cells(22),
            Content = new DocColumn(
                new Stack
                {
                    Spacing = 0,
                    Children =
                    {
                        ShowcasePaneHelpers.DimCaption("Name"),
                        new TextInput { Text = "Jane", Width = Length.Cells(20) }
                    }
                },
                new Stack
                {
                    Spacing = 0,
                    Children =
                    {
                        ShowcasePaneHelpers.DimCaption("Email"),
                        new TextInput { Text = "jane@example.com", Width = Length.Cells(20) }
                    }
                })
        };
        var preferencesGroup = new GroupBox
        {
            HeaderText = "Pre&ferences",
            Width = Length.Cells(22),
            Content = new DocColumn(
                new RadioButton { Text = "Li&ght theme", GroupName = "theme" },
                new RadioButton { Text = "&Dark theme", GroupName = "theme", IsChecked = true },
                new RadioButton { Text = "S&ystem default", GroupName = "theme" })
        };

        var innerGroup = new GroupBox
        {
            HeaderText = "&Network",
            Content = new DocColumn(
                new CheckBox { Text = "&Use proxy", IsChecked = true },
                new CheckBox { Text = "&Verify certificates" })
        };
        var outerGroup = new GroupBox
        {
            HeaderText = "&Connection",
            Content = new DocColumn(
                new Text("Timeout: 30s"),
                innerGroup)
        };

        var roundedGroup = new GroupBox
        {
            HeaderText = "&Rounded",
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Content = new Stack
            {
                Padding = new Thickness(1, 0),
                Children =
                {
                    new Text("Smooth"),
                    ShowcasePaneHelpers.DimCaption("edges.")
                }
            }
        };
        var lightGroup = new GroupBox
        {
            HeaderText = "L&ight",
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Content = new Stack
            {
                Padding = new Thickness(1, 0),
                Children =
                {
                    new Text("Thin"),
                    ShowcasePaneHelpers.DimCaption("border.")
                }
            }
        };
        var heavyGroup = new GroupBox
        {
            HeaderText = "&Heavy",
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Heavy,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Content = new Stack
            {
                Padding = new Thickness(1, 0),
                Children =
                {
                    new Text("Thick"),
                    ShowcasePaneHelpers.DimCaption("frame.")
                }
            }
        };
        var untitledGroup = new GroupBox { Content = new Text("No title") };
        var unicodeGroup = new GroupBox { HeaderText = "&界 Tools", Content = new Text("Wide glyph") };
        var asciiGroup = new GroupBox
        {
            HeaderText = "&ASCII",
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Ascii,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Content = new Text("Basic")
        };
        var tinyGroup = new GroupBox
        {
            HeaderText = "&T",
            Width = Length.Cells(7),
            Height = Length.Cells(3),
            Content = new Text("x")
        };

        return new DocPage(
            Title,
            "<info>GroupBox</info> frames one content control with a titled border for visual grouping.",
            new DocSection(
                "🗂️",
                "Basic group",
                "Use a GroupBox to frame related options with a descriptive header label.",
                new DocExample(
                    "Settings checkboxes",
                    "Toggle any option and the status reflects the last changed setting.",
                    settingsGroup,
                    "var group = new GroupBox\n{\n    HeaderText = \"&Settings\",\n    Content = new Stack { ... },\n};")),
            new DocSection(
                "🗂️",
                "Multiple groups",
                "Place groups side by side for a form layout that separates distinct concerns.",
                new DocExample(
                    "Personal and preferences",
                    "Personal contains text inputs for identity. Preferences uses radio buttons for theme selection.",
                    new DocRow(personalGroup, preferencesGroup))),
            new DocSection(
                "🗂️",
                "Nested groups",
                "GroupBoxes nest cleanly when one logical section owns a subordinate grouping.",
                new DocExample(
                    "Connection containing network",
                    "The outer Connection group frames a timeout label alongside the nested Network group.",
                    outerGroup)),
            new DocSection(
                "🗂️",
                "Themed frames",
                "GroupBox exposes its own <info>Border</info> property, so an application can select a glyph family per instance instead of accepting the theme default.",
                new DocExample(
                    "Framed groups",
                    "Rounded, Light, and Heavy each set an explicit local <info>Border</info> while keeping independent content and header placement.",
                    new DocRow(roundedGroup, lightGroup, heavyGroup)),
                new DocExample(
                    "Untitled, Unicode, ASCII, and compact",
                    "Edge specimens prove omitted titles, wide header cells, an explicit ASCII glyph family, and a compact frame with visible content.",
                    new DocRow(untitledGroup, unicodeGroup, asciiGroup, tinyGroup))));
    }

}
