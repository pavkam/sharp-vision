// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies NavigationView composition, selection, groups, scrolling, mutation, Unicode, and resize through mounted surfaces.</summary>
public sealed class NavigationViewSurfaceTests
{
    /// <summary>Verifies inactive and hovered items preserve the NavigationView background.</summary>
    [Fact]
    public async Task Pointer_WhenMovedOverItem_ChangesForegroundWithoutPaintingBackgroundAsync()
    {
        // Arrange
        var item = new NavigationViewItem { Header = "Home" };
        var view = CreateView(header: null, 12, useDefaultChrome: true);
        view.Items.Add(item);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        var theme = view.Theme.ShouldNotBeNull();
        var itemOrigin = new Point(item.Bounds.X, item.Bounds.Y);
        var viewBackground = surface.Cell(itemOrigin).Style.Background;
        item.GetResolvedAppearance(VisualState.Normal).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        surface.Cell(itemOrigin).Style.Background.ShouldBe(viewBackground);

        // Act
        await surface.Pointer.MoveToAsync(item);

        // Assert
        item.GetResolvedAppearance(item.GetAppearanceState()).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        surface.Cell(itemOrigin).Style.Background.ShouldBe(viewBackground);
        surface.Cell(itemOrigin).Style.Foreground.ShouldBe(
            ThemeColorHelper.HoveredForeground(theme));
    }

