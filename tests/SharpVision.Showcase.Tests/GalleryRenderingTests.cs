// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;




/// <summary>Proves every showcase page lays out and renders into semantic terminal cells.</summary>
public sealed class GalleryRenderingTests
{
    /// <summary>Verifies the default page is readable and automatically scrollable at a normal size.</summary>
    [Fact]
    public void Render_WhenViewportIsTypical_ShowsDocumentationAndAutomaticScrolling()
    {
        using var gallery = CreateThemedGallery();
        var size = new Size(80, 24);

        new Engine().Layout(gallery, size);
        using Frame frame = new(size);
        gallery.Render(frame.Canvas);
        var screen = new Screen(frame);
        var view = gallery.Content.Parent.ShouldBeOfType<Stack>();

        gallery.Bounds.ShouldBe(new Rect(0, 0, 80, 24));
        screen.Text.ShouldContain("SHARP VISION");
        screen.Text.ShouldContain("Components");
        screen.Text.ShouldContain("Overview");
        screen.Count("Border").ShouldBeGreaterThanOrEqualTo(2);
        screen.HasNonDefaultColor().ShouldBeTrue();
        view.Extent.Height.ShouldBeGreaterThan(view.Viewport.Height);
        screen.ValidateContinuations();
    }

    /// <summary>Verifies responsive Text receives the committed documentation-pane width instead of an unbounded horizontal measure.</summary>
    [Fact]
    public void Render_WhenDocumentationPaneIsNarrow_WrapsCompleteTextGuidance()
    {
        using var gallery = CreateThemedGallery();
        gallery.Select(1);
        var size = new Size(80, 40);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);

        gallery.Render(frame.Canvas);

