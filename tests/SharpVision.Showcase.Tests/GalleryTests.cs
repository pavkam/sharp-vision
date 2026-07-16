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
        "Expander",
        "FigletText",
        "Grid",
        "GroupBox",
        "List",
        "Menu",
        "NavigationView",
        "Overlay",
        "Popup",
        "Prism",
        "ProgressBar",
        "RadioButton",
        "ScrollBar",
        "Separator",
        "Stack",
        "TabControl",
        "Table",
        "Text",
        "TextInput",
        "Window",
        "Theming",
    ];

    /// <summary>Verifies the gallery starts with the exact documented page catalog.</summary>
    [Fact]
    public void Constructor_WhenCreated_RegistersDocumentedPageCatalog()
    {
        using Gallery gallery = new();

        _ = gallery.ShouldBeOfType<Gallery>();
        _ = gallery.Sidebar.ShouldBeOfType<Dock>();
        gallery.Pages.ShouldBe(_controls);
        gallery.SelectedPage.ShouldBe("Button");
        _ = gallery.CurrentPage.ShouldNotBeNull();
    }

    /// <summary>Verifies programmatic dashboard selection swaps the main page.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_UpdatesSelectedPageAndContent()
    {
        using Gallery gallery = new();
        var previous = gallery.CurrentPage;

        gallery.Select(1);

        gallery.SelectedPage.ShouldBe("Canvas");
        gallery.CurrentPage.ShouldNotBeSameAs(previous);
    }

    /// <summary>Verifies changing components resets the retained documentation viewport to the new page header.</summary>
    [Fact]
    public void Select_WhenDocumentationWasScrolled_ResetsTheNewPageToTop()
    {
        using Gallery gallery = new();
        new Engine().Layout(gallery, new Size(80, 24));
        var main = gallery.CurrentPage.Parent.ShouldBeOfType<Stack>();
        main.ScrollBy(0, int.MaxValue).ShouldBeTrue();

        gallery.Select(1);

        main.VerticalOffset.ShouldBe(0);
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

            _ = FindText(gallery.CurrentPage, $"<accent><b>{gallery.SelectedPage}</b></accent>").ShouldNotBeNull(gallery.SelectedPage);
        }
    }

    /// <summary>Verifies showcase documentation opts into wrapping despite Text preserving visible overflow by default.</summary>
    [Fact]
    public void CreatePage_WhenDocumentationIsBuilt_UsesWrappedText()
    {
        using var page = Gallery.CreatePage(0);
        new Engine().Layout(page, new Size(80, 24));
        var document = FindText(page, "<accent><b>Button</b></accent>").ShouldNotBeNull();

        document.Overflow.ShouldBe(Overflow.Wrap);
    }

    /// <summary>Verifies pages that still supply a practical recipe wrap it, now that documentation prose is
    /// optional data a pane may provide rather than a mandatory chrome section (retained composite pages,
    /// such as the Doc.Page-composed Button page, no longer include this heading at all).</summary>
    [Fact]
    public void CreatePage_WhenEachPageIsSelected_IncludesWrappedPracticalRecipe()
    {
        using Gallery gallery = new();

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            var name = gallery.Pages[index];
            gallery.Select(index);
            new Engine().Layout(gallery, new Size(80, 24));
            var recipe = FindText(gallery.CurrentPage, "Practical recipe");

            if (recipe is { } found)
            {
                found.Overflow.ShouldBe(Overflow.Wrap, name);
            }
        }
    }

    /// <summary>Verifies every page creates fresh detached panes containing its named control.</summary>
    [Fact]
    public void CreatePage_WhenEveryPageIsCreatedTwice_ReturnsFreshRetainedCompositeTrees()
    {
        using Gallery gallery = new();

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            var name = gallery.Pages[index];
            using var first = Gallery.CreatePage(index);
            using var second = Gallery.CreatePage(index);
            var firstRoot = first.OwnedControlAt(0);
            var secondRoot = second.OwnedControlAt(0);
            var engine = new Engine();
            engine.Layout(first, new Size(80, 24));
            engine.Layout(second, new Size(80, 24));

            first.ShouldNotBeSameAs(second);
            typeof(Container).IsAssignableFrom(first.GetType()).ShouldBeFalse(name);
            first.OwnedControlCount.ShouldBe(1, name);
            second.OwnedControlCount.ShouldBe(1, name);
            first.OwnedControlAt(0).ShouldBeSameAs(firstRoot, name);
            second.OwnedControlAt(0).ShouldBeSameAs(secondRoot, name);
            (first.Pending & Invalidation.Measure).ShouldBe(Invalidation.None, name);
            (second.Pending & Invalidation.Measure).ShouldBe(Invalidation.None, name);
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
            gallery.CurrentPage.ShouldNotBeSameAs(previous);
            previous = gallery.CurrentPage;
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

        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            if (FindText(control.OwnedControlAt(index), content) is { } found)
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

        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            if (ContainsType(control.OwnedControlAt(index), name))
            {
                return true;
            }
        }

        return false;
    }
}
