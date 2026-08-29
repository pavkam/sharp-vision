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

    /// <summary>Verifies relief contributions replace only the semantic relief kind.</summary>
    [Fact]
    public void Apply_WhenReliefIsSet_PreservesOtherBorderMembers()
    {
        var border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Paired);
        var overlay = new BorderOverlay(relief: BorderRelief.Sunken);

        var result = overlay.Apply(border);

        result.Sides.ShouldBe(border.Sides);
        result.GlyphStyle.ShouldBe(border.GlyphStyle);
        result.Foreground.ShouldBe(border.Foreground);
        result.Relief.ShouldBe(BorderRelief.Sunken);
        result.Background.ShouldBe(border.Background);
        result.Attributes.ShouldBe(border.Attributes);
    }

    /// <summary>Verifies an unknown relief contribution is rejected before a complete Border is changed.</summary>
    [Fact]
    public void Constructor_WhenReliefIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => _ = new BorderOverlay(relief: (BorderRelief) 99));

        exception.ParamName.ShouldBe("relief");
    }
}
