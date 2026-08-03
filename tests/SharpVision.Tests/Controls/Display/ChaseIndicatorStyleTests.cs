// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies complete chase-indicator presentations and partial style composition.</summary>
public sealed class ChaseIndicatorStyleTests
{
    /// <summary>Verifies the zero-initialized style resolves to the established circle recipe.</summary>
    [Fact]
    public void Circle_WhenResolved_UsesFilledAndHollowCircle()
    {
        var actual = default(ChaseIndicatorStyle);

        actual.ShouldBe(ChaseIndicatorStyle.Circle);
        actual.Active.ShouldBe(new Rune('●'));
        actual.Inactive.ShouldBe(new Rune('◯'));
        actual.HeadColor.ShouldBe(ThemeColor.Accent);
        actual.TrailColor.ShouldBe(ThemeColor.Muted);
        actual.TrackColor.ShouldBe(ThemeColor.Muted);
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

    /// <summary>Verifies a partial active-glyph contribution preserves omitted members.</summary>
    [Fact]
    public void Apply_WhenOnlyActiveIsSupplied_PreservesOmittedMembers()
    {
        var baseline = ChaseIndicatorStyle.Circle;
        var actual = baseline.With(active: new Rune('*'));

        actual.Active.ShouldBe(new Rune('*'));
        actual.Inactive.ShouldBe(baseline.Inactive);
        actual.HeadColor.ShouldBe(baseline.HeadColor);
        actual.TrailColor.ShouldBe(baseline.TrailColor);
        actual.TrackColor.ShouldBe(baseline.TrackColor);
        actual.Appearance.ShouldBeSameAs(baseline.Appearance);
    }

    /// <summary>Verifies a partial head-color contribution preserves omitted members.</summary>
    /// <remarks>See #160.</remarks>
    [Fact]
    public void Apply_WhenOnlyHeadColorIsSupplied_PreservesOmittedMembers()
    {
        var baseline = ChaseIndicatorStyle.Circle;
        var actual = baseline.With(headColor: ThemeColor.Warning);

        actual.HeadColor.ShouldBe((ColorValue) ThemeColor.Warning);
        actual.TrailColor.ShouldBe(baseline.TrailColor);
        actual.TrackColor.ShouldBe(baseline.TrackColor);
        actual.Active.ShouldBe(baseline.Active);
        actual.Inactive.ShouldBe(baseline.Inactive);
    }

    /// <summary>Verifies partial style construction rejects a transparent color contribution immediately.</summary>
    /// <remarks>See #160.</remarks>
    [Fact]
    public void With_WhenColorIsTransparent_Throws()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            ChaseIndicatorStyle.Default.With(headColor: Color.Transparent));

        exception.ParamName.ShouldBe("headColor");
    }

    /// <summary>Verifies appearance contributions compose while preserving glyphs.</summary>
    [Fact]
    public void Apply_WhenAppearanceIsSupplied_ComposesProfile()
    {
        var baseline = ChaseIndicatorStyle.Circle;
        var actual = baseline.With(
            appearance: new AppearanceProfileSet(
                normal: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.Accent))));

        actual.Active.ShouldBe(baseline.Active);
        actual.Inactive.ShouldBe(baseline.Inactive);
        actual.Appearance.Normal.Face.Foreground.ShouldBe(ThemeColor.Accent);
    }

    /// <summary>Verifies equivalent complete profiles compare semantically.</summary>
    [Fact]
    public void Equality_WhenProfilesAreEquivalent_IsSemantic()
    {
        var baseline = ChaseIndicatorStyle.Circle;
        var equivalent = new ChaseIndicatorStyle(
            baseline.Active,
            baseline.Inactive,
            baseline.HeadColor,
            baseline.TrailColor,
            baseline.TrackColor,
            Copy(baseline.Appearance));

        equivalent.ShouldBe(baseline);
        equivalent.GetHashCode().ShouldBe(baseline.GetHashCode());
    }

    /// <summary>Verifies active and inactive glyphs reject non-one-cell values.</summary>
    [Theory]
    [InlineData(true, 0x4E16)]
    [InlineData(false, 0)]
    public void Constructor_WhenGlyphIsWideOrControl_Throws(bool active, int scalar)
    {
        var exception = Should.Throw<ArgumentException>(() => new ChaseIndicatorStyle(
            active ? new Rune(scalar) : new Rune('*'),
            active ? new Rune('.') : new Rune(scalar),
            ChaseIndicatorStyle.Default.HeadColor,
            ChaseIndicatorStyle.Default.TrailColor,
            ChaseIndicatorStyle.Default.TrackColor,
            ChaseIndicatorStyle.Circle.Appearance));

        exception.ParamName.ShouldBe(active ? "active" : "inactive");
    }

    /// <summary>Verifies every chase-indicator part foreground rejects transparent paint.</summary>
    /// <remarks>See #160.</remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = ChaseIndicatorStyle.Default;
        ColorValue transparent = Color.Transparent;

        var exception = Should.Throw<ArgumentException>(() => new ChaseIndicatorStyle(
            baseline.Active,
            baseline.Inactive,
            part == 0 ? transparent : baseline.HeadColor,
            part == 1 ? transparent : baseline.TrailColor,
            part == 2 ? transparent : baseline.TrackColor,
            baseline.Appearance));

        exception.ParamName.ShouldBe(part switch
        {
            0 => "headColor",
            1 => "trailColor",
            _ => "trackColor"
        });
    }

    /// <summary>Verifies partial style construction rejects invalid glyph contributions immediately.</summary>
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0x4E16)]
    public void With_WhenGlyphIsInvalid_Throws(bool active, int scalar)
    {
        var exception = Should.Throw<ArgumentException>(() => ChaseIndicatorStyle.Default.With(
            active: active ? new Rune(scalar) : null,
            inactive: active ? null : new Rune(scalar)));

        exception.ParamName.ShouldBe(active ? "active" : "inactive");
    }

    /// <summary>Verifies a missing complete appearance is rejected.</summary>
    [Fact]
    public void Constructor_WhenAppearanceIsNull_Throws()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new ChaseIndicatorStyle(
                new Rune('*'),
                new Rune('.'),
                ChaseIndicatorStyle.Default.HeadColor,
                ChaseIndicatorStyle.Default.TrailColor,
                ChaseIndicatorStyle.Default.TrackColor,
                null!));

        exception.ParamName.ShouldBe("appearance");
    }

    private static ThemeProfile Copy(ThemeProfile profile) => new(
        profile.Normal,
        profile.PointerOver,
        profile.FocusWithin,
        profile.Focused,
        profile.Current,
        profile.Selected,
        profile.Checked,
        profile.Indeterminate,
        profile.Pressed,
        profile.Disabled);
}
