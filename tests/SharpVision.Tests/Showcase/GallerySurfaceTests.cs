// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Showcase;

using System.Text.Json;

using SharpVision.Showcase;
using SharpVision.Showcase.Controls;
using SharpVision.Showcase.Panes;

/// <summary>Proves the production Showcase shell renders after application theme publication.</summary>
public sealed class GallerySurfaceTests
{
    /// <summary>Verifies the JsonView page demonstrates deep structure, long horizontal content, and a generous viewport.</summary>
    [Fact]
    public void JsonViewPage_WhenCreated_ShowsDeepDocumentAndLongValue()
    {
        // Arrange
        var page = new JsonViewPane();

        // Act
        var jsonView = OwnedTree.Find<JsonView>(page).ShouldNotBeNull();
        var example = OwnedTree.Find<DocExample>(page).ShouldNotBeNull();
        using var document = JsonDocument.Parse(jsonView.Json);
        var root = document.RootElement;
        var runtime = root.GetProperty("release").GetProperty("runtime");

        // Assert
        jsonView.Height.ShouldBe(Length.Cells(20));
        jsonView.Width.ShouldBe(Length.Auto);
        example.Width.ShouldBe(Length.Percent(50));
        root.GetProperty("description").GetString().ShouldNotBeNull().Length.ShouldBeGreaterThan(120);
        runtime.GetProperty("capabilities").GetProperty("colorDepth").GetInt32().ShouldBe(24);
        root.GetProperty("contributors")[0].GetProperty("roles").GetArrayLength().ShouldBeGreaterThan(1);
    }

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
        var blockShadow = OwnedTree.FindAll<Button>(page).Single(static button =>
            button.Text == "&Block: independent glyph");

        // Act
        var shadowCells = Enumerable
            .Range(composite.Bounds.X + 1, composite.Bounds.Width)
            .Select(x => surface.Cell(new Point(x, composite.Bounds.Bottom)))
            .Concat(Enumerable
                .Range(composite.Bounds.Y + 1, composite.Bounds.Height)
                .Select(y => surface.Cell(new Point(composite.Bounds.Right, y))))
            .ToArray();
        var visibleShadow = shadowCells.Any(static cell =>
            cell.Text == "·" && cell.Style.Attributes == TerminalAttributes.Dim);

        // composite.Bounds.Right/Bottom above fall in the DocColumn's outer margin, which the
        // private inner Stack never painted even before the fix. The one-row spacing gap between
        // the "Filled/Composite" and "Block/Flat" DocRows sits strictly inside the demo-row block
        // instead, so it only shows the dotted backdrop when the wrapping DocRow's Face is
        // actually forwarded to the Stack that renders it.
        var elevatedRow = (composite.Parent?.Parent as DocRow).ShouldNotBeNull();
        var depthRow = (blockShadow.Parent?.Parent as DocRow).ShouldNotBeNull();
        elevatedRow.Bounds.Bottom.ShouldBeLessThan(depthRow.Bounds.Y);
        var gapCells = Enumerable
            .Range(elevatedRow.Bounds.X, elevatedRow.Bounds.Width)
            .Select(x => surface.Cell(new Point(x, elevatedRow.Bounds.Bottom)))
            .ToArray();
        var preservedBackdropInGap = gapCells.Any(static cell => cell.Text == "·");

        // Assert
        visibleShadow.ShouldBeTrue();
        preservedBackdropInGap.ShouldBeTrue();
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

    /// <summary>Verifies JsonView is presented with the hierarchical collection examples.</summary>
    [Fact]
    public void Navigation_WhenCatalogIsBuilt_GroupsJsonViewUnderCollections()
    {
        // Arrange
        var gallery = new Gallery();

        // Act
        var jsonView = gallery.Navigation.Single(static item => item.Text == JsonViewPane.Title);

        // Assert
        var itemHost = jsonView.Parent.ShouldNotBeNull();
        var group = itemHost.Parent.ShouldBeOfType<NavigationViewGroup>();
        group.Header.ShouldBe("Collections");
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

    /// <summary>Verifies the product identity uses Classy without clipping at compact and normal widths.</summary>
    [Theory]
    [InlineData(80, 24, "SV", Ambiguous.Narrow)]
    [InlineData(120, 40, "SharpVision", Ambiguous.Narrow)]
    [InlineData(120, 40, "SV", Ambiguous.Wide)]
    public async Task Render_WhenApplicationStarts_UsesResponsiveClassyProductIdentityWithoutClippingAsync(
        int width,
        int height,
        string expectedContent,
        Ambiguous ambiguousWidth)
    {
        // Arrange
        var gallery = new Gallery();

        // Act
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(width, height),
            TerminalOptions.Minimal with
            {
                Capabilities = TerminalCapabilities.Conservative with { AmbiguousWidth = ambiguousWidth }
            },
            TestContext.Current.CancellationToken);
        var title = OwnedTree.Find<FigletText>(gallery.ApplicationBar).ShouldNotBeNull();

        // Assert
        title.Font.Name.ShouldBe("Classy");
        title.Content.ShouldBe(expectedContent);
        gallery.ApplicationBar.Padding.Vertical.ShouldBe(0);
        gallery.ApplicationBar.Bounds.Height.ShouldBe(title.DesiredSize.Height);
        title.DesiredSize.Width.ShouldBeLessThanOrEqualTo(title.Bounds.Width);
        title.DesiredSize.Height.ShouldBeLessThanOrEqualTo(title.Bounds.Height);
        Enumerable.Range(title.Bounds.Y, title.Bounds.Height)
            .SelectMany(y => Enumerable.Range(title.Bounds.X, title.Bounds.Width)
                .Select(x => surface.Cell(new Point(x, y)).Text))
            .ShouldContain("▄");
    }

