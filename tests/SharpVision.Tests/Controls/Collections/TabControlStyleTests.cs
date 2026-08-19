// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using SharpVision.Tests.Styling;

/// <summary>Verifies the immutable tab-strip presentation record's invalidation policy.</summary>
public sealed class TabControlStyleTests
{
    /// <summary>Verifies a divider-glyph-only change is render-affecting, establishing the
    /// baseline the theme-resolution tests below build on.</summary>
    [Fact]
    public void Definition_Compare_WhenDividerGlyphChanges_IsRender()
    {
        var previous = TabControlStyle.Default;
        var current = previous with { DividerGlyph = new Rune('|') };

        TabControlStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies a Theme swap that resolves <see cref="TabControlStyle.SelectionIndicatorColor"/>
    /// to a different literal color is render-affecting, even though the style value itself is
    /// byte-identical on both sides.
    ///
    /// <para>Every sibling style with its own extra semantic colors resolves each one against both
    /// Themes in its own Compare delegate (CalendarStyle, ChartStyle, JsonViewStyle,
    /// ChaseIndicatorStyle, ProgressBarStyle, SliderStyle, ScrollBarStyle, TreeViewStyle,
    /// TableStyle all do this). TabControlStyle's Compare instead answers only
    /// <c>previous != current</c> - a raw structural comparison that cannot see two Themes mapping
    /// the same symbolic <c>SemanticColor.Accent</c> reference to two different literal colors, so
    /// a Theme swap that changes only the "accent" palette entry left the selection-indicator
    /// underline holding the previous Theme's color.</para>
    /// </summary>
    [Fact]
    public void Definition_Compare_WhenSelectionIndicatorColorResolvesDifferentlyAcrossThemes_IsRender()
    {
        var style = TabControlStyle.Default;
        var previousTheme = ThemeCatalog.Parse(ThemeJson.Create());
        var currentTheme = ThemeCatalog.Parse(ThemeJson.Create(accent: "#ff0000"));

        ControlBase.ResolveColor(style.SelectionIndicatorColor, previousTheme)
            .ShouldNotBe(ControlBase.ResolveColor(style.SelectionIndicatorColor, currentTheme));

        TabControlStyle.Definition.Compare(style, previousTheme, style, currentTheme)
            .ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies a Theme swap that resolves <see cref="TabControlStyle.DividerColor"/> to a
    /// different literal color is render-affecting, mirroring
    /// <see cref="Definition_Compare_WhenSelectionIndicatorColorResolvesDifferentlyAcrossThemes_IsRender"/>
    /// for the divider's own semantic color.</summary>
    [Fact]
    public void Definition_Compare_WhenDividerColorResolvesDifferentlyAcrossThemes_IsRender()
    {
        var style = TabControlStyle.Default;
        var previousTheme = ThemeCatalog.Parse(ThemeJson.Create());
        var currentTheme = ThemeCatalog.Parse(ThemeJson.Create(controlBorderForeground: "#ff00ff"));

        ControlBase.ResolveColor(style.DividerColor, previousTheme)
            .ShouldNotBe(ControlBase.ResolveColor(style.DividerColor, currentTheme));

        TabControlStyle.Definition.Compare(style, previousTheme, style, currentTheme)
            .ShouldBe(InvalidationImpact.Render);
    }
}
