// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

/// <summary>Verifies the presentation of six controls that once kept chrome glyphs on the control
/// class - Expander, MenuItem, MenuSeparator, NavigationViewGroup, NavigationViewItem, and
/// NavigationViewSeparator - plus several other structural/glyph members (Table's grid glyphs and
/// part colors, StatusBarItem's separators, Text's ellipsis marker, Chart's glyph family, JsonView's
/// disclosure pair, Calendar's navigation arrows) that a theme could once reach through the
/// control's own leaf section.
///
/// <para>A theme's "styles" object is now closed to exactly the six well-known role sections, so a
/// leaf resolves no section of its own any more: these members are reachable only through a
/// locally assigned <c>Style</c>, never through a theme. Every test below that once authored a
/// theme section now assigns a local style instead, and asserts through a rendered cell rather than
/// the resolved style alone wherever a render-level sibling did not already exist elsewhere -
/// resolving correctly is not evidence that a control's render path actually reads the resolved
/// value.</para>
/// </summary>
public sealed class StrandedGlyphThemingTests
{
    /// <summary>Verifies a locally assigned style reaches the expander's disclosure arrow.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsExpanderGlyphs_DrawsThemAsync()
    {
        var expander = new Expander
        {
            HeaderText = "Section",
            IsExpanded = true,
            Style = ExpanderStyle.Default with { ExpandedGlyph = new Rune('-'), CollapsedGlyph = new Rune('+') }
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(20, 4),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(0, 0)).Text.ShouldBe("-");
    }

    /// <summary>Verifies the collapsed arrow follows the same style, so it cannot half-move a
    /// two-state pair.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsExpanderGlyphs_DrawsTheCollapsedOneTooAsync()
    {
        var expander = new Expander
        {
            HeaderText = "Section",
            IsExpanded = false,
            Style = ExpanderStyle.Default with { ExpandedGlyph = new Rune('-'), CollapsedGlyph = new Rune('+') }
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(20, 4),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(0, 0)).Text.ShouldBe("+");
    }

    /// <summary>Verifies a locally assigned style reaches the menu divider - the case that made the
    /// library hold two separators disagreeing about who owns a divider glyph.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsMenuSeparatorGlyph_DrawsItAsync()
    {
        var separator = new MenuSeparator { Style = MenuSeparatorStyle.Default with { Glyph = new Rune('=') } };
        await using var surface = await ComponentSurface.MountAsync(
            separator,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(0, 0)).Text.ShouldBe("=");
    }

    /// <summary>Verifies the same for the navigation divider, the third separator in the family.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsNavigationSeparatorGlyph_DrawsItAsync()
    {
        var separator = new NavigationViewSeparator
        {
            Style = NavigationViewSeparatorStyle.Default with { Glyph = new Rune('~') }
        };
        await using var surface = await ComponentSurface.MountAsync(
            separator,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(0, 0)).Text.ShouldBe("~");
    }

    /// <summary>Verifies a themed check marker reaches a rendered menu row, which the resolved
    /// style alone does not establish.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsMenuItemMarkers_DrawsTheCheckedOneAsync()
    {
        var item = new MenuItem
        {
            Text = "Toggle",
            Kind = MenuItemKind.Check,
            IsChecked = true,
            Style = MenuItemStyle.Default with { CheckedGlyph = new Rune('x') }
        };
        var menu = new Menu { Orientation = Orientation.Vertical, Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        Row(surface, 0, 20).ShouldContain("x");
    }

    /// <summary>Verifies the radio kind reads its own pair, so a mixed menu cannot render
    /// half-styled. That is why the style carries both pairs rather than one: an entry's kind is a
    /// semantic fact the entry owns, not a presentation choice a style makes.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsMenuItemMarkers_DrawsTheRadioPairAsync()
    {
        var item = new MenuItem
        {
            Text = "Choice",
            Kind = MenuItemKind.Radio,
            IsChecked = true,
            Style = MenuItemStyle.Default with { RadioCheckedGlyph = new Rune('*') }
        };
        var menu = new Menu { Orientation = Orientation.Vertical, Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        Row(surface, 0, 20).ShouldContain("*");
    }

    /// <summary>Verifies a themed navigation marker reaches a rendered row.
    ///
    /// <para>A style resolving correctly is not evidence for this. A style can resolve perfectly
    /// while the control still reads the hardcoded registry - reverting the render site to
    /// <c>ControlGlyphs.Navigation.ItemIdle</c> left every style-layer test green, which is exactly
    /// how this class of wiring gap survives.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsNavigationItemMarkers_DrawsThemAsync()
    {
        var view = NavigationViewWith(new NavigationViewItem
        {
            Text = "Home",
            Style = NavigationViewItemStyle.Default with { IdleMarker = new Rune('#') }
        });
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(16, 4),
            TestContext.Current.CancellationToken);

        Row(surface, 0, 16).ShouldContain("#");
    }

    /// <summary>Verifies a themed group disclosure glyph reaches a rendered row, for the same
    /// reason.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsNavigationGroupGlyphs_DrawsThemAsync()
    {
        var group = new NavigationViewGroup
        {
            Header = "Tools",
            IsExpanded = true,
            Style = NavigationViewGroupStyle.Default with { ExpandedGlyph = new Rune('-'), CollapsedGlyph = new Rune('+') }
        };
        group.Items.Add(new NavigationViewItem { Text = "Edit" });
        var view = NavigationViewWith(group);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(16, 5),
            TestContext.Current.CancellationToken);

        Row(surface, 0, 16).ShouldContain("-");
    }

