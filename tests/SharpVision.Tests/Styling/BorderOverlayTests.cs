// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies partial border overlays apply only their supplied members.</summary>
public sealed class BorderOverlayTests
{
    /// <summary>Verifies a partial border set replaces only the supplied member.</summary>
    [Fact]
    public void Apply_WhenOnlyForegroundIsSet_PreservesBorderDefinition()
    {
        var original = new Border(
            BorderSide.All,
            BorderGlyphStyle.Heavy,
            SemanticColor.ControlBorder,
            SemanticColor.Control,
            SemanticDecoration.Border);
        var set = new BorderOverlay(foreground: SemanticColor.ActiveBorder);

        var result = set.Apply(original);

        result.Sides.ShouldBe(BorderSide.All);
        result.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        result.Foreground.SemanticColor.ShouldBe(SemanticColor.ActiveBorder);
        result.Background.SemanticColor.ShouldBe(SemanticColor.Control);
        result.Attributes.SemanticDecoration.ShouldBe(SemanticDecoration.Border);
    }

    /// <summary>Verifies edge-color contributions replace only the optional per-edge mapping.</summary>
    [Fact]
    public void Apply_WhenEdgeColorsAreSet_PreservesUniformBorderMembers()
    {
        var border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Paired);
        var colors = BorderEdgeColors.Sunken(Color.Rgb(255, 255, 255), Color.Rgb(0, 0, 0));
        var overlay = BorderOverlay.WithEdgeColors(colors);

        var result = overlay.Apply(border);

        result.Sides.ShouldBe(border.Sides);
        result.GlyphStyle.ShouldBe(border.GlyphStyle);
        result.Foreground.ShouldBe(border.Foreground);
        result.EdgeColors.ShouldBe(colors);
        result.Background.ShouldBe(border.Background);
        result.Attributes.ShouldBe(border.Attributes);
    }
}
