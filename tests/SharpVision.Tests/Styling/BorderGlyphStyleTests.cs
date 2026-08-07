// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies custom BorderGlyphStyle construction preserves every segment.</summary>
public sealed class BorderGlyphStyleTests
{
    /// <summary>Verifies custom border art retains all eight validated Runes.</summary>
    [Fact]
    public void Constructor_WhenBorderGlyphStyleIsCustom_PreservesEverySegment()
    {
        var style = new BorderGlyphStyle(
            new Rune('1'),
            new Rune('2'),
            new Rune('3'),
            new Rune('4'),
            new Rune('5'),
            new Rune('6'),
            new Rune('7'),
            new Rune('8'));

        style.TopLeft.ShouldBe(new Rune('1'));
        style.Top.ShouldBe(new Rune('2'));
        style.TopRight.ShouldBe(new Rune('3'));
        style.Right.ShouldBe(new Rune('4'));
        style.BottomRight.ShouldBe(new Rune('5'));
        style.Bottom.ShouldBe(new Rune('6'));
        style.BottomLeft.ShouldBe(new Rune('7'));
        style.Left.ShouldBe(new Rune('8'));
    }
}
