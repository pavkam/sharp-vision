using SharpVision.Controls;

using Shouldly;

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
        FindText(first).ShouldContain("Examples");
        FindText(first).ShouldContain("Properties");
        FindText(first).ShouldContain("Interaction");
        FindText(first).ShouldContain("Content");
        FindText(first).ShouldContain("Control?");
        FindText(first).ShouldContain("null");
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
            values[2],
            [new PropertyDescription("Content", "Control?", "null", "Description")],
            () => new ControlText("Example")));
    }

    /// <summary>Verifies a page requires property documentation.</summary>
    [Fact]
    public void Constructor_WhenPropertiesAreEmpty_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => new Page(
            "Sample",
            "Summary",
            "Interaction",
            [],
            () => new ControlText("Example")));
    }

    /// <summary>Verifies a page requires a live example factory.</summary>
    [Fact]
    public void Constructor_WhenFactoryIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() => new Page(
            "Sample",
            "Summary",
            "Interaction",
            [new PropertyDescription("Content", "Control?", "null", "Description")],
            null!));
    }

    private static Page CreatePage() => new(
        "Sample",
        "Explains a sample control.",
        "Use the keyboard or pointer.",
        [new PropertyDescription("Content", "Control?", "null", "Owns the displayed content.")],
        () => new ControlText("Example"));

    private static string FindText(Control control)
    {
        var text = new List<string>();
        Visit(control, text);
        return string.Join('\n', text);
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

        if (control is not Container container)
        {
            return;
        }

        foreach (var child in container.Children)
        {
            Visit(child, text);
        }
    }
}
