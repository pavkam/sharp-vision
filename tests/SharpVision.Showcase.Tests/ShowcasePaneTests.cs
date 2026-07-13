// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Showcase.Panes;
using SharpVision.Terminal.Geometry;

using ControlStack = Stack;
using ControlText = Controls.Text;

/// <summary>Verifies showcase pane documentation chrome and live example ownership.</summary>
public sealed class ShowcasePaneTests
{
    /// <summary>Verifies pane construction returns fresh trees and all documentation sections.</summary>
    [Fact]
    public void Constructor_WhenPaneIsValid_BuildsCompleteFreshDocumentationTrees()
    {
        using TestPane first = CreatePane();
        using TestPane second = CreatePane();

        first.ShouldNotBeSameAs(second);
        FindText(first).ShouldContain("Sample");
        FindText(first).ShouldContain("Overview");
        FindText(first).ShouldContain("Use this control when");
        FindText(first).ShouldContain("Explore it in the live example");
        FindText(first).ShouldContain("Examples");
        FindText(first).ShouldContain("Technical details");
        FindText(first).ShouldContain("Interaction");
        FindText(first).ShouldContain("Content");
        FindText(first).ShouldContain("Control?");
        FindText(first).ShouldContain("null");
        FindAll<Table>(first).Count.ShouldBe(2);
        FindAll<Table>(first)[1].Columns.Select(static column => column.Header)
            .ShouldBe(["Input", "Behavior", "Result"]);

        RichText narrative = FindAll<RichText>(first)
            .Single(value => InlineText(value).StartsWith("Use this control when", StringComparison.Ordinal));
        narrative.Parent.ShouldNotBeOfType<Border>();
    }

    /// <summary>Verifies interaction metadata renders as a standalone table without a prose card.</summary>
    [Fact]
    public void Constructor_WhenInteractionsAreStructured_RendersDedicatedInteractionTable()
    {
        using TestPane content = new TestPane(
            "Sample",
            "Summary",
            [new InteractionDescription("Keyboard", "Press Enter", "Activates the command.")],
            [new PropertyDescription("Content", "Control?", "null", "Description")]);

        FindText(content).ShouldContain("Keyboard");
        FindText(content).ShouldContain("Press Enter");
        FindText(content).ShouldContain("Activates the command.");
        List<Table> tables = FindAll<Table>(content);
        tables.Count.ShouldBe(2);
        tables[1].Rows.Count.ShouldBe(1);
    }

    /// <summary>Verifies the borderless narrative remeasures and wraps at the committed page width.</summary>
    [Fact]
    public void Constructor_WhenNarrativeIsNarrow_WrapsRichTextBeyondOneContentLine()
    {
        using TestPane content = new TestPane(
            "Sample",
            "Use this control when a long explanation needs to remain readable in a narrow terminal page.",
            [new InteractionDescription(
                "General",
                "Open the live example, resize the terminal, and confirm that the guidance stays readable.",
                "Open the live example, resize the terminal, and confirm that the guidance stays readable.")],
            [new PropertyDescription("Content", "Control?", "null", "Owns the displayed content.")]);
        new Engine().Layout(content, new Size(72, 40));
        RichText recipe = FindAll<RichText>(content).Single(value => InlineText(value).StartsWith("Use this control when", StringComparison.Ordinal));

        recipe.Bounds.Height.ShouldBeGreaterThan(2);
        recipe.Parent.ShouldNotBeOfType<Border>();
    }

    /// <summary>Verifies pane identity and documentation values reject blanks and missing content.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenRequiredTextIsBlank_ThrowsArgumentException(int field)
    {
        var values = new[] { "Sample", "Summary", "Interaction" };
        values[field] = " ";

        _ = Should.Throw<ArgumentException>(() => new TestPane(
            values[0],
            values[1],
            [new InteractionDescription("General", "Use the documented control interaction.", values[2])],
            [new PropertyDescription("Content", "Control?", "null", "Description")]));
    }

    /// <summary>Verifies a pane requires property documentation.</summary>
    [Fact]
    public void Constructor_WhenPropertiesAreEmpty_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => new TestPane(
            "Sample",
            "Summary",
            [new InteractionDescription("General", "Use the documented control interaction.", "Interaction")],
            []));
    }

    private static TestPane CreatePane() => new(
        "Sample",
        "Explains a sample control.",
        [new InteractionDescription("General", "Use the documented control interaction.", "Use the keyboard or pointer.")],
        [new PropertyDescription("Content", "Control?", "null", "Owns the displayed content.")]);

    private static List<T> FindAll<T>(Control control) where T : Control
    {
        List<T> matches = new List<T>();
        Visit(control, matches);
        return matches;
    }

    private static void Visit<T>(Control control, List<T> matches) where T : Control
    {
        if (control is T match)
        {
            matches.Add(match);
        }

        if (control is Container container)
        {
            foreach (Control child in container.Children)
            {
                Visit(child, matches);
            }
        }
    }

    private static string FindText(Control control)
    {
        List<string> text = new List<string>();
        Visit(control, text);
        return string.Join('\n', text);
    }

    private static string InlineText(RichText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string> values = new List<string>();

        foreach (Inline inline in text.Inlines)
        {
            switch (inline)
            {
                case Run run:
                    values.Add(run.Content);
                    break;
                case Hyperlink hyperlink:
                    values.Add(hyperlink.Content);
                    break;
                default:
                    break;
            }
        }

        return string.Join('\n', values);
    }

    private static void Visit(Control control, List<string> text)
    {
        if (control is RichText richText)
        {
            foreach (Inline inline in richText.Inlines)
            {
                switch (inline)
                {
                    case Run run:
                        text.Add(run.Content);
                        break;
                    case Hyperlink hyperlink:
                        text.Add(hyperlink.Content);
                        break;
                    default:
                        break;
                }
            }
        }
        else if (control is ControlText controlText)
        {
            text.Add(controlText.Content);
        }

        if (control is not Container container)
        {
            return;
        }

        foreach (Control child in container.Children)
        {
            Visit(child, text);
        }
    }

    private sealed class TestPane: ShowcasePane
    {
        internal TestPane()
            : base(
                "Sample",
                "Explains a sample control.",
                [new InteractionDescription("General", "Use the documented control interaction.", "Use the keyboard or pointer.")],
                [new PropertyDescription("Content", "Control?", "null", "Owns the displayed content.")])
        {
        }

        internal TestPane(
            string name,
            string summary,
            InteractionDescription[] interactions,
            PropertyDescription[] properties)
            : base(name, summary, interactions, properties)
        {
        }

        protected override void BuildExamples(ControlStack examples) =>
            examples.Children.Add(new ControlText("Example"));
    }
}
