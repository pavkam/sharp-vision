// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Proves tab selection and navigation through mounted terminal surfaces.</summary>
public sealed class TabControlSurfaceTests
{
    /// <summary>Verifies a lifecycle failure during mounted insertion leaves a rendered,
    /// keyboard-interactive page/header pair rather than a half-committed tab.</summary>
    [Fact]
    public async Task Insert_WhenPageLifecycleFails_RendersAndNavigatesCommittedTabsAsync()
    {
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(24, 4),
            TestContext.Current.CancellationToken);
        var first = new TabItem { HeaderText = "First", Content = new ControlText("First body") };
        first.ParentChanged += (_, _) =>
        {
            if (first.Parent is not null)
            {
                throw new InvalidOperationException("page lifecycle failed");
            }
        };

        await surface.UpdateAsync(
            () =>
            {
                var exception = Should.Throw<InvalidOperationException>(() => tabs.Items.Add(first));
                exception.Message.ShouldBe("page lifecycle failed");
            },
            "insert first tab through a failing lifecycle observer");
        await surface.UpdateAsync(
            () => tabs.Items.Add(new TabItem
            {
                HeaderText = "Second",
                Content = new ControlText("Second body")
            }),
            "insert second tab after lifecycle failure");

        tabs.SelectedItem.ShouldBeSameAs(first);
        surface.Cell(new Point(1, 0)).Text.ShouldBe("F");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("F");
        await surface.UpdateAsync(() => tabs.Focus().ShouldBeTrue(), "focus committed tab control");
        await surface.Keyboard.PressAsync(Code.Right);

