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
