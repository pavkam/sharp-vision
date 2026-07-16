// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the progressive documentation structure shared by showcase pages.</summary>
public sealed class ShowcaseContentTests
{
    private static readonly Dictionary<string, string[]> _sections =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Button"] = ["Start here", "Commands", "Window roles", "Shadow depth"],
            ["Canvas"] = ["Canvas layout", "Constraints", "Drawing fundamentals", "Useful custom drawing"],
            ["CheckBox"] = ["Two-state choice", "Three-state policy", "Marks", "Form recipe"],
            ["ComboBox"] = ["Start here", "Commit versus dismiss", "Long choices", "Constrained placement"],
            ["Dock"] = ["Application shell", "Order and spacing", "Sizing from the remainder", "Constrained space"],
            ["Expander"] = ["Expansion state", "Composition and availability", "Unicode and replacement"],
            ["FigletText"] = ["Live editor", "Font comparison", "Layout options", "Large output"],
            ["Grid"] = ["Track fundamentals", "Percentage and limits", "Responsive form", "Constrained space"],
            ["GroupBox"] = ["Frame and header", "Unicode and style scope", "Glyphs and constrained space"],
            ["List"] = ["Single selection", "Selection modes", "Templates", "Long data"],
            ["Menu"] = ["Command menu", "Menu bar", "Popup composition", "Selection and invocation"],
            ["NavigationView"] = ["Selection and availability", "Groups and separators", "Footer and overflow"],
            ["Overlay"] = ["Layering", "Stable ties", "Pointer transparency", "Clipping"],
            ["Popup"] = ["Anchored menu", "Placement", "Fallback and clamp", "Lifecycle"],
            ["ProgressBar"] = ["Determinate range", "Orientation and uncertainty", "Live mutation"],
            ["RadioButton"] = ["Named group", "Arrow traversal", "Unnamed scope", "Events"],
            ["ScrollBar"] = ["Range anatomy", "Input parity", "Live range", "Tiny rails"],
            ["Separator"] = ["Orientation", "Glyph and style"],
            ["Stack"] = ["Orientation", "Mixed sizing", "Visibility", "Constrained space"],
            ["TabControl"] = ["Selection and content", "Unicode and overflow", "Ownership and repair"],
            ["Table"] = ["Column sizing", "Interactive cells", "Dynamic rows", "Boundary states"],
            ["Text"] = ["Safe content", "Markup", "Overflow", "Unicode"],
            ["TextInput"] = ["Editing and submission", "Selection", "Clipboard and history", "Multiline"],
            ["Window"] = ["Draggable window", "Modal dialog", "Shadow depth", "Title placement"],
            ["Theming"] = ["Application theme", "Catalog", "Visual states", "Third-party controls"],
        };

    /// <summary>Verifies a section renders orientation, ordered examples, and escaped source text.</summary>
    [Fact]
    public void Section_WhenExamplesIncludeSource_RendersOrderedEscapedDocumentation()
    {
        // Arrange
        var first = Doc.Example(
            "One command",
            "Activate the command and observe the result.",
            new Button { Content = new ControlText("Run") },
            "var values = new List<string>();\nvar path = @\"C:\\demo\";\nvar compare = 2 < 3;");
        var second = Doc.Example(
            "Second command",
            "This example remains after the first.",
            new Button { Content = new ControlText("Stop") });
        using var section = Doc.Section(
            "🧭",
            "Start here",
            "Begin with the smallest useful command.",
            first,
            second);
        var size = new Size(80, 30);
        new Engine().Layout(section, size);
        using Frame frame = new(size);

        // Act
        section.Render(frame.Canvas);

        // Assert
        var screen = new Screen(frame);
        screen.Text.ShouldContain("Start here");
        screen.Text.ShouldContain("Begin with the smallest useful command.");
        screen.Text.ShouldContain("C#");
        screen.Text.ShouldContain("List<string>");
        screen.Text.ShouldContain("C:\\demo");
        screen.Text.ShouldContain("2 < 3");
        screen.Text.IndexOf("One command", StringComparison.Ordinal)
            .ShouldBeLessThan(screen.Text.IndexOf("Second command", StringComparison.Ordinal));
        var content = ControlTree.Text(section);
        content.ShouldContain("<accent><b>🧭 Start here</b></accent>");
        content.ShouldContain("<d>Begin with the smallest useful command.</d>");
        content.ShouldContain("<b>One command</b>");
        content.ShouldContain("<info><b>C#</b></info>");
    }

    /// <summary>Verifies every catalog page exposes progressive guidance and reproducible source.</summary>
    [Fact]
    public void Content_WhenEveryPageBuilds_ContainsRequiredSectionsAndSource()
    {
        // Arrange
        using Gallery gallery = new();
        var engine = new Engine();
        List<string> missing = [];

        // Act
        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            var pageName = gallery.Pages[index];
            using var page = Gallery.CreatePage(index);
            engine.Layout(page, new Size(100, 80));
            var content = ControlTree.Text(page);

            foreach (var section in _sections[pageName])
            {
                if (!content.Contains($" {section}</b></accent>", StringComparison.Ordinal))
                {
                    missing.Add($"{pageName}: {section}");
                }

                var sectionText = ControlTree.FindAll<ControlText>(page).SingleOrDefault(text =>
                    text.Content.Contains($" {section}</b></accent>", StringComparison.Ordinal));

                if (sectionText is null || !HasSingleIconPrefix(sectionText.Content, section))
                {
                    missing.Add($"{pageName}: {section} icon");
                }
            }

            if (!content.Contains("<b>C#</b>", StringComparison.Ordinal))
            {
                missing.Add($"{pageName}: C# source");
            }
        }

        // Assert
        missing.ShouldBeEmpty(string.Join(Environment.NewLine, missing));
    }

    private static bool HasSingleIconPrefix(string content, string heading)
    {
        const string opening = "<accent><b>";

        if (!content.StartsWith(opening, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = $" {heading}</b></accent>";
        var suffixIndex = content.IndexOf(suffix, StringComparison.Ordinal);

        if (suffixIndex <= opening.Length)
        {
            return false;
        }

        var icon = content[opening.Length..suffixIndex];
        return !string.IsNullOrWhiteSpace(icon) && !icon.Contains(' ');
    }
}
