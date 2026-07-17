// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

internal sealed class ExpanderPane: CompositeControl
{
    internal ExpanderPane() => InitializeContent(CreateContent());
    internal const string Title = "Expander";

    private static Dock CreateContent()
    {
        var status = new Text("Expanded: True");
        var basic = new Expander { Header = "Advanced settings", Content = new Stack { Spacing = 1, Children = { new CheckBox { Content = new Text("Debug mode") }, new CheckBox { Content = new Text("Verbose logging") } } } };
        basic.ExpandedChanged += (_, _) => status.Content = $"Expanded: {basic.IsExpanded}";
        var collapsed = new Expander { Header = "Collapsed by default", IsExpanded = false, Content = new Text("Hidden until you expand.") };
        var nested = new Expander { Header = "Outer section", Content = new Stack { Spacing = 1, Children = { new Text("Outer content."), new Expander { Header = "Inner section", Content = new Text("Nested content.") } } } };
        var faq1 = new Expander { Header = "What is SharpVision?", IsExpanded = false, Content = new Text("A .NET terminal UI framework.") };
        var faq2 = new Expander { Header = "How to create controls?", IsExpanded = false, Content = new Text("Derive from Control or CompositeControl.") };

        return Doc.Page(Title, "Displays a collapsible section with a focusable header toggle and optional content.",
            Doc.Section("📂", "Basic expander", "Click header or Enter/Space to toggle.",
                Doc.Example("Settings section", "Toggle to show or hide.", Doc.Column(basic, status), "var exp = new Expander { Header = \"Settings\" };"),
                Doc.Example("Initially collapsed", "Set IsExpanded = false.", collapsed)),
            Doc.Section("📂", "Nested expanders", "Expanders inside expanders.",
                Doc.Example("Two-level nesting", "Both toggle independently.", nested)),
            Doc.Section("📂", "FAQ pattern", "Collapsed expanders create an accordion.",
                Doc.Example("Stacked questions", "Click any header.", Doc.Column(faq1, faq2))),
            Doc.Section("📂", "Rounded chrome override", "Expanders are square framed surfaces by default; override BorderGlyphs when a softer frame suits the content.",
                Doc.Example(
                    "Rounded connection settings",
                    "This specimen keeps the default one-cell frame and replaces only its square glyph family.",
                    new Expander
                    {
                        Header = "Connection settings",
                        BorderGlyphs = Glyphs.Rounded,
                        Padding = new Thickness(1, 0),
                        Content = new Stack
                        {
                            Spacing = 1,
                            Children =
                            {
                                new CheckBox { Content = new Text("Use proxy") },
                                new CheckBox { Content = new Text("Verify SSL") },
                            },
                        },
                    },
                    "var expander = new Expander\n{\n    Header = \"Settings\",\n    BorderGlyphs = Glyphs.Rounded,\n};")));
    }
}