    /// <summary>Verifies a locally styled table reaches the grid glyphs at the rendered cell. Table
    /// was the largest remaining case - seven members, three of them colors - and the only one where
    /// the control already participated in the styling engine through a part slot while owning no
    /// primary style of its own.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsTableGlyphs_DrawsThemAsync()
    {
        var table = new Table
        {
            Width = Length.Cells(14),
            ShowGridLines = true,
            Style = TableStyle.Default with
            {
                Glyphs = TableStyle.Default.Glyphs with { Horizontal = new Rune('='), Vertical = new Rune('!') }
            }
        };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Columns.Add(TableColumn.Auto("B"));
        table.Rows.Add(new TableRow([new ControlText { Content = "1" }, new ControlText { Content = "2" }]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(14, 5),
            TestContext.Current.CancellationToken);

        var rendered = string.Concat(Enumerable.Range(0, 5).Select(y => Row(surface, y, 14)));
        rendered.ShouldContain("=");
        rendered.ShouldContain("!");
    }

    /// <summary>Verifies a styled cell padding actually reaches layout, not just the resolved style.
    ///
    /// <para>Nothing in the repository pinned this before the move - replacing the presenter's read
    /// with a hardcoded <c>default</c> left every Table suite green.</para>
    /// </summary>
    [Fact]
    public void Measure_WhenLocalStyleSetsTableCellPadding_WidensTheTable()
    {
        var padded = MeasuredTableWidth(TableStyle.Default with { CellPadding = new Thickness(3, 0) });
        var unpadded = MeasuredTableWidth(null);

        padded.ShouldBe(unpadded + 6, "one column, three cells of padding on each side");
    }

    /// <summary>Verifies an unstyled table leaves the three nullable part colors inheriting. They
    /// stayed nullable on purpose: null means "inherit the table's own resolved face", which no
    /// fixed <c>ControlColor</c> can express, and for the header background it also decides whether
    /// the header row is filled at all.</summary>
    [Fact]
    public void Resolve_WhenNoLocalStyleIsAssigned_LeavesPartColorsInheriting()
    {
        var style = TableStyle.Definition.Resolve(null, ThemeCatalog.Parse(ThemeJson.Create()));

        style.HeaderForeground.ShouldBeNull();
        style.HeaderBackground.ShouldBeNull();
        style.GridLineColor.ShouldBeNull();
    }

    /// <summary>Verifies the overlay's nullable-leaf branch resolves an explicit JSON null back to
    /// inheriting - the mechanic the three nullable part colors need, and the reason the overlay
    /// needed nullable leaf support at all. Exercised directly against <see cref="Theme.Overlay"/> -
    /// a leaf declares no theme section of its own to carry this scenario through any more, but the
    /// mechanic is unrelated to which section context reaches it.</summary>
    [Fact]
    public void Overlay_WhenAnExplicitNullPartColor_ReturnsToInheriting()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var result = (TableStyle) theme.Overlay(
            TableStyle.Default with { HeaderForeground = SemanticColor.Accent },
            ParseOverrides(/*lang=json,strict*/ """{"headerForeground":null}"""),
            "styles.table.normal");

        result.HeaderForeground.ShouldBeNull();
    }

