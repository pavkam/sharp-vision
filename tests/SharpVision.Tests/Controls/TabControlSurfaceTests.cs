// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies TabControl appearance, selection, focus, repair, overflow, Unicode, and resize through mounted surfaces.</summary>
public sealed class TabControlSurfaceTests
{
    /// <summary>Verifies exact headers, separator, content, pointer selection, focus, event order, and selected style.</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsClicked_CommitsSelectedAppearanceAndContentAsync()
    {
        // Arrange
        var first = Create("General", "General body");
        var second = Create("界", "Wide body");
        var style = ThemeTestSupport.OverlayStyle<TabControl>(
            (State.Selected, new ThemeOverlay(attributes: Attributes.Reverse)));
        var tabs = Create(style, first, second);
        var observations = new List<string>();
        tabs.SelectionChanged += (_, _) => observations.Add(
            $"{tabs.SelectedIndex}:{first.IsSelected}:{second.IsSelected}");
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
             General │ 界
            ────────────────────
            General body


            """);

        // Act
        await surface.Pointer.ClickAsync(second.HeaderPart);

        // Assert
        tabs.SelectedItem.ShouldBeSameAs(second);
        first.Content.ShouldNotBeNull().Bounds.ShouldBe(default);
        second.Content.ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 2, 20, 3));
        surface.ShouldHaveState(second.HeaderPart, State.Hovered | State.Focused);
        surface.Cell(new Point(11, 0)).Style.Attributes.HasFlag(Attributes.Reverse).ShouldBeTrue();
        surface.Cell(new Point(12, 0)).IsContinuation.ShouldBeTrue();
        observations.ShouldBe(["1:False:True"]);
        surface.ShouldRender("""
             General │ 界
            ────────────────────
            Wide body


            """);
    }

    /// <summary>Verifies arrows wrap, Home/End select, and navigation skips a disabled header.</summary>
    [Fact]
    public async Task Keyboard_WhenHeadersNavigate_WrapsAndSkipsUnavailableTabsAsync()
    {
        // Arrange
        var first = Create("One", "First");
        var disabled = Create("Two", "Disabled");
        disabled.IsEnabled = false;
        var third = Create("Three", "Third");
        var tabs = Create(style: null, first, disabled, third);
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveState(first.HeaderPart, State.Focused);

        // Act and assert Right skips disabled
        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedItem.ShouldBeSameAs(third);
        surface.ShouldHaveState(third.HeaderPart, State.Focused);

        // Act and assert wrap
        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedItem.ShouldBeSameAs(first);

        // Act and assert End/Home
        await surface.Keyboard.PressAsync(Code.End);
        tabs.SelectedItem.ShouldBeSameAs(third);
        await surface.Keyboard.PressAsync(Code.Home);
        tabs.SelectedItem.ShouldBeSameAs(first);
        disabled.IsSelected.ShouldBeFalse();
        disabled.HeaderPart.EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies selected removal and content replacement clear every previous page cell.</summary>
    [Fact]
    public async Task UpdateAsync_WhenSelectedPageAndContentChange_RepairsIdentityAndStaleCellsAsync()
    {
        // Arrange
        var first = Create("One", "Old content\nOld tail");
        var second = Create("Two", "Second");
        var tabs = Create(style: null, first, second);
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(16, 5),
            TestContext.Current.CancellationToken);

        // Act remove selected
        await surface.UpdateAsync(() => tabs.Items.RemoveAt(0), "remove selected tab");

        // Assert successor and stale clearing
        tabs.SelectedItem.ShouldBeSameAs(second);
        first.Parent.ShouldBeNull();
        surface.ShouldRender("""
             Two
            ────────────────
            Second


            """);

        // Act replace selected content
        await surface.UpdateAsync(
            () => second.Content = new ControlText("New"),
            "replace selected tab content");

        // Assert replacement
        surface.ShouldRender("""
             Two
            ────────────────
            New


            """);
    }

    /// <summary>Verifies clipped header navigation reveals a wide selected label and resize reflows the same strip.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHeaderStripOverflows_RevealsSelectedUnicodeHeaderAsync()
    {
        // Arrange
        var first = Create("One", "First");
        var middle = Create("Disabled", "Middle");
        var wide = Create("界", "Wide body");
        var tabs = Create(style: null, first, middle, wide);
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(10, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act select final overflowing header
        await surface.Keyboard.PressAsync(Code.End);

        // Assert clipped reveal
        tabs.SelectedItem.ShouldBeSameAs(wide);
        tabs.HeaderOffset.ShouldBe(11);
        wide.HeaderPart.Bounds.ShouldBe(new Rect(6, 0, 4, 1));
        middle.HeaderPart.Bounds.ShouldBe(new Rect(-5, 0, 10, 1));
        middle.HeaderPart.Content.ShouldNotBeNull().Bounds.ShouldBe(new Rect(-4, 0, 8, 1));
        surface.ShouldRender("""
            bled │ 界 
            ──────────
            Wide body

            """);
        surface.Cell(new Point(7, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(8, 0)).IsContinuation.ShouldBeTrue();

        // Act resize
        await surface.ResizeAsync(new Size(20, 4));

        // Assert reflow
        tabs.HeaderOffset.ShouldBe(1);
        surface.ShouldRender("""
            One │ Disabled │ 界 
            ────────────────────
            Wide body

            """);
    }

    /// <summary>Verifies a one-cell strip exposes the selected label and resize restores its content region.</summary>
    [Fact]
    public async Task ResizeAsync_WhenSurfaceStartsTiny_RevealsHeaderAndContentWithoutStaleCellsAsync()
    {
        // Arrange
        var item = Create("General", "Body");
        var tabs = Create(style: null, item);
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Assert tiny label clipping
        tabs.HeaderOffset.ShouldBe(1);
        surface.ShouldRender("G");
        item.Content.ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 1, 1, 0));

        // Act
        await surface.ResizeAsync(new Size(9, 3));

        // Assert expanded geometry
        tabs.HeaderOffset.ShouldBe(0);
        item.HeaderPart.Bounds.ShouldBe(new Rect(0, 0, 9, 1));
        item.Content.ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 2, 9, 1));
        surface.ShouldRender("""
             General 
            ─────────
            Body
            """);
    }

    private static TabControl Create(ControlStyle<TabControl>? style, params TabItem[] items)
    {
        var result = new TabControl
        {
            Style = style,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        foreach (var item in items)
        {
            result.Items.Add(item);
        }

        return result;
    }

    private static TabItem Create(string header, string content) => new()
    {
        Header = header,
        Content = new ControlText(content),
    };
}
