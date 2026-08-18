// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies complete chase-indicator presentations.</summary>
public sealed class ChaseIndicatorStyleTests
{
    /// <summary>Verifies Default resolves to the established circle recipe.</summary>
    [Fact]
    public void Circle_ResolvesFilledAndHollowCircleAndIsDefault()
    {
        var actual = ChaseIndicatorStyle.Default;

        actual.ShouldBe(ChaseIndicatorStyle.Circle);
        actual.Active.ShouldBe(new Rune('●'));
        actual.Inactive.ShouldBe(new Rune('◯'));
        actual.HeadColor.ShouldBe(SemanticColor.Accent);
        actual.TrailColor.ShouldBe(SemanticColor.Muted);
        actual.TrackColor.ShouldBe(SemanticColor.Muted);
    }

    /// <summary>Verifies every retained named glyph recipe is exposed.</summary>
    [Fact]
    public void Presets_WhenResolved_RetainEstablishedRecipes()
    {
        ChaseIndicatorStyle.Diamond.Active.ShouldBe(new Rune('◆'));
        ChaseIndicatorStyle.Square.Active.ShouldBe(new Rune('■'));
        ChaseIndicatorStyle.Up.Active.ShouldBe(new Rune('▲'));
        ChaseIndicatorStyle.Down.Active.ShouldBe(new Rune('▼'));
        ChaseIndicatorStyle.Left.Active.ShouldBe(new Rune('◀'));
        ChaseIndicatorStyle.Right.Active.ShouldBe(new Rune('▶'));
    }

    /// <summary>Verifies a <c>with</c> expression preserves omitted members.</summary>
    [Fact]
    public void With_WhenOnlyActiveIsSupplied_PreservesOmittedMembers()
    {
        var baseline = ChaseIndicatorStyle.Circle;
        var actual = baseline with { Active = new Rune('*') };

        actual.Active.ShouldBe(new Rune('*'));
        actual.Inactive.ShouldBe(baseline.Inactive);
        actual.HeadColor.ShouldBe(baseline.HeadColor);
        actual.TrailColor.ShouldBe(baseline.TrailColor);
        actual.TrackColor.ShouldBe(baseline.TrackColor);
    }

    /// <summary>Verifies a <c>with</c> expression rejects a transparent color contribution, since
    /// each color property validates in its own init accessor.</summary>
    [Fact]
    public void With_WhenColorIsTransparent_Throws() =>
        _ = Should.Throw<ArgumentException>(() => ChaseIndicatorStyle.Default with { HeadColor = Color.Transparent });

    /// <summary>Verifies equality compares every record member structurally.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var baseline = ChaseIndicatorStyle.Circle;
        var equivalent = new ChaseIndicatorStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.Active,
            baseline.Inactive,
            baseline.HeadColor,
            baseline.TrailColor,
            baseline.TrackColor);

        equivalent.ShouldBe(baseline);
        equivalent.GetHashCode().ShouldBe(baseline.GetHashCode());
    }

    /// <summary>Verifies active and inactive glyphs reject non-one-cell values from the constructor.</summary>
    [Theory]
    [InlineData(true, 0x4E16)]
    [InlineData(false, 0)]
    public void Constructor_WhenGlyphIsWideOrControl_Throws(bool active, int scalar)
    {
        var baseline = ChaseIndicatorStyle.Default;

        _ = Should.Throw<ArgumentException>(() => new ChaseIndicatorStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            active ? new Rune(scalar) : new Rune('*'),
            active ? new Rune('.') : new Rune(scalar),
            baseline.HeadColor,
            baseline.TrailColor,
            baseline.TrackColor));
    }

    /// <summary>Verifies a <c>with</c> expression rejects an invalid glyph contribution too.</summary>
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0x4E16)]
    public void With_WhenGlyphIsInvalid_Throws(bool active, int scalar)
    {
        var baseline = ChaseIndicatorStyle.Default;

        _ = Should.Throw<ArgumentException>(() => active
            ? baseline with { Active = new Rune(scalar) }
            : baseline with { Inactive = new Rune(scalar) });
    }

    /// <summary>Verifies every chase-indicator part foreground rejects transparent paint from the constructor.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = ChaseIndicatorStyle.Default;
        ControlColor transparent = Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => new ChaseIndicatorStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.Active,
            baseline.Inactive,
            part == 0 ? transparent : baseline.HeadColor,
            part == 1 ? transparent : baseline.TrailColor,
            part == 2 ? transparent : baseline.TrackColor));
    }
}
