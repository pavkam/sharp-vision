using SharpVision.Controls;

using Shouldly;

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies showcase registration, navigation, and public-control composition.</summary>
public sealed class GalleryTests
{
    /// <summary>Verifies the gallery starts with a sidebar and selected content page.</summary>
    [Fact]
    public void Constructor_WhenCreated_RegistersAllControlFamilies()
    {
        using var gallery = new Gallery();

        _ = gallery.Root.ShouldBeOfType<Dock>();
        gallery.Sidebar.Items.Count.ShouldBe(5);
        gallery.SelectedPage.ShouldBe("Borders & Shadows");
        _ = gallery.Content.ShouldNotBeNull();
    }

    /// <summary>Verifies programmatic sidebar selection swaps the main page.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_UpdatesSelectedPageAndContent()
    {
        using var gallery = new Gallery();
        var previous = gallery.Content;

        gallery.Sidebar.SelectedIndex = 1;

        gallery.SelectedPage.ShouldBe("Typography");
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
}
