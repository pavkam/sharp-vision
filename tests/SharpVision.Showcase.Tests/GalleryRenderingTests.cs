// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;


using ControlText = SharpVision.Controls.Text;

/// <summary>Proves every showcase page lays out and renders into semantic terminal cells.</summary>
public sealed class GalleryRenderingTests
{
    /// <summary>Verifies the default page is readable and automatically scrollable at a normal size.</summary>
    [Fact]
    public void Render_WhenViewportIsTypical_ShowsDocumentationAndAutomaticScrolling()
    {
        using Gallery gallery = CreateThemedGallery();
        Size size = new(80, 24);

        new Engine().Layout(gallery, size);
        using Frame frame = new(size);
        gallery.Render(frame.Canvas);
        Screen screen = new(frame);
        Stack view = gallery.Content.Parent.ShouldBeOfType<Stack>();

        gallery.Bounds.ShouldBe(new Rect(0, 0, 80, 24));
        screen.Text.ShouldContain("SHARP VISION");
        screen.Text.ShouldContain("Components");
        screen.Text.ShouldContain("Overview");
        screen.Text.ShouldContain("Button");
        screen.HasNonDefaultColor().ShouldBeTrue();
        view.Extent.Height.ShouldBeGreaterThan(view.Viewport.Height);
        screen.ValidateContinuations();
    }

