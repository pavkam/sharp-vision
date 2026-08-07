// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies the progress-bar glyph family, plus complete-style behavior (presets,
/// equality, transparent-color rejection).</summary>
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

    /// <summary>Verifies the standard preset resolves the documented defaults.</summary>
    [Fact]
    public void Default_ResolvesDocumentedDefaults()
    {
        var actual = ProgressBarStyle.Default;

        actual.FillColor.ShouldBe((ControlColor) SemanticColor.Accent);
        actual.TrackColor.ShouldBe((ControlColor) SemanticColor.Muted);
        actual.IndeterminateColor.ShouldBe((ControlColor) SemanticColor.Accent);
        actual.Glyphs.ShouldBe(ProgressBarGlyphs.Default);
    }

    /// <summary>Verifies equality compares every record member structurally.</summary>
    [Fact]
    public void StyleEquality_WhenEveryMemberMatches_IsEqual()
    {
        var baseline = ProgressBarStyle.Default;
        var equivalent = new ProgressBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.FillColor,
            baseline.TrackColor,
            baseline.IndeterminateColor,
            baseline.Glyphs);

        baseline.ShouldBe(equivalent);
        baseline.GetHashCode().ShouldBe(equivalent.GetHashCode());
    }

    /// <summary>Verifies a supplied fill color preserves omitted complete members.</summary>
    [Fact]
    public void With_WhenOnlyFillColorIsSupplied_PreservesOmittedMembers()
    {
        var baseline = ProgressBarStyle.Default;
        var actual = baseline with { FillColor = Color.Rgb(255, 0, 0) };

        actual.FillColor.ShouldBe((ControlColor) Color.Rgb(255, 0, 0));
        actual.TrackColor.ShouldBe(baseline.TrackColor);
        actual.IndeterminateColor.ShouldBe(baseline.IndeterminateColor);
        actual.Glyphs.ShouldBe(baseline.Glyphs);
    }

    /// <summary>Verifies every progress-bar part foreground rejects transparent paint from the constructor.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = ProgressBarStyle.Default;
        ControlColor transparent = Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => new ProgressBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            part == 0 ? transparent : baseline.FillColor,
            part == 1 ? transparent : baseline.TrackColor,
            part == 2 ? transparent : baseline.IndeterminateColor,
            baseline.Glyphs));
    }

    /// <summary>Verifies a <c>with</c> expression rejects a transparent part foreground too, since
    /// each color property validates in its own init accessor.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void With_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = ProgressBarStyle.Default;
        ControlColor transparent = Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => part switch
        {
            0 => baseline with { FillColor = transparent },
            1 => baseline with { TrackColor = transparent },
            _ => baseline with { IndeterminateColor = transparent }
        });
    }

    /// <summary>Verifies progress-bar glyph families reject non-one-cell values.</summary>
    [Fact]
    public void Glyphs_WhenFillIsWideRune_Throws()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProgressBarGlyphs(
            new Rune('世'), new Rune('.'), new Rune('?')));

        exception.ParamName.ShouldBe("fill");
    }
}