        var screen = new Screen(frame);
        screen.Text.ShouldContain("command paths.");
        gallery.Content.Parent.ShouldBeOfType<Stack>()
            .HorizontalBarVisibility.ShouldBe(ScrollBarVisibility.Hidden);
    }

    /// <summary>Verifies the Shadow page stages both modes separately with a readable block-glyph footprint.</summary>
    [Fact]
    public void Render_WhenShadowPageIsSelected_ShowsSeparatedCompositeAndBlockGlyphStages()
    {
        using var gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "Shadow"));
        var size = new Size(100, 60);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);

        gallery.Render(frame.Canvas);

        var screen = new Screen(frame);
        screen.Text.ShouldContain("Composite stage");
        screen.Text.ShouldContain("Block glyph stage");
        screen.Text.ShouldContain("░");
        screen.ValidateContinuations();
    }

    /// <summary>Verifies the Button page demonstrates both shadow modes and a stationary flat variant.</summary>
    [Fact]
    public void CreateExamples_WhenButtonPageIsSelected_ProvidesShadowAndFlatVariants()
    {
        using var gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "Button"));
        new Engine().Layout(gallery, new Size(100, 60));

        var buttons = FindAll<Button>(gallery.Content);

        buttons.ShouldContain(button => button.ShadowMode == ShadowMode.Composite);
        buttons.ShouldContain(button => !button.HasShadow);
        buttons.ShouldContain(button =>
            button.ShadowMode == ShadowMode.BlockGlyph &&
            button.ShadowGlyph == new Rune('░'));
    }

    /// <summary>Verifies the Canvas page pairs each positioning concept with its own live framed specimen.</summary>
    [Fact]
    public void Render_WhenCanvasPageIsSelected_ShowsGuidedPlacementExamples()
    {
        using var gallery = CreateThemedGallery();
        gallery.Select(2);
        var size = new Size(120, 80);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);

        gallery.Render(frame.Canvas);

        var screen = new Screen(frame);
        var edge = Find<Border>(
            gallery.Content,
            static value => value.Child is ControlText { Content: "Right 2 / Bottom 1" });
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

    /// <summary>Verifies the Window page exposes chrome variants and centers its dialog actions.</summary>
    [Fact]
    public void Render_WhenWindowPageIsSelected_ShowsChromeOptionsAndCenteredActions()
    {
        using var gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "Window"));
        var size = new Size(120, 80);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);

        gallery.Render(frame.Canvas);

        var windows = FindAll<Window>(gallery.Content);
        var screen = new Screen(frame);
        screen.Text.ShouldContain("Apply");
        screen.Text.ShouldContain("Cancel");
        windows.Count.ShouldBeGreaterThanOrEqualTo(4);
        windows.ShouldContain(window =>
            window.Glyphs == Glyphs.Paired &&
            window.TitlePlacement == WindowTitlePlacement.Center);

        var dialog = windows.Single(window => window.Title == "Project settings");
        dialog.Bounds.Height.ShouldBeGreaterThan(10);
        var actions = FindAll<Button>(dialog);
        actions.Count.ShouldBe(2);
        var left = actions.Min(button => button.Bounds.X);
        var right = actions.Max(button => button.Bounds.Right);
        var actionCenter = (left + right) / 2;
        var dialogCenter = dialog.Bounds.X + (dialog.Bounds.Width / 2);
        Math.Abs(actionCenter - dialogCenter).ShouldBeLessThanOrEqualTo(1);
        actions.ShouldAllBe(button => button.Bounds.Bottom <= dialog.Bounds.Bottom - 1);
        actions.ShouldAllBe(button =>
            button.Content.ShouldNotBeNull().Bounds.Height > 0 &&
            button.Content.Bounds.Bottom <= dialog.Bounds.Bottom - 1);
    }

    /// <summary>Verifies the List page paints a distinct surface and selected-row highlight.</summary>
    [Fact]
    public void Render_WhenListPageIsSelected_PaintsSurfaceAndSelectedRow()
    {
        using var gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "List"));
        var size = new Size(120, 80);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);

        gallery.Render(frame.Canvas);

        var active = FindAll<List>(gallery.Content).Single(list => list.IsEnabled);
        active.Background.ShouldBe(Color.Indexed(0));
        frame.GetCell(new Point(active.Bounds.X, active.Bounds.Y + 1)).Style.Background
            .ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies the Text page demonstrates every supported terminal text attribute tag.</summary>
    [Fact]
    public void CreateExamples_WhenTextPageIsSelected_ShowsTerminalAttributeMarkup()
    {
        using var gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "Text"));
        new Engine().Layout(gallery, new Size(120, 80));

        ControlText marked = FindAll<ControlText>(gallery.Content)
            .Single(text => text.Content.Contains("<rapidblink>", StringComparison.Ordinal));

        marked.Content.ShouldContain("<b>");
        marked.Content.ShouldContain("<d>");
        marked.Content.ShouldContain("<i>");
        marked.Content.ShouldContain("<u>");
        marked.Content.ShouldContain("<blink>");
        marked.Content.ShouldContain("<reverse>");
        marked.Content.ShouldContain("<s>");
        marked.Content.ShouldContain("<hidden>");
        marked.Content.ShouldContain("<rapidblink>");
        marked.Content.ShouldContain("<overline>");
        marked.Content.ShouldContain("<u=curly><uc=11>");
    }

    /// <summary>Verifies the Text markup action keeps its intrinsic content width.</summary>
    [Fact]
    public void Render_WhenTextMarkupActionIsLaidOut_UsesIntrinsicButtonWidth()
    {
        using var gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "Text"));
        new Engine().Layout(gallery, new Size(120, 80));

        var button = FindAll<Button>(gallery.Content)
            .Single(value => value.Content is ControlText { Content: "Append markup" });

        button.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        button.Bounds.Width.ShouldBe(button.DesiredSize.Width);
        button.Bounds.Width.ShouldBeLessThan(40);
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

    private static List<T> FindAll<T>(Control control) where T : Control
    {
        List<T> matches = [];
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

    private static int IndexOf(Gallery gallery, string page)
    {
        ArgumentNullException.ThrowIfNull(gallery);
        ArgumentException.ThrowIfNullOrWhiteSpace(page);
        var index = gallery.Pages.Select(static value => value).ToList().IndexOf(page);
        return index >= 0 ? index : throw new InvalidOperationException($"The {page} page is not registered.");
    }

    /// <summary>Verifies every page renders safely at tiny, typical, and large terminal sizes.</summary>
    [Theory]
    [InlineData(30, 8)]
    [InlineData(80, 24)]
    [InlineData(140, 40)]
    public void Render_WhenEveryPageUsesViewport_PreservesSelectionAndValidCells(int width, int height)
    {
        using var gallery = CreateThemedGallery();
        var size = new Size(width, height);
        var engine = new Engine();

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            gallery.Select(index);
            engine.Layout(gallery, size);
            using Frame frame = new(size);

            Should.NotThrow(() => gallery.Render(frame.Canvas));
            var screen = new Screen(frame);
            gallery.SelectedIndex.ShouldBe(index);
            gallery.SelectedPage.ShouldBe(gallery.Pages[index]);
            gallery.Bounds.ShouldBe(new Rect(0, 0, width, height));
            screen.ValidateContinuations();

            if (width >= 80 && height >= 24)
            {
                screen.Text.ShouldContain(gallery.SelectedPage);
                screen.Text.ShouldContain("Overview");
            }
        }
    }

    private static Gallery CreateThemedGallery()
    {
        var gallery = new Gallery();
        ApplyTheme(gallery, Themes.Dark);
        return gallery;
    }

    private static void ApplyTheme(Control control, Theme theme)
    {
        var context = ThemeContext.Create(theme);
        ApplyThemeContext(control, context);
    }

    private static void ApplyThemeContext(Control control, ThemeContext context)
    {
        control.SetThemeContext(context);
        control.VisitChildren(child => ApplyThemeContext(child, context));
    }
}
