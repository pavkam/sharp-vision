using SharpVision.Controls;

using Shouldly;

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies showcase registration, navigation, and public-control composition.</summary>
public sealed class GalleryTests
{
    private static readonly string[] _controls =
    [
        "Border",
        "Button",
        "Canvas",
        "CheckBox",
        "Dock",
        "FigletText",
        "Grid",
        "List",
        "Overlay",
        "RadioButton",
        "RichText",
        "ScrollBar",
        "ScrollView",
        "Shadow",
        "Stack",
        "Text",
        "TextInput",
    ];

    /// <summary>Verifies the gallery starts with one page per concrete shipped control.</summary>
    [Fact]
    public void Constructor_WhenCreated_RegistersEveryConcreteControl()
    {
        using var gallery = new Gallery();

        _ = gallery.Root.ShouldBeOfType<Dock>();
        gallery.Sidebar.Items.ShouldBe(_controls);
        gallery.SelectedPage.ShouldBe("Border");
        _ = gallery.Content.ShouldNotBeNull();
    }

    /// <summary>Verifies programmatic sidebar selection swaps the main page.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_UpdatesSelectedPageAndContent()
    {
        using var gallery = new Gallery();
        var previous = gallery.Content;

        gallery.Sidebar.SelectedIndex = 1;

        gallery.SelectedPage.ShouldBe("Button");
        gallery.Content.ShouldNotBeSameAs(previous);
    }

    /// <summary>Verifies every registered page includes typed RichText documentation.</summary>
    [Fact]
    public void CreatePage_WhenEachPageIsSelected_ContainsRichTextDescription()
    {
        using var gallery = new Gallery();

        for (var index = 0; index < gallery.Sidebar.Items.Count; index++)
        {
            gallery.Sidebar.SelectedIndex = index;

            ContainsRichText(gallery.Content).ShouldBeTrue(gallery.SelectedPage);
        }
    }

    /// <summary>Verifies every page creates fresh detached examples containing its named control.</summary>
    [Fact]
    public void CreateExamples_WhenEveryPageBuildsTwice_ReturnsFreshMatchingControlTrees()
    {
        using var gallery = new Gallery();

        foreach (var page in gallery.Pages)
        {
            using var first = page.CreateExamples();
            using var second = page.CreateExamples();

            first.ShouldNotBeSameAs(second);
            first.Parent.ShouldBeNull();
            second.Parent.ShouldBeNull();
            ContainsType(first, page.Name).ShouldBeTrue(page.Name);
            ContainsType(second, page.Name).ShouldBeTrue(page.Name);
        }
    }

    /// <summary>Verifies every page explains a useful set of meaningful public properties.</summary>
    [Fact]
    public void Properties_WhenCatalogLoads_DescribeMeaningfulControlAttributes()
    {
        using var gallery = new Gallery();

        foreach (var page in gallery.Pages)
        {
            page.Properties.Count.ShouldBeGreaterThanOrEqualTo(3, page.Name);

            foreach (var property in page.Properties)
            {
                property.Name.ShouldNotBeNullOrWhiteSpace(page.Name);
                property.Type.ShouldNotBeNullOrWhiteSpace(page.Name);
                property.Default.ShouldNotBeNullOrWhiteSpace(page.Name);
                property.Description.Length.ShouldBeGreaterThan(24, $"{page.Name}.{property.Name}");
            }
        }
    }

    /// <summary>Verifies every navigation entry builds a newly owned page with matching identity.</summary>
    [Fact]
    public void SelectedIndex_WhenEveryPageIsSelected_UpdatesSelectedEntryAndContent()
    {
        using var gallery = new Gallery();
        Control? previous = null;

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            gallery.Sidebar.SelectedIndex = index;

            gallery.Selected.ShouldBeSameAs(gallery.Pages[index]);
            gallery.SelectedPage.ShouldBe(_controls[index]);
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

        foreach (var child in container.Children)
        {
            if (ContainsRichText(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsType(Control control, string name)
    {
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
}
