// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies StatusBar layout and interaction after mount: item add/remove/clear, alignment
/// and spacing changes, the documented truncation order under constrained width, one-cell separator
/// precedence, hidden versus collapsed items, separator glyph changes, retained content focus, and
/// disabled content.</summary>
public sealed class StatusBarInteractionTests
{
    /// <summary>Verifies items added, inserted, removed, and cleared after layout re-arrange the row
    /// exactly and never leave stale cells behind.</summary>
    [Fact]
    public async Task Items_WhenMutatedAfterLayout_RearrangesRowWithoutStaleCellsAsync()
    {
        // Arrange
        var bar = new StatusBar();
        var ready = Item("Ready");
        bar.Items.Add(ready);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(16, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("Ready           ");
        var mode = Item("INS");
        var position = Item("1:1", StatusBarItemAlignment.Right);

        // Act and assert
        await surface.UpdateAsync(() => bar.Items.Add(position), "append a right item");
        surface.ShouldRender("Ready        1:1");
        await surface.UpdateAsync(() => bar.Items.Insert(0, mode), "insert a left item first");
        surface.ShouldRender("INS Ready    1:1");
        await surface.UpdateAsync(() => bar.Items.Remove(ready).ShouldBeTrue(), "remove the middle item");
        surface.ShouldRender("INS          1:1");
        ready.Parent.ShouldBeNull();
        await surface.UpdateAsync(bar.Items.Clear, "clear every item");
        surface.ShouldRender("                ");
        mode.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies changing an item's Alignment after layout moves it between the edge groups
    /// while the collection order is preserved.</summary>
    [Fact]
    public async Task Alignment_WhenChangedAfterLayout_MovesItemBetweenEdgeGroupsAsync()
    {
        // Arrange
        var bar = new StatusBar();
        var first = Item("A");
        var second = Item("B");
        bar.Items.Add(first);
        bar.Items.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(8, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("A B     ");

        // Act
        await surface.UpdateAsync(() => first.Alignment = StatusBarItemAlignment.Right, "send the first item right");

        // Assert
        surface.ShouldRender("B      A");
        bar.Items.IndexOf(first).ShouldBe(0);
        await surface.UpdateAsync(() => second.Alignment = StatusBarItemAlignment.Right, "send both right");
        surface.ShouldRender("     A B");
    }

    /// <summary>Verifies the documented truncation order under constrained width: the trailing
    /// group keeps its space from the right, earlier trailing items shrink next, and leading items
    /// receive only what remains.</summary>
    [Theory]
    [InlineData(16, "Ready Ln 1 UTF-8")]
    [InlineData(12, "R Ln 1 UTF-8")]
    [InlineData(8, "Ln UTF-8")]
    [InlineData(5, "UTF-8")]
    [InlineData(3, "UTF")]
    public async Task Arrange_WhenWidthIsConstrained_YieldsLeadingThenEarlierTrailingItemsAsync(int width, string expected)
    {
        // Arrange
        var bar = new StatusBar();
        var ready = Item("Ready");
        var line = Item("Ln 1", StatusBarItemAlignment.Right);
        var encoding = Item("UTF-8", StatusBarItemAlignment.Right);
        bar.Items.Add(ready);
        bar.Items.Add(line);
        bar.Items.Add(encoding);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(width, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(expected.PadRight(width));
        encoding.Bounds.Right.ShouldBeLessThanOrEqualTo(width);
        (ready.Bounds.Right <= line.Bounds.X || ready.Bounds.Width == 0).ShouldBeTrue();
    }

    /// <summary>Verifies a resize re-runs the truncation order in both directions.</summary>
    [Fact]
    public async Task ResizeAsync_WhenWidthShrinksAndGrows_ReflowsTheRowAsync()
    {
        // Arrange
        var bar = new StatusBar();
        bar.Items.Add(Item("Ready"));
        bar.Items.Add(Item("UTF-8", StatusBarItemAlignment.Right));
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("Ready  UTF-8");

        // Act and assert
        await surface.ResizeAsync(new Size(7, 1));
        surface.ShouldRender("R UTF-8");
        await surface.ResizeAsync(new Size(14, 1));
        surface.ShouldRender("Ready    UTF-8");
    }

    /// <summary>Verifies both separators frame the content and, at a one-cell width, only the left
    /// separator survives while content and the right separator get no cells.</summary>
    [Fact]
    public async Task Separators_WhenWidthIsOneCell_KeepOnlyTheLeftSeparatorAsync()
    {
        // Arrange
        var bar = new StatusBar();
        var text = new ControlText("ab");
        var item = new StatusBarItem
        {
            Content = text,
            ShowLeftSeparator = true,
            ShowRightSeparator = true
        };
        bar.Items.Add(item);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("│ab│");

        // Act
        await surface.ResizeAsync(new Size(1, 1));

        // Assert
        surface.ShouldRender("│");
        text.Bounds.Width.ShouldBe(0);

        // Act two cells: the right separator takes the last distinct cell before any content
        await surface.ResizeAsync(new Size(2, 1));
        surface.ShouldRender("││");
        text.Bounds.Width.ShouldBe(0);
        await surface.ResizeAsync(new Size(3, 1));

        // Assert
        surface.ShouldRender("│a│");
        text.Bounds.Width.ShouldBe(1);
    }

    /// <summary>Verifies a hidden item keeps its layout space while a collapsed item releases both
    /// its width and its spacing, and restoring visibility brings it back.</summary>
    [Fact]
    public async Task Visibility_WhenItemHidesOrCollapses_RetainsOrReleasesSpaceAsync()
    {
        // Arrange
        var bar = new StatusBar();
        var first = Item("AA");
        var second = Item("BB");
        var third = Item("CC");
        bar.Items.Add(first);
        bar.Items.Add(second);
        bar.Items.Add(third);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("AA BB CC  ");

        // Act and assert
        await surface.UpdateAsync(() => second.Visibility = Visibility.Hidden, "hide the middle item");
        surface.ShouldRender("AA    CC  ");
        await surface.UpdateAsync(() => second.Visibility = Visibility.Collapsed, "collapse the middle item");
        surface.ShouldRender("AA CC     ");
        second.Bounds.Width.ShouldBe(0);
        await surface.UpdateAsync(() => second.Visibility = Visibility.Visible, "restore the middle item");
        surface.ShouldRender("AA BB CC  ");
    }

    /// <summary>Verifies Spacing changes after layout re-gap the items and a negative value is rejected.</summary>
    [Fact]
    public async Task Spacing_WhenChangedAfterLayout_RegapsItemsAsync()
    {
        // Arrange
        var bar = new StatusBar();
        bar.Items.Add(Item("A"));
        bar.Items.Add(Item("B"));
        bar.Items.Add(Item("C", StatusBarItemAlignment.Right));
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("A B      C");

        // Act and assert
        await surface.UpdateAsync(() => bar.Spacing = 3, "widen the spacing");
        surface.ShouldRender("A   B    C");
        await surface.UpdateAsync(() => bar.Spacing = 0, "remove the spacing");
        surface.ShouldRender("AB       C");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Spacing = -1);

        // Act squeeze: the spacing between groups is reserved before the trailing leading item
        await surface.UpdateAsync(() => bar.Spacing = 3, "widen the spacing again");
        await surface.ResizeAsync(new Size(8, 1));

        // Assert the second leading item is the one that yields
        surface.ShouldRender("A      C");
        bar.Items[1].Bounds.Width.ShouldBe(0);
    }

    /// <summary>Verifies separator glyph overrides assigned after layout repaint immediately and
    /// clearing them restores the styled glyph.</summary>
    [Fact]
    public async Task SeparatorGlyphs_WhenChangedAfterLayout_RepaintAndRestoreAsync()
    {
        // Arrange
        var bar = new StatusBar();
        var item = new StatusBarItem
        {
            Content = new ControlText("x"),
            ShowLeftSeparator = true,
            ShowRightSeparator = true
        };
        bar.Items.Add(item);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(3, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("│x│");

        // Act and assert
        await surface.UpdateAsync(
            () =>
            {
                item.LeftSeparator = StatusBarSeparatorGlyphs.Chevron;
                item.RightSeparator = StatusBarSeparatorGlyphs.Bullet;
            },
            "override both separators");
        surface.ShouldRender("›x•");
        await surface.UpdateAsync(
            () =>
            {
                item.LeftSeparator = null;
                item.RightSeparator = null;
            },
            "clear the overrides");
        surface.ShouldRender("│x│");
        await surface.UpdateAsync(() => item.ShowRightSeparator = false, "drop the right separator");
        surface.ShouldRender("│x ");
        _ = Should.Throw<ArgumentException>(() => item.LeftSeparator = new Rune('界'));
    }

    /// <summary>Verifies retained interactive content joins Tab traversal while the bar and its
    /// items stay outside it, and keyboard activation reaches the content.</summary>
    [Fact]
    public async Task Focus_WhenItemHostsAButton_TabReachesContentButNotTheBarAsync()
    {
        // Arrange
        var before = new Button("Before");
        var inside = new Button("Inside");
        var bar = new StatusBar();
        var item = new StatusBarItem { Content = inside };
        bar.Items.Add(item);
        var stack = new Stack();
        stack.Children.Add(before);
        stack.Children.Add(bar);
        var clicks = 0;
        inside.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(14, 5),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(before);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(inside);
        bar.CanTabStop.ShouldBeFalse();
        item.CanTabStop.ShouldBeFalse();
        await surface.Keyboard.PressAsync(Code.Enter);
        clicks.ShouldBe(1);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(before);
    }

    /// <summary>Verifies disabling the bar disables hosted content: it renders disabled, cannot be
    /// focused or clicked, and recovers when the bar is re-enabled.</summary>
    [Fact]
    public async Task IsEnabled_WhenBarIsDisabled_BlocksHostedContentAndRecoversAsync()
    {
        // Arrange
        var inside = new Button("Inside");
        var bar = new StatusBar();
        bar.Items.Add(new StatusBarItem { Content = inside });
        var clicks = 0;
        inside.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(14, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => bar.IsEnabled = false, "disable the bar");
        await surface.Pointer.ClickAsync(inside);

        // Assert
        clicks.ShouldBe(0);
        surface.ShouldHaveState(inside, VisualState.Disabled);
        inside.CanFocus.ShouldBeFalse();
        await surface.UpdateAsync(() => bar.IsEnabled = true, "re-enable the bar");
        await surface.Pointer.ClickAsync(inside);
        clicks.ShouldBe(1);
        surface.ShouldHaveFocus(inside);
    }

    private static StatusBarItem Item(string text, StatusBarItemAlignment alignment = StatusBarItemAlignment.Left) =>
        new() { Content = new ControlText(text), Alignment = alignment };
}
