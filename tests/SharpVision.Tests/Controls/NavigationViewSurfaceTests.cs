// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies NavigationView composition, selection, groups, scrolling, mutation, Unicode, and resize through mounted surfaces.</summary>
public sealed class NavigationViewSurfaceTests
{
    /// <summary>Verifies header, main, group, separators, footer, border, indentation, and Unicode draw exact cells.</summary>
    [Fact]
    public async Task Render_WhenEverySectionIsPresent_DrawsExactRetainedSidebarAsync()
    {
        // Arrange
        var home = new NavigationViewItem { Header = "Home", Glyph = "◆" };
        var group = new NavigationViewGroup { Header = "Tools" };
        group.AddItem(new NavigationViewItem { Header = "Edit" });
        var about = new NavigationViewItem { Header = "About", Glyph = "界" };
        var view = CreateView("界 NAV", 20);
        view.BorderThickness = new Thickness(1);
        view.BorderGlyphs = Glyphs.Rounded;
        view.Items.Add(home);
        view.Items.Add(group);
        view.Items.Add(new NavigationViewSeparator());
        view.FooterItems.Add(new NavigationViewSeparator());
        view.FooterItems.Add(about);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 9),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
            ╭──────────────────╮
            │ 界 NAV           │
            │ · ◆ Home         │
            │ ▼ Tools          │
            │   · Edit         │
            │──────────────────│
            │──────────────────│
            │ · 界 About       │
            ╰──────────────────╯
            """);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("界");
        surface.Cell(new Point(3, 1)).IsContinuation.ShouldBeTrue();
        surface.Cell(new Point(4, 7)).Text.ShouldBe("界");
        surface.Cell(new Point(5, 7)).IsContinuation.ShouldBeTrue();
        view.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies Tab entry, pointer selection, arrows, Home/End, disabled skipping, footer traversal, and event order.</summary>
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
        first.Style = ThemeTestSupport.OverlayStyle<NavigationViewItem>(
            (State.Selected, new ThemeOverlay(attributes: Attributes.Reverse)));
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

        // Assert first entry
        view.SelectedItem.ShouldBeSameAs(first);
        surface.ShouldHaveState(first, State.Focused);
        surface.Cell(new Point(first.Bounds.X + 1, first.Bounds.Y))
            .Style.Attributes.HasFlag(Attributes.Reverse).ShouldBeTrue();

        // Act flat navigation
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(child);
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(footer);
        var footerOffset = view.VerticalOffset;
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedItem.ShouldBeSameAs(footer);
        await surface.Keyboard.PressAsync(Code.Home);
        view.SelectedItem.ShouldBeSameAs(first);
        await surface.Keyboard.PressAsync(Code.End);

        // Assert footer and order
        view.SelectedItem.ShouldBeSameAs(footer);
        view.VerticalOffset.ShouldBe(footerOffset);
        disabled.IsSelected.ShouldBeFalse();
        observations.ShouldBe(["First", "Child", "Footer", "First", "Footer"]);

        // Act pointer parity
        await surface.Pointer.ClickAsync(first);

        // Assert pointer selection
        view.SelectedItem.ShouldBeSameAs(first);
        surface.ShouldHaveState(first, State.Hovered | State.Focused);
    }

    /// <summary>Verifies pointer and keyboard group toggles repair selection when descendants disappear.</summary>
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
        await surface.Keyboard.PressAsync(Code.Tab);
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

        // Act keyboard expand
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert expanded
        group.IsExpanded.ShouldBeTrue();
        surface.ShouldRender("""
             ▼ Group
               · First
               · Second
             › After

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

        // Act navigate to final main item
        for (var index = 1; index < items.Length; index++)
        {
            await surface.Keyboard.PressAsync(Code.Down);
        }

        // Assert overflow and footer pinning
        view.SelectedItem.ShouldBeSameAs(items[^1]);
        view.VerticalOffset.ShouldBeGreaterThan(0);
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
        view.VerticalOffset.ShouldBe(0);
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

    private static NavigationView CreateView(string? header, int width) => new()
    {
        Header = header,
        Width = Length.Cells(width),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
}
