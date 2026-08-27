// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies optional border-edge colors and relief factory mappings.</summary>
public sealed class BorderEdgeColorsTests
{
    /// <summary>Verifies absent edge overrides preserve the border's uniform foreground.</summary>
    [Fact]
    public void Resolve_WhenEdgesAreNotOverridden_InheritsUniformForeground()
    {
        var foreground = (ControlColor) Color.Rgb(1, 2, 3);
        var colors = new BorderEdgeColors();

        colors.ResolveTop(foreground).ShouldBe(foreground);
        colors.ResolveRight(foreground).ShouldBe(foreground);
        colors.ResolveBottom(foreground).ShouldBe(foreground);
        colors.ResolveLeft(foreground).ShouldBe(foreground);
    }

    /// <summary>Verifies a raised frame lights its leading edges and shades its trailing edges.</summary>
    [Fact]
    public void Raised_WhenCreated_MapsHighlightAndShadeToPhysicalEdges()
    {
        var highlight = (ControlColor) Color.Rgb(255, 255, 255);
        var shade = (ControlColor) Color.Rgb(0, 0, 0);

        var colors = BorderEdgeColors.Raised(highlight, shade);

        colors.Top.ShouldBe(highlight);
        colors.Left.ShouldBe(highlight);
        colors.Right.ShouldBe(shade);
        colors.Bottom.ShouldBe(shade);
    }

    /// <summary>Verifies a sunken frame shades its leading edges and lights its trailing edges.</summary>
    [Fact]
    public void Sunken_WhenCreated_MapsShadeAndHighlightToPhysicalEdges()
    {
        var highlight = (ControlColor) Color.Rgb(255, 255, 255);
        var shade = (ControlColor) Color.Rgb(0, 0, 0);

        var colors = BorderEdgeColors.Sunken(highlight, shade);

        colors.Top.ShouldBe(shade);
        colors.Left.ShouldBe(shade);
        colors.Right.ShouldBe(highlight);
        colors.Bottom.ShouldBe(highlight);
    }

    /// <summary>Verifies an edge override cannot use transparent foreground paint.</summary>
    [Fact]
    public void Constructor_WhenAnEdgeIsTransparent_Throws()
    {
        Action action = () => _ = new BorderEdgeColors(top: Color.Transparent);

        _ = action.ShouldThrow<ArgumentException>();
    }
}
