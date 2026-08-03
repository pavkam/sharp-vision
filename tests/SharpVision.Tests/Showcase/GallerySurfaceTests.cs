// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Showcase;

using SharpVision.Showcase;
using SharpVision.Showcase.Panes;

/// <summary>Proves the production Showcase shell renders after application theme publication.</summary>
public sealed class GallerySurfaceTests
{
    /// <summary>Verifies the Button showcase gives its composite shadow visible backdrop content to restyle.</summary>
    [Fact]
    public async Task ButtonPage_WhenCompositeShadowIsRendered_PreservesVisibleBackdropGlyphAsync()
    {
        // Arrange
        var page = new ButtonPane();
        await using var surface = await ComponentSurface.MountAsync(
            page,
            new Size(120, 160),
            TestContext.Current.CancellationToken);
        var composite = OwnedTree.FindAll<Button>(page).Single(static button =>
            button.Text == "Com&posite: parent pattern");

        // Act
        var shadowCells = Enumerable
            .Range(composite.Bounds.X + 1, composite.Bounds.Width)
            .Select(x => surface.Cell(new Point(x, composite.Bounds.Bottom)))
            .Concat(Enumerable
                .Range(composite.Bounds.Y + 1, composite.Bounds.Height)
                .Select(y => surface.Cell(new Point(composite.Bounds.Right, y))))
            .ToArray();
        var visibleShadow = shadowCells.Any(static cell =>
            cell.Text == "·" && cell.Style.Attributes == Attributes.Dim);

        // Assert
        visibleShadow.ShouldBeTrue();
    }

    /// <summary>Verifies MessageBox is presented with the other dialog examples.</summary>
    [Fact]
    public void Navigation_WhenCatalogIsBuilt_GroupsMessageBoxUnderDialogs()
    {
        // Arrange
        var gallery = new Gallery();

        // Act
        var messageBox = gallery.Navigation.Single(static item => item.Text == MessageBoxPane.Title);

        // Assert
        var itemHost = messageBox.Parent.ShouldNotBeNull();
        var group = itemHost.Parent.ShouldBeOfType<NavigationViewGroup>();
        group.Header.ShouldBe("Dialogs");
    }

    /// <summary>Verifies startup renders the theme picker and global action instead of a blank themed frame.</summary>
    [Fact]
    public async Task Render_WhenApplicationStarts_ShowsTheGlobalThemeAndQuitControlsAsync()
    {
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(120, 40),
            TestContext.Current.CancellationToken);

        var text = new StringBuilder();

        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < 120; x++)
            {
                _ = text.Append(surface.Cell(new Point(x, y)).Text);
            }

            _ = text.AppendLine();
        }

        text.ToString().ShouldContain("Dark");
        text.ToString().ShouldContain("Quit  Ctrl+Q");
    }

    /// <summary>Verifies the MessageBox example launches into the application plane without nested specimen surfaces.</summary>
    [Fact]
    public async Task MessageBoxPage_WhenLauncherIsInvoked_UsesApplicationPresentationWithoutExampleSurfacesAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(120, 40),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == MessageBoxPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open MessageBox showcase page");
        var page = gallery.CurrentPage;
        var launcher = OwnedTree.FindAll<Button>(page).Single(static button =>
            button.Text == "&OK");

        // Assert simplified example composition
        OwnedTree.FindAll<Overlay>(page).ShouldBeEmpty();
        OwnedTree.FindAll<GroupBox>(page).ShouldBeEmpty();

        // Act
        await surface.Pointer.ClickAsync(launcher);

        // Assert application-wide presentation
        var messageBox = OwnedTree.Find<MessageBox>(gallery).ShouldNotBeNull();
        messageBox.Header.ShouldBe("MessageBox");
        messageBox.Bounds.X.ShouldBe((120 - messageBox.Bounds.Width) / 2);
        messageBox.Bounds.Y.ShouldBe((40 - messageBox.Bounds.Height) / 2);

        // Cleanup
        await surface.Keyboard.PressAsync(Code.Escape);
    }

    /// <summary>
    /// Verifies every production catalog page mounts, lays out, and renders
    /// without throwing at three representative sizes. Gallery.CreatePage is a
    /// long-standing internal seam with no callers before this test; only 3 of
    /// the catalog's pages were ever constructed by the test suite, so a
    /// broken constructor or layout failure in any of the others would not
    /// have failed CI.
    /// </summary>
    /// <param name="index">The catalog page index under test.</param>
    [Theory]
    [MemberData(nameof(CatalogIndices))]
    public async Task CreatePage_WhenMountedAtSupportedSize_LaysOutAndRendersWithoutThrowingAsync(int index)
    {
        var name = Gallery.CatalogName(index);

        foreach (var size in new[] { new Size(30, 8), new Size(80, 24), new Size(140, 40) })
        {
            var page = Gallery.CreatePage(index);

            try
            {
                await using var surface = await ComponentSurface.MountAsync(
                    page, size, TestContext.Current.CancellationToken);

                page.Bounds.Width.ShouldBeLessThanOrEqualTo(size.Width);
                page.IsDisposed.ShouldBeFalse();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Catalog page '{name}' (index {index}) failed to mount at {size.Width}x{size.Height}.",
                    exception);
            }
        }
    }

    /// <summary>Verifies every catalog page has a unique name and a defined sidebar group.</summary>
    [Fact]
    public void Catalog_WhenEnumerated_HasUniqueNamesAndKnownGroups()
    {
        var names = new List<string>();
        var knownGroups = new HashSet<string>(Gallery.CatalogGroups, StringComparer.Ordinal);

        for (var index = 0; index < Gallery.CatalogCount; index++)
        {
            names.Add(Gallery.CatalogName(index));
            knownGroups.ShouldContain(Gallery.CatalogGroup(index));
        }

        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(names.Count);
    }

    /// <summary>Provides every catalog page index for the mounting theory.</summary>
    public static IEnumerable<object[]> CatalogIndices() =>
        Enumerable.Range(0, Gallery.CatalogCount).Select(static index => new object[] { index });
}
