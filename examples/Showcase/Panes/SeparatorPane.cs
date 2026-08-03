// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Separator control with horizontal, vertical, and contextual layout specimens.</summary>
internal sealed class SeparatorPane: CompositeControlBase
{
    internal SeparatorPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Separator";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var standalone = new Separator { HorizontalAlignment = HorizontalAlignment.Stretch, Width = Length.Cells(40) };

        var betweenStack = new Stack { Spacing = 0, Width = Length.Cells(40) };
        betweenStack.Children.Add(new Text("Above the separator"));
        betweenStack.Children.Add(new Separator { HorizontalAlignment = HorizontalAlignment.Stretch });
        betweenStack.Children.Add(new Text("Below the separator"));

        var leftPanel = new Dock
        {
            Width = Length.Cells(18),
            Height = Length.Cells(5),
            Padding = new Thickness(1, 0),
            Children = { new Text("Left panel") }
        };
        var rightPanel = new Dock { Padding = new Thickness(1, 0), Children = { new Text("Right panel") } };
        var verticalSep = new Separator
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var sideBySide = new Stack
        {
            Orientation = Orientation.Horizontal,
            Width = Length.Cells(40),
            Height = Length.Cells(5)
        };
        sideBySide.Children.Add(leftPanel);
        sideBySide.Children.Add(verticalSep);
        sideBySide.Children.Add(rightPanel);

        var settingsPanel = new Stack { Spacing = 0, Width = Length.Cells(40) };
        settingsPanel.Children.Add(new Text("General")
        {
            Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default)
        });
        settingsPanel.Children.Add(new Text("  Language: English"));
        settingsPanel.Children.Add(new Text("  Region: US"));
        settingsPanel.Children.Add(new Separator { HorizontalAlignment = HorizontalAlignment.Stretch });
        settingsPanel.Children.Add(new Text("Display")
        {
            Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default)
        });
        settingsPanel.Children.Add(new Text("  Theme: Dark"));
        settingsPanel.Children.Add(new Text("  Font size: 14"));
        settingsPanel.Children.Add(new Separator { HorizontalAlignment = HorizontalAlignment.Stretch });
        settingsPanel.Children.Add(new Text("Advanced")
        {
            Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default)
        });
        settingsPanel.Children.Add(new Text("  Debug mode: Off"));

        return new DocPage(
            Title,
            "<info>Separator</info> draws a non-interactive horizontal or vertical divider line between content regions.",
            new DocSection(
                "➖",
                "Horizontal separator",
                "The default horizontal line stretches across the available width to divide stacked content.",
                new DocExample(
                    "Standalone line",
                    "A separator fills the width of its parent container.",
                    new DocCard(standalone)),
                new DocExample(
                    "Between text items",
                    "Place a separator between two text controls in a Stack to create a visual break.",
                    new DocCard(betweenStack),
                    "var stack = new Stack();\nstack.Children.Add(new Text(\"Above\"));\nstack.Children.Add(new Separator());\nstack.Children.Add(new Text(\"Below\"));")),
            new DocSection(
                "➖",
                "Vertical separator",
                "Set <info>Orientation</info> to <info>Vertical</info> to divide side-by-side panels with a thin column divider.",
                new DocExample(
                    "Panel divider",
                    "A vertical separator stretches between two horizontally arranged panels.",
                    new DocCard(sideBySide),
                    "var sep = new Separator\n{\n    Orientation = Orientation.Vertical,\n};")),
            new DocSection(
                "➖",
                "In context",
                "Separators group related settings into visual sections without requiring heavier framing.",
                new DocExample(
                    "Settings panel",
                    "Horizontal separators divide a settings list into General, Display, and Advanced sections.",
                    new DocCard(settingsPanel))));
    }
}
