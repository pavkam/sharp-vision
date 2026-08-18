// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies a partial overlay composes onto a complete set without discarding the
/// members it does not name.</summary>
public sealed class AppearanceStatesOverlayTests
{
    /// <summary>Verifies an empty partial profile leaves every baseline appearance unchanged.</summary>
    [Fact]
    public void Compose_WhenSetIsEmpty_PreservesBaseline()
    {
        var baseline = CreateProfile();

        var actual = baseline.Compose(default);

        actual.Normal.ShouldBe(baseline.Normal);
        actual.IsPointerOver.ShouldBe(baseline.IsPointerOver);
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
    public void Compose_WhenNormalMembersArePartial_CompletesAgainstBaseline()
    {
        var baseline = CreateProfile();
        var set = new AppearanceStatesOverlay(
            normal: new AppearanceOverlay(
                border: new BorderOverlay(glyphStyle: BorderGlyphStyle.Heavy)));

        var actual = baseline.Compose(set);

        actual.Normal.Face.ShouldBe(baseline.Normal.Face);
        actual.Normal.Border.Sides.ShouldBe(baseline.Normal.Border.Sides);
        actual.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        actual.Normal.Border.Foreground.ShouldBe(baseline.Normal.Border.Foreground);
        actual.Normal.Shadow.ShouldBe(baseline.Normal.Shadow);
    }

    /// <summary>Verifies a later state contribution overlays rather than replaces inherited state members.</summary>
    [Fact]
    public void Compose_WhenStateMembersArePartial_OverlaysInheritedStateMembers()
    {
        var baseline = CreateProfile();
        var set = new AppearanceStatesOverlay(
            pointerOver: new AppearanceOverlay(
                border: new BorderOverlay(foreground: SemanticColor.ActiveBorder)));

        var actual = baseline.Compose(set);

        var border = actual.IsPointerOver.Border.ShouldNotBeNull();

        actual.IsPointerOver.Face.ShouldBe(baseline.IsPointerOver.Face);
        border.Foreground.ShouldBe(SemanticColor.ActiveBorder);
    }

    /// <summary>Verifies profile overlay composes supplied state members and retains omitted profiles.</summary>
    [Fact]
    public void Overlay_WhenLaterAppearanceIsPartial_ComposesSuppliedMembers()
    {
        var earlier = new AppearanceStatesOverlay(
            pointerOver: new AppearanceOverlay(
                face: new FaceOverlay(foreground: SemanticColor.ActiveText)),
            focused: new AppearanceOverlay(
                face: new FaceOverlay(attributes: SemanticDecoration.FocusedText)));
        var later = new AppearanceStatesOverlay(
            pointerOver: new AppearanceOverlay(
                border: new BorderOverlay(foreground: SemanticColor.ActiveBorder)));

        var actual = earlier.Overlay(later);

        var pointerOver = actual.IsPointerOver.ShouldNotBeNull();

        pointerOver.Face.ShouldBe(earlier.IsPointerOver.ShouldNotBeNull().Face);
        pointerOver.Border.ShouldBe(later.IsPointerOver.ShouldNotBeNull().Border);
        actual.Focused.ShouldBe(earlier.Focused);
    }

    private static AppearanceStates CreateProfile() => new(
        CreateAppearance(),
        pointerOver: new AppearanceOverlay(
            face: new FaceOverlay(foreground: SemanticColor.ActiveText)),
        focusWithin: new AppearanceOverlay(
            border: new BorderOverlay(foreground: SemanticColor.ActiveBorder)),
        focused: new AppearanceOverlay(
            face: new FaceOverlay(attributes: SemanticDecoration.FocusedText)),
        current: new AppearanceOverlay(
            face: new FaceOverlay(background: SemanticColor.ActiveControl)),
        selected: new AppearanceOverlay(
            face: new FaceOverlay(background: SemanticColor.SelectedControl)),
        @checked: new AppearanceOverlay(
            face: new FaceOverlay(attributes: SemanticDecoration.SelectedText)),
        indeterminate: new AppearanceOverlay(
            border: new BorderOverlay(glyphStyle: BorderGlyphStyle.Paired)),
        pressed: new AppearanceOverlay(
            face: new FaceOverlay(foreground: SemanticColor.PressedText)),
        disabled: new AppearanceOverlay(
            face: new FaceOverlay(foreground: SemanticColor.DisabledText)));

    private static ControlAppearance CreateAppearance() => new(
        AppearanceTestValues.Face(background: SemanticColor.Control),
        AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Rounded),
        AppearanceTestValues.Shadow());
}