    /// <summary>Verifies resizing switches the Classy identity without leaving the compact copy behind.</summary>
    [Fact]
    public async Task Render_WhenViewportResizes_SwitchesBetweenCompactAndFullClassyIdentityAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(120, 40),
            TestContext.Current.CancellationToken);
        var title = OwnedTree.Find<FigletText>(gallery.ApplicationBar).ShouldNotBeNull();
        title.Content.ShouldBe("SharpVision");

        // Act and assert compact layout
        await surface.ResizeAsync(new Size(80, 24));
        title.Content.ShouldBe("SV");
        title.DesiredSize.Width.ShouldBeLessThanOrEqualTo(title.Bounds.Width);

        // Act and assert normal layout restoration
        await surface.ResizeAsync(new Size(120, 40));
        title.Content.ShouldBe("SharpVision");
        title.DesiredSize.Width.ShouldBeLessThanOrEqualTo(title.Bounds.Width);
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

    /// <summary>Verifies the file-dialog example does not retain a decorative surface behind its application-wide Window.</summary>
    [Fact]
    public async Task FilePickerPage_WhenLauncherIsInvoked_UsesApplicationPresentationWithoutExampleSurfacesAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(120, 40),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == FilePickerPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open FilePicker showcase page");
        var page = gallery.CurrentPage;
        var launcher = OwnedTree.FindAll<Button>(page).Single(static button =>
            button.Text == "&Open one file");

        // Assert simplified example composition
        OwnedTree.FindAll<Overlay>(page).ShouldBeEmpty();

        // Act
        await surface.Pointer.ClickAsync(launcher);

        // Assert application-wide presentation
        var picker = OwnedTree.Find<FilePickerDialog>(gallery).ShouldNotBeNull();
        picker.Header.ShouldBe("Open a file");
        picker.Parent.ShouldNotBeSameAs(page);

        // Cleanup
        await surface.Keyboard.PressAsync(Code.Escape);
    }

    /// <summary>Verifies quitting from the grouped FilePicker navigation entry disposes the
    /// complete showcase tree without retaining a disposed current or selected item.</summary>
    [Fact]
    public async Task Quit_WhenFilePickerPageIsSelected_StopsWithoutCleanupFailureAsync()
    {
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(120, 40),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == FilePickerPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "select FilePicker before quit");

        await Should.NotThrowAsync(async () =>
            await surface.Application.StopAsync(TestContext.Current.CancellationToken));

        surface.Application.Failure.ShouldBeNull();
        gallery.Disposed.ShouldBeTrue();
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
                page.Disposed.ShouldBeFalse();
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

    /// <summary>Verifies the dedicated Charts category exposes every shipped chart control.</summary>
    [Fact]
    public void Catalog_WhenChartsAreEnumerated_ContainsCompleteChartFamily()
    {
        // Arrange
        var chartNames = Enumerable.Range(0, Gallery.CatalogCount)
            .Where(static index => Gallery.CatalogGroup(index) == "Charts")
            .Select(static index => Gallery.CatalogName(index))
            .ToArray();

        // Act and assert
        chartNames.ShouldBe([
            "HorizontalBarChart",
            "VerticalBarChart",
            "LineChart",
            "AreaChart",
            "Sparkline"
        ]);
    }

    /// <summary>Verifies every chart page presents multiple recipe-backed examples and a consistent live action.</summary>
    [Fact]
    public void ChartPages_WhenCreated_UseConsistentExamplesRecipesAndActions()
    {
        CompositeControlBase[] pages = [
            new HorizontalBarChartPane(),
            new VerticalBarChartPane(),
            new LineChartPane(),
            new AreaChartPane(),
            new SparklinePane()
        ];

        foreach (var page in pages)
        {
            var examples = OwnedTree.FindAll<DocExample>(page);
            var recipes = OwnedTree.FindAll<Expander>(page)
                .Count(static expander => expander.HeaderText == "C# recipe");

            examples.Count.ShouldBeInRange(2, 3);
            recipes.ShouldBe(examples.Count);
            OwnedTree.FindAll<ChartActionRow>(page).Count.ShouldBeGreaterThanOrEqualTo(1);
        }
    }

    /// <summary>Verifies Sparkline examples identify each externally styled series.</summary>
    [Fact]
    public void SparklinePage_WhenCreated_ProvidesExternalSeriesKeys()
    {
        // Arrange
        var page = new SparklinePane();

        // Act
        var labels = OwnedTree.FindAll<ControlText>(page)
            .Select(static text => text.Content)
            .ToArray();

        // Assert
        labels.ShouldContain("<accent>■</accent> Load");
        labels.ShouldContain("<warning>■</warning> Availability");
    }

    /// <summary>Verifies the shared chart action row aligns status text with the button caption.</summary>
    [Fact]
    public async Task ChartActionRow_WhenMounted_AlignsStatusWithButtonContentAsync()
    {
        var button = new Button { Text = "&Add data" };
        var status = new ControlText("Ready");
        var row = new ChartActionRow(button, status);

        await using var surface = await ComponentSurface.MountAsync(
            row,
            new Size(30, 3),
            TestContext.Current.CancellationToken);

        status.Bounds.Y.ShouldBe(button.Bounds.Y + 1);
        status.Bounds.X.ShouldBeGreaterThan(button.Bounds.Right);
    }

    /// <summary>Provides every catalog page index for the mounting theory.</summary>
    public static IEnumerable<object[]> CatalogIndices() =>
        Enumerable.Range(0, Gallery.CatalogCount).Select(static index => new object[] { index });
}