    /// <summary>Verifies header, main, group, separators, footer, indentation, and Unicode draw exact borderless cells.</summary>
    [ComponentBehaviorEvidence(
        typeof(NavigationViewSeparator),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Render_WhenEverySectionIsPresent_DrawsExactRetainedSidebarAsync()
    {
        // Arrange
        var home = new NavigationViewItem { Header = "Home", Glyph = "◆" };
        var group = new NavigationViewGroup { Header = "Tools" };
        group.AddItem(new NavigationViewItem { Header = "Edit" });
        var about = new NavigationViewItem { Header = "About", Glyph = "界" };
        var mainSeparator = new NavigationViewSeparator();
        var footerSeparator = new NavigationViewSeparator();
        var view = CreateView("界 NAV", 20, useDefaultChrome: true);
        view.Items.Add(home);
        view.Items.Add(group);
        view.Items.Add(mainSeparator);
        view.FooterItems.Add(footerSeparator);
        view.FooterItems.Add(about);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 9),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                              界 NAV
                              · ◆ Home
                              ▼ Tools
                                · Edit
                             ────────────────────


                             ────────────────────
                              · 界 About
                             """);
        surface.Cell(new Point(1, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
        surface.Cell(new Point(3, 8)).Text.ShouldBe("界");
        surface.Cell(new Point(4, 8)).IsContinuation.ShouldBeTrue();
        home.GetResolvedAppearance(VisualState.Normal).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        surface.Cell(new Point(home.Bounds.X, home.Bounds.Y)).Style.Background.ShouldBe(
            surface.Cell(new Point(0, 5)).Style.Background);
        view.SelectedItem.ShouldBeNull();

        // Act and assert excluded separator interaction
        await surface.Pointer.MoveToAsync(mainSeparator);
        mainSeparator.IsPointerOver.ShouldBeFalse();
        mainSeparator.IsFocused.ShouldBeFalse();
        mainSeparator.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies Tab entry, pointer selection, arrows, Home/End, disabled skipping, footer traversal, and event order.</summary>
    [ComponentBehaviorEvidence(
        typeof(NavigationView),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    [ComponentBehaviorEvidence(
        typeof(NavigationViewItem),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.UnavailableCleanup)]
    [Fact]
    public async Task Input_WhenItemsNavigate_UsesOneFlatEligibleSelectionOrderAsync()
    {
        // Arrange
        var first = new NavigationViewItem { Header = "First" };
        var disabled = new NavigationViewItem { Header = "Disabled", IsEnabled = false };
        var child = new NavigationViewItem { Header = "Child" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.AddItem(child);
        var footer = new NavigationViewItem { Header = "Footer" };
        var view = CreateView(header: null, 14);
        view.Items.Add(first);
        view.Items.Add(disabled);
        view.Items.Add(group);
        view.FooterItems.Add(footer);
        var observations = new List<string>();
        view.SelectionChanged += (_, _) => observations.Add(view.SelectedItem?.Header ?? "none");
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 5),
            TestContext.Current.CancellationToken);

        // Act Tab entry
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert focus entry precedes directional selection
        view.SelectedItem.ShouldBeNull();
        surface.ShouldHaveState(view, VisualState.Focused);
        first.IsFocused.ShouldBeFalse();

        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(first);
        first.IsSelected.ShouldBeTrue();

        // Act flat navigation
        await surface.Keyboard.PressAsync(Code.Down);
        (group.GetAppearanceState() & VisualState.Current).ShouldBe(VisualState.Current);
        view.SelectedItem.ShouldBeSameAs(first);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(child);
        surface.ShouldHaveFocus(view);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(footer);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(footer);
        await surface.Keyboard.PressAsync(Code.Home);
        view.SelectedItem.ShouldBeSameAs(first);
        await surface.Keyboard.PressAsync(Code.End);

        // Assert footer and order
        view.SelectedItem.ShouldBeSameAs(footer);
        disabled.IsSelected.ShouldBeFalse();
        observations.ShouldBe(["First", "Child", "Footer", "First", "Footer"]);

        // Act pointer parity through distinct held and released states
        await surface.Pointer.MoveToAsync(first);
        await surface.Pointer.PressAsync();

        // Assert item press does not steal root focus or select before release
        first.IsPressed.ShouldBeTrue();
        first.IsFocused.ShouldBeFalse();
        view.SelectedItem.ShouldBeSameAs(footer);
        surface.ShouldHaveCapture(first);

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert pointer selection
        view.SelectedItem.ShouldBeSameAs(first);
        surface.ShouldHaveState(view, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldHaveState(first, VisualState.PointerOver);
        first.IsPressed.ShouldBeFalse();

        // Act unavailable while another item press is held
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => first.IsEnabled = false, "disable held NavigationViewItem");

        // Assert item cleanup preserves completed selection
        first.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        view.SelectedItem.ShouldBeSameAs(first);

        // Act and assert focus-owner cleanup
        await surface.UpdateAsync(() => view.IsEnabled = false, "disable focused NavigationView");
        view.IsPressed.ShouldBeFalse();
        view.IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies pointer and keyboard group toggles repair selection when descendants disappear.</summary>
    [ComponentBehaviorEvidence(
        typeof(NavigationViewGroup),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenSelectedGroupCollapses_RepairsSelectionAndRetainedBoundsAsync()
    {
        // Arrange
        var first = new NavigationViewItem { Header = "First" };
        var second = new NavigationViewItem { Header = "Second" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.AddItem(first);
        group.AddItem(second);
        var after = new NavigationViewItem { Header = "After" };
        var view = CreateView(header: null, 14);
        view.Items.Add(group);
        view.Items.Add(after);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(first);

        // Act pointer collapse
        await surface.Pointer.ClickAsync(group, new Point(1, 0));

        // Assert repair
        group.IsExpanded.ShouldBeFalse();
        first.EffectiveIsVisible.ShouldBeFalse();
        second.EffectiveIsVisible.ShouldBeFalse();
        view.HitTest(new Point(first.Bounds.X, first.Bounds.Y)).ShouldNotBeSameAs(first);
        view.SelectedItem.ShouldBeSameAs(after);
        first.IsTabStop.ShouldBeFalse();
        surface.ShouldRender("""
                              ▶ Group
                              › After



                             """);

        // Act pointer expand
        await surface.Pointer.ClickAsync(group, new Point(1, 0));

        // Assert expanded
        group.IsExpanded.ShouldBeTrue();
        surface.ShouldRender("""
                              ▼ Group
                                · First
                                · Second
                              › After

                             """);
    }

    /// <summary>Verifies a pointer click on a grouped item commits selection through the owning view.</summary>
    [ComponentBehaviorEvidence(typeof(NavigationView), ComponentBehavior.RetainedPointerActivation)]
    [Fact]
    public async Task Pointer_WhenGroupedItemIsClicked_SelectsItemThroughOwnerAsync()
    {
        // Arrange
        var group = new NavigationViewGroup { Header = "Core" };
        var models = new NavigationViewItem { Header = "Models" };
        var services = new NavigationViewItem { Header = "Services" };
        group.AddItem(models);
        group.AddItem(services);
        var view = CreateView(header: null, 18);
        view.Items.Add(group);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(18, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(models);

        // Assert
        view.SelectedItem.ShouldBeSameAs(models);
        models.IsSelected.ShouldBeTrue();
        surface.ShouldHaveFocus(view);
    }

    /// <summary>Verifies owner-focused keyboard navigation reaches group headers and collapses their retained rows.</summary>
    [Fact]
    public async Task Input_WhenGroupsReceiveKeyboardNavigation_CollapsesEachGroupAsync()
    {
        // Arrange
        var core = new NavigationViewGroup { Header = "Core" };
        var models = new NavigationViewItem { Header = "Models" };
        core.AddItem(models);
        var tests = new NavigationViewGroup { Header = "Tests" };
        var unit = new NavigationViewItem { Header = "Unit" };
        tests.AddItem(unit);
        var view = CreateView(header: null, 14);
        view.Items.Add(core);
        view.Items.Add(new NavigationViewSeparator());
        view.Items.Add(tests);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(14, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act collapse first group, navigate to the second header, and collapse it
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        core.IsExpanded.ShouldBeFalse();
        tests.IsExpanded.ShouldBeFalse();
        models.EffectiveIsVisible.ShouldBeFalse();
        unit.EffectiveIsVisible.ShouldBeFalse();
        core.Bounds.Height.ShouldBe(1);
        tests.Bounds.Height.ShouldBe(1);
        surface.ShouldHaveFocus(view);
        (tests.GetAppearanceState() & VisualState.Current).ShouldBe(VisualState.Current);
        surface.ShouldRender("""
                              ▶ Core
                             ──────────────
                              ▶ Tests


                             """);
    }

    /// <summary>Verifies main scrolling, selected removal repair, footer pinning, and resize offset clamping.</summary>
    [Fact]
    public async Task ResizeAsync_WhenMainItemsOverflowAndMutate_RepairsSelectionOffsetAndCellsAsync()
    {
        // Arrange
        var items = Enumerable.Range(1, 8)
            .Select(index => new NavigationViewItem { Header = $"Page {index}" })
            .ToArray();
        var footer = new NavigationViewItem { Header = "Footer" };
        var view = CreateView("NAV", 12);

        foreach (var item in items)
        {
            view.Items.Add(item);
        }

        view.FooterItems.Add(footer);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(12, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);

        // Act navigate to final main item
        for (var index = 1; index < items.Length; index++)
        {
            await surface.Keyboard.PressAsync(Code.Down);
        }

        // Assert overflow and footer pinning
        view.SelectedItem.ShouldBeSameAs(items[^1]);
        surface.ShouldRender("""
                              NAV
                              · Page 4  │
                              · Page 5  │
                              · Page 6  ┃
                              · Page 7  ┃
                              › Page 8  ┃
                              · Footer
                             """);

        // Act remove selected and resize
        await surface.UpdateAsync(() => view.Items.Remove(items[^1]), "remove selected navigation item");
        await surface.ResizeAsync(new Size(12, 10));

        // Assert nearest repair, clamp, and stale clearing
        view.SelectedItem.ShouldBeSameAs(footer);
        surface.ShouldRender("""
                              NAV
                              · Page 1
                              · Page 2
                              · Page 3
                              · Page 4
                              · Page 5
                              · Page 6
                              · Page 7

                              › Footer
                             """);
    }

    private static NavigationView CreateView(string? header, int width, bool useDefaultChrome = false)
    {
        var view = new NavigationView
        {
            Header = header,
            Width = Length.Cells(width),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _ = useDefaultChrome;

        return view;
    }
}
