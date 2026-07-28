// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using System.Text.Json;

/// <summary>Verifies complete radio-button presentations and partial style composition.</summary>
public sealed class RadioButtonStyleTests
{
    /// <summary>Verifies the zero-initialized style resolves to exact parenthesized marks.</summary>
    [Fact]
    public void Parentheses_WhenResolved_UsesExactThreeCellMarks()
    {
        var actual = default(RadioButtonStyle);

        actual.ShouldBe(RadioButtonStyle.Parentheses);
        actual.UncheckedText.ShouldBe("( )");
        actual.CheckedText.ShouldBe("(•)");
        actual.MarkWidth.ShouldBe(3);
    }

    /// <summary>Verifies the compact glyph preset retains the established radio marks.</summary>
    [Fact]
    public void Glyph_WhenResolved_UsesOneCellMarks()
    {
        var actual = RadioButtonStyle.Glyph;

        actual.MarkStyle.ShouldBe(RadioButtonMarkStyle.Circle);
        actual.UncheckedText.ShouldBe("○");
        actual.CheckedText.ShouldBe("◉");
        actual.MarkWidth.ShouldBe(1);
    }

    /// <summary>Verifies partial glyph replacement preserves omitted style members.</summary>
    [Fact]
    public void Apply_WhenOnlyGlyphsAreSupplied_PreservesOmittedMembers()
    {
        var baseline = RadioButtonStyle.Glyph;
        var glyphs = new RadioButtonGlyphs(new Rune('o'), new Rune('x'));
        var set = new RadioButtonStyleSet(glyphs: glyphs);

        var actual = set.Apply(baseline);

        actual.Glyphs.ShouldBe(glyphs);
        actual.MarkStyle.ShouldBe(baseline.MarkStyle);
        actual.Appearance.ShouldBeSameAs(baseline.Appearance);
    }

    /// <summary>Verifies appearance contributions compose while preserving mark presentation.</summary>
    [Fact]
    public void Apply_WhenAppearanceIsSupplied_ComposesProfile()
    {
        var baseline = RadioButtonStyle.Parentheses;
        var set = new RadioButtonStyleSet(
            appearance: new AppearanceProfileSet(
                @checked: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.Accent))));

        var actual = set.Apply(baseline);

        actual.UncheckedText.ShouldBe(baseline.UncheckedText);
        actual.Appearance.Checked.Face.ShouldNotBeNull().Foreground.ShouldBe(ThemeColor.Accent);
    }

    /// <summary>Verifies equivalent complete profiles compare semantically.</summary>
    [Fact]
    public void Equality_WhenProfilesAreEquivalent_IsSemantic()
    {
        var baseline = RadioButtonStyle.Parentheses;
        var equivalent = new RadioButtonStyle(baseline.MarkStyle, baseline.Glyphs, Copy(baseline.Appearance));

        equivalent.ShouldBe(baseline);
        equivalent.GetHashCode().ShouldBe(baseline.GetHashCode());
    }

    /// <summary>Verifies an undefined mark style is rejected.</summary>
    [Fact]
    public void Constructor_WhenMarkStyleIsUndefined_Throws()
    {
        var baseline = RadioButtonStyle.Parentheses;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new RadioButtonStyle((RadioButtonMarkStyle) 99, baseline.Glyphs, baseline.Appearance));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies partial style construction rejects an undefined mark family immediately.</summary>
    [Fact]
    public void StyleSet_WhenMarkStyleIsUndefined_ThrowsAtConstruction()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new RadioButtonStyleSet(markStyle: (RadioButtonMarkStyle) 99));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies supplied default glyphs are normalized through the validated resolved pair.</summary>
    [Fact]
    public void StyleSet_WhenDefaultGlyphsAreSupplied_RetainsResolvedPair()
    {
        var actual = new RadioButtonStyleSet(glyphs: default(RadioButtonGlyphs));

        actual.Glyphs.ShouldBe(RadioButtonGlyphs.Default);
    }

    /// <summary>Verifies radio glyphs reject non-one-cell values.</summary>
    [Theory]
    [InlineData(0x4E16)]
    [InlineData(0)]
    public void Glyphs_WhenGlyphIsWideOrControl_Throws(int scalar)
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new RadioButtonGlyphs(new Rune(scalar), new Rune('x')));

        exception.ParamName.ShouldBe("uncheckedMark");
    }

    /// <summary>Verifies a missing complete appearance is rejected.</summary>
    [Fact]
    public void Constructor_WhenAppearanceIsNull_Throws()
    {
        var baseline = RadioButtonStyle.Parentheses;

        var exception = Should.Throw<ArgumentNullException>(() =>
            new RadioButtonStyle(baseline.MarkStyle, baseline.Glyphs, null!));

        exception.ParamName.ShouldBe("appearance");
    }

    /// <summary>Verifies the typed glyph definition retains deterministic authored strings.</summary>
    [Fact]
    public void Deserialize_WhenRadioGlyphsAreAuthored_PreservesTypedShape()
    {
        var json = /*lang=json,strict*/
            """{"markStyle":"circle","glyphs":{"unchecked":"o","checked":"x"}}""";

        var definition = JsonSerializer.Deserialize<RadioButtonStyleDefinition>(json).ShouldNotBeNull();

        definition.MarkStyle.ShouldBe("circle");
        definition.Glyphs.ShouldNotBeNull().Unchecked.ShouldBe("o");
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
