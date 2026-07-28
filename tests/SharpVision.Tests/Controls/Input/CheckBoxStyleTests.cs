// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using System.Text.Json;

/// <summary>Verifies complete checkbox presentations and partial style composition.</summary>
public sealed class CheckBoxStyleTests
{
    /// <summary>Verifies the default value resolves to the three-cell bracket preset.</summary>
    [Fact]
    public void Default_WhenStyleIsZeroInitialized_ResolvesBrackets()
    {
        var actual = default(CheckBoxStyle);

        actual.ShouldBe(CheckBoxStyle.Default);
        actual.ShouldBe(CheckBoxStyle.Brackets);
        actual.MarkStyle.ShouldBe(CheckBoxMarkStyle.Brackets);
        actual.MarkWidth.ShouldBe(3);
    }

    /// <summary>Verifies the bracket preset retains the established horizontal-line indeterminate recipe.</summary>
    [Fact]
    public void Brackets_WhenResolved_UsesHorizontalLineIndeterminateGlyph()
    {
        var actual = CheckBoxStyle.Brackets;

        actual.Glyphs.Indeterminate.ShouldBe(new Rune('─'));
    }

    /// <summary>Verifies non-bracket built-ins reserve one terminal cell.</summary>
    [Fact]
    public void Presets_WhenNotBracketed_ReserveOneCell()
    {
        CheckBoxStyle.Tick.MarkWidth.ShouldBe(1);
        CheckBoxStyle.Square.MarkWidth.ShouldBe(1);
    }

    /// <summary>Verifies partial glyph replacement preserves omitted style members.</summary>
    [Fact]
    public void Apply_WhenOnlyGlyphsAreSupplied_PreservesOmittedMembers()
    {
        var baseline = CheckBoxStyle.Brackets;
        var glyphs = new CheckBoxGlyphs(new Rune('o'), new Rune('x'), new Rune('-'));
        var set = new CheckBoxStyleSet(glyphs: glyphs);

        var actual = set.Apply(baseline);

        actual.Glyphs.ShouldBe(glyphs);
        actual.MarkStyle.ShouldBe(baseline.MarkStyle);
        actual.Appearance.ShouldBeSameAs(baseline.Appearance);
    }

    /// <summary>Verifies appearance contributions compose while preserving mark presentation.</summary>
    [Fact]
    public void Apply_WhenAppearanceIsSupplied_ComposesProfile()
    {
        var baseline = CheckBoxStyle.Default;
        var set = new CheckBoxStyleSet(
            appearance: new AppearanceProfileSet(
                @checked: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.Accent))));

        var actual = set.Apply(baseline);

        actual.Glyphs.ShouldBe(baseline.Glyphs);
        actual.Appearance.Checked.Face.ShouldNotBeNull().Foreground.ShouldBe(ThemeColor.Accent);
    }

    /// <summary>Verifies equivalent complete profiles compare semantically.</summary>
    [Fact]
    public void Equality_WhenProfilesAreEquivalent_IsSemantic()
    {
        var baseline = CheckBoxStyle.Default;
        var equivalent = new CheckBoxStyle(baseline.MarkStyle, baseline.Glyphs, Copy(baseline.Appearance));

        equivalent.ShouldBe(baseline);
        equivalent.GetHashCode().ShouldBe(baseline.GetHashCode());
    }

    /// <summary>Verifies an undefined mark style is rejected.</summary>
    [Fact]
    public void Constructor_WhenMarkStyleIsUndefined_Throws()
    {
        var baseline = CheckBoxStyle.Default;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new CheckBoxStyle((CheckBoxMarkStyle) 99, baseline.Glyphs, baseline.Appearance));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies partial style construction rejects an undefined mark family immediately.</summary>
    [Fact]
    public void StyleSet_WhenMarkStyleIsUndefined_ThrowsAtConstruction()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new CheckBoxStyleSet(markStyle: (CheckBoxMarkStyle) 99));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies partial style construction rejects the invalid zero-initialized glyph family immediately.</summary>
    [Fact]
    public void StyleSet_WhenGlyphsAreInvalid_ThrowsAtConstruction()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new CheckBoxStyleSet(glyphs: default(CheckBoxGlyphs)));

        exception.ParamName.ShouldBe("uncheckedMark");
    }

    /// <summary>Verifies a missing complete appearance is rejected.</summary>
    [Fact]
    public void Constructor_WhenAppearanceIsNull_Throws()
    {
        var baseline = CheckBoxStyle.Default;

        var exception = Should.Throw<ArgumentNullException>(() =>
            new CheckBoxStyle(baseline.MarkStyle, baseline.Glyphs, null!));

        exception.ParamName.ShouldBe("appearance");
    }

    /// <summary>Verifies the typed glyph definition retains deterministic authored strings.</summary>
    [Fact]
    public void Deserialize_WhenCheckBoxGlyphsAreAuthored_PreservesTypedShape()
    {
        var json = /*lang=json,strict*/
            """{"markStyle":"square","glyphs":{"unchecked":"o","checked":"x","indeterminate":"-"}}""";

        var definition = JsonSerializer.Deserialize<CheckBoxStyleDefinition>(json).ShouldNotBeNull();

        definition.MarkStyle.ShouldBe("square");
        definition.Glyphs.ShouldNotBeNull().Checked.ShouldBe("x");
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