        tabs.SelectedIndex.ShouldBe(1);
        surface.Cell(new Point(0, 2)).Text.ShouldBe("S");
    }

    /// <summary>Verifies selectable text follows the one presented page, preserves Unicode
    /// graphemes, and excludes both collapsed pages and private header chrome.</summary>
    [Fact]
    public async Task SelectableText_WhenSelectedPageChanges_ProjectsOnlyVisiblePageContentAsync()
    {
        // Arrange
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsTextSelectionEnabled = true
        };
        tabs.Items.Add(new TabItem { HeaderText = "First header", Content = new ControlText("Hidden page") });
        tabs.Items.Add(new TabItem { HeaderText = "Second header", Content = new ControlText("A\u0301界") });
        tabs.SelectedIndex = 1;
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;

        // Act
        await surface.UpdateAsync(
            () => snapshot = tabs.GetSelectableTextSnapshot(),
            "project selected tab text");

        // Assert
        snapshot.ShouldNotBeNull().Text.ShouldBe("A\u0301界");
        snapshot.IsAuthoritative.ShouldBeFalse();
        snapshot.Glyphs.Count.ShouldBe(2);
        snapshot.Glyphs[0].Range.ShouldBe(new Selection(0, 2));
        snapshot.Glyphs[1].Bounds.Width.ShouldBe(2);

        await surface.UpdateAsync(() => tabs.SelectedIndex = 0, "select first page");
        await surface.UpdateAsync(
            () => snapshot = tabs.GetSelectableTextSnapshot(),
            "project replacement selected tab text");

        snapshot.ShouldNotBeNull().Text.ShouldBe("Hidden page");
    }

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

    /// <summary>Verifies a locally assigned style's strip glyphs reach the rendered cells.
    ///
    /// <para>The two strip glyphs and two strip colors lived on the control class with no style
    /// type and no <c>styles.*</c> key, so nothing could reach them - ASCII divider and underline
    /// glyphs, which a terminal without dependable box-drawing coverage needs, had to be set per
    /// instance. This asserts the glyph actually drawn, not merely that the style resolved:
    /// resolving correctly while the control still read the code-owned registry is exactly the
    /// half-wired state a style-level test alone would call green. A leaf declares no theme
    /// section of its own any more, so a locally assigned Style is the only surviving door.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsTheStrip_DrawsTheAuthoredDividerAsync()
    {
        // Arrange
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HeaderWidth = Length.Cells(6),
            Style = TabControlStyle.Default with { DividerGlyph = new Rune('!'), UnderlineGlyph = new Rune('~') }
        };
        tabs.Items.Add(new TabItem { HeaderText = "One", Content = new ControlText("First") });
        tabs.Items.Add(new TabItem { HeaderText = "Two", Content = new ControlText("Second") });

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(6, 0)).Text.ShouldBe("!");
    }

    /// <summary>Verifies a live Theme swap that changes only the resolved "accent" color - leaving
    /// "control"'s own resolved appearance untouched - still repaints TabControlStyle's code-owned
    /// SelectionIndicatorColor default and still publishes an ActualStyle notification.
    ///
    /// <para>TabControl used to resolve <c>ActualStyle</c> by hand-calling
    /// <c>TabControlStyle.Definition.Resolve(Style, Theme)</c> instead of registering a primary
    /// style slot through <c>InitializeStyle</c>. Without that slot,
    /// <c>ControlBase.GetThemeChangeImpact</c> had nothing of TabControlStyle's own to inspect and
    /// fell back to comparing the generic "control" appearance, which this Theme pair deliberately
    /// keeps identical - so the regression this guards against would report
    /// <see cref="InvalidationImpact.None"/> and leave the indicator color stuck on the old
    /// Theme's value. A leaf declares no theme section of its own any more, so this now isolates
    /// the scenario through the one surviving lever: the semantic color TabControlStyle's
    /// code-owned default already points at (<see cref="SemanticColor.Accent"/>), which "control"
    /// itself never references.</para>
    /// </summary>
    [Fact]
    public async Task Theme_WhenOnlyAccentColorChanges_RepaintsTheSelectionIndicatorAsync()
    {
        // Arrange - two themes whose "control" section resolves byte-identically (same background
        // and foreground); only "accent" differs.
        var mounted = ThemeCatalog.Parse(ThemeJson.Create());
        var repainted = ThemeCatalog.Parse(ThemeJson.Create(accent: "#ffcc00"));
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
            mounted,
            TestContext.Current.CancellationToken);

        var indicatorBefore = surface.Cell(new Point(0, 1)).Style.Foreground;
        var controlBackgroundBefore = surface.Cell(new Point(1, 0)).Style.Background;
        var notifications = new List<string?>();
        tabs.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = repainted, "swap accent-only theme");

        // Assert - the style-owned color repaints to the new theme's resolved accent...
        var expectedIndicator = TerminalPalette.Project(repainted.ResolveColor(SemanticColor.Accent), ColorDepth.Basic16);
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(expectedIndicator);
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldNotBe(indicatorBefore);

        // ...while "control"'s own resolved background - untouched by the accent-only diff -
        // proves this was not a wholesale repaint that would make the isolation moot.
        surface.Cell(new Point(1, 0)).Style.Background.ShouldBe(controlBackgroundBefore);

        // ...and the primary-slot machinery published the ActualStyle notification the hand-rolled
        // implementation never claimed.
        notifications.ShouldContain(nameof(TabControl.ActualStyle));
    }

    /// <summary>Verifies a live Theme swap that changes only the resolved "controlBorder" color -
    /// leaving every other resolved value untouched - still repaints TabControlStyle's code-owned
    /// DividerColor default across the header row's divider cells.
    ///
    /// <para>The sibling of <see cref="Theme_WhenOnlyAccentColorChanges_RepaintsTheSelectionIndicatorAsync"/>
    /// for the strip's other code-owned color: <c>DividerColor</c> completes to
    /// <see cref="SemanticColor.ControlBorder"/>, and "control"'s own JSON border authors that
    /// foreground symbolically (<c>styles.control.normal.border.foreground</c> is itself the token
    /// "controlBorder", not a literal), so the resolved <c>ControlStyle</c> record is byte-identical
    /// between the two themes below even though the underlying "colors.controlBorder" literal
    /// differs. Only a repaint that genuinely re-resolves <c>DividerColor</c> against the newly
    /// mounted Theme - rather than reusing a literal cached from the first render - can move the
    /// divider cell's rendered foreground.</para>
    /// </summary>
    [Fact]
    public async Task Theme_WhenOnlyControlBorderColorChanges_RepaintsTheDividerAsync()
    {
        // Arrange - two themes whose "control" section resolves byte-identically (both author the
        // border foreground as the symbolic "controlBorder" token); only the underlying literal
        // "colors.controlBorder" differs.
        var mounted = ThemeCatalog.Parse(ThemeJson.Create());
        var repainted = ThemeCatalog.Parse(ThemeJson.Create(controlBorderForeground: "#33cc66"));
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
            mounted,
            TestContext.Current.CancellationToken);

        var dividerBefore = surface.Cell(new Point(6, 0)).Style.Foreground;

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = repainted, "swap controlBorder-only theme");

        // Assert - the divider repaints to the new theme's resolved controlBorder color...
        var expectedDivider = TerminalPalette.Project(repainted.ResolveColor(SemanticColor.ControlBorder), ColorDepth.Basic16);
        surface.Cell(new Point(6, 0)).Style.Foreground.ShouldBe(expectedDivider);
        surface.Cell(new Point(6, 0)).Style.Foreground.ShouldNotBe(dividerBefore);
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

    /// <summary>Verifies keyboard and pointer header behavior commit selection only on completed
    /// input, and the full TabControl disabled contract: direct and ancestor-inherited disabled
    /// state, stable geometry across a genuine resize, and re-enable recovery.</summary>
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

        // Assert cleanup preserves the completed selection and disabled appearance
        tabs.SelectedIndex.ShouldBe(1);
        tabs.IsPressed.ShouldBeFalse();
        tabs.HeaderAt(0).IsPressed.ShouldBeFalse();
        tabs.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(tabs, VisualState.Disabled);

        // Act re-enable
        await surface.UpdateAsync(() => tabs.IsEnabled = true, "re-enable TabControl");

        // Assert re-enable recovery: keyboard navigation resumes
        surface.ShouldHaveState(tabs, VisualState.Normal);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(tabs);
        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedIndex.ShouldBe(0);

        // Arrange a disabled TabControl and an independently-mounted enabled twin at the same size
        var disabledTwin = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsEnabled = false
        };
        disabledTwin.Items.Add(new TabItem { HeaderText = "One", Content = new ControlText("One") });
        disabledTwin.Items.Add(new TabItem { HeaderText = "Two", Content = new ControlText("Two") });
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabledTwin,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var enabledTwin = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        enabledTwin.Items.Add(new TabItem { HeaderText = "One", Content = new ControlText("One") });
        enabledTwin.Items.Add(new TabItem { HeaderText = "Two", Content = new ControlText("Two") });
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledTwin,
            new Size(20, 5),
            TestContext.Current.CancellationToken);

        // Act genuine resize of both twins to a different shared size
        await disabledSurface.ResizeAsync(new Size(14, 6));
        await enabledSurface.ResizeAsync(new Size(14, 6));

        // Assert stable geometry: disabling never perturbs layout
        disabledTwin.Bounds.ShouldBe(enabledTwin.Bounds);
        disabledTwin.DesiredSize.ShouldBe(enabledTwin.DesiredSize);

        // Arrange an ancestor container that owns a TabControl
        var ancestorTabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        ancestorTabs.Items.Add(new TabItem { HeaderText = "One", Content = new ControlText("One") });
        var ancestor = new Overlay { Children = { ancestorTabs } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            ancestor,
            new Size(20, 5),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor container
        await ancestorSurface.UpdateAsync(() => ancestor.IsEnabled = false, "disable ancestor container");

        // Assert the owned TabControl inherits Disabled without being disabled itself
        ancestorTabs.IsEnabled.ShouldBeTrue();
        ancestorSurface.ShouldHaveState(ancestorTabs, VisualState.Disabled);
    }

    /// <summary>Verifies a TabItem inherits Disabled from its owning TabControl, can also be
    /// disabled directly while the TabControl stays enabled, keeps stable geometry across a
    /// genuine resize while disabled, and recovers on re-enable.</summary>
    [Fact]
    public async Task Enabled_WhenOwnerOrItemToggles_UpdatesTabItemDisabledStateAsync()
    {
        // Arrange
        var first = new TabItem { HeaderText = "First", Content = new ControlText("First body") };
        var second = new TabItem { HeaderText = "Second", Content = new ControlText("Second body") };
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
        tabs.SelectedIndex.ShouldBe(0);

        // Act direct disable of the unselected item while the owning TabControl stays enabled.
        // The initially selected item is deliberately left alone here: TabControl auto-reselects
        // away from a directly-disabled *selected* item, which would collapse it and make it
        // useless for the geometry comparison below.
        await surface.UpdateAsync(() => second.IsEnabled = false, "disable one TabItem directly");

        // Assert direct disable does not disturb the owning TabControl or its selection
        tabs.IsEnabled.ShouldBeTrue();
        tabs.SelectedIndex.ShouldBe(0);
        second.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(second, VisualState.Disabled);
        first.EffectiveIsEnabled.ShouldBeTrue();

        // Act re-enable the item directly
        await surface.UpdateAsync(() => second.IsEnabled = true, "re-enable one TabItem directly");

        // Assert re-enable recovery
        second.EffectiveIsEnabled.ShouldBeTrue();
        surface.ShouldHaveState(second, VisualState.Normal);

        // Act disable the owning TabControl
        await surface.UpdateAsync(() => tabs.IsEnabled = false, "disable owning TabControl");

        // Assert ancestor-inherited disable reaches every owned TabItem without flipping their
        // own IsEnabled property or disturbing selection
        tabs.SelectedIndex.ShouldBe(0);
        first.IsEnabled.ShouldBeTrue();
        second.IsEnabled.ShouldBeTrue();
        first.EffectiveIsEnabled.ShouldBeFalse();
        second.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(first, VisualState.Disabled);
        surface.ShouldHaveState(second, VisualState.Disabled);

        // Act genuine resize while ancestor-disabled
        await surface.ResizeAsync(new Size(20, 5));

        // Arrange an independently-mounted enabled twin at the same size
        var comparisonFirst = new TabItem { HeaderText = "First", Content = new ControlText("First body") };
        var comparisonSecond = new TabItem { HeaderText = "Second", Content = new ControlText("Second body") };
        var comparisonTabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        comparisonTabs.Items.Add(comparisonFirst);
        comparisonTabs.Items.Add(comparisonSecond);
        await using var comparisonSurface = await ComponentSurface.MountAsync(
            comparisonTabs,
            new Size(20, 5),
            TestContext.Current.CancellationToken);

        // Assert stable geometry: disabling never perturbs layout
        first.Bounds.ShouldBe(comparisonFirst.Bounds);
        first.DesiredSize.ShouldBe(comparisonFirst.DesiredSize);

        // Act re-enable the owning TabControl
        await surface.UpdateAsync(() => tabs.IsEnabled = true, "re-enable owning TabControl");

        // Assert re-enable recovery
        first.EffectiveIsEnabled.ShouldBeTrue();
        second.EffectiveIsEnabled.ShouldBeTrue();
        surface.ShouldHaveState(first, VisualState.Normal);
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
        tabs.Items.Add(new TabItem { HeaderText = "Disabled", Content = new ControlText("Two"), IsEnabled = false });
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
        var first = new TabItem { HeaderText = "First", IsClosable = true, Content = new ControlText("One") };
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

    /// <summary>Verifies runtime-collapsing the selected page's TabItem on a mounted surface both
    /// repairs selection to the nearest eligible page and clears the previously selected page's
    /// stale rendered content from the content area - not merely from the header strip, which the
    /// unrendered-index math alone would not prove.</summary>
    [Fact]
    public async Task Content_WhenSelectedTabItemCollapsesAtRuntime_RepairsSelectionAndClearsStaleContentAsync()
    {
        // Arrange
        var first = new TabItem { HeaderText = "General", Content = new ControlText("General body") };
        var second = new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced body") };
        var third = new TabItem { HeaderText = "Options", Content = new ControlText("Options body") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        tabs.Items.Add(third);
        tabs.SelectedIndex = 1;
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(0, 2)).Text.ShouldBe("A");

        // Act
        await surface.UpdateAsync(() => second.Visibility = Visibility.Collapsed, "collapse selected TabItem");

        // Assert - selection moved off the now-collapsed page, and the content row shows only the
        // newly selected page's text, with no leftover "Advanced body" glyph anywhere on that row.
        tabs.SelectedIndex.ShouldNotBe(1);
        tabs.Items[tabs.SelectedIndex].ShouldNotBeSameAs(second);
        var selectedText = ((ControlText) tabs.Items[tabs.SelectedIndex].Content!).Content;
        surface.Cell(new Point(0, 2)).Text.ShouldBe(selectedText[..1]);

        var contentRow = new StringBuilder();
        for (var x = 0; x < 30; x++)
        {
            _ = contentRow.Append(surface.Cell(new Point(x, 2)).Text);
        }

        contentRow.ToString().ShouldNotContain("Advanced");
    }
}
