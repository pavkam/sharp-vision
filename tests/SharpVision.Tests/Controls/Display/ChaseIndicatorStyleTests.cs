// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

using System.Text.Json;

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
        var set = new ChaseIndicatorStyleSet(active: new Rune('*'));

        var actual = set.Apply(baseline);

        actual.Active.ShouldBe(new Rune('*'));
        actual.Inactive.ShouldBe(baseline.Inactive);
        actual.Appearance.ShouldBeSameAs(baseline.Appearance);
    }

    /// <summary>Verifies appearance contributions compose while preserving glyphs.</summary>
    [Fact]
    public void Apply_WhenAppearanceIsSupplied_ComposesProfile()
    {
        var baseline = ChaseIndicatorStyle.Circle;
        var set = new ChaseIndicatorStyleSet(
            appearance: new AppearanceProfileSet(
                normal: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.Accent))));

        var actual = set.Apply(baseline);

        actual.Active.ShouldBe(baseline.Active);
        actual.Inactive.ShouldBe(baseline.Inactive);
        actual.Appearance.Normal.Face.Foreground.ShouldBe(ThemeColor.Accent);
    }

    /// <summary>Verifies equivalent complete profiles compare semantically.</summary>
    [Fact]
    public void Equality_WhenProfilesAreEquivalent_IsSemantic()
    {
        var baseline = ChaseIndicatorStyle.Circle;
        var equivalent = new ChaseIndicatorStyle(baseline.Active, baseline.Inactive, Copy(baseline.Appearance));

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
            ChaseIndicatorStyle.Circle.Appearance));

        exception.ParamName.ShouldBe(active ? "active" : "inactive");
    }

    /// <summary>Verifies partial style construction rejects invalid glyph contributions immediately.</summary>
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0x4E16)]
    public void StyleSet_WhenGlyphIsInvalid_ThrowsAtConstruction(bool active, int scalar)
    {
        var exception = Should.Throw<ArgumentException>(() => new ChaseIndicatorStyleSet(
            active: active ? new Rune(scalar) : null,
            inactive: active ? null : new Rune(scalar)));

        exception.ParamName.ShouldBe(active ? "active" : "inactive");
    }

    /// <summary>Verifies a missing complete appearance is rejected.</summary>
    [Fact]
    public void Constructor_WhenAppearanceIsNull_Throws()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new ChaseIndicatorStyle(new Rune('*'), new Rune('.'), null!));

        exception.ParamName.ShouldBe("appearance");
    }

    /// <summary>Verifies authored glyph strings deserialize deterministically.</summary>
    [Fact]
    public void Deserialize_WhenChaseGlyphsAreAuthored_PreservesStrings()
    {
        var json = /*lang=json,strict*/ """{"active":"*","inactive":"."}""";

        var definition = JsonSerializer.Deserialize<ChaseIndicatorStyleDefinition>(json).ShouldNotBeNull();

        definition.Active.ShouldBe("*");
        definition.Inactive.ShouldBe(".");
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
