// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the semantic-theme and terminal color-resolution boundary.</summary>
public sealed class ColorResolutionTests
{
    /// <summary>Verifies a concrete RGB color preserves every channel.</summary>
    [Fact]
    public void Color_WhenConcrete_PreservesValue()
    {
        var color = Color.Rgb(10, 20, 30);

        color.IsRgb.ShouldBeTrue();
        color.Red.ShouldBe((byte) 10);
        color.Green.ShouldBe((byte) 20);
        color.Blue.ShouldBe((byte) 30);
    }
}