    /// <summary>Verifies an unstyled table still resolves the placeholder colors to the documented
    /// Muted/Error defaults rather than leaving them unset, since they are required members with no
    /// null state to fall back to - unlike the nullable trio above, a synthetic status indicator has
    /// no table face to fall back to, the same shape <c>TreeViewStyle.LoadingColor</c>/
    /// <c>FailedColor</c> already use.</summary>
    [Fact]
    public void Resolve_WhenNoLocalStyleIsAssigned_UsesDocumentedPlaceholderDefaults()
    {
        var style = TableStyle.Definition.Resolve(null, ThemeCatalog.Parse(ThemeJson.Create()));

        style.PlaceholderForeground.ShouldBe((ControlColor) SemanticColor.Muted);
        style.PlaceholderErrorForeground.ShouldBe((ControlColor) SemanticColor.Error);
    }

    /// <summary>Verifies a locally styled status bar reaches the separator glyph at the rendered
    /// cell.
    ///
    /// <para>The item's separator used to be one nullable Rune carrying two facts: whether a
    /// separator exists, and which glyph it is. Presence is now <c>ShowLeftSeparator</c>, which
    /// reserves the cell, and the glyph is the style's.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsStatusBarSeparators_DrawsThemAsync()
    {
        var item = new StatusBarItem
        {
            ShowLeftSeparator = true,
            Content = new ControlText { Content = "Ready" },
            Style = StatusBarItemStyle.Default with { LeftSeparatorGlyph = new Rune('#'), RightSeparatorGlyph = new Rune('%') }
        };
        var bar = new StatusBar { Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        Row(surface, 0, 20).ShouldContain("#");
    }

    /// <summary>Verifies a per-item override still beats the styled glyph. Splitting presence out
    /// must not cost the per-item choice the showcase relies on.</summary>
    [Fact]
    public void ActualSeparator_WhenTheItemOverridesTheGlyph_KeepsTheOverrideUnderALocalStyle()
    {
        using var item = new StatusBarItem
        {
            ShowLeftSeparator = true,
            LeftSeparator = StatusBarSeparatorGlyphs.Chevron,
            Style = StatusBarItemStyle.Default with { LeftSeparatorGlyph = new Rune('#'), RightSeparatorGlyph = new Rune('%') }
        };

        item.ActualLeftSeparator.ShouldBe(StatusBarSeparatorGlyphs.Chevron);
        item.ActualRightSeparator.ShouldBe(new Rune('%'), "the unoverridden side still follows the style");
    }

    /// <summary>The counter-case for the split: an item that shows no separator reserves no cell, so
    /// a styled glyph cannot make one appear.</summary>
    [Fact]
    public async Task Render_WhenTheItemShowsNoSeparator_DrawsNoneEvenWithALocalStyleAsync()
    {
        var item = new StatusBarItem
        {
            Content = new ControlText { Content = "Ready" },
            Style = StatusBarItemStyle.Default with { LeftSeparatorGlyph = new Rune('#'), RightSeparatorGlyph = new Rune('%') }
        };
        var bar = new StatusBar { Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        Row(surface, 0, 20).ShouldNotContain("#");
    }

    /// <summary>Verifies a locally styled control reaches the truncation marker at the rendered
    /// cell.
    ///
    /// <para>This has the widest blast radius of any single glyph in the library - every elided
    /// string renders it.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsTheEllipsis_DrawsItAsync()
    {
        var text = new ControlText
        {
            Content = "Truncate me please",
            Overflow = Overflow.Ellipsis,
            Style = TextStyle.Default with { EllipsisGlyph = new Rune('>') }
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        Row(surface, 0, 8).ShouldEndWith(">");
    }

    /// <summary>The counter-case: an unstyled control keeps the code-owned marker.</summary>
    [Fact]
    public async Task Render_WhenNoLocalStyleIsAssigned_KeepsTheCodeOwnedMarkerAsync()
    {
        var text = new ControlText { Content = "Truncate me please", Overflow = Overflow.Ellipsis };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        Row(surface, 0, 8).ShouldNotContain(">");
    }

    /// <summary>Verifies a local style wins over the code-owned default, and that assigning null
    /// returns the control to that code-owned default - the escape hatch the old
    /// <c>ResetGlyphs()</c> never provided, since it restored code-owned values by a different
    /// door than the one every other reset now shares.</summary>
    [Fact]
    public void ActualStyle_WhenALocalStyleOverridesTheEllipsis_WinsThenClearingRestoresTheCodeOwnedDefault()
    {
        using var text = new ControlText { Content = "x" };

        text.ActualStyle.EllipsisGlyph.ShouldBe(TextStyle.Default.EllipsisGlyph);

        text.Style = TextStyle.Default with { EllipsisGlyph = new Rune('~') };
        text.ActualStyle.EllipsisGlyph.ShouldBe(new Rune('~'));

        text.Style = null;
        text.ActualStyle.EllipsisGlyph.ShouldBe(TextStyle.Default.EllipsisGlyph);
    }

    /// <summary>Verifies the chart glyph family can be authored through the overlay engine at all.
    ///
    /// <para><c>ChartGlyphs</c> was the only glyph family in the library that was not a record and
    /// did not implement the fragment interface, so <c>Theme.Overlay</c> never recursed into it and
    /// its JSON fell through to <c>JsonSerializer</c>. Depending on which path that took, an author
    /// got either an opaque "styles.chart.normal.glyphs is invalid" or a silent fall back to the
    /// code-owned family - and <c>ChartStyle.Glyphs</c>'s getter swallowed the silent case with a
    /// <c>field == default ? Default : field</c> guard. Exercised directly against
    /// <see cref="Theme.Overlay"/> since "chart" is no longer an authorable theme section, but the
    /// fragment-conformance regression this guards is about the type, not the section.</para>
    /// </summary>
    [Fact]
    public void Overlay_WhenAuthoringChartGlyphs_AppliesThem()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var result = (ChartStyle) theme.Overlay(
            ChartStyle.Default,
            ParseOverrides(/*lang=json,strict*/ """{"glyphs":{"bar":"#","verticalAxis":"!","horizontalAxis":"="}}"""),
            "styles.chart.normal");

        result.Glyphs.Bar.ShouldBe(new Rune('#'));
        result.Glyphs.VerticalAxis.ShouldBe(new Rune('!'));
        result.Glyphs.HorizontalAxis.ShouldBe(new Rune('='));
        result.Glyphs.Point.ShouldBe(ChartGlyphs.Default.Point, "an unauthored member keeps the code-owned glyph");
    }

    /// <summary>Verifies an invalid chart glyph is rejected with the dotted path rather than
    /// swallowed. The old getter's default-guard meant this could once fail silently.</summary>
    [Fact]
    public void Overlay_WhenAuthoringAWideChartGlyph_RejectsIt()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        Should.Throw<InvalidDataException>(() => theme.Overlay(
                ChartStyle.Default,
                ParseOverrides(/*lang=json,strict*/ """{"glyphs":{"bar":"漢"}}"""),
                "styles.chart.normal"))
            .Message.ShouldContain("styles.chart.normal.glyphs.bar");
    }

    /// <summary>Verifies a locally styled axis rule reaches a rendered chart cell.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsTheChartAxis_DrawsItAsync()
    {
        var chart = new VerticalBarChart
        {
            Series = new[] { new ChartSeries("S", [new ChartDataPoint("a", 1), new ChartDataPoint("b", 2)]) },
            Style = ChartStyle.Default with
            {
                Glyphs = ChartStyle.Default.Glyphs with { HorizontalAxis = new Rune('=') }
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(20, 8),
            TestContext.Current.CancellationToken);

        var rendered = string.Concat(Enumerable.Range(0, 8).Select(y => Row(surface, y, 20)));
        rendered.ShouldContain("=");
    }

    /// <summary>Verifies a locally styled disclosure arrow reaches a rendered JsonView row. The
    /// arrow is part of the measured line text and hit-testing measures it to compute the clickable
    /// span, so the three former literals had to stay in lockstep by hand.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsJsonViewDisclosure_DrawsItAsync()
    {
        var view = new JsonView
        {
            Json = _nestedJson,
            Style = JsonViewStyle.Default with { CollapsedGlyph = new Rune('+'), ExpandedGlyph = new Rune('-') }
        };
        view.ExpandAll();
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(24, 5),
            TestContext.Current.CancellationToken);

        view.ActualStyle.ExpandedGlyph.ShouldBe(new Rune('-'), "the style must resolve before the render can");

        var rendered = string.Concat(Enumerable.Range(0, 5).Select(y => Row(surface, y, 24)));
        rendered.ShouldContain("-");
    }

    /// <summary>Verifies a locally styled month-navigation arrow reaches a rendered Calendar.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleSetsCalendarNavigation_DrawsItAsync()
    {
        using var calendar = new UiCalendar
        {
            Style = CalendarStyle.Default with { PreviousMonthGlyph = new Rune('{'), NextMonthGlyph = new Rune('}') }
        };
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(24, 10),
            TestContext.Current.CancellationToken);

        var rendered = string.Concat(Enumerable.Range(0, 10).Select(y => Row(surface, y, 24)));
        rendered.ShouldContain("{");
        rendered.ShouldContain("}");
    }

