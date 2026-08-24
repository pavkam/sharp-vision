// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies complete slider presentations and their glyph family.</summary>
public sealed class SliderStyleTests
{
    /// <summary>Verifies the standard preset resolves the code-owned defaults.</summary>
    [Fact]
    public void Default_ResolvesCodeOwnedDefaults()
    {
        var actual = SliderStyle.Default;

        actual.FillColor.ShouldBe(SemanticColor.Accent);
        actual.TrackColor.ShouldBe(SemanticColor.Muted);
        actual.ThumbColor.ShouldBe(SemanticColor.Accent);
        actual.Glyphs.ShouldBe(SliderGlyphs.Default);
    }

    /// <summary>Verifies every bundled theme resolves Slider to the code-owned
    /// Accent/Muted/Accent mapping. Every curated theme's own "slider" section used to author
    /// exactly this mapping and has since been deleted as redundant; this pins that the deletion
    /// changed nothing observable.</summary>
    [Fact]
    public void EveryTheme_ResolvesTheCodeOwnedFillTrackAndThumbColors()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var style = SliderStyle.Definition.Resolve(null, theme);

            style.FillColor.SemanticColor.ShouldBe(SemanticColor.Accent, slug);
            style.TrackColor.SemanticColor.ShouldBe(SemanticColor.Muted, slug);
            style.ThumbColor.SemanticColor.ShouldBe(SemanticColor.Accent, slug);
        }
    }

    /// <summary>Verifies equality compares every record member structurally.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var baseline = SliderStyle.Default;
        var equivalent = new SliderStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.FillColor,
            baseline.TrackColor,
            baseline.ThumbColor,
            baseline.Glyphs);

        baseline.ShouldBe(equivalent);
    }

    /// <summary>Verifies every slider part foreground rejects transparent paint from the constructor.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = SliderStyle.Default;
        ControlColor transparent = Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => new SliderStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            part == 0 ? transparent : baseline.FillColor,
            part == 1 ? transparent : baseline.TrackColor,
            part == 2 ? transparent : baseline.ThumbColor,
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
        var baseline = SliderStyle.Default;
        ControlColor transparent = Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => part switch
        {
            0 => baseline with { FillColor = transparent },
            1 => baseline with { TrackColor = transparent },
            _ => baseline with { ThumbColor = transparent }
        });
    }

    /// <summary>Verifies slider glyph families reject non-one-cell values.</summary>
    [Theory]
    [InlineData(0x4E16)]
    [InlineData(0)]
    public void Glyphs_WhenGlyphIsWideOrControl_Throws(int scalar)
    {
        var exception = Should.Throw<ArgumentException>(() => CreateGlyphs(new Rune(scalar)));

        exception.ParamName.ShouldBe("horizontalTrack");
    }

    private static SliderGlyphs CreateGlyphs(Rune horizontalTrack) => new(
        horizontalTrack,
        new Rune('='),
        new Rune('.'),
        new Rune('='),
        new Rune('#'));
}
