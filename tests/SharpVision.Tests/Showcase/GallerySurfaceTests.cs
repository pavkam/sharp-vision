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
            button.Text == "Com&posite: parent");
        var blockShadow = OwnedTree.FindAll<Button>(page).Single(static button =>
            button.Text == "&Block: glyph");

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
        gallery.IsDisposed.ShouldBeTrue();
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

    /// <summary>
    /// Verifies the Window page's drag stage is never forced narrower than it asks to be at a
    /// compact 80x24 terminal, and that its interactive windows stay fully inside it. The
    /// generic mounted-size theory above only asserts the outer page fits the viewport; it
    /// cannot catch an inner Overlay declared wider than its GroupBox/sidebar-narrowed content
    /// column. A too-wide Overlay does not overflow visibly - Arrange silently clamps its Bounds
    /// down to whatever the column offers - so the only observable symptom is that clamp firing
    /// at all: the arranged width falls short of the requested one. DocPage's Hidden horizontal
    /// scrollbar then means whatever got clamped away has no scroll path back into view.
    /// </summary>
    [Fact]
    public async Task WindowPage_WhenMountedAtCompactSize_DragStageIsNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == WindowPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Window showcase page");
        var page = gallery.CurrentPage;

        // Act: the drag stage is the only Overlay hosting both interactive windows at once.
        var dragStage = OwnedTree.FindAll<Overlay>(page)
            .Single(static overlay => overlay.Children.OfType<Window>().Count() == 2);
        var dragStageWindows = dragStage.Children.OfType<Window>().ToArray();

        // Assert: the requested cell width was actually honored, not silently squeezed down by
        // the sidebar-narrowed content column - a squeeze is exactly what an unreachable,
        // non-scrollable overflow looks like from the arranged tree.
        dragStage.Width.Kind.ShouldBe(LengthKind.Cells);
        dragStage.Bounds.Width.ShouldBe((int) dragStage.Width.Value);

        dragStageWindows.Length.ShouldBe(2);

        foreach (var window in dragStageWindows)
        {
            window.Bounds.X.ShouldBeGreaterThanOrEqualTo(dragStage.Bounds.X);
            window.Bounds.Right.ShouldBeLessThanOrEqualTo(dragStage.Bounds.Right);
        }

        dragStage.Bounds.Right.ShouldBeLessThanOrEqualTo(page.Bounds.Right);
    }

    /// <summary>
    /// Verifies the Window page's dialog stage is never forced narrower than it asks to be at a
    /// compact 80x24 terminal, and that its status text stays fully inside it. Mirrors the drag
    /// stage regression above for the second Overlay this page declares.
    /// </summary>
    [Fact]
    public async Task WindowPage_WhenMountedAtCompactSize_DialogStageIsNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == WindowPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Window showcase page");
        var page = gallery.CurrentPage;

        // Act: the dialog stage is the only Overlay hosting the modal confirmation Window; every
        // border-family catalog specimen below it is also a lone Window inside its own Overlay.
        var dialogStage = OwnedTree.FindAll<Overlay>(page)
            .Single(static overlay => overlay.Children.OfType<Window>()
                .Any(static window => window.Header == "Confirm deployment"));
        var texts = OwnedTree.FindAll<ControlText>(dialogStage).ToArray();
        var workspaceStatus = texts.Single(
            static text => text.Content.StartsWith("Workspace action:", StringComparison.Ordinal));
        var dialogStatus = texts.Single(
            static text => text.Content.StartsWith("Dialog:", StringComparison.Ordinal));

        // Assert: the requested cell width was actually honored, and the dialog's own status
        // text stays inside it. IOverlayPositionConstraint clamps the dialog Window's own
        // origin, so asserting containment on the Window itself would be vacuous.
        dialogStage.Width.Kind.ShouldBe(LengthKind.Cells);
        dialogStage.Bounds.Width.ShouldBe((int) dialogStage.Width.Value);

        workspaceStatus.Bounds.Right.ShouldBeLessThanOrEqualTo(dialogStage.Bounds.Right);
        dialogStatus.Bounds.Right.ShouldBeLessThanOrEqualTo(dialogStage.Bounds.Right);
    }

    /// <summary>
    /// Verifies the MenuItem page's roster frame is never forced narrower than it asks to be at
    /// a compact 80x24 terminal, and that its captured "More options" submenu popup stays inside
    /// the specimen GroupBox on the axis the DocExample column narrows.
    /// </summary>
    [Fact]
    public async Task MenuItemPage_WhenMountedAtCompactSize_SubmenuStaysInsideSpecimenFrameAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == MenuItemPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open MenuItem showcase page");
        var page = gallery.CurrentPage;

        // Act: the roster frame is the only Dock this page declares a fixed cell width for.
        var rosterFrame = OwnedTree.FindAll<Dock>(page)
            .Single(static dock => dock.Width.Kind == LengthKind.Cells);
        var moreItem = OwnedTree.FindAll<MenuItem>(page)
            .Single(static item => item.Text.Contains("More", StringComparison.Ordinal));
        var groupBox = OwnedTree.FindAll<GroupBox>(page).Single();

        await surface.UpdateAsync(moreItem.PerformInvoke, "open More options submenu");

        var submenuPopup = OwnedTree.FindAll<Popup>(page).Single();

        // Assert: the requested cell width was actually honored, and the opened submenu's own
        // frame stays inside the specimen GroupBox horizontally.
        rosterFrame.Width.Kind.ShouldBe(LengthKind.Cells);
        rosterFrame.Bounds.Width.ShouldBe(44);

        submenuPopup.IsOpen.ShouldBeTrue();
        submenuPopup.SurfaceBounds.Right.ShouldBeLessThanOrEqualTo(groupBox.Bounds.Right);
    }

    /// <summary>
    /// Verifies the Popup page's menu and placement stages are never forced narrower than they
    /// ask to be at a compact 80x24 terminal, and that every control they position stays inside
    /// its own stage.
    /// </summary>
    [Fact]
    public async Task PopupPage_WhenMountedAtCompactSize_MenuAndPlacementStagesFitContentColumnAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == PopupPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Popup showcase page");
        var page = gallery.CurrentPage;

        // Act: both application stages are clipped, unlike their unclipped inner interaction
        // overlays, and both request the same 46-cell width; their declared heights differ.
        var stages = OwnedTree.FindAll<Overlay>(page)
            .Where(static overlay => overlay.ClipToBounds &&
                overlay.Width.Kind == LengthKind.Cells &&
                (int) overlay.Width.Value == 46)
            .ToArray();
        var menuStage = stages.Single(static overlay => (int) overlay.Height.Value == 15);
        var placementStage = stages.Single(static overlay => (int) overlay.Height.Value == 24);
        var backdropAction = OwnedTree.FindAll<Button>(page)
            .Single(static button => button.Text.Contains("Workspace action", StringComparison.Ordinal));
        var placementButtons = OwnedTree.FindAll<Button>(placementStage).ToArray();

        // Assert: the requested cell widths were actually honored, and every positioned control
        // stays inside its own stage instead of being silently squeezed by the content column.
        menuStage.Bounds.Width.ShouldBe(46);
        placementStage.Bounds.Width.ShouldBe(46);

        backdropAction.Bounds.X.ShouldBeGreaterThanOrEqualTo(menuStage.Bounds.X);
        backdropAction.Bounds.Right.ShouldBeLessThanOrEqualTo(menuStage.Bounds.Right);

        placementButtons.Length.ShouldBe(5);

        foreach (var button in placementButtons)
        {
            button.Bounds.X.ShouldBeGreaterThanOrEqualTo(placementStage.Bounds.X);
            button.Bounds.Right.ShouldBeLessThanOrEqualTo(placementStage.Bounds.Right);
        }
    }

    /// <summary>
    /// Verifies the Text page's five overflow-policy specimens each keep the full 20-cell width
    /// they request instead of being silently drained by Tracks.Shrink, which empties tracks
    /// last-to-first rather than evenly when a single row requests more width than the ~46-cell
    /// content column offers.
    /// </summary>
    [Fact]
    public async Task TextPage_WhenMountedAtCompactSize_OverflowSpecimensKeepFullRequestedWidthAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == TextPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Text showcase page");
        var page = gallery.CurrentPage;

        // Act: the five overflow-policy cards are the only 20-cell-wide Docks this page declares.
        var overflowCards = OwnedTree.FindAll<Dock>(page)
            .Where(static dock => dock.Width.Kind == LengthKind.Cells && (int) dock.Width.Value == 20)
            .ToArray();

        // Assert: the requested cell width was actually honored for every card instead of being
        // silently drained down to zero by Tracks.Shrink when the row overflows the content column.
        overflowCards.Length.ShouldBe(5);

        foreach (var card in overflowCards)
        {
            card.Bounds.Width.ShouldBe(20);
        }
    }

    /// <summary>
    /// Verifies the Flyout page's placement diagram stage is never forced narrower than it asks
    /// to be at a compact 80x24 terminal, and that every direction button it positions stays
    /// inside it. Mirrors the Popup placement-diagram regression above for its structural sibling.
    /// </summary>
    [Fact]
    public async Task FlyoutPage_WhenMountedAtCompactSize_PlacementStageFitsContentColumnAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == FlyoutPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Flyout showcase page");
        var page = gallery.CurrentPage;

        // Act: the placement diagram is the only clipped 46-cell stage on this page requesting a
        // 19-cell height; the ShowAt stage also requests 46 cells but is only 11 tall.
        var placementStage = OwnedTree.FindAll<Overlay>(page)
            .Single(static overlay => overlay.ClipToBounds &&
                overlay.Width.Kind == LengthKind.Cells &&
                (int) overlay.Width.Value == 46 &&
                overlay.Height.Kind == LengthKind.Cells &&
                (int) overlay.Height.Value == 19);
        var placementButtons = OwnedTree.FindAll<Button>(placementStage).ToArray();

        // Assert: the requested cell width was actually honored, and every direction button stays
        // inside the stage instead of being silently squeezed by the content column.
        placementStage.Bounds.Width.ShouldBe(46);

        placementButtons.Length.ShouldBe(5);

        foreach (var button in placementButtons)
        {
            button.Bounds.X.ShouldBeGreaterThanOrEqualTo(placementStage.Bounds.X);
            button.Bounds.Right.ShouldBeLessThanOrEqualTo(placementStage.Bounds.Right);
        }
    }

    /// <summary>
    /// Verifies the StatusBar page's editor workspace is never forced narrower than it asks to
    /// be at a compact 80x24 terminal, and that its interactive right-aligned items still
    /// receive non-zero width rather than starving behind the trailing static items.
    /// </summary>
    [Fact]
    public async Task StatusBarPage_WhenMountedAtCompactSize_WorkspaceKeepsInteractiveItemsVisibleAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == StatusBarPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open StatusBar showcase page");
        var page = gallery.CurrentPage;

        // Act
        var workspace = OwnedTree.FindAll<StatusBarWorkspace>(page).Single();

        // Assert: the requested cell width was actually honored, and the retained CheckBox plus
        // the live pointer-coordinate item both keep visible width. StatusBarHost allocates
        // right-aligned items in reverse insertion order and clamps each to remaining space, so
        // a too-wide workspace starves the earliest-added right-aligned items first.
        workspace.Width.Kind.ShouldBe(LengthKind.Cells);
        workspace.Bounds.Width.ShouldBe((int) workspace.Width.Value);

        workspace.AutoSave.Bounds.Width.ShouldBeGreaterThan(0);
        workspace.PointerStatus.Bounds.Width.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Verifies the Table page's mixed-column example is never forced narrower than it asks to
    /// be at a compact 80x24 terminal, and that every cell it owns stays inside it. Mirrors the
    /// Window/MenuItem/Popup/StatusBar regressions above for the same silent-clamp defect family.
    /// </summary>
    [Fact]
    public async Task TablePage_WhenMountedAtCompactSize_MixedColumnsExampleIsNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == TablePane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Table showcase page");
        var page = gallery.CurrentPage;

        // Act: the mixed-column table is the only one on this page requesting non-zero RowSpacing.
        var primary = OwnedTree.FindAll<Table>(page).Single(static table => table.RowSpacing == 1);
        var cells = OwnedTree.FindAll<ControlText>(primary).ToArray();

        // Assert: the requested cell width was actually honored, and every cell it owns - including
        // the wrapped detail text and the marked link - stays inside its own bounds.
        primary.Width.Kind.ShouldBe(LengthKind.Cells);
        primary.Bounds.Width.ShouldBe((int) primary.Width.Value);

        cells.ShouldNotBeEmpty();

        foreach (var cell in cells)
        {
            cell.Bounds.X.ShouldBeGreaterThanOrEqualTo(primary.Bounds.X);
            cell.Bounds.Right.ShouldBeLessThanOrEqualTo(primary.Bounds.Right);
        }
    }

    /// <summary>
    /// Verifies the Button page's Window-roles dialog is never forced narrower than it asks to be
    /// at a compact 80x24 terminal, and that its default/cancel actions and status text stay fully
    /// inside it.
    /// </summary>
    [Fact]
    public async Task ButtonPage_WhenMountedAtCompactSize_DialogExampleIsNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == ButtonPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Button showcase page");
        var page = gallery.CurrentPage;

        // Act: this page declares exactly one Window, the IsDefault/IsCancel role specimen.
        var dialog = OwnedTree.FindAll<Window>(page).Single();
        var roleButtons = OwnedTree.FindAll<Button>(dialog).ToArray();
        var roleStatus = OwnedTree.FindAll<ControlText>(dialog).Single(
            static text => text.Content.StartsWith("Window action:", StringComparison.Ordinal));

        // Assert: the requested cell width was actually honored, and the Apply/Cancel actions plus
        // the role status text stay inside it instead of being silently squeezed by the content
        // column.
        dialog.Width.Kind.ShouldBe(LengthKind.Cells);
        dialog.Bounds.Width.ShouldBe((int) dialog.Width.Value);

        roleButtons.Length.ShouldBe(2);

        foreach (var button in roleButtons)
        {
            button.Bounds.X.ShouldBeGreaterThanOrEqualTo(dialog.Bounds.X);
            button.Bounds.Right.ShouldBeLessThanOrEqualTo(dialog.Bounds.Right);
        }

        roleStatus.Bounds.Right.ShouldBeLessThanOrEqualTo(dialog.Bounds.Right);
    }

    /// <summary>
    /// Verifies the Button page's shadow-depth stage is never forced narrower than it asks to be
    /// at a compact 80x24 terminal, and that every filled, composite, block, flat, and disabled
    /// specimen it positions stays inside it.
    /// </summary>
    [Fact]
    public async Task ButtonPage_WhenMountedAtCompactSize_ShadowStageIsNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == ButtonPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Button showcase page");
        var page = gallery.CurrentPage;

        // Act: this page declares exactly one Overlay, the shadow-depth stage.
        var shadowStage = OwnedTree.FindAll<Overlay>(page).Single();
        var shadowButtons = OwnedTree.FindAll<Button>(shadowStage).ToArray();

        // Assert: the requested cell width was actually honored, and every shadow-depth specimen
        // stays inside it instead of being silently squeezed by the content column.
        shadowStage.Width.Kind.ShouldBe(LengthKind.Cells);
        shadowStage.Bounds.Width.ShouldBe((int) shadowStage.Width.Value);

        shadowButtons.Length.ShouldBe(5);

        foreach (var button in shadowButtons)
        {
            button.Bounds.X.ShouldBeGreaterThanOrEqualTo(shadowStage.Bounds.X);
            button.Bounds.Right.ShouldBeLessThanOrEqualTo(shadowStage.Bounds.Right);
        }
    }

    /// <summary>
    /// Verifies the Table page's interactive-cells example is never forced narrower than it asks
    /// to be at a compact 80x24 terminal, and that its owned Button and CheckBox stay inside it.
    /// Extends the Table/Button/Window/MenuItem/Popup/StatusBar regressions above to a second,
    /// independently affected example on the same page.
    /// </summary>
    [Fact]
    public async Task TablePage_WhenMountedAtCompactSize_InteractiveCellsExampleIsNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == TablePane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Table showcase page");
        var page = gallery.CurrentPage;

        // Act: the interactive table is the only one on this page owning a CheckBox cell.
        var interactive = OwnedTree.FindAll<Table>(page)
            .Single(static table => OwnedTree.FindAll<CheckBox>(table).Count > 0);
        var runChecks = OwnedTree.FindAll<Button>(interactive)
            .Single(static button => button.Text == "&Run checks");
        var includeTests = OwnedTree.FindAll<CheckBox>(interactive)
            .Single(static checkBox => checkBox.Text == "&Include integration tests");

        // Assert: the requested cell width was actually honored, and the owned Button/CheckBox
        // cells stay inside it instead of being silently squeezed by the content column.
        interactive.Width.Kind.ShouldBe(LengthKind.Cells);
        interactive.Bounds.Width.ShouldBe((int) interactive.Width.Value);

        runChecks.Bounds.X.ShouldBeGreaterThanOrEqualTo(interactive.Bounds.X);
        runChecks.Bounds.Right.ShouldBeLessThanOrEqualTo(interactive.Bounds.Right);
        includeTests.Bounds.X.ShouldBeGreaterThanOrEqualTo(interactive.Bounds.X);
        includeTests.Bounds.Right.ShouldBeLessThanOrEqualTo(interactive.Bounds.Right);
    }

    /// <summary>
    /// Verifies the HorizontalBarChart page's three fixed-width charts are never forced narrower
    /// than they ask to be at a compact 80x24 terminal.
    /// </summary>
    [Fact]
    public async Task HorizontalBarChartPage_WhenMountedAtCompactSize_ChartsAreNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == HorizontalBarChartPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open HorizontalBarChart showcase page");
        var page = gallery.CurrentPage;

        // Act: this page declares exactly three HorizontalBarChart specimens, distinguished by
        // series count and the value-label flag only the first one sets.
        var charts = OwnedTree.FindAll<HorizontalBarChart>(page);
        var positive = charts.Single(static chart => chart.Series.Count == 1 && chart.ShowValueLabels);
        var mixed = charts.Single(static chart => chart.Series.Count == 1 && !chart.ShowValueLabels);
        var grouped = charts.Single(static chart => chart.Series.Count == 2);

        // Assert: the requested cell width was actually honored for every chart instead of being
        // silently squeezed by the content column.
        foreach (var chart in new[] { positive, mixed, grouped })
        {
            chart.Width.Kind.ShouldBe(LengthKind.Cells);
            chart.Bounds.Width.ShouldBe((int) chart.Width.Value);
        }
    }

    /// <summary>
    /// Verifies the VerticalBarChart page's two fixed-width charts are never forced narrower than
    /// they ask to be at a compact 80x24 terminal.
    /// </summary>
    [Fact]
    public async Task VerticalBarChartPage_WhenMountedAtCompactSize_ChartsAreNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == VerticalBarChartPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open VerticalBarChart showcase page");
        var page = gallery.CurrentPage;

        // Act: this page declares exactly two VerticalBarChart specimens, distinguished by series
        // count.
        var charts = OwnedTree.FindAll<VerticalBarChart>(page);
        var grouped = charts.Single(static chart => chart.Series.Count == 2);
        var bounded = charts.Single(static chart => chart.Series.Count == 1);

        // Assert: the requested cell width was actually honored for every chart instead of being
        // silently squeezed by the content column.
        foreach (var chart in new[] { grouped, bounded })
        {
            chart.Width.Kind.ShouldBe(LengthKind.Cells);
            chart.Bounds.Width.ShouldBe((int) chart.Width.Value);
        }
    }

    /// <summary>
    /// Verifies the LineChart page's two fixed-width charts are never forced narrower than they
    /// ask to be at a compact 80x24 terminal. The third, bound chart uses Percent(100) and is
    /// unaffected by this defect family.
    /// </summary>
    [Fact]
    public async Task LineChartPage_WhenMountedAtCompactSize_ChartsAreNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == LineChartPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open LineChart showcase page");
        var page = gallery.CurrentPage;

        // Act: only two charts on this page request a fixed cell width; the bound chart requests
        // Percent(100) and is excluded here.
        var charts = OwnedTree.FindAll<LineChart>(page)
            .Where(static chart => chart.Width.Kind == LengthKind.Cells)
            .ToArray();
        var narrowRange = charts.Single(static chart => chart.Series.Count == 1);
        var colored = charts.Single(static chart => chart.Series.Count == 2);

        // Assert: the requested cell width was actually honored for both charts instead of being
        // silently squeezed by the content column.
        foreach (var chart in new[] { narrowRange, colored })
        {
            chart.Width.Kind.ShouldBe(LengthKind.Cells);
            chart.Bounds.Width.ShouldBe((int) chart.Width.Value);
        }
    }

    /// <summary>
    /// Verifies the Dock page's application-shell and successive-percentages examples are never
    /// forced narrower than they ask to be at a compact 80x24 terminal, and that the successive-
    /// percentages example's cards still receive the recomputed 23/11-cell split its own
    /// description text now cites.
    /// </summary>
    [Fact]
    public async Task DockPage_WhenMountedAtCompactSize_ShellAndPercentageExamplesAreNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == DockPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Dock showcase page");
        var page = gallery.CurrentPage;

        // Act: the shell and percentages docks are the only ones on this page with a 13-cell and
        // a 5-cell requested height respectively.
        var allSides = OwnedTree.FindAll<Dock>(page)
            .Single(static dock => dock.Height.Kind == LengthKind.Cells && (int) dock.Height.Value == 13);
        var remaining = OwnedTree.FindAll<Dock>(page)
            .Single(static dock => dock.Height.Kind == LengthKind.Cells && (int) dock.Height.Value == 5);
        var allSidesChildren = allSides.Children.ToArray();
        var remainingChildren = remaining.Children.ToArray();

        // Assert: the requested cell widths were actually honored, every shell region stays inside
        // its dock, and the two percentage cards received the width their own recomputed caption
        // and description text now cite.
        allSides.Width.Kind.ShouldBe(LengthKind.Cells);
        allSides.Bounds.Width.ShouldBe((int) allSides.Width.Value);
        remaining.Width.Kind.ShouldBe(LengthKind.Cells);
        remaining.Bounds.Width.ShouldBe((int) remaining.Width.Value);

        allSidesChildren.Length.ShouldBe(5);

        foreach (var child in allSidesChildren)
        {
            child.Bounds.X.ShouldBeGreaterThanOrEqualTo(allSides.Bounds.X);
            child.Bounds.Right.ShouldBeLessThanOrEqualTo(allSides.Bounds.Right);
        }

        remainingChildren.Length.ShouldBe(3);
        remainingChildren[0].Bounds.Width.ShouldBe(23);
        remainingChildren[1].Bounds.Width.ShouldBe(11);

        foreach (var child in remainingChildren)
        {
            child.Bounds.X.ShouldBeGreaterThanOrEqualTo(remaining.Bounds.X);
            child.Bounds.Right.ShouldBeLessThanOrEqualTo(remaining.Bounds.Right);
        }
    }

    /// <summary>
    /// Verifies the Stack page's vertical and horizontal-orientation examples are never forced
    /// narrower than they ask to be at a compact 80x24 terminal, and that their owned regions
    /// stay inside them.
    /// </summary>
    [Fact]
    public async Task StackPage_WhenMountedAtCompactSize_VerticalAndHorizontalExamplesAreNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == StackPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Stack showcase page");
        var page = gallery.CurrentPage;

        // Act: the two 46-cell stacks on this page are distinguished by orientation.
        var stacks = OwnedTree.FindAll<Stack>(page)
            .Where(static stack => stack.Width.Kind == LengthKind.Cells && (int) stack.Width.Value == 46)
            .ToArray();
        var vertical = stacks.Single(static stack => stack.Orientation == Orientation.Vertical);
        var horizontal = stacks.Single(static stack => stack.Orientation == Orientation.Horizontal);
        var verticalChildren = vertical.Children.ToArray();
        var horizontalChildren = horizontal.Children.ToArray();

        // Assert: the requested cell widths were actually honored, and every owned region stays
        // inside its stack instead of being silently squeezed by the content column.
        vertical.Bounds.Width.ShouldBe(46);
        horizontal.Bounds.Width.ShouldBe(46);

        verticalChildren.Length.ShouldBe(3);
        horizontalChildren.Length.ShouldBe(2);

        foreach (var child in verticalChildren)
        {
            child.Bounds.X.ShouldBeGreaterThanOrEqualTo(vertical.Bounds.X);
            child.Bounds.Right.ShouldBeLessThanOrEqualTo(vertical.Bounds.Right);
        }

        foreach (var child in horizontalChildren)
        {
            child.Bounds.X.ShouldBeGreaterThanOrEqualTo(horizontal.Bounds.X);
            child.Bounds.Right.ShouldBeLessThanOrEqualTo(horizontal.Bounds.Right);
        }
    }

    /// <summary>
    /// Verifies every fixed-width TabControl on the TabControl page is never forced narrower than
    /// it asks to be at a compact 80x24 terminal, and that the narrow-header overflow specimen's
    /// HeaderWidth remains independently narrower than its five headers so the overflow demo it
    /// documents still fires regardless of the page's own content-column width.
    /// </summary>
    [Fact]
    public async Task TabControlPage_WhenMountedAtCompactSize_AllExamplesAreNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == TabControlPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open TabControl showcase page");
        var page = gallery.CurrentPage;

        // Act: this page declares five fixed-width TabControls, distinguished by height and the
        // first item's header.
        var tabControls = OwnedTree.FindAll<TabControl>(page);
        var tabs = tabControls.Single(static control =>
            control.Height.Kind == LengthKind.Cells && (int) control.Height.Value == 8);
        var overflowTabs = tabControls.Single(static control =>
            control.HeaderWidth.Kind == LengthKind.Cells && (int) control.HeaderWidth.Value == 10);
        var dyn = tabControls.Single(static control =>
            control.Height.Kind == LengthKind.Cells && (int) control.Height.Value == 6 &&
            control.Items[0].HeaderText == "&Tab 1");
        var disabledTabs = tabControls.Single(static control =>
            control.Height.Kind == LengthKind.Cells && (int) control.Height.Value == 6 &&
            control.Items[0].HeaderText == "Acti&ve");
        var styled = tabControls.Single(static control =>
            control.Height.Kind == LengthKind.Cells && (int) control.Height.Value == 6 &&
            control.Items[0].HeaderText == "&Profile");

        // Assert: the requested cell width was actually honored for every TabControl instead of
        // being silently squeezed by the content column.
        foreach (var control in new[] { tabs, overflowTabs, dyn, disabledTabs, styled })
        {
            control.Width.Kind.ShouldBe(LengthKind.Cells);
            control.Bounds.Width.ShouldBe((int) control.Width.Value);
        }

        // The overflow demo depends on HeaderWidth staying narrower than five headers need, which
        // is unrelated to the control's own (now narrower) outer Width.
        overflowTabs.HeaderWidth.Kind.ShouldBe(LengthKind.Cells);
        overflowTabs.HeaderWidth.Value.ShouldBe(10);
        overflowTabs.Items.Count.ShouldBe(5);
    }

    /// <summary>
    /// Verifies the Control page's box-model example is never forced narrower than it asks to be
    /// at a compact 80x24 terminal, and that its innermost content Text - now wrapped as a safety
    /// margin against the narrower interior width - stays inside its own nested content box.
    /// </summary>
    [Fact]
    public async Task ControlPage_WhenMountedAtCompactSize_BoxModelExampleIsNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == ControlPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open Control showcase page");
        var page = gallery.CurrentPage;

        // Act: the box-model example is the only 46-cell Dock this page declares.
        var boxModel = OwnedTree.FindAll<Dock>(page)
            .Single(static dock => dock.Width.Kind == LengthKind.Cells && (int) dock.Width.Value == 46);
        var innerDock = boxModel.Children.OfType<Dock>().Single();
        var content = OwnedTree.FindAll<ControlText>(innerDock).Single(
            static text => text.Content == "content inside padding; margin remains outside");

        // Assert: the requested cell width was actually honored, and the innermost content Text
        // wraps rather than being clipped by its now-smaller nested content box.
        boxModel.Width.Kind.ShouldBe(LengthKind.Cells);
        boxModel.Bounds.Width.ShouldBe((int) boxModel.Width.Value);

        content.Overflow.ShouldBe(Overflow.Wrap);
        content.Bounds.X.ShouldBeGreaterThanOrEqualTo(innerDock.Bounds.X);
        content.Bounds.Right.ShouldBeLessThanOrEqualTo(innerDock.Bounds.Right);
    }

    /// <summary>
    /// Verifies the GroupBox page's three side-by-side rows are never forced narrower than they
    /// ask to be at a compact 80x24 terminal. Extends the Window/MenuItem/Popup/StatusBar/Table/
    /// Button/HorizontalBarChart/VerticalBarChart/LineChart/Dock/Stack/TabControl/Control
    /// regressions above to a page whose overflow came from two fixed-width GroupBoxes (and their
    /// TextInputs) requesting more than the shared row together, rather than a single oversized
    /// control.
    /// </summary>
    [Fact]
    public async Task GroupBoxPage_WhenMountedAtCompactSize_RowsAreNotClampedNarrowerThanRequestedAsync()
    {
        // Arrange
        var gallery = new Gallery();
        await using var surface = await ComponentSurface.MountScreenAsync(
            gallery,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var pageIndex = gallery.Pages
            .Select(static (name, index) => (name, index))
            .Single(static entry => entry.name == GroupBoxPane.Title)
            .index;
        await surface.UpdateAsync(() => gallery.Select(pageIndex), "open GroupBox showcase page");
        var page = gallery.CurrentPage;

        // Act: this page declares exactly three DocRows, distinguished by how many GroupBoxes
        // each one lays out side by side.
        var rows = OwnedTree.FindAll<DocRow>(page).ToArray();
        var identityAndPreferencesRow = rows.Single(static row => OwnedTree.FindAll<GroupBox>(row).Count == 2);
        var framedRow = rows.Single(static row => OwnedTree.FindAll<GroupBox>(row).Count == 3);
        var edgeCaseRow = rows.Single(static row => OwnedTree.FindAll<GroupBox>(row).Count == 4);

        var personalGroup = OwnedTree.FindAll<GroupBox>(identityAndPreferencesRow)
            .Single(static group => group.HeaderText == "&Personal");
        var preferencesGroup = OwnedTree.FindAll<GroupBox>(identityAndPreferencesRow)
            .Single(static group => group.HeaderText == "Pre&ferences");
        var textInputs = OwnedTree.FindAll<TextInput>(personalGroup).ToArray();
        var tinyGroup = OwnedTree.FindAll<GroupBox>(edgeCaseRow)
            .Single(static group => group.HeaderText == "&T");

        // Assert: the requested cell widths were actually honored for every explicitly sized
        // control instead of being silently squeezed by the shared row - a squeeze is exactly
        // what an unreachable, non-scrollable overflow looks like from the arranged tree.
        personalGroup.Width.Kind.ShouldBe(LengthKind.Cells);
        personalGroup.Bounds.Width.ShouldBe((int) personalGroup.Width.Value);
        preferencesGroup.Width.Kind.ShouldBe(LengthKind.Cells);
        preferencesGroup.Bounds.Width.ShouldBe((int) preferencesGroup.Width.Value);
        tinyGroup.Width.Kind.ShouldBe(LengthKind.Cells);
        tinyGroup.Bounds.Width.ShouldBe((int) tinyGroup.Width.Value);

        textInputs.Length.ShouldBe(2);

        foreach (var input in textInputs)
        {
            input.Width.Kind.ShouldBe(LengthKind.Cells);
            input.Bounds.Width.ShouldBe((int) input.Width.Value);
        }

        // Assert: the auto-sized, shortened-caption boxes on the remaining two rows resolved to
        // their own full natural width rather than being clipped down to fit the row.
        var framedGroups = OwnedTree.FindAll<GroupBox>(framedRow).ToArray();
        framedGroups.Length.ShouldBe(3);
        framedGroups.Sum(static group => group.Bounds.Width).ShouldBe(framedRow.Bounds.Width - (2 * 2));

        var edgeCaseGroups = OwnedTree.FindAll<GroupBox>(edgeCaseRow).ToArray();
        edgeCaseGroups.Length.ShouldBe(4);
        edgeCaseGroups.Sum(static group => group.Bounds.Width).ShouldBe(edgeCaseRow.Bounds.Width - (3 * 2));

        // Assert: every row still fits inside the ~46-cell content column instead of overflowing
        // it, which is the condition DocPage's hidden horizontal scrollbar cannot recover from.
        foreach (var row in rows)
        {
            row.Bounds.Right.ShouldBeLessThanOrEqualTo(page.Bounds.Right);
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
