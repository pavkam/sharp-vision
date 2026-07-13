using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Showcase.Panes;
using SharpVision.Terminal.Geometry;

using Shouldly;

using ControlStack = SharpVision.Controls.Stack;
using ControlText = SharpVision.Controls.Text;

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies one reusable documentation page and its live example ownership.</summary>
public sealed class PageTests
{
    /// <summary>Verifies page creation returns fresh examples and all documentation sections.</summary>
    [Fact]
    public void CreateContent_WhenPageIsValid_BuildsCompleteFreshDocumentationTrees()
    {
        var page = CreatePage();

        using var first = page.CreateContent();
        using var second = page.CreateContent();

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

        var narrative = FindAll<RichText>(first)
            .Single(value => InlineText(value).StartsWith("Use this control when", StringComparison.Ordinal));
        narrative.Parent.ShouldNotBeOfType<Border>();
    }

    /// <summary>Verifies interaction metadata renders as a standalone table without a prose card.</summary>
    [Fact]
    public void CreateContent_WhenInteractionsAreStructured_RendersDedicatedInteractionTable()
    {
        var page = new Page(
            "Sample",
            "Summary",
            [new InteractionDescription("Keyboard", "Press Enter", "Activates the command.")],
            [new PropertyDescription("Content", "Control?", "null", "Description")],
            static () => new TestPane(
                "Sample",
                "Summary",
                [new InteractionDescription("Keyboard", "Press Enter", "Activates the command.")],
                [new PropertyDescription("Content", "Control?", "null", "Description")]));

        using var content = page.CreateContent();

        FindText(content).ShouldContain("Keyboard");
        FindText(content).ShouldContain("Press Enter");
        FindText(content).ShouldContain("Activates the command.");
        var tables = FindAll<Table>(content);
        tables.Count.ShouldBe(2);
        tables[1].Rows.Count.ShouldBe(1);
    }

    /// <summary>Verifies the borderless narrative remeasures and wraps at the committed page width.</summary>
    [Fact]
    public void CreateContent_WhenNarrativeIsNarrow_WrapsRichTextBeyondOneContentLine()
    {
        var page = new Page(
            "Sample",
            "Use this control when a long explanation needs to remain readable in a narrow terminal page.",
            [new InteractionDescription(
                "General",
                "Open the live example, resize the terminal, and confirm that the guidance stays readable.",
                "Open the live example, resize the terminal, and confirm that the guidance stays readable.")],
            [new PropertyDescription("Content", "Control?", "null", "Owns the displayed content.")],
            static () => new TestPane(
                "Sample",
                "Use this control when a long explanation needs to remain readable in a narrow terminal page.",
                [new InteractionDescription(
                    "General",
                    "Open the live example, resize the terminal, and confirm that the guidance stays readable.",
                    "Open the live example, resize the terminal, and confirm that the guidance stays readable.")],
                [new PropertyDescription("Content", "Control?", "null", "Owns the displayed content.")]));
        using var content = page.CreateContent();
        new Engine().Layout(content, new Size(72, 40));
        var recipe = FindAll<RichText>(content).Single(value => InlineText(value).StartsWith("Use this control when", StringComparison.Ordinal));

        recipe.Bounds.Height.ShouldBeGreaterThan(2);
        recipe.Parent.ShouldNotBeOfType<Border>();
    }

    /// <summary>Verifies page identity and documentation values reject blanks and missing content.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenRequiredTextIsBlank_ThrowsArgumentException(int field)
    {
        var values = new[] { "Sample", "Summary", "Interaction" };
        values[field] = " ";

        _ = Should.Throw<ArgumentException>(() => new Page(
            values[0],
            values[1],
            [new InteractionDescription("General", "Use the documented control interaction.", values[2])],
            [new PropertyDescription("Content", "Control?", "null", "Description")],
            static () => new TestPane()));
    }

    /// <summary>Verifies a page requires property documentation.</summary>
    [Fact]
    public void Constructor_WhenPropertiesAreEmpty_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => new Page(
            "Sample",
            "Summary",
            [new InteractionDescription("General", "Use the documented control interaction.", "Interaction")],
            [],
            static () => new TestPane()));
    }

    /// <summary>Verifies a page requires a live example factory.</summary>
    [Fact]
    public void Constructor_WhenFactoryIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() => new Page(
            "Sample",
            "Summary",
            [new InteractionDescription("General", "Use the documented control interaction.", "Interaction")],
            [new PropertyDescription("Content", "Control?", "null", "Description")],
            null!));
    }

    private static Page CreatePage() => new(
        "Sample",
        "Explains a sample control.",
        [new InteractionDescription("General", "Use the documented control interaction.", "Use the keyboard or pointer.")],
        [new PropertyDescription("Content", "Control?", "null", "Owns the displayed content.")],
        static () => new TestPane());

    private static List<T> FindAll<T>(Control control) where T : Control
    {
        var matches = new List<T>();
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
            foreach (var child in container.Children)
            {
                Visit(child, matches);
            }
        }
    }

    private static string FindText(Control control)
    {
        var text = new List<string>();
        Visit(control, text);
        return string.Join('\n', text);
    }

    private static string InlineText(RichText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var values = new List<string>();

        foreach (var inline in text.Inlines)
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
            foreach (var inline in richText.Inlines)
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

        foreach (var child in container.Children)
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
