// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies partial appearance profiles compose without discarding inherited members.</summary>
public sealed class AppearanceProfileSetTests
{
    /// <summary>Verifies composition rejects a missing complete baseline.</summary>
    [Fact]
    public void Apply_WhenBaselineIsNull_Throws()
    {
        var exception = Should.Throw<ArgumentNullException>(() => StyleResolution.Apply(null!, default));

        exception.ParamName.ShouldBe("baseline");
    }

    /// <summary>Verifies an empty partial profile leaves every baseline appearance unchanged.</summary>
    [Fact]
    public void Apply_WhenSetIsEmpty_PreservesBaseline()
    {
        var baseline = CreateProfile();

        var actual = StyleResolution.Apply(baseline, default);

        actual.Normal.ShouldBe(baseline.Normal);
        actual.PointerOver.ShouldBe(baseline.PointerOver);
        actual.FocusWithin.ShouldBe(baseline.FocusWithin);
        actual.Focused.ShouldBe(baseline.Focused);
        actual.Current.ShouldBe(baseline.Current);
        actual.Selected.ShouldBe(baseline.Selected);
        actual.Checked.ShouldBe(baseline.Checked);
        actual.Indeterminate.ShouldBe(baseline.Indeterminate);
        actual.Pressed.ShouldBe(baseline.Pressed);
        actual.Disabled.ShouldBe(baseline.Disabled);
    }

    /// <summary>Verifies a partial normal contribution completes against the baseline normal appearance.</summary>
    [Fact]
    public void Apply_WhenNormalMembersArePartial_CompletesAgainstBaseline()
    {
        var baseline = CreateProfile();
        var set = new AppearanceProfileSet(
            normal: new AppearanceSet(
                border: new BorderSet(glyphStyle: BorderGlyphStyle.Heavy)));

        var actual = StyleResolution.Apply(baseline, set);

        actual.Normal.Face.ShouldBe(baseline.Normal.Face);
        actual.Normal.Border.Sides.ShouldBe(baseline.Normal.Border.Sides);
        actual.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        actual.Normal.Border.Foreground.ShouldBe(baseline.Normal.Border.Foreground);
        actual.Normal.Shadow.ShouldBe(baseline.Normal.Shadow);
    }

    /// <summary>Verifies a later state contribution overlays rather than replaces inherited state members.</summary>
    [Fact]
    public void Apply_WhenStateMembersArePartial_OverlaysInheritedStateMembers()
    {
        var baseline = CreateProfile();
        var set = new AppearanceProfileSet(
            pointerOver: new AppearanceSet(
                border: new BorderSet(foreground: ThemeColor.ActiveBorder)));

        var actual = StyleResolution.Apply(baseline, set);

        var border = actual.PointerOver.Border.ShouldNotBeNull();

        actual.PointerOver.Face.ShouldBe(baseline.PointerOver.Face);
        border.Foreground.ShouldBe(ThemeColor.ActiveBorder);
    }

    /// <summary>Verifies profile overlay composes supplied state members and retains omitted profiles.</summary>
    [Fact]
    public void Overlay_WhenLaterProfileIsPartial_ComposesSuppliedMembers()
    {
        var earlier = new AppearanceProfileSet(
            pointerOver: new AppearanceSet(
                face: new FaceSet(foreground: ThemeColor.ActiveText)),
            focused: new AppearanceSet(
                face: new FaceSet(attributes: ThemeDecoration.FocusedText)));
        var later = new AppearanceProfileSet(
            pointerOver: new AppearanceSet(
                border: new BorderSet(foreground: ThemeColor.ActiveBorder)));

        var actual = earlier.Overlay(later);

        var pointerOver = actual.PointerOver.ShouldNotBeNull();

        pointerOver.Face.ShouldBe(earlier.PointerOver.ShouldNotBeNull().Face);
        pointerOver.Border.ShouldBe(later.PointerOver.ShouldNotBeNull().Border);
        actual.Focused.ShouldBe(earlier.Focused);
    }

    private static ThemeProfile CreateProfile() => new(
        CreateAppearance(),
        pointerOver: new AppearanceSet(
            face: new FaceSet(foreground: ThemeColor.ActiveText)),
        focusWithin: new AppearanceSet(
            border: new BorderSet(foreground: ThemeColor.ActiveBorder)),
        focused: new AppearanceSet(
            face: new FaceSet(attributes: ThemeDecoration.FocusedText)),
        current: new AppearanceSet(
            face: new FaceSet(background: ThemeColor.ActiveControl)),
        selected: new AppearanceSet(
            face: new FaceSet(background: ThemeColor.SelectedControl)),
        @checked: new AppearanceSet(
            face: new FaceSet(attributes: ThemeDecoration.SelectedText)),
        indeterminate: new AppearanceSet(
            border: new BorderSet(glyphStyle: BorderGlyphStyle.Paired)),
        pressed: new AppearanceSet(
            face: new FaceSet(foreground: ThemeColor.PressedText)),
        disabled: new AppearanceSet(
            face: new FaceSet(foreground: ThemeColor.DisabledText)));

    private static ThemeAppearance CreateAppearance() => new(
        AppearanceTestValues.Face(background: ThemeColor.Control),
        AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Rounded),
        AppearanceTestValues.Shadow());
}
