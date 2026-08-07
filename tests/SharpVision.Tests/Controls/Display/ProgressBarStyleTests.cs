// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies the progress-bar glyph family. Complete-style behavior (presets, equality,
/// transparent-color rejection) is covered by <see cref="ProgressBarStyleWithTests"/>.</summary>
public sealed class ProgressBarStyleTests
{
    /// <summary>Verifies progress-bar glyph families reject non-one-cell values.</summary>
    [Theory]
    [InlineData(0x4E16)]
    [InlineData(0)]
    public void Glyphs_WhenGlyphIsWideOrControl_Throws(int scalar)
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ProgressBarGlyphs(new Rune(scalar), new Rune('.'), new Rune('?')));

        exception.ParamName.ShouldBe("fill");
    }

    /// <summary>Verifies equivalent glyph families compare structurally.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var glyph = new Rune('·');
        var baseline = new ProgressBarGlyphs(glyph, glyph, glyph);
        var equivalent = new ProgressBarGlyphs(glyph, glyph, glyph);

        baseline.ShouldBe(equivalent);
        baseline.GetHashCode().ShouldBe(equivalent.GetHashCode());
    }
}
