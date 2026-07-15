// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;


/// <summary>Verifies showcase registration, navigation, and public-control composition.</summary>
public sealed class GalleryTests
{
    private static readonly string[] _controls =
    [
        "Button",
        "Canvas",
        "CheckBox",
        "ComboBox",
        "Dock",
        "FigletText",
        "Grid",
        "List",
        "Menu",
        "Overlay",
        "Popup",
        "RadioButton",
        "ScrollBar",
        "Stack",
        "Table",
        "Text",
        "TextInput",
        "Window",
        "Theming",
    ];

    /// <summary>Verifies the gallery starts with one page per concrete shipped control.</summary>
    [Fact]
    public void Constructor_WhenCreated_RegistersEveryConcreteControl()
    {
        using Gallery gallery = new();

        _ = gallery.ShouldBeOfType<Gallery>();
        _ = gallery.Sidebar.ShouldBeOfType<Dock>();
        gallery.Pages.ShouldBe(_controls);
        gallery.SelectedPage.ShouldBe("Button");
        _ = gallery.Content.ShouldNotBeNull();
    }

    /// <summary>Verifies programmatic dashboard selection swaps the main page.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_UpdatesSelectedPageAndContent()
    {
        using Gallery gallery = new();
        var previous = gallery.Content;

        gallery.Select(1);

        gallery.SelectedPage.ShouldBe("Canvas");
        gallery.Content.ShouldNotBeSameAs(previous);
    }

    /// <summary>Verifies changing components resets the retained documentation viewport to the new page header.</summary>
    [Fact]
    public void Select_WhenDocumentationWasScrolled_ResetsTheNewPageToTop()
    {
        using Gallery gallery = new();
        new Engine().Layout(gallery, new Size(80, 24));
        var previousBody = FindScrollableBody(gallery.Content).ShouldNotBeNull();
        previousBody.ScrollBy(0, int.MaxValue).ShouldBeTrue();

        gallery.Select(1);
        new Engine().Layout(gallery, new Size(80, 24));
        var currentBody = FindScrollableBody(gallery.Content).ShouldNotBeNull();

        currentBody.ShouldNotBeSameAs(previousBody);
        currentBody.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies every registered page includes responsive marked Text documentation.</summary>
    [Fact]
    public void CreatePage_WhenEachPageIsSelected_ContainsWrappedTextDescription()
    {
        using Gallery gallery = new();

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            gallery.Select(index);
            new Engine().Layout(gallery, new Size(80, 24));

            _ = FindText(gallery.Content, "<b>Overview</b>").ShouldNotBeNull(gallery.SelectedPage);
        }
    }

    /// <summary>Verifies showcase documentation opts into wrapping despite Text preserving visible overflow by default.</summary>
    [Fact]
    public void CreatePage_WhenDocumentationIsBuilt_UsesWrappedText()
    {
        using var page = Gallery.CreatePage(0);
        new Engine().Layout(page, new Size(80, 24));
        var document = FindText(page, "<b>Overview</b>").ShouldNotBeNull();

        document.Overflow.ShouldBe(Overflow.Wrap);
    }

    /// <summary>Verifies scrolling examples leaves the selected page identity pinned above the viewport.</summary>
    [Fact]
    public void CreatePage_WhenBodyScrolls_KeepsPageHeaderFixed()
    {
        using var page = Gallery.CreatePage(0);
        var engine = new Engine();
        var size = new Size(52, 12);
        engine.Layout(page, size);
        var header = FindText(page, "<b>Button</b>").ShouldNotBeNull();
        var firstSection = FindText(page, "Start here").ShouldNotBeNull();
        var body = FindScrollableBody(page).ShouldNotBeNull();
        var headerBefore = header.Bounds;
        var sectionBefore = firstSection.Bounds;

        body.ScrollBy(0, int.MaxValue).ShouldBeTrue();
        engine.Layout(page, size);

        header.Bounds.ShouldBe(headerBefore);
        firstSection.Bounds.Y.ShouldBeLessThan(sectionBefore.Y);
        body.HorizontalBarVisibility.ShouldBe(ScrollBarVisibility.Hidden);
    }

    /// <summary>Verifies pages that still supply a practical recipe wrap it, now that documentation prose is
    /// optional data a pane may provide rather than a mandatory chrome section (View-based pages, such
    /// as the Doc.Page-composed Button page, no longer include this heading at all).</summary>
    [Fact]
    public void CreatePage_WhenEachPageIsSelected_IncludesWrappedPracticalRecipe()
    {
        using Gallery gallery = new();

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            var name = gallery.Pages[index];
            gallery.Select(index);
            new Engine().Layout(gallery, new Size(80, 24));
            var recipe = FindText(gallery.Content, "Practical recipe");

            if (recipe is { } found)
            {
                found.Overflow.ShouldBe(Overflow.Wrap, name);
            }
        }
    }

    /// <summary>Verifies every page creates fresh detached panes containing its named control.</summary>
    [Fact]
    public void CreatePage_WhenEveryPageBuildsTwice_ReturnsFreshMatchingControlTrees()
    {
        using Gallery gallery = new();

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            var name = gallery.Pages[index];
            using var first = Gallery.CreatePage(index);
            using var second = Gallery.CreatePage(index);
            var engine = new Engine();
            engine.Layout(first, new Size(80, 24));
            engine.Layout(second, new Size(80, 24));

            first.ShouldNotBeSameAs(second);
            first.Parent.ShouldBeNull();
            second.Parent.ShouldBeNull();
            ContainsType(first, name).ShouldBeTrue(name);
            ContainsType(second, name).ShouldBeTrue(name);
        }
    }

    /// <summary>Verifies every navigation entry builds a newly owned page with matching identity.</summary>
    [Fact]
    public void SelectedIndex_WhenEveryPageIsSelected_UpdatesSelectedEntryAndContent()
    {
        using Gallery gallery = new();
        Control? previous = null;

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            gallery.Select(index);

            gallery.SelectedPage.ShouldBe(_controls[index]);
            gallery.SelectedIndex.ShouldBe(index);
            gallery.Content.ShouldNotBeSameAs(previous);
            previous = gallery.Content;
        }
    }

    private static ControlText? FindText(Control control, string content)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        if (control is ControlText text &&
            text.Content.Contains(content, StringComparison.Ordinal))
        {
            return text;
        }

        if (control is not Container container)
        {
            return null;
        }

        foreach (var child in container.Children)
        {
            if (FindText(child, content) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static bool ContainsType(Control control, string name)
    {
        if (string.Equals(name, "Theming", StringComparison.Ordinal) && control is ShowcasePanel)
        {
            return true;
        }

        if (string.Equals(control.GetType().Name, name, StringComparison.Ordinal))
        {
            return true;
        }

        if (control is not Container container)
        {
            return false;
        }

        foreach (var child in container.Children)
        {
            if (ContainsType(child, name))
            {
                return true;
            }
        }

        return false;
    }

    private static Stack? FindScrollableBody(Control control)
    {
        if (control is Stack { AutoScroll: true } stack)
        {
            return stack;
        }

        if (control is not Container container)
        {
            return null;
        }

        foreach (var child in container.Children)
        {
            if (FindScrollableBody(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
