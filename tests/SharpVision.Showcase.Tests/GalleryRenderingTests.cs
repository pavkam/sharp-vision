using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

using Shouldly;

namespace SharpVision.Showcase.Tests;

/// <summary>Proves every showcase page lays out and renders into semantic terminal cells.</summary>
public sealed class GalleryRenderingTests
{
    /// <summary>Verifies the default page is readable and automatically scrollable at a normal size.</summary>
    [Fact]
    public void Render_WhenViewportIsTypical_ShowsDocumentationAndAutomaticScrolling()
    {
        using var gallery = new Gallery();
        var size = new Size(80, 24);

        new Engine().Layout(gallery.Root, size);
        using var frame = new Frame(size);
        gallery.Root.Render(frame.Canvas);
        var screen = new Screen(frame);
        var view = gallery.Content.Parent.ShouldBeOfType<ScrollView>();

        gallery.Root.Bounds.ShouldBe(new Rect(0, 0, 80, 24));
        screen.Text.ShouldContain("SHARP VISION");
        screen.Text.ShouldContain("Components");
        screen.Text.ShouldContain("Overview");
        screen.Text.ShouldContain("Practical recipe");
        screen.Count("Border").ShouldBeGreaterThanOrEqualTo(2);
        screen.HasNonDefaultColor().ShouldBeTrue();
        view.Extent.Height.ShouldBeGreaterThan(view.Viewport.Height);
        screen.ValidateContinuations();
    }

    /// <summary>Verifies responsive RichText receives the committed documentation-pane width instead of an unbounded horizontal measure.</summary>
    [Fact]
    public void Render_WhenDocumentationPaneIsNarrow_WrapsCompleteRichTextGuidance()
    {
        using var gallery = new Gallery();
        gallery.Select(1);
        var size = new Size(80, 40);
        new Engine().Layout(gallery.Root, size);
        using var frame = new Frame(size);

        gallery.Root.Render(frame.Canvas);

        var screen = new Screen(frame);
        screen.Text.ShouldContain("command paths.");
        gallery.Content.Parent.ShouldBeOfType<ScrollView>()
            .HorizontalBarVisibility.ShouldBe(ScrollBarVisibility.Hidden);
    }

    /// <summary>Verifies the Shadow page stages both modes separately with a readable block-glyph footprint.</summary>
    [Fact]
    public void Render_WhenShadowPageIsSelected_ShowsSeparatedCompositeAndBlockGlyphStages()
    {
        using var gallery = new Gallery();
        gallery.Select(13);
        var size = new Size(100, 60);
        new Engine().Layout(gallery.Root, size);
        using var frame = new Frame(size);

        gallery.Root.Render(frame.Canvas);

        var screen = new Screen(frame);
        screen.Text.ShouldContain("Composite stage");
        screen.Text.ShouldContain("Block glyph stage");
        screen.Text.ShouldContain("░");
        screen.ValidateContinuations();
    }

    /// <summary>Verifies the Canvas page pairs each positioning concept with its own live framed specimen.</summary>
    [Fact]
    public void Render_WhenCanvasPageIsSelected_ShowsGuidedPlacementExamples()
    {
        using var gallery = new Gallery();
        gallery.Select(2);
        var size = new Size(120, 80);
        new Engine().Layout(gallery.Root, size);
        using var frame = new Frame(size);

        gallery.Root.Render(frame.Canvas);

        var screen = new Screen(frame);
        var edge = Find<Border>(
            gallery.Content,
            static value => value.Child is Controls.Text { Content: "Right 2 / Bottom 1" });
        edge.ShouldNotBeNull().Bounds.Right.ShouldBeLessThanOrEqualTo(size.Width);
        screen.Text.ShouldContain("Fixed placement");
        screen.Text.ShouldContain("Percentage placement");
        screen.Text.ShouldContain("Edge constraints");
        screen.Text.ShouldContain("Layering and clipping");
        screen.Text.ShouldContain("fixed 2,1");
        screen.Text.ShouldContain("50%,50%");
        screen.Text.ShouldContain("Right 2 / Bottom 1");
        screen.ValidateContinuations();
    }

    private static T? Find<T>(Control control, Func<T, bool> predicate) where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(predicate);

        if (control is T match && predicate(match))
        {
            return match;
        }

        if (control is not Container container)
        {
            return null;
        }

        foreach (var child in container.Children)
        {
            if (Find(child, predicate) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Verifies every page renders safely at tiny, typical, and large terminal sizes.</summary>
    [Theory]
    [InlineData(30, 8)]
    [InlineData(80, 24)]
    [InlineData(140, 40)]
    public void Render_WhenEveryPageUsesViewport_PreservesSelectionAndValidCells(int width, int height)
    {
        using var gallery = new Gallery();
        var size = new Size(width, height);
        var engine = new Engine();

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            gallery.Select(index);
            engine.Layout(gallery.Root, size);
            using var frame = new Frame(size);

            Should.NotThrow(() => gallery.Root.Render(frame.Canvas));
            var screen = new Screen(frame);
            gallery.SelectedIndex.ShouldBe(index);
            gallery.SelectedPage.ShouldBe(gallery.Pages[index].Name);
            gallery.Root.Bounds.ShouldBe(new Rect(0, 0, width, height));
            screen.ValidateContinuations();

            if (width >= 80 && height >= 24)
            {
                screen.Text.ShouldContain(gallery.SelectedPage);
                screen.Text.ShouldContain("Overview");
                screen.Text.ShouldContain("Practical recipe");
            }
        }
    }
}
