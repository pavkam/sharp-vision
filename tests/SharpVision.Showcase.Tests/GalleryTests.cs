// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using SharpVision.Showcase.Panes;
using SharpVision.Text;

/// <summary>Verifies showcase registration, navigation, and public-control composition.</summary>
public sealed class GalleryTests
{
    private static readonly string[] _controls =
    [
        "Border",
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
        "RichText",
        "ScrollBar",
        "ScrollView",
        "Shadow",
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
        gallery.Children.Count.ShouldBe(1);
        _ = gallery.Children[0].ShouldBeOfType<Dock>();
        _ = gallery.Sidebar.ShouldBeOfType<Border>();
        gallery.Pages.ShouldBe(_controls);
        gallery.SelectedPage.ShouldBe("Border");
        _ = gallery.Content.ShouldNotBeNull();
    }

    /// <summary>Verifies programmatic dashboard selection swaps the main page.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_UpdatesSelectedPageAndContent()
    {
        using Gallery gallery = new();
        Control previous = gallery.Content;

        gallery.Select(1);

        gallery.SelectedPage.ShouldBe("Button");
        gallery.Content.ShouldNotBeSameAs(previous);
    }

    /// <summary>Verifies changing components resets the retained documentation viewport to the new page header.</summary>
    [Fact]
    public void Select_WhenDocumentationWasScrolled_ResetsTheNewPageToTop()
    {
        using Gallery gallery = new();
        new Engine().Layout(gallery, new Size(80, 24));
        ScrollView main = gallery.Content.Parent.ShouldBeOfType<ScrollView>();
        main.ScrollBy(0, int.MaxValue).ShouldBeTrue();

        gallery.Select(1);

        main.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies every registered page includes typed RichText documentation.</summary>
    [Fact]
    public void CreatePage_WhenEachPageIsSelected_ContainsRichTextDescription()
    {
        using Gallery gallery = new();

        for (int index = 0; index < gallery.Pages.Count; index++)
        {
            gallery.Select(index);

            ContainsRichText(gallery.Content).ShouldBeTrue(gallery.SelectedPage);
        }
    }

    /// <summary>Verifies a newly created RichText document wraps words by default for responsive documentation.</summary>
    [Fact]
    public void Constructor_WhenRichTextIsCreated_UsesWordWrapping()
    {
        RichText document = new();

        document.Wrapping.ShouldBe(Wrapping.Word);
    }

    /// <summary>Verifies every control page includes a wrapped bordered practical recipe alongside live examples.</summary>
    [Fact]
    public void CreatePage_WhenEachPageIsSelected_IncludesWrappedPracticalRecipe()
    {
        using Gallery gallery = new();

        for (int index = 0; index < gallery.Pages.Count; index++)
        {
            string name = gallery.Pages[index];
            gallery.Select(index);
            RichText? recipe = FindRichText(gallery.Content, "Practical recipe");

            _ = recipe.ShouldNotBeNull(name);
            recipe.Wrapping.ShouldBe(Wrapping.Word, name);
        }
    }

    /// <summary>Verifies every page creates fresh detached panes containing its named control.</summary>
    [Fact]
    public void CreatePage_WhenEveryPageBuildsTwice_ReturnsFreshMatchingControlTrees()
    {
        using Gallery gallery = new();

        for (int index = 0; index < gallery.Pages.Count; index++)
        {
            string name = gallery.Pages[index];
            using ShowcasePane first = Gallery.CreatePage(index);
            using ShowcasePane second = Gallery.CreatePage(index);

            first.ShouldNotBeSameAs(second);
            first.Parent.ShouldBeNull();
            second.Parent.ShouldBeNull();
            ContainsType(first, name).ShouldBeTrue(name);
            ContainsType(second, name).ShouldBeTrue(name);
        }
    }

    /// <summary>Verifies every page explains a useful set of meaningful public properties.</summary>
    [Fact]
    public void Properties_WhenCatalogLoads_DescribeMeaningfulControlAttributes()
    {
        using Gallery gallery = new();

        for (int index = 0; index < gallery.Pages.Count; index++)
        {
            string name = gallery.Pages[index];
            using ShowcasePane pane = Gallery.CreatePage(index);
            pane.Properties.Count.ShouldBeGreaterThanOrEqualTo(3, name);

            foreach (PropertyDescription property in pane.Properties)
            {
                property.Name.ShouldNotBeNullOrWhiteSpace(name);
                property.Type.ShouldNotBeNullOrWhiteSpace(name);
                property.Default.ShouldNotBeNullOrWhiteSpace(name);
                property.Description.Length.ShouldBeGreaterThan(24, $"{name}.{property.Name}");
            }
        }
    }

    /// <summary>Verifies every navigation entry builds a newly owned page with matching identity.</summary>
    [Fact]
    public void SelectedIndex_WhenEveryPageIsSelected_UpdatesSelectedEntryAndContent()
    {
        using Gallery gallery = new();
        Control? previous = null;

        for (int index = 0; index < gallery.Pages.Count; index++)
        {
            gallery.Select(index);

            gallery.SelectedPage.ShouldBe(_controls[index]);
            gallery.SelectedIndex.ShouldBe(index);
            gallery.Content.ShouldNotBeSameAs(previous);
            previous = gallery.Content;
        }
    }

    private static bool ContainsRichText(Control control)
    {
        if (control is RichText)
        {
            return true;
        }

        if (control is not Container container)
        {
            return false;
        }

        foreach (Control child in container.Children)
        {
            if (ContainsRichText(child))
            {
                return true;
            }
        }

        return false;
    }

    private static RichText? FindRichText(Control control, string content)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        if (control is RichText richText && richText.Inlines.OfType<Run>().Any(inline =>
                string.Equals(inline.Content, content, StringComparison.Ordinal)))
        {
            return richText;
        }

        if (control is not Container container)
        {
            return null;
        }

        foreach (Control child in container.Children)
        {
            if (FindRichText(child, content) is { } found)
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

        foreach (Control child in container.Children)
        {
            if (ContainsType(child, name))
            {
                return true;
            }
        }

        return false;
    }
}