    /// <summary>Verifies responsive RichText receives the committed documentation-pane width instead of an unbounded horizontal measure.</summary>
    [Fact]
    public void Render_WhenDocumentationPaneIsNarrow_WrapsCompleteRichTextGuidance()
    {
        using Gallery gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "Button"));
        Size size = new(80, 40);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);

        gallery.Render(frame.Canvas);

        Screen screen = new(frame);
        screen.Text.ShouldContain("command paths.");
        gallery.Content.Parent.ShouldBeOfType<Stack>()
            .HorizontalBarVisibility.ShouldBe(ScrollBarVisibility.Hidden);
    }

    /// <summary>Verifies the Button page demonstrates both shadow modes and a stationary flat variant.</summary>
    [Fact]
    public void CreateExamples_WhenButtonPageIsSelected_ProvidesShadowAndFlatVariants()
    {
        using Gallery gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "Button"));
        new Engine().Layout(gallery, new Size(100, 60));

        List<Button> buttons = FindAll<Button>(gallery.Content);

        buttons.ShouldContain(button => button.ShadowMode == ShadowMode.Composite);
        buttons.ShouldContain(button => !button.HasShadow);
        buttons.ShouldContain(button =>
            button.ShadowMode == ShadowMode.BlockGlyph &&
            button.ShadowGlyph == new System.Text.Rune('░'));
    }

    /// <summary>Verifies the Canvas page pairs each positioning concept with its own live framed specimen.</summary>
    [Fact]
    public void Render_WhenCanvasPageIsSelected_ShowsGuidedPlacementExamples()
    {
        using Gallery gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "Canvas"));
        Size size = new(120, 80);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);

        gallery.Render(frame.Canvas);

        Screen screen = new(frame);
        Dock? edge = Find<Dock>(
            gallery.Content,
            static value => value.Children.Count == 1 &&
                value.Children[0] is ControlText { Content: "Right 2 / Bottom 1" });
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
        using Gallery gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "Window"));
        Size size = new(120, 80);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);

        gallery.Render(frame.Canvas);

        List<Window> windows = FindAll<Window>(gallery.Content);
        Screen screen = new(frame);
        screen.Text.ShouldContain("Apply");
        screen.Text.ShouldContain("Cancel");
        windows.Count.ShouldBeGreaterThanOrEqualTo(4);
        windows.ShouldContain(window =>
            window.Glyphs == Glyphs.Paired &&
            window.TitlePlacement == WindowTitlePlacement.Center);

        Window dialog = windows.Single(window => window.Title == "Project settings");
        dialog.Bounds.Height.ShouldBeGreaterThan(10);
        List<Button> actions = FindAll<Button>(dialog);
        actions.Count.ShouldBe(2);
        int left = actions.Min(button => button.Bounds.X);
        int right = actions.Max(button => button.Bounds.Right);
        int actionCenter = (left + right) / 2;
        int dialogCenter = dialog.Bounds.X + (dialog.Bounds.Width / 2);
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
        using Gallery gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "List"));
        Size size = new(120, 80);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);

        gallery.Render(frame.Canvas);

        List active = FindAll<List>(gallery.Content).Single(list => list.IsEnabled);
        active.Background.ShouldBe(Color.Indexed(0));
        frame.GetCell(new Point(active.Bounds.X, active.Bounds.Y + 1)).Style.Background
            .ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies the RichText page demonstrates every supported terminal text attribute.</summary>
    [Fact]
    public void CreateExamples_WhenRichTextPageIsSelected_ShowsTerminalAttributeRuns()
    {
        using Gallery gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "RichText"));
        new Engine().Layout(gallery, new Size(120, 80));

        List<Run> runs = [.. FindAll<RichText>(gallery.Content).SelectMany(static richText => richText.Inlines.OfType<Run>())];

        runs.ShouldContain(run => run.Attributes == Attributes.Bold);
        runs.ShouldContain(run => run.Attributes == Attributes.Dim);
        runs.ShouldContain(run => run.Attributes == Attributes.Italic);
        runs.ShouldContain(run => run.Attributes == Attributes.Underline);
        runs.ShouldContain(run => run.Attributes == Attributes.Blink);
        runs.ShouldContain(run => run.Attributes == Attributes.Reverse);
        runs.ShouldContain(run => run.Attributes == Attributes.Strike);
        runs.ShouldContain(run => run.Attributes == Attributes.Hidden);
        runs.ShouldContain(run => run.Attributes == Attributes.RapidBlink);
        runs.ShouldContain(run => run.Attributes == Attributes.Overline);
        runs.ShouldContain(run =>
            run.Underline == Underline.Curly &&
            run.UnderlineColor == Color.Indexed(11));
        runs.ShouldContain(run => run.Attributes == (Attributes.Bold | Attributes.Underline | Attributes.Italic));
    }

    /// <summary>Verifies the RichText action keeps its intrinsic content width instead of filling the document column.</summary>
    [Fact]
    public void Render_WhenRichTextActionIsLaidOut_UsesIntrinsicButtonWidth()
    {
        using Gallery gallery = CreateThemedGallery();
        gallery.Select(IndexOf(gallery, "RichText"));
        new Engine().Layout(gallery, new Size(120, 80));

        Button button = FindAll<Button>(gallery.Content)
            .Single(value => value.Content is ControlText { Content: "Append a Run" });

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

        foreach (Control child in container.Children)
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
            foreach (Control child in container.Children)
            {
                Visit(child, matches);
            }
        }
    }

    private static int IndexOf(Gallery gallery, string page)
    {
        ArgumentNullException.ThrowIfNull(gallery);
        ArgumentException.ThrowIfNullOrWhiteSpace(page);
        int index = gallery.Pages.Select(static value => value).ToList().IndexOf(page);
        return index >= 0 ? index : throw new InvalidOperationException($"The {page} page is not registered.");
    }

    /// <summary>Verifies every page renders safely at tiny, typical, and large terminal sizes.</summary>
    [Theory]
    [InlineData(30, 8)]
    [InlineData(80, 24)]
    [InlineData(140, 40)]
    public void Render_WhenEveryPageUsesViewport_PreservesSelectionAndValidCells(int width, int height)
    {
        using Gallery gallery = CreateThemedGallery();
        Size size = new(width, height);
        Engine engine = new();

        for (int index = 0; index < gallery.Pages.Count; index++)
        {
            gallery.Select(index);
            engine.Layout(gallery, size);
            using Frame frame = new(size);

            Should.NotThrow(() => gallery.Render(frame.Canvas));
            Screen screen = new(frame);
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
        Gallery gallery = new();
        ApplyTheme(gallery, Themes.Dark);
        return gallery;
    }

    private static void ApplyTheme(Control control, Theme theme)
    {
        ThemeContext context = ThemeContext.Create(theme);
        ApplyThemeContext(control, context);
    }

    private static void ApplyThemeContext(Control control, ThemeContext context)
    {
        control.SetThemeContext(context);
        control.VisitChildren(child => ApplyThemeContext(child, context));
    }
}
