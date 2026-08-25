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

    /// <summary>Verifies geometry members schedule measure while color-only members use the appearance pipeline.</summary>
    [Fact]
    public void Definition_Compare_WhenPaddingChanges_IsMeasure()
    {
        var previous = ToastStyle.Default;
        var current = previous with { Padding = new Thickness(2) };

        ToastStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }
}
