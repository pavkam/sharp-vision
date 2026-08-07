// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Diagnostics.CodeAnalysis;


/// <summary>Verifies the six controls that kept chrome glyphs on the control class are now reachable
/// from a theme.
///
/// <para>Each read a hardcoded entry from the <c>internal static</c> <c>ControlGlyphs</c> registry
/// and exposed a per-instance override as the only customization path. So a theme swapping the
/// application to an ASCII-safe glyph set moved checkboxes, scrollbars, progress bars, and
/// separators, and left menu markers, disclosure arrows, and navigation markers exactly where they
/// were - with no way to follow.</para>
///
/// <para>The two separators were the sharpest cases: <c>MenuSeparator</c> and
/// <c>NavigationViewSeparator</c> are dividers whose glyph property was byte-for-byte the shape
/// <c>Separator</c> had before it gained <c>SeparatorStyle</c>, so the library contained three
/// separators that disagreed about who owns a divider glyph.</para>
///
/// <para>Every case is asserted through a rendered cell rather than the resolved style alone. A
/// style that resolves correctly while the control still reads the hardcoded registry passes every
/// style-layer assertion.</para>
/// </summary>
public sealed class StrandedGlyphThemingTests
{
    /// <summary>Verifies a theme reaches the expander's disclosure arrow.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsExpanderGlyphs_DrawsThemAsync()
    {
        var expander = new Expander { HeaderText = "Section", Expanded = true };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(20, 4),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "expander": { "normal": { "expandedGlyph": "-", "collapsedGlyph": "+" } } """),
            "author disclosure glyphs");

        surface.Cell(new Point(0, 0)).Text.ShouldBe("-");
    }

    /// <summary>Verifies the collapsed arrow follows the same section, so a theme cannot half-move a
    /// two-state pair.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsExpanderGlyphs_DrawsTheCollapsedOneTooAsync()
    {
        var expander = new Expander { HeaderText = "Section", Expanded = false };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(20, 4),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "expander": { "normal": { "expandedGlyph": "-", "collapsedGlyph": "+" } } """),
            "author disclosure glyphs");

        surface.Cell(new Point(0, 0)).Text.ShouldBe("+");
    }

    /// <summary>Verifies the expander's content indent is theme-reachable, since it is the same kind
    /// of member as <c>ButtonStyle.Padding</c> and moves measured layout.</summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsExpanderIndent_AppliesIt()
    {
        var theme = Authored(""", "expander": { "normal": { "contentIndent": 5 } } """);

        ExpanderStyle.Definition.Resolve(null, theme).ContentIndent.ShouldBe(5);
    }

    /// <summary>Verifies a theme reaches the menu divider - the case that made the library hold two
    /// separators disagreeing about who owns a divider glyph.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsMenuSeparatorGlyph_DrawsItAsync()
    {
        var separator = new MenuSeparator();
        await using var surface = await ComponentSurface.MountAsync(
            separator,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "menuSeparator": { "normal": { "glyph": "=" } } """),
            "author menu divider");

        surface.Cell(new Point(0, 0)).Text.ShouldBe("=");
    }

    /// <summary>Verifies the same for the navigation divider, the third separator in the family.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsNavigationSeparatorGlyph_DrawsItAsync()
    {
        var separator = new NavigationViewSeparator();
        await using var surface = await ComponentSurface.MountAsync(
            separator,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "navigationViewSeparator": { "normal": { "glyph": "~" } } """),
            "author navigation divider");

        surface.Cell(new Point(0, 0)).Text.ShouldBe("~");
    }

    /// <summary>Verifies all four menu markers resolve from the theme. The private resolver was
    /// already named <c>ThemeMenuGlyph</c> while consulting no theme at all - it read four hardcoded
    /// registry entries, and the name was the only trace of an intent the code did not implement.</summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsMenuItemMarkers_ResolvesAllFour()
    {
        var theme = Authored(
            """, "menuItem": { "normal": { "uncheckedGlyph": ".", "checkedGlyph": "x", "radioUncheckedGlyph": "o", "radioCheckedGlyph": "*" } } """);

        var style = MenuItemStyle.Definition.Resolve(null, theme);

        style.UncheckedGlyph.ShouldBe(new Rune('.'));
        style.CheckedGlyph.ShouldBe(new Rune('x'));
        style.RadioUncheckedGlyph.ShouldBe(new Rune('o'));
        style.RadioCheckedGlyph.ShouldBe(new Rune('*'));
    }

    /// <summary>Verifies a themed check marker reaches a rendered menu row, which is what the style
    /// resolving correctly does not on its own establish.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsMenuItemMarkers_DrawsTheCheckedOneAsync()
    {
        var item = new MenuItem { Text = "Toggle", Kind = MenuItemKind.Check, IsChecked = true };
        var menu = new Menu { Orientation = Orientation.Vertical, Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "menuItem": { "normal": { "uncheckedGlyph": ".", "checkedGlyph": "x", "radioUncheckedGlyph": "o", "radioCheckedGlyph": "*" } } """),
            "author menu markers");

        Row(surface, 0, 20).ShouldContain("x");
    }

    /// <summary>Verifies the radio kind reads its own pair, so a mixed menu cannot render
    /// half-themed. That is why the style carries both pairs rather than one: an entry's kind is a
    /// semantic fact the entry owns, not a presentation choice the theme makes.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsMenuItemMarkers_DrawsTheRadioPairAsync()
    {
        var item = new MenuItem { Text = "Choice", Kind = MenuItemKind.Radio, IsChecked = true };
        var menu = new Menu { Orientation = Orientation.Vertical, Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "menuItem": { "normal": { "uncheckedGlyph": ".", "checkedGlyph": "x", "radioUncheckedGlyph": "o", "radioCheckedGlyph": "*" } } """),
            "author menu markers");

        Row(surface, 0, 20).ShouldContain("*");
    }

    /// <summary>Verifies the navigation group's disclosure pair and indent are theme-reachable.</summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsNavigationGroup_ResolvesGlyphsAndIndent()
    {
        var theme = Authored(
            """, "navigationViewGroup": { "normal": { "collapsedGlyph": "+", "expandedGlyph": "-", "itemIndent": 4 } } """);

        var style = NavigationViewGroupStyle.Definition.Resolve(null, theme);

        style.CollapsedGlyph.ShouldBe(new Rune('+'));
        style.ExpandedGlyph.ShouldBe(new Rune('-'));
        style.ItemIndent.ShouldBe(4);
    }

    /// <summary>Verifies the navigation item's two markers are theme-reachable.</summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsNavigationItemMarkers_ResolvesBoth()
    {
        var theme = Authored(
            """, "navigationViewItem": { "normal": { "idleMarker": ".", "currentMarker": ">" } } """);

        var style = NavigationViewItemStyle.Definition.Resolve(null, theme);

        style.IdleMarker.ShouldBe(new Rune('.'));
        style.CurrentMarker.ShouldBe(new Rune('>'));
    }

    /// <summary>Verifies a themed navigation marker reaches a rendered row.
    ///
    /// <para>The style-layer assertion above is not evidence for this. A style can resolve perfectly
    /// while the control still reads the hardcoded registry - reverting the render site to
    /// <c>ControlGlyphs.Navigation.ItemIdle</c> left every style-layer test green, which is exactly
    /// how this class of wiring gap survives.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsNavigationItemMarkers_DrawsThemAsync()
    {
        var view = NavigationViewWith(new NavigationViewItem { Text = "Home" });
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(16, 4),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "navigationViewItem": { "normal": { "idleMarker": "#", "currentMarker": ">" } } """),
            "author navigation markers");

        Row(surface, 0, 16).ShouldContain("#");
    }

    /// <summary>Verifies a themed group disclosure glyph reaches a rendered row, for the same
    /// reason.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsNavigationGroupGlyphs_DrawsThemAsync()
    {
        var group = new NavigationViewGroup { Header = "Tools", Expanded = true };
        group.Items.Add(new NavigationViewItem { Text = "Edit" });
        var view = NavigationViewWith(group);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(16, 5),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "navigationViewGroup": { "normal": { "collapsedGlyph": "+", "expandedGlyph": "-" } } """),
            "author group disclosure");

        Row(surface, 0, 16).ShouldContain("-");
    }

    /// <summary>Verifies a theme reaches the table's grid glyphs at the rendered cell. Table was the
    /// largest remaining case - seven members, three of them colors - and the only one where the
    /// control already participated in the styling engine through a part slot while owning no
    /// primary style of its own.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsTableGlyphs_DrawsThemAsync()
    {
        var table = new Table { Width = Length.Cells(14), ShowGridLines = true };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Columns.Add(TableColumn.Auto("B"));
        table.Rows.Add(new TableRow([new ControlText { Content = "1" }, new ControlText { Content = "2" }]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(14, 5),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "table": { "normal": { "glyphs": { "horizontal": "=", "vertical": "!", "cross": "+" } } } """),
            "author grid glyphs");

        var rendered = string.Concat(Enumerable.Range(0, 5).Select(y => Row(surface, y, 14)));
        rendered.ShouldContain("=");
        rendered.ShouldContain("!");
    }

    /// <summary>Verifies the table's cell padding is theme-reachable, and that it still moves
    /// measured layout rather than only paint.</summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsTableCellPadding_AppliesIt()
    {
        var theme = Authored(""", "table": { "normal": { "cellPadding": { "x": 2, "y": 0 } } } """);

        TableStyle.Definition.Resolve(null, theme).CellPadding.Horizontal.ShouldBe(4);
    }

    /// <summary>Verifies a themed cell padding actually reaches layout, not just the resolved style.
    ///
    /// <para>Nothing in the repository pinned this before the move - replacing the presenter's read
    /// with a hardcoded <c>default</c> left every Table suite green. The property changed owner, but
    /// the gap it exposes is older than the change.</para>
    /// </summary>
    [Fact]
    public void Measure_WhenThemeAuthorsTableCellPadding_WidensTheTable()
    {
        var padded = MeasuredTableWidth(Authored(
            """, "table": { "normal": { "cellPadding": { "x": 3, "y": 0 } } } """));
        var unpadded = MeasuredTableWidth(ThemeCatalog.Parse(ThemeJson.Create()));

        padded.ShouldBe(unpadded + 6, "one column, three cells of padding on each side");
    }

    /// <summary>Verifies the three nullable part colors round-trip through a theme. They stayed
    /// nullable on purpose: null means "inherit the table's own resolved face", which no fixed
    /// <c>ControlColor</c> can express, and for the header background it also decides whether the
    /// header row is filled at all.</summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsTablePartColors_ResolvesThem()
    {
        var theme = Authored(
            """, "table": { "normal": { "headerForeground": "accent", "headerBackground": "surface", "gridLineColor": "muted" } } """);

        var style = TableStyle.Definition.Resolve(null, theme);

        style.HeaderForeground.ShouldBe((ControlColor?) SemanticColor.Accent);
        style.HeaderBackground.ShouldBe((ControlColor?) SemanticColor.Surface);
        style.GridLineColor.ShouldBe((ControlColor?) SemanticColor.Muted);
    }

    /// <summary>The counter-case for the nullable trio, and the reason the overlay needed nullable
    /// leaf support at all: an unauthored theme leaves all three null, so a table keeps inheriting
    /// its own face instead of acquiring a header band nobody asked for.</summary>
    [Fact]
    public void Resolve_WhenNoThemeAuthorsTablePartColors_LeavesThemInheriting()
    {
        var style = TableStyle.Definition.Resolve(null, ThemeCatalog.Parse(ThemeJson.Create()));

        style.HeaderForeground.ShouldBeNull();
        style.HeaderBackground.ShouldBeNull();
        style.GridLineColor.ShouldBeNull();
    }

    /// <summary>Verifies an explicit JSON null clears an authored part color back to inheriting,
    /// which is the whole point of the nullable-leaf branch in the overlay.</summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsAnExplicitNullPartColor_ReturnsToInheriting()
    {
        var theme = Authored(""", "table": { "normal": { "headerForeground": null } } """);

        TableStyle.Definition.Resolve(null, theme).HeaderForeground.ShouldBeNull();
    }

    /// <summary>Verifies a theme reaches the status-bar separator glyph at the rendered cell.
    ///
    /// <para>The item's separator used to be one nullable Rune carrying two facts: whether a
    /// separator exists, and which glyph it is. That is why no theme could reach it - restyling the
    /// glyph would have meant deciding, per item, whether there was one at all. Presence is now
    /// <c>ShowLeftSeparator</c>, which reserves the cell, and the glyph is the theme's.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsStatusBarSeparators_DrawsThemAsync()
    {
        var item = new StatusBarItem { ShowLeftSeparator = true, Content = new ControlText { Content = "Ready" } };
        var bar = new StatusBar { Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "statusBarItem": { "normal": { "leftSeparatorGlyph": "#", "rightSeparatorGlyph": "%" } } """),
            "author status separators");

        Row(surface, 0, 20).ShouldContain("#");
    }

    /// <summary>Verifies a per-item override still beats the themed glyph. Splitting presence out
    /// must not cost the per-item choice the showcase relies on.</summary>
    [Fact]
    public void ActualSeparator_WhenTheItemOverridesTheGlyph_KeepsTheOverrideUnderATheme()
    {
        var theme = Authored(
            """, "statusBarItem": { "normal": { "leftSeparatorGlyph": "#", "rightSeparatorGlyph": "%" } } """);
        using var item = new StatusBarItem
        {
            ShowLeftSeparator = true,
            LeftSeparator = StatusBarSeparatorGlyphs.Chevron
        };
        item.PropagateTheme(theme);

        item.ActualLeftSeparator.ShouldBe(StatusBarSeparatorGlyphs.Chevron);
        item.ActualRightSeparator.ShouldBe(new Rune('%'), "the unoverridden side still follows the theme");
    }

    /// <summary>The counter-case for the split: an item that shows no separator reserves no cell, so
    /// a themed glyph cannot make one appear.</summary>
    [Fact]
    public async Task Render_WhenTheItemShowsNoSeparator_DrawsNoneEvenUnderAThemeAsync()
    {
        var item = new StatusBarItem { Content = new ControlText { Content = "Ready" } };
        var bar = new StatusBar { Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "statusBarItem": { "normal": { "leftSeparatorGlyph": "#", "rightSeparatorGlyph": "%" } } """),
            "author status separators");

        Row(surface, 0, 20).ShouldNotContain("#");
    }

    /// <summary>Verifies a theme reaches the truncation marker at the rendered cell.
    ///
    /// <para>This has the widest blast radius of any single glyph in the library - every elided
    /// string renders it - and was reachable only per <c>Text</c> instance, so a theme built for a
    /// terminal without dependable ellipsis coverage had to be applied at every construction
    /// site.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsTheEllipsis_DrawsItAsync()
    {
        var text = new ControlText { Content = "Truncate me please", Overflow = Overflow.Ellipsis };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(""", "text": { "normal": { "ellipsisGlyph": ">" } } """),
            "author truncation marker");

        Row(surface, 0, 8).ShouldEndWith(">");
    }

    /// <summary>The counter-case: an unauthored theme keeps the code-owned marker, so the fifteen
    /// bundled themes render every elided string exactly as before.</summary>
    [Fact]
    public async Task Render_WhenNoThemeAuthorsTheEllipsis_KeepsTheCodeOwnedMarkerAsync()
    {
        var text = new ControlText { Content = "Truncate me please", Overflow = Overflow.Ellipsis };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = ThemeCatalog.Parse(ThemeJson.Create()),
            "unauthored theme");

        Row(surface, 0, 8).ShouldNotContain(">");
    }

    /// <summary>Verifies a per-instance local style still wins, so moving the glyph to the theme did
    /// not remove the ability to override one label.</summary>
    [Fact]
    public void ActualStyle_WhenALocalStyleOverridesTheEllipsis_WinsOverTheTheme()
    {
        var theme = Authored(""", "text": { "normal": { "ellipsisGlyph": ">" } } """);
        using var text = new ControlText { Content = "x" };
        text.PropagateTheme(theme);

        text.ActualStyle.EllipsisGlyph.ShouldBe(new Rune('>'));

        text.Style = TextStyle.Default with { EllipsisGlyph = new Rune('~') };
        text.ActualStyle.EllipsisGlyph.ShouldBe(new Rune('~'));

        text.Style = null;
        text.ActualStyle.EllipsisGlyph.ShouldBe(new Rune('>'));
    }

    /// <summary>Verifies the chart glyph family can be applied by a theme at all.
    ///
    /// <para><c>ChartGlyphs</c> was the only glyph family in the library that was not a record and
    /// did not implement the fragment interface, so <c>Theme.Overlay</c> never recursed into it and
    /// its JSON fell through to <c>JsonSerializer</c>. Depending on which path that took, an author
    /// got either an opaque "styles.chart.normal.glyphs is invalid" or a silent fall back to the
    /// code-owned family - and <c>ChartStyle.Glyphs</c>'s getter swallowed the silent case with a
    /// <c>field == default ? Default : field</c> guard.</para>
    /// </summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsChartGlyphs_AppliesThem()
    {
        var theme = Authored(
            """, "chart": { "normal": { "glyphs": { "bar": "#", "verticalAxis": "!", "horizontalAxis": "=" } } } """);

        var glyphs = ChartStyle.Definition.Resolve(null, theme).Glyphs;

        glyphs.Bar.ShouldBe(new Rune('#'));
        glyphs.VerticalAxis.ShouldBe(new Rune('!'));
        glyphs.HorizontalAxis.ShouldBe(new Rune('='));
        glyphs.Point.ShouldBe(ChartGlyphs.Default.Point, "an unauthored member keeps the code-owned glyph");
    }

    /// <summary>Verifies an invalid chart glyph is now rejected with the dotted path rather than
    /// swallowed. The old getter's default-guard meant a theme could fail silently.</summary>
    [Fact]
    public void Resolve_WhenThemeAuthorsAWideChartGlyph_RejectsIt()
    {
        var theme = Authored(""", "chart": { "normal": { "glyphs": { "bar": "漢" } } } """);

        Should.Throw<InvalidDataException>(() => ChartStyle.Definition.Resolve(null, theme))
            .Message.ShouldContain("styles.chart.normal.glyphs.bar");
    }

    /// <summary>Verifies a themed axis rule reaches a rendered chart cell. The axis colour was
    /// already themable while the glyph was a literal, so a theme could recolour an axis it could
    /// not replace - the specific oddity the report names.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsTheChartAxis_DrawsItAsync()
    {
        var chart = new VerticalBarChart
        {
            Series = new[] { new ChartSeries("S", [new ChartDataPoint("a", 1), new ChartDataPoint("b", 2)]) }
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(20, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "chart": { "normal": { "glyphs": { "horizontalAxis": "=" } } } """),
            "author chart axis");

        var rendered = string.Concat(Enumerable.Range(0, 8).Select(y => Row(surface, y, 20)));
        rendered.ShouldContain("=");
    }

    /// <summary>Verifies a themed disclosure arrow reaches a rendered JsonView row. The arrow is
    /// part of the measured line text and hit-testing measures it to compute the clickable span, so
    /// the three former literals had to stay in lockstep by hand.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsJsonViewDisclosure_DrawsItAsync()
    {
        var view = new JsonView { Json = _nestedJson };
        view.ExpandAll();
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(24, 5),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "jsonView": { "normal": { "collapsedGlyph": "+", "expandedGlyph": "-" } } """),
            "author disclosure arrows");

        view.ActualStyle.ExpandedGlyph.ShouldBe(new Rune('-'), "the style must resolve before the render can");

        var rendered = string.Concat(Enumerable.Range(0, 5).Select(y => Row(surface, y, 24)));
        rendered.ShouldContain("-");
    }

    /// <summary>Verifies a themed month-navigation arrow reaches a rendered Calendar.
    /// <c>NavigationColor</c> exists precisely to paint this affordance, so the style had already
    /// accepted the part as theme-owned and simply stopped at the colour.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsCalendarNavigation_DrawsItAsync()
    {
        using var calendar = new UiCalendar();
        await using var surface = await ComponentSurface.MountAsync(
            calendar,
            new Size(24, 10),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = Authored(
                """, "calendar": { "normal": { "previousMonthGlyph": "{", "nextMonthGlyph": "}" } } """),
            "author navigation arrows");

        var rendered = string.Concat(Enumerable.Range(0, 10).Select(y => Row(surface, y, 24)));
        rendered.ShouldContain("{");
        rendered.ShouldContain("}");
    }

    /// <summary>Verifies the popup anchor arrows are theme-reachable and, critically, run through
    /// the same width resolution their eight border neighbours get. All four are East Asian
    /// Ambiguous, so under <c>Ambiguous.Wide</c> an unresolved arrow measures two cells and
    /// overruns its one-cell frame slot.</summary>
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

    /// <summary>The counter-case across all six: an unauthored theme keeps every code-owned glyph, so
    /// the fifteen bundled themes - none of which authors any of these keys - render as before.</summary>
    [Fact]
    public void Resolve_WhenNoThemeAuthorsTheseKeys_KeepsEveryCodeOwnedGlyph()
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

    /// <summary>Verifies a local style still wins over the theme, and that assigning null returns the
    /// control to theme ownership - the escape hatch the old <c>ResetGlyphs()</c> never provided,
    /// since it restored code-owned values rather than theme-owned ones.</summary>
    [Fact]
    public void Style_WhenAssignedThenCleared_OverridesTheThemeThenReturnsToIt()
    {
        var theme = Authored(""", "menuSeparator": { "normal": { "glyph": "=" } } """);
        using var separator = new MenuSeparator();
        separator.PropagateTheme(theme);

        separator.Style = MenuSeparatorStyle.Default with { Glyph = new Rune('~') };
        separator.ActualStyle.Glyph.ShouldBe(new Rune('~'));

        separator.Style = null;
        separator.ActualStyle.Glyph.ShouldBe(new Rune('='));
    }

    /// <summary>Verifies each new key is admitted by name. An unqualified section a library style
    /// type does not own is rejected as a typo, so a key that never registered would fail loading
    /// rather than being silently ignored.</summary>
    [Theory]
    [InlineData("expander")]
    [InlineData("menuItem")]
    [InlineData("menuSeparator")]
    [InlineData("navigationViewGroup")]
    [InlineData("navigationViewItem")]
    [InlineData("navigationViewSeparator")]
    public void Parse_WhenTheNewKeyIsAuthored_IsAdmitted(string key) =>
        _ = ThemeCatalog.Parse(ThemeJson.Create(extraStyles: $$"""", "{{key}}": { "normal": { } } """"));

    private static Theme Authored(string extraStyles) =>
        ThemeCatalog.Parse(ThemeJson.Create(extraStyles: extraStyles));

    private static int MeasuredTableWidth(Theme theme)
    {
        using var table = new Table();
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Rows.Add(new TableRow([new ControlText { Content = "Value" }]));
        table.PropagateTheme(theme);
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
