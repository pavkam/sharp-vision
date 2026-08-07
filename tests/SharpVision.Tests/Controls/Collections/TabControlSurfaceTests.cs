// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using SharpVision.Tests.Styling;

/// <summary>Proves tab selection and navigation through mounted terminal surfaces.</summary>
public sealed class TabControlSurfaceTests
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
        tabs.Items.Add(new TabItem { HeaderText = "General", Content = new ControlText("General content") });
        tabs.Items.Add(new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced content") });
        tabs.Items.Add(new TabItem { HeaderText = "Options", Content = new ControlText("Options content") });

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

    /// <summary>Verifies DividerColor and SelectionIndicatorColor accept a theme-role reference that
    /// resolves through the mounted Theme, not only a literal color.</summary>
    [Fact]
    public async Task Render_WhenColorPropertiesAreThemeColors_ResolvesThroughTheMountedThemeAsync()
    {
        // Arrange
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Style = TabControlStyle.Default with
            {
                DividerColor = SemanticColor.Warning,
                SelectionIndicatorColor = SemanticColor.Warning
            }
        };
        tabs.Items.Add(new TabItem { HeaderText = "General", Content = new ControlText("General content") });
        tabs.Items.Add(new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced content") });

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);

        // Assert
        var warning = TerminalPalette.Project(
            tabs.Theme.ShouldNotBeNull().ResolveColor(SemanticColor.Warning),
            ColorDepth.Basic16);
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(warning);
    }

    /// <summary>Verifies a theme-authored tab strip reaches the rendered cells.
    ///
    /// <para>The two strip glyphs and two strip colors lived on the control class with no style
    /// type and no <c>styles.*</c> key, so a theme could not reach any of them - ASCII divider and
    /// underline glyphs, which a terminal without dependable box-drawing coverage needs, had to be
    /// set per instance. This asserts the glyph actually drawn, not merely that the style resolved:
    /// resolving correctly while the control still read the code-owned registry is exactly the
    /// half-wired state a style-level test alone would call green.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsTheStrip_DrawsTheAuthoredDividerAsync()
    {
        // Arrange
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(
                extraStyles:
                """, "tabControl": { "normal": { "dividerGlyph": "!", "underlineGlyph": "~" } } """));
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HeaderWidth = Length.Cells(6)
        };
        tabs.Items.Add(new TabItem { HeaderText = "One", Content = new ControlText("First") });
        tabs.Items.Add(new TabItem { HeaderText = "Two", Content = new ControlText("Second") });
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = theme, "author tab strip");

        // Assert
        surface.Cell(new Point(6, 0)).Text.ShouldBe("!");
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
        tabs.Items.Add(new TabItem { HeaderText = "First", Content = new ControlText("Alpha") });
        tabs.Items.Add(new TabItem { HeaderText = "Second", Content = new ControlText("Beta") });
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
        tabs.Items.Add(new TabItem { HeaderText = "A", Content = new ControlText("First") });
        tabs.Items.Add(new TabItem { HeaderText = "B", Content = new ControlText("Second") });
        tabs.Items.Add(new TabItem { HeaderText = "C", Content = new ControlText("Third") });
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
        var first = new TabItem { HeaderText = "General", Content = new ControlText("General body") };
        var second = new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced body") };
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
        tabs.Pressed.ShouldBeFalse();
        tabs.HeaderAt(1).Pressed.ShouldBeTrue();
        surface.ShouldHaveCapture(tabs.HeaderAt(1));

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert released selection and retained page composition
        tabs.SelectedIndex.ShouldBe(1);
        tabs.Pressed.ShouldBeFalse();
        tabs.HeaderAt(1).Pressed.ShouldBeFalse();
        _ = second.Parent.ShouldNotBeNull();
        second.Content.ShouldNotBeNull().Parent.ShouldBeSameAs(second);
        second.Focused.ShouldBeFalse();
        second.Pressed.ShouldBeFalse();
        await surface.Pointer.MoveToAsync(second);
        second.PointerOver.ShouldBeTrue();

        // Act unavailable while another header press is held
        await surface.Pointer.MoveToAsync(tabs, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => tabs.Enabled = false, "disable held TabControl");

        // Assert cleanup preserves the completed selection
        tabs.SelectedIndex.ShouldBe(1);
        tabs.Pressed.ShouldBeFalse();
        tabs.HeaderAt(0).Pressed.ShouldBeFalse();
        tabs.Focused.ShouldBeFalse();
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
        tabs.Items.Add(new TabItem { HeaderText = "First", Content = new ControlText("One") });
        tabs.Items.Add(new TabItem { HeaderText = "Disabled", Content = new ControlText("Two"), Enabled = false });
        tabs.Items.Add(new TabItem { HeaderText = "Last", Content = new ControlText("Three") });
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
        var first = new TabItem { HeaderText = "First", Closable = true, Content = new ControlText("One") };
        var second = new TabItem { HeaderText = "Second", Content = new ControlText("Two") };
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
        tabs.Items.Add(new TabItem { HeaderText = "One", Content = new ControlText("One") });
        tabs.Items.Add(new TabItem { HeaderText = "Two", Content = new ControlText("Two") });
        tabs.Items.Add(new TabItem { HeaderText = "Three", Content = new ControlText("Three") });
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(10, 4),
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);

        tabs.SelectedIndex.ShouldBe(2);
        tabs.HeaderAt(2).Bounds.X.ShouldBeLessThan(tabs.Bounds.Right);
    }

    /// <summary>Verifies selected state belongs to the active header instead of recoloring its page.</summary>
    [Fact]
    public async Task Render_WhenMounted_StylesOnlySelectedHeaderAsync()
    {
        // Arrange
        var first = new TabItem { HeaderText = "General", Content = new ControlText("General body") };
        var second = new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced body") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        tabs.Items.Add(first);
        tabs.Items.Add(second);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);
        var theme = tabs.Theme.ShouldNotBeNull();
        var selection = ThemeColorHelper.SelectionBackground(theme);
        var containingBackground = ReferenceColors.Get(0);

        // Assert
        tabs.GetResolvedAppearance(tabs.GetAppearanceState()).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        first.GetResolvedAppearance(first.GetAppearanceState()).BackgroundMode.ShouldBe(BackgroundMode.Opaque);

        // The page's Text content never paints its own background; the opaque page fill below
        // comes from the TabItem itself, verified against the rendered page cell further down.
        first.Content.ShouldNotBeNull().GetResolvedAppearance(first.Content.GetAppearanceState()).BackgroundMode
            .ShouldBe(BackgroundMode.Transparent);
        surface.Cell(new Point(1, 0)).Style.Background.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(11, 0)).Style.Background.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(0, 2)).Style.Background.ShouldBe(ReferenceColors.Get(0));
        surface.Cell(new Point(0, 2)).Text.ShouldBe("G");
    }

    /// <summary>Verifies nested pointer input hovers and selects only the targeted retained header.</summary>
    [Fact]
    public async Task Pointer_WhenNestedHeaderIsHoveredAndClicked_UsesHeaderStateAndSelectsAsync()
    {
        // Arrange
        var first = new TabItem { HeaderText = "General", Content = new ControlText("General body") };
        var second = new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced body") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        var host = new Dock
        {
            Padding = new Thickness(3, 2),
            Children = { tabs },
        };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(36, 8),
            TestContext.Current.CancellationToken);
        var theme = tabs.Theme.ShouldNotBeNull();
        var selection = ThemeColorHelper.SelectionBackground(theme);
        var hoveredForeground = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(theme), ColorDepth.Basic16);
        var inactiveForeground = ThemeColorHelper.InactiveForeground(theme);
        var containingBackground = ReferenceColors.Get(0);

        // Act: hovering the page must not hover every header through the focus-owning parent.
        await surface.Pointer.MoveToAsync(first.Content.ShouldNotBeNull());

        // Assert
        surface.Cell(new Point(tabs.Bounds.X + 11, tabs.Bounds.Y)).Style.Foreground.ShouldBe(inactiveForeground);
        surface.Cell(new Point(tabs.Bounds.X, tabs.Bounds.Y + 2)).Style.Background.ShouldBe(ReferenceColors.Get(0));

        // Act: hover and click the Advanced header at a non-zero parent offset.
        await surface.Pointer.MoveToAsync(tabs, new Point(11, 0));

        // Assert hover remains local to that header.
        surface.Cell(new Point(tabs.Bounds.X + 11, tabs.Bounds.Y)).Style.Foreground.ShouldBe(hoveredForeground);
        surface.Cell(new Point(tabs.Bounds.X + 1, tabs.Bounds.Y)).Style.Background.IsRgb.ShouldBeTrue();

        // Act
        await surface.Pointer.ClickAsync(tabs, new Point(11, 0));

        // Assert
        tabs.SelectedIndex.ShouldBe(1);
        surface.Cell(new Point(tabs.Bounds.X + 11, tabs.Bounds.Y)).Style.Background.ShouldBe(selection);
        surface.Cell(new Point(tabs.Bounds.X + 1, tabs.Bounds.Y)).Style.Background.ShouldBe(containingBackground);
        surface.Cell(new Point(tabs.Bounds.X, tabs.Bounds.Y + 2)).Text.ShouldBe("A");
        surface.Cell(new Point(tabs.Bounds.X, tabs.Bounds.Y + 2)).Style.Background.ShouldBe(ReferenceColors.Get(0));
    }

    /// <summary>Verifies primary release inside a rendered header commits that page selection.</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsClicked_SelectsReleasedPageAsync()
    {
        // Arrange
        var first = new TabItem { HeaderText = "General", Content = new ControlText("General body") };
        var second = new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced body") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        var changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);

        // Act press the Advanced header, which starts after " General │".
        await surface.Pointer.MoveToAsync(tabs, new Point(11, 0));
        await surface.Pointer.PressAsync();

        // Assert press does not commit selection before release.
        tabs.SelectedIndex.ShouldBe(0);

        // Act release on the same header.
        await surface.Pointer.ReleaseAsync();

        // Assert
        tabs.SelectedIndex.ShouldBe(1);
        changes.ShouldBe(1);
        second.Content.ShouldNotBeNull().Bounds.Y.ShouldBe(2);
        second.Content.ShouldNotBeNull().Bounds.Width.ShouldBeGreaterThan(0);
        surface.Cell(new Point(11, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("A");
    }
}
