using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Showcase.Panes;
using SharpVision.Terminal.Geometry;
using SharpVision.Text;

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
        using var gallery = new Gallery();

        _ = gallery.ShouldBeOfType<Gallery>();
        gallery.Children.Count.ShouldBe(1);
        _ = gallery.Children[0].ShouldBeOfType<Dock>();
        _ = gallery.Sidebar.ShouldBeOfType<Border>();
        gallery.Pages.Select(static page => page.Name).ShouldBe(_controls);
        gallery.SelectedPage.ShouldBe("Border");
        _ = gallery.Content.ShouldNotBeNull();
    }

    /// <summary>Verifies programmatic dashboard selection swaps the main page.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_UpdatesSelectedPageAndContent()
    {
        using var gallery = new Gallery();
        var previous = gallery.Content;

        gallery.Select(1);

        gallery.SelectedPage.ShouldBe("Button");
        gallery.Content.ShouldNotBeSameAs(previous);
    }

    /// <summary>Verifies changing components resets the retained documentation viewport to the new page header.</summary>
    [Fact]
    public void Select_WhenDocumentationWasScrolled_ResetsTheNewPageToTop()
    {
        using var gallery = new Gallery();
        new Engine().Layout(gallery, new Size(80, 24));
        var main = gallery.Content.Parent.ShouldBeOfType<ScrollView>();
        main.ScrollBy(0, int.MaxValue).ShouldBeTrue();

        gallery.Select(1);

        main.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies every registered page includes typed RichText documentation.</summary>
    [Fact]
    public void CreatePage_WhenEachPageIsSelected_ContainsRichTextDescription()
    {
        using var gallery = new Gallery();

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            gallery.Select(index);

            ContainsRichText(gallery.Content).ShouldBeTrue(gallery.SelectedPage);
        }
    }

    /// <summary>Verifies a newly created RichText document wraps words by default for responsive documentation.</summary>
    [Fact]
    public void Constructor_WhenRichTextIsCreated_UsesWordWrapping()
    {
        var document = new RichText();

        document.Wrapping.ShouldBe(Wrapping.Word);
    }

    /// <summary>Verifies every control page includes a wrapped bordered practical recipe alongside live examples.</summary>
    [Fact]
    public void CreatePage_WhenEachPageIsSelected_IncludesWrappedPracticalRecipe()
    {
        using var gallery = new Gallery();

        for (var index = 0; index < gallery.Pages.Count; index++)
        {
            var entry = gallery.Pages[index];
            gallery.Select(index);
            var recipe = FindRichText(gallery.Content, "Practical recipe");

            _ = recipe.ShouldNotBeNull(entry.Name);
            recipe.Wrapping.ShouldBe(Wrapping.Word, entry.Name);
        }
    }

    /// <summary>Verifies every page creates fresh detached panes containing its named control.</summary>
    [Fact]
    public void CreatePane_WhenEveryPageBuildsTwice_ReturnsFreshMatchingControlTrees()
    {
        using var gallery = new Gallery();

        foreach (var entry in gallery.Pages)
        {
            using var first = entry.CreatePane();
            using var second = entry.CreatePane();

            first.ShouldNotBeSameAs(second);
            first.Parent.ShouldBeNull();
            second.Parent.ShouldBeNull();
            ContainsType(first, entry.Name).ShouldBeTrue(entry.Name);
            ContainsType(second, entry.Name).ShouldBeTrue(entry.Name);
        }
    }

    /// <summary>Verifies every page explains a useful set of meaningful public properties.</summary>
    [Fact]
    public void Properties_WhenCatalogLoads_DescribeMeaningfulControlAttributes()
    {
        using var gallery = new Gallery();

        foreach (var entry in gallery.Pages)
        {
            using var pane = entry.CreatePane();
            pane.Properties.Count.ShouldBeGreaterThanOrEqualTo(3, entry.Name);

            foreach (var property in pane.Properties)
            {
                property.Name.ShouldNotBeNullOrWhiteSpace(entry.Name);
                property.Type.ShouldNotBeNullOrWhiteSpace(entry.Name);
                property.Default.ShouldNotBeNullOrWhiteSpace(entry.Name);
                property.Description.Length.ShouldBeGreaterThan(24, $"{entry.Name}.{property.Name}");
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
            gallery.Select(index);

            gallery.Selected.ShouldBe(gallery.Pages[index]);
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

        foreach (var child in container.Children)
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

        foreach (var child in container.Children)
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
