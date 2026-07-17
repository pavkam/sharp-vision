// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

using System.Text;

/// <summary>Verifies the public immutable theme-glyph value contract from an unfriended consumer.</summary>
public sealed class ThemeGlyphContractTests
{
    /// <summary>Verifies every focused local glyph family can return to live theme resolution.</summary>
    [Fact]
    public void LocalGlyphOverrides_WhenReset_ResumeThemeValues()
    {
        // Arrange
        var menuItem = new MenuItem { UncheckedGlyph = new Rune('!'), CheckedGlyph = new Rune('!') };
        var menuSeparator = new MenuSeparator { Glyph = new Rune('!') };
        var navigationItem = new NavigationViewItem { IdleMarker = new Rune('!'), CurrentMarker = new Rune('!') };
        var navigationGroup = new NavigationViewGroup { CollapsedGlyph = new Rune('!'), ExpandedGlyph = new Rune('!') };
        var navigationSeparator = new NavigationViewSeparator { Glyph = new Rune('!') };
        var separator = new Separator { HorizontalGlyph = new Rune('!'), VerticalGlyph = new Rune('!') };
        var tabs = new TabControl { DividerGlyph = new Rune('!'), UnderlineGlyph = new Rune('!') };
        var table = new Table { HorizontalGridGlyph = new Rune('!'), VerticalGridGlyph = new Rune('!'), CrossGridGlyph = new Rune('!') };
        var text = new Controls.Text { EllipsisGlyph = new Rune('!') };
        var window = new Window { CloseGlyph = new Rune('!') };

        // Act
        menuItem.ResetGlyphs();
        menuSeparator.ResetGlyph();
        navigationItem.ResetMarkers();
        navigationGroup.ResetGlyphs();
        navigationSeparator.ResetGlyph();
        separator.ResetGlyphs();
        tabs.ResetGlyphs();
        table.ResetGridGlyphs();
        text.ResetEllipsisGlyph();
        window.ResetCloseGlyph();

        // Assert
        menuItem.UncheckedGlyph.ShouldBe(Themes.Dark.Glyphs.Selection.MenuCheckUnchecked.Value);
        menuSeparator.Glyph.ShouldBe(Themes.Dark.Glyphs.Separators.Menu.Value);
        navigationItem.IdleMarker.ShouldBe(Themes.Dark.Glyphs.Navigation.ItemIdle.Value);
        navigationGroup.CollapsedGlyph.ShouldBe(Themes.Dark.Glyphs.Navigation.GroupCollapsed.Value);
        navigationSeparator.Glyph.ShouldBe(Themes.Dark.Glyphs.Navigation.Separator.Value);
        separator.HorizontalGlyph.ShouldBe(Themes.Dark.Glyphs.Separators.Horizontal.Value);
        tabs.DividerGlyph.ShouldBe(Themes.Dark.Glyphs.Separators.TabDivider.Value);
        table.HorizontalGridGlyph.ShouldBe(Themes.Dark.Glyphs.Separators.TableHorizontal.Value);
        text.EllipsisGlyph.ShouldBe(Themes.Dark.Glyphs.Text.Ellipsis.Value);
        window.CloseGlyph.ShouldBe(Themes.Dark.Glyphs.Chrome.WindowClose.Value);
    }

    /// <summary>Verifies a theme glyph retains distinct primary and terminal-safe fallback Runes.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_ExposesPrimaryAndFallback()
    {
        // Arrange and act
        var glyph = new ThemedGlyph(new Rune('▶'), new Rune('>'));

        // Assert
        glyph.Value.ShouldBe(new Rune('▶'));
        glyph.Fallback.ShouldBe(new Rune('>'));
    }

    /// <summary>Verifies invalid values fail before an unusable glyph can be published.</summary>
    [Fact]
    public void Constructor_WhenValueIsNotOnePrintableCell_Throws()
    {
        // Arrange, act, and assert
        _ = Should.Throw<ArgumentException>(() => new ThemedGlyph(new Rune('\n'), new Rune('?')));
        _ = Should.Throw<ArgumentException>(() => new ThemedGlyph(new Rune('界'), new Rune('?')));
        _ = Should.Throw<ArgumentException>(() => new ThemedGlyph(new Rune('x'), new Rune('\0')));
    }

