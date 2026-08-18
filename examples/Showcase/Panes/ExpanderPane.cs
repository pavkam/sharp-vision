// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

internal sealed class ExpanderPane: CompositeControlBase
{
    internal ExpanderPane() => InitializeContent(CreateContent());
    internal const string Title = "Expander";

    private static DocPage CreateContent()
    {
        var status = new Text("Expanded: True");
        var basic = new Expander
        {
            HeaderText = "&Advanced settings",
            Content = new Stack
            {
                Spacing = 1,
                Children =
                {
                    new CheckBox { Text = "&Debug mode" },
                    new CheckBox { Text = "&Verbose logging" }
                }
            }
        };
        basic.ExpandedChanged += (_, _) => status.Content = $"Expanded: {basic.IsExpanded}";
        var collapsed = new Expander
        {
            HeaderText = "&Collapsed by default",
            IsExpanded = false,
            Content = new Text("Hidden until you expand.")
        };
        var nested = new Expander
        {
            HeaderText = "&Outer section",
            Content = new Stack
            {
                Spacing = 1,
                Children =
                {
                    new Text("Outer content."),
                    new Expander { HeaderText = "&Inner section", Content = new Text("Nested content.") }
                }
            }
        };
        var faq1 = new Expander
        {
            HeaderText = "&What is SharpVision?",
            IsExpanded = false,
            Content = new Text("A .NET terminal UI framework.")
        };
        var faq2 = new Expander
        {
            HeaderText = "&How to create controls?",
            IsExpanded = false,
            Content = new Text("Derive from Control or CompositeControl.")
        };

        return new DocPage(Title, "<info>Expander</info> displays a collapsible section with a focusable header toggle and optional content.",
            new DocSection("📂", "Basic expander", "Click the header or press <reverse>Enter</reverse>/<reverse>Space</reverse> to toggle.",
                new DocExample("Settings section", "Toggle to show or hide.", new DocColumn(basic, status),
                    "var exp = new Expander { HeaderText = \"&Settings\" };"),
                new DocExample("Initially collapsed", "Set <info>Expanded</info> to <info>false</info>.", collapsed)),
            new DocSection("📂", "Nested expanders", "Expanders inside expanders.",
                new DocExample("Two-level nesting", "Both toggle independently.", nested)),
            new DocSection("📂", "Content indent",
                "<info>ExpanderStyle.ContentIndent</info> controls the leading cell inset below the header. The default is two cells.",
                new DocExample(
                    "Custom indentation",
                    "Compare zero, default, and deep indentation side by side.",
                    new DocColumn(
                        new Expander { HeaderText = "Indent &0", Style = Indent(0), Content = new Text("Flush left.") },
                        new Expander { HeaderText = "Indent &2", Style = Indent(2), Content = new Text("Default.") },
                        new Expander { HeaderText = "Indent &6", Style = Indent(6), Content = new Text("Deep.") }),
                    "expander.Style = ExpanderStyle.Default with { ContentIndent = 4 };")),
            new DocSection("📂", "FAQ pattern", "Collapsed expanders create an accordion.",
                new DocExample("Stacked questions", "Click any header.", new DocColumn(faq1, faq2))),
            new DocSection("📂", "Custom disclosure glyphs",
                "<info>ExpanderStyle</info>'s <info>CollapsedGlyph</info> and <info>ExpandedGlyph</info> replace the default disclosure markers. A theme's own <info>styles.expander</info> section reaches them for every expander at once; assign <info>Style = null</info> to return one to theme ownership.",
                new DocExample(
                    "Plus/minus disclosure",
                    "Replace the built-in arrows with familiar plus and minus marks.",
                    new DocColumn(
                        new Expander
                        {
                            HeaderText = "&Expanded section",
                            Style = PlusMinus,
                            Content = new Text("Visible content.")
                        },
                        new Expander
                        {
                            HeaderText = "Fo&lded section",
                            IsExpanded = false,
                            Style = PlusMinus,
                            Content = new Text("Hidden content.")
                        }),
                    "expander.Style = ExpanderStyle.Default with\n{\n    ExpandedGlyph = new Rune('-'),\n    CollapsedGlyph = new Rune('+')\n};")),
            new DocSection("📂", "Themed chrome",
                "Expanders receive their frame from the active theme so application code does not disturb their layout contract.",
                new DocExample(
                    "Connection settings",
                    "This specimen uses the complete themed frame while customizing only content padding.",
                    new Expander
                    {
                        HeaderText = "Con&nection settings",
                        Padding = new Thickness(1, 0),
                        Content = new Stack
                        {
                            Spacing = 1,
                            Children =
                            {
                                new CheckBox { Text = "&Use proxy" },
                                new CheckBox { Text = "Verify &SSL" }
                            }
                        }
                    },
                    "var expander = new Expander\n{\n    HeaderText = \"&Settings\",\n    Padding = new Thickness(1, 0),\n};")));
    }

    private static ExpanderStyle PlusMinus => ExpanderStyle.Default with
    {
        ExpandedGlyph = new Rune('-'),
        CollapsedGlyph = new Rune('+')
    };

    private static ExpanderStyle Indent(int cells) => ExpanderStyle.Default with { ContentIndent = cells };
}
