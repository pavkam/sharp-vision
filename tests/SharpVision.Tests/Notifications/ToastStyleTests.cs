// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Notifications;

/// <summary>Verifies Toast's public positions, animations, semantic presets, fallback, and geometry invalidation.</summary>
public sealed class ToastStyleTests
{
    /// <summary>Verifies named presets remain open-ended complete style values rather than a severity enum.</summary>
    [Fact]
    public void Presets_WhenRead_UseSemanticAccentColors()
    {
        ToastStyle.Info.Face.Background.ShouldBe(new ControlColor(SemanticColor.Window));
        ToastStyle.Error.Face.Background.ShouldBe(new ControlColor(SemanticColor.Window));
        ToastStyle.Warning.Face.Background.ShouldBe(new ControlColor(SemanticColor.Window));
        ToastStyle.Success.Face.Background.ShouldBe(new ControlColor(SemanticColor.Window));
        ToastStyle.Trace.Face.Background.ShouldBe(new ControlColor(SemanticColor.Window));
        ToastStyle.Info.AdornmentColor.ShouldBe(new ControlColor(SemanticColor.Info));
        ToastStyle.Error.AdornmentColor.ShouldBe(new ControlColor(SemanticColor.Error));
        ToastStyle.Warning.AdornmentColor.ShouldBe(new ControlColor(SemanticColor.Warning));
        ToastStyle.Success.AdornmentColor.ShouldBe(new ControlColor(SemanticColor.Success));
        ToastStyle.Trace.AdornmentColor.ShouldBe(new ControlColor(SemanticColor.Muted));
        ToastStyle.Default.ShouldBe(ToastStyle.Info);
    }

    /// <summary>Verifies a theme's Popup role completes the Toast leaf style without a Toast theme section.</summary>
    [Fact]
    public void Definition_Resolve_WhenNoLocalStyle_FallsBackToPopup()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var resolved = ToastStyle.Definition.Resolve(null, theme);

        resolved.Face.ShouldBe(theme.GetStyleSet(PopupStyle.Default).Normal.Face);
        resolved.Border.Sides.ShouldBe(theme.GetStyleSet(PopupStyle.Default).Normal.Border.Sides);
        resolved.Border.GlyphStyle.ShouldBe(theme.GetStyleSet(PopupStyle.Default).Normal.Border.GlyphStyle);
        resolved.Border.Background.ShouldBe(theme.GetStyleSet(PopupStyle.Default).Normal.Border.Background);
        resolved.Border.Foreground.ShouldBe(new ControlColor(SemanticColor.Info));
        resolved.Shadow.ShouldBe(theme.GetStyleSet(PopupStyle.Default).Normal.Shadow);
    }

    /// <summary>Verifies a theme-authored non-Flat popup relief reaches the resolved Toast border
    /// instead of being forced Flat, matching every other popup-derived surface.</summary>
    [Fact]
    public void Definition_Resolve_WhenThemeAuthorsPopupRelief_PreservesRelief()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create().Replace(
            "\"popup\": { \"normal\": { \"border\": { \"sides\":\"all\", \"glyphStyle\":\"rounded\" } } }",
            "\"popup\": { \"normal\": { \"border\": { \"sides\":\"all\", \"glyphStyle\":\"rounded\", \"relief\":\"sunken\" } } }",
            StringComparison.Ordinal));

        var resolved = ToastStyle.Definition.Resolve(null, theme);

        resolved.Border.Relief.ShouldBe(BorderRelief.Sunken);
    }

    /// <summary>Verifies geometry members schedule measure while color-only members use the appearance pipeline.</summary>
    [Fact]
    public void Definition_Compare_WhenPaddingChanges_IsMeasure()
    {
        var previous = ToastStyle.Default;
        var current = previous with { Padding = new Thickness(2) };

        ToastStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies every dedicated Toast paint channel invalidates before the next frame.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Style_WhenPaintOnlyMemberChanges_InvalidatesRender(int member)
    {
        using var toast = new Toast { Style = ToastStyle.Info };
        toast.Clear(Invalidation.All);

        toast.Style = member switch
        {
            0 => ToastStyle.Info with { TitleFace = ToastStyle.Info.TitleFace with { Foreground = SemanticColor.Error } },
            1 => ToastStyle.Info with { AdornmentColor = SemanticColor.Error },
            _ => ToastStyle.Info with { CloseColor = SemanticColor.Error }
        };

        toast.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies constructor and with-expression writes reject transparent paint channels.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PaintColor_WhenTransparent_ThrowsAtTheStyleBoundary(bool adornment)
    {
        var value = (ControlColor) Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => adornment
            ? ToastStyle.Info with { AdornmentColor = value }
            : ToastStyle.Info with { CloseColor = value });

        _ = Should.Throw<ArgumentException>(() => new ToastStyle(
            ToastStyle.Info.Face,
            ToastStyle.Info.Border,
            ToastStyle.Info.Shadow,
            ToastStyle.Info.TitleFace,
            adornment ? value : ToastStyle.Info.AdornmentColor,
            ToastStyle.Info.CloseGlyph,
            adornment ? ToastStyle.Info.CloseColor : value,
            ToastStyle.Info.Padding,
            ToastStyle.Info.ContentGap,
            ToastStyle.Info.AdornmentGap));
    }

    /// <summary>Verifies unchanged semantic Toast tokens repaint when their Theme mappings change.</summary>
    [Fact]
    public void SetTheme_WhenSemanticToastColorResolvesDifferently_InvalidatesRender()
    {
        var previous = ThemeCatalog.Parse(ThemeJson.Create());
        var current = ThemeCatalog.Parse(ThemeJson.Create().Replace(
            "\"__info\":\"#0000ff\"",
            "\"__info\":\"#ff00ff\"",
            StringComparison.Ordinal));
        using var toast = new Toast { Style = ToastStyle.Info };
        toast.SetTheme(previous);
        _ = toast.ActualStyle;
        toast.Clear(Invalidation.All);

        toast.SetTheme(current);

        toast.Pending.ShouldBe(Invalidation.Render);
    }
}
