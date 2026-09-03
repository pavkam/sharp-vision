// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Notifications;

/// <summary>Verifies complete immutable InfoBar presentations.</summary>
public sealed class InfoBarStyleTests
{
    /// <summary>Verifies the default aliases the informational preset and all presets are distinct.</summary>
    [Fact]
    public void Presets_WhenResolved_AreCompleteAndDefaultAliasesInfo()
    {
        InfoBarStyle.Default.ShouldBeSameAs(InfoBarStyle.Info);
        InfoBarStyle.Success.ShouldNotBe(InfoBarStyle.Info);
        InfoBarStyle.Warning.ShouldNotBe(InfoBarStyle.Info);
        InfoBarStyle.Error.ShouldNotBe(InfoBarStyle.Info);
    }

    /// <summary>Verifies negative geometry and transparent paint are rejected before construction completes.</summary>
    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 0, 2)]
    public void Constructor_WhenGeometryOrPaintIsInvalid_Throws(
        int contentGap,
        int adornmentGap,
        int transparentPart)
    {
        var baseline = InfoBarStyle.Info;

        _ = Should.Throw<ArgumentException>(() => new InfoBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.TitleFace,
            transparentPart == 1 ? Color.Transparent : baseline.AdornmentColor,
            baseline.DismissGlyph,
            transparentPart == 2 ? Color.Transparent : baseline.DismissColor,
            baseline.Padding,
            contentGap,
            adornmentGap));
    }

    /// <summary>Verifies immutable replacement accessors preserve validation.</summary>
    [Fact]
    public void With_WhenReplacementIsInvalid_Throws()
    {
        var baseline = InfoBarStyle.Info;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => baseline with { ContentGap = -1 });
        _ = Should.Throw<ArgumentOutOfRangeException>(() => baseline with { AdornmentGap = -1 });
        _ = Should.Throw<ArgumentException>(() => baseline with { DismissColor = Color.Transparent });
        _ = Should.Throw<ArgumentException>(() => baseline with { AdornmentColor = Color.Transparent });
    }

    /// <summary>Verifies the default value-type glyph cannot bypass printable one-cell validation.</summary>
    [Fact]
    public void DismissGlyph_WhenDefaultValueIsAssigned_Throws()
    {
        var baseline = InfoBarStyle.Info;

        _ = Should.Throw<ArgumentException>(() => baseline with { DismissGlyph = default });
        _ = Should.Throw<ArgumentException>(() => new InfoBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.TitleFace,
            baseline.AdornmentColor,
            default,
            baseline.DismissColor,
            baseline.Padding,
            baseline.ContentGap,
            baseline.AdornmentGap));
    }

    /// <summary>Verifies complete presets use an opaque control surface and their named semantic accents.</summary>
    [Theory]
    [InlineData(0, SemanticColor.Info)]
    [InlineData(1, SemanticColor.Success)]
    [InlineData(2, SemanticColor.Warning)]
    [InlineData(3, SemanticColor.Error)]
    public void Presets_WhenRead_UseNamedSemanticAccent(int preset, SemanticColor accent)
    {
        var style = preset switch
        {
            0 => InfoBarStyle.Info,
            1 => InfoBarStyle.Success,
            2 => InfoBarStyle.Warning,
            _ => InfoBarStyle.Error
        };

        style.Face.Background.ShouldBe(new ControlColor(SemanticColor.Window));
        style.TitleFace.Foreground.ShouldBe(new ControlColor(accent));
        style.AdornmentColor.ShouldBe(new ControlColor(accent));
        style.DismissColor.ShouldBe(new ControlColor(accent));
        style.Border.Foreground.ShouldBe(new ControlColor(accent));
        style.Border.Sides.ShouldBe(BorderSide.All);
        style.Shadow.IsVisible.ShouldBeFalse();
    }

    /// <summary>Verifies the leaf style completes through the ordinary Control style role.</summary>
    [Fact]
    public void Definition_WhenNoLocalStyle_UsesOneHopControlFallback()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());
        var control = theme.GetStyleSet(ControlStyle.Default).Normal;

        var resolved = InfoBarStyle.Definition.Resolve(null, theme);

        resolved.Face.ShouldBe(control.Face);
        resolved.Border.GlyphStyle.ShouldBe(control.Border.GlyphStyle);
        resolved.Border.Background.ShouldBe(control.Face.Background);
        resolved.Border.Foreground.ShouldBe(new ControlColor(SemanticColor.Info));
        resolved.Shadow.IsVisible.ShouldBeFalse();
    }

    /// <summary>Verifies every dedicated geometry channel schedules measurement.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Definition_WhenGeometryChanges_RequiresMeasure(int member)
    {
        var previous = InfoBarStyle.Info;
        var current = member switch
        {
            0 => previous with { Padding = new Thickness(2) },
            1 => previous with { ContentGap = 2 },
            2 => previous with { AdornmentGap = 2 },
            _ => previous with { DismissGlyph = new ControlGlyph(new Rune('x'), new Rune('x')) }
        };

        InfoBarStyle.Definition.Compare(previous, null, current, null)
            .ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies local paint replacement and reset use render-only invalidation.</summary>
    [Fact]
    public void Style_WhenPaintChangesThenResets_InvalidatesRenderAndRestoresFallback()
    {
        using var bar = new InfoBar { Style = InfoBarStyle.Info };
        bar.Clear(Invalidation.All);

        bar.Style = InfoBarStyle.Info with { DismissColor = SemanticColor.Error };

        bar.Pending.ShouldBe(Invalidation.Render);
        bar.Clear(Invalidation.All);

        bar.Style = null;

        bar.Style.ShouldBeNull();
        bar.ActualStyle.ShouldBe(InfoBarStyle.Definition.Resolve(null, bar.Theme));
        bar.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies semantic accent remapping repaints a locally authored complete style.</summary>
    [Fact]
    public void SetTheme_WhenSemanticAccentChanges_InvalidatesRender()
    {
        var previous = ThemeCatalog.Parse(ThemeJson.Create());
        var current = ThemeCatalog.Parse(ThemeJson.Create().Replace(
            "\"__info\":\"#0000ff\"",
            "\"__info\":\"#ff00ff\"",
            StringComparison.Ordinal));
        using var bar = new InfoBar { Style = InfoBarStyle.Info };
        bar.SetTheme(previous);
        _ = bar.ActualStyle;
        bar.Clear(Invalidation.All);

        bar.SetTheme(current);

        bar.Pending.ShouldBe(Invalidation.Render);
    }
}
