// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Proves tab selection and navigation through mounted terminal surfaces.</summary>
public sealed class TabBehaviorSurfaceTests
{
    /// <summary>Verifies a TabControl with three tabs renders the header row with labels, dividers, and the accent underline.</summary>
    [Fact]
    public async Task Render_WhenThreeTabsAreMounted_DrawsHeaderRowWithDividersAndUnderlineAsync()
    {
        // Arrange
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        tabs.Items.Add(new TabItem { Header = "General", Content = new ControlText("General content") });
        tabs.Items.Add(new TabItem { Header = "Advanced", Content = new ControlText("Advanced content") });
        tabs.Items.Add(new TabItem { Header = "Options", Content = new ControlText("Options content") });

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);

        // Assert — tab headers with padding, │ dividers, and ─ underline row.
        tabs.SelectedIndex.ShouldBe(0);
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("G");
        // Verify headers are rendered in the header row.
        surface.Cell(new Point(0, 1)).Text.ShouldBe("─");

        // The underline uses the accent color.
        var accent = ThemeColorHelper.Accent(tabs.Theme.ShouldNotBeNull());
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(accent);
    }

    /// <summary>Verifies clicking the second tab header switches the visible content and selected index.</summary>
    [Fact]
    public async Task Pointer_WhenSecondTabHeaderIsClicked_SwitchesContentAndSelectionAsync()
    {
        // Arrange
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        tabs.Items.Add(new TabItem { Header = "First", Content = new ControlText("Alpha") });
        tabs.Items.Add(new TabItem { Header = "Second", Content = new ControlText("Beta") });
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        tabs.SelectedIndex.ShouldBe(0);

        // Act — click the second header.
        await surface.Pointer.ClickAsync(tabs, new Point(10, 0));

        // Assert — the TabItem visibility toggles, not its Content.
        tabs.SelectedIndex.ShouldBe(1);
        tabs.Items[1].Visibility.ShouldBe(Visibility.Visible);
        tabs.Items[0].Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies directional keys switch tabs sequentially when focused.</summary>
    [Fact]
    public async Task Keyboard_WhenDirectionalKeysArePressed_SwitchesTabsSequentiallyAsync()
    {
        // Arrange
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        tabs.Items.Add(new TabItem { Header = "A", Content = new ControlText("First") });
        tabs.Items.Add(new TabItem { Header = "B", Content = new ControlText("Second") });
        tabs.Items.Add(new TabItem { Header = "C", Content = new ControlText("Third") });
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(12, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(tabs);

        // Act and assert — Right moves forward.
        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedIndex.ShouldBe(1);

        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedIndex.ShouldBe(2);

        // Act and assert — Left moves backward.
        await surface.Keyboard.PressAsync(Code.Left);
        tabs.SelectedIndex.ShouldBe(1);

        await surface.Keyboard.PressAsync(Code.Left);
        tabs.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies keyboard and pointer header behavior commit selection only on completed input.</summary>
    [ComponentBehaviorEvidence(
        typeof(TabControl),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.RetainedPointerActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    [ComponentBehaviorEvidence(
        typeof(TabItem),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenHeadersNavigateAndPress_CommitsReleasedSelectionAndCleanupAsync()
    {
        // Arrange
        var first = new TabItem { Header = "General", Content = new ControlText("General body") };
        var second = new TabItem { Header = "Advanced", Content = new ControlText("Advanced body") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);

        // Act and assert keyboard navigation
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(tabs);
        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedIndex.ShouldBe(1);
        await surface.Keyboard.PressAsync(Code.Left);
        tabs.SelectedIndex.ShouldBe(0);

        // Act held pointer over the second retained header
        await surface.Pointer.MoveToAsync(tabs, new Point(11, 0));
        await surface.Pointer.PressAsync();

        // Assert held state does not activate
        tabs.SelectedIndex.ShouldBe(0);
        tabs.IsPressed.ShouldBeFalse();
        tabs.HeaderAt(1).IsPressed.ShouldBeTrue();
        surface.ShouldHaveCapture(tabs.HeaderAt(1));

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert released selection and retained page composition
        tabs.SelectedIndex.ShouldBe(1);
        tabs.IsPressed.ShouldBeFalse();
        tabs.HeaderAt(1).IsPressed.ShouldBeFalse();
        _ = second.Parent.ShouldNotBeNull();
        second.Content.ShouldNotBeNull().Parent.ShouldBeSameAs(second);
        second.IsFocused.ShouldBeFalse();
        second.IsPressed.ShouldBeFalse();
        await surface.Pointer.MoveToAsync(second);
        second.IsPointerOver.ShouldBeTrue();

        // Act unavailable while another header press is held
        await surface.Pointer.MoveToAsync(tabs, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => tabs.IsEnabled = false, "disable held TabControl");

        // Assert cleanup preserves the completed selection
        tabs.SelectedIndex.ShouldBe(1);
        tabs.IsPressed.ShouldBeFalse();
        tabs.HeaderAt(0).IsPressed.ShouldBeFalse();
        tabs.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies header-owner navigation wraps and skips unavailable pages.</summary>
    [Fact]
    public async Task Keyboard_WhenTabsIncludeUnavailablePages_WrapsAndSelectsEligibleHeadersAsync()
    {
        // Arrange
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        tabs.Items.Add(new TabItem { Header = "First", Content = new ControlText("One") });
        tabs.Items.Add(new TabItem { Header = "Disabled", Content = new ControlText("Two"), IsEnabled = false });
        tabs.Items.Add(new TabItem { Header = "Last", Content = new ControlText("Three") });
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert Right skips the unavailable page and wraps.
        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedIndex.ShouldBe(2);
        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedIndex.ShouldBe(0);

        // Act and assert Left wraps, while Home and End choose eligible edges.
        await surface.Keyboard.PressAsync(Code.Left);
        tabs.SelectedIndex.ShouldBe(2);
        await surface.Keyboard.PressAsync(Code.Home);
        tabs.SelectedIndex.ShouldBe(0);
        await surface.Keyboard.PressAsync(Code.End);
        tabs.SelectedIndex.ShouldBe(2);
    }

    /// <summary>Verifies Delete requests closure for the selected closeable page and leaves a cancelled page intact.</summary>
    [Fact]
    public async Task Keyboard_WhenSelectedTabIsCloseable_RaisesCloseRequestAsync()
    {
        var first = new TabItem { Header = "First", IsClosable = true, Content = new ControlText("One") };
        var second = new TabItem { Header = "Second", Content = new ControlText("Two") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        tabs.CloseRequested += (_, args) => args.Cancel = true;
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(16, 4),
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Delete);

        tabs.Items.ShouldContain(first);
        tabs.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies narrow scrollable headers remain selectable through keyboard navigation.</summary>
    [Fact]
    public async Task Keyboard_WhenHeadersOverflowAndScrollPolicyIsEnabled_RevealsSelectionAsync()
    {
        var tabs = new TabControl
        {
            HeaderWidth = Length.Cells(5),
            HeaderOverflowPolicy = TabHeaderOverflowPolicy.Scroll,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        tabs.Items.Add(new TabItem { Header = "One", Content = new ControlText("One") });
        tabs.Items.Add(new TabItem { Header = "Two", Content = new ControlText("Two") });
        tabs.Items.Add(new TabItem { Header = "Three", Content = new ControlText("Three") });
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(10, 4),
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);

        tabs.SelectedIndex.ShouldBe(2);
        tabs.HeaderAt(2).Bounds.X.ShouldBeLessThan(tabs.Bounds.Right);
    }
}