    /// <summary>Verifies the popup anchor arrows are theme-reachable through "popup" - one of the
    /// six well-known role sections, and critically, run through the same width resolution their
    /// eight border neighbours get. All four are East Asian Ambiguous, so under
    /// <c>Ambiguous.Wide</c> an unresolved arrow measures two cells and overruns its one-cell frame
    /// slot.</summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsPopupAnchorGlyphs_AppliesThem()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create().Replace(
            """"popup": { "normal": { "border": { "sides":"all", "glyphStyle":"rounded" } } }"""",
            """"popup": { "normal": { "border": { "sides":"all", "glyphStyle":"rounded" }, "anchorGlyphs": { "pointingUp": "^", "pointingDown": "v" } } }"""",
            StringComparison.Ordinal));

        var anchors = theme.GetStyleSet(PopupStyle.Default).Normal.AnchorGlyphs;

        anchors.PointingUp.ShouldBe(new Rune('^'));
        anchors.PointingDown.ShouldBe(new Rune('v'));
        anchors.PointingLeft.ShouldBe(PopupAnchorGlyphs.Default.PointingLeft);
    }

    /// <summary>The counter-case: an unauthored theme keeps the code-owned arrows, so the fifteen
    /// bundled themes draw the same frame they always did.</summary>
    [Fact]
    public void Resolve_WhenNoThemeAuthorsPopupAnchorGlyphs_KeepsTheCodeOwnedArrows()
    {
        var anchors = ThemeCatalog.Parse(ThemeJson.Create())
            .GetStyleSet(PopupStyle.Default).Normal.AnchorGlyphs;

        anchors.ShouldBe(PopupAnchorGlyphs.Default);
    }

    /// <summary>Verifies every one of the six controls whose chrome glyphs used to live on the
    /// control class keeps its code-owned default when no local Style is assigned. A leaf resolves
    /// no theme section of its own any more, so an unstyled resolution is the only baseline left to
    /// pin - the fifteen bundled themes, none of which could author any of these members any more,
    /// render exactly as this asserts.</summary>
    [Fact]
    public void Resolve_WhenNoLocalStyleIsAssigned_KeepsEveryCodeOwnedGlyph()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        ExpanderStyle.Definition.Resolve(null, theme).CollapsedGlyph
            .ShouldBe(ControlGlyphs.Disclosure.Collapsed.Value);
        MenuSeparatorStyle.Definition.Resolve(null, theme).Glyph
            .ShouldBe(ControlGlyphs.Separators.Menu.Value);
        MenuItemStyle.Definition.Resolve(null, theme).CheckedGlyph
            .ShouldBe(ControlGlyphs.Selection.MenuCheckChecked.Value);
        NavigationViewSeparatorStyle.Definition.Resolve(null, theme).Glyph
            .ShouldBe(ControlGlyphs.Navigation.Separator.Value);
        NavigationViewGroupStyle.Definition.Resolve(null, theme).CollapsedGlyph
            .ShouldBe(ControlGlyphs.Navigation.GroupCollapsed.Value);
        NavigationViewItemStyle.Definition.Resolve(null, theme).IdleMarker
            .ShouldBe(ControlGlyphs.Navigation.ItemIdle.Value);
    }

    private static Dictionary<string, JsonElement> ParseOverrides(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static int MeasuredTableWidth(TableStyle? style)
    {
        using var table = new Table { Style = style };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Rows.Add(new TableRow([new ControlText { Content = "Value" }]));
        new LayoutEngine().Layout(table, new Size(60, 10));
        return table.DesiredSize.Width;
    }

    [StringSyntax(StringSyntaxAttribute.Json)]
    private const string _nestedJson = """{"a":{"b":1}}""";

    private static NavigationView NavigationViewWith(NavigationViewItem entry)
    {
        var view = NewView();
        view.Items.Add(entry);
        return view;
    }

    private static NavigationView NavigationViewWith(NavigationViewGroup entry)
    {
        var view = NewView();
        view.Items.Add(entry);
        return view;
    }

    private static NavigationView NewView() => new()
    {
        Width = Length.Cells(16),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    private static string Row(ComponentSurface surface, int y, int width)
    {
        var builder = new StringBuilder(width);

        for (var x = 0; x < width; x++)
        {
            _ = builder.Append(surface.Cell(new Point(x, y)).Text);
        }

        return builder.ToString();
    }
}