    /// <summary>Verifies the public palette publishes every complete semantic group.</summary>
    [Fact]
    public void ThemeGlyphs_WhenConstructed_ExposesCompleteGroups()
    {
        // Arrange
        var levels = Enumerable.Range(0, 9)
            .Select(index => Glyph(index == 0 ? '.' : '#'))
            .ToArray();
        var chrome = new ChromeGlyphs(
            Glyph('+'), Glyph('-'), Glyph('+'), Glyph('|'),
            Glyph('+'), Glyph('-'), Glyph('+'), Glyph('|'),
            Glyph('#'), Glyph('x'));
        var progress = new ProgressGlyphs(Glyph('.'), Glyph('#'), Glyph('?'), levels, levels);
        var disclosure = new DisclosureGlyphs(Glyph('>'), Glyph('v'), Glyph('v'));
        var selection = new SelectionGlyphs(
            Glyph(' '), Glyph('x'), Glyph('-'),
            Glyph('o'), Glyph('x'), Glyph('-'),
            Glyph('o'), Glyph('x'), Glyph('-'),
            Glyph('o'), Glyph('x'),
            Glyph(' '), Glyph('x'),
            Glyph('o'), Glyph('x'));
        var navigation = new NavigationGlyphs(Glyph('.'), Glyph('>'), Glyph('>'), Glyph('v'), Glyph('-'));
        var scrollBars = new ScrollBarGlyphs(
            Glyph('^'), Glyph('v'), Glyph('<'), Glyph('>'), Glyph('.'), Glyph('#'),
            Glyph('-'), Glyph('='), Glyph('|'), Glyph('#'));
        var separators = new SeparatorGlyphs(
            Glyph('-'), Glyph('|'), Glyph('-'), Glyph('-'), Glyph('|'), Glyph('+'), Glyph('|'), Glyph('-'));
        var text = new TextGlyphs(Glyph('.'));

        // Act
        var glyphs = new ThemeGlyphs(
            chrome,
            progress,
            disclosure,
            selection,
            navigation,
            scrollBars,
            separators,
            text);

        // Assert
        glyphs.Chrome.ShouldBe(chrome);
        glyphs.Progress.ShouldBeSameAs(progress);
        glyphs.Disclosure.ShouldBe(disclosure);
        glyphs.Selection.ShouldBe(selection);
        glyphs.Navigation.ShouldBe(navigation);
        glyphs.ScrollBars.ShouldBe(scrollBars);
        glyphs.Separators.ShouldBe(separators);
        glyphs.Separators.TableCross.Value.ShouldBe(new Rune('+'));
        glyphs.Text.ShouldBe(text);
    }

    /// <summary>Verifies removed schema-one theme files fail instead of silently receiving glyph defaults.</summary>
    [Fact]
    public void Parse_WhenSchemaOneIsRead_ThrowsUnsupportedVersion()
    {
        // Arrange
        const string json = /*lang=json,strict*/ """
            {
              "version": 1,
              "roles": { "background": "idx:0", "foreground": "idx:15" }
            }
            """;

        // Act
        var error = Should.Throw<InvalidDataException>(() => ThemeFile.Parse(json));

        // Assert
        error.Message.ShouldContain("unsupported schema version 1");
    }

    /// <summary>Verifies the built-in dark theme publishes a complete Unicode and repair palette.</summary>
    [Fact]
    public void Dark_WhenLoaded_ExposesCompleteGlyphPalette()
    {
        // Arrange and act
        var glyphs = Themes.Dark.Glyphs;

        // Assert
        Themes.Dark.SchemaVersion.ShouldBe(2);
        glyphs.Chrome.TopLeft.Value.ShouldBe(new Rune('╭'));
        glyphs.Chrome.TopLeft.Fallback.ShouldBe(new Rune('+'));
        glyphs.Progress.Empty.Value.ShouldBe(new Rune('░'));
        glyphs.Progress.Full.Value.ShouldBe(new Rune('█'));
        glyphs.Progress.HorizontalFractions.Length.ShouldBe(9);
        glyphs.Progress.VerticalFractions.Length.ShouldBe(9);
        glyphs.Disclosure.Collapsed.Value.ShouldBe(new Rune('▶'));
        glyphs.Disclosure.DropDown.Value.ShouldBe(new Rune('▼'));
        glyphs.Text.Ellipsis.Value.ShouldBe(new Rune('…'));
    }

    /// <summary>Verifies live theme replacement updates defaults while an explicit local value wins until reset.</summary>
    [Fact]
    public async Task Theme_WhenReplaced_UpdatesExistingControlUnlessLocallyOverriddenAsync()
    {
        // Arrange
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(8, 1)));
        var bar = new ProgressBar();
        await using var application = new Application(bar, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Act and assert
        await application.Dispatcher.InvokeAsync(() =>
        {
            bar.FillGlyph.ShouldBe(Themes.Dark.Glyphs.Progress.Full.Value);
            bar.FillGlyph = new Rune('!');
            application.Theme = Themes.White;
            bar.FillGlyph.ShouldBe(new Rune('!'));

            bar.ResetGlyphs();
            bar.FillGlyph.ShouldBe(Themes.White.Glyphs.Progress.Full.Value);
        }, TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static ThemedGlyph Glyph(char value) => new(new Rune(value), new Rune(value));
}
