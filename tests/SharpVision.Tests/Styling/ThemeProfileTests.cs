// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;


/// <summary>Verifies global semantic theme-profile composition.</summary>
public sealed class ThemeProfileTests
{
    /// <summary>Verifies one state overlay changes only the supplied composite member.</summary>
    [Fact]
    public void Resolve_WhenPointerIsOver_OverlaysActiveBorderForeground()
    {
        var normal = CreateAppearance();
        var profile = new ThemeProfile(
            normal,
            pointerOver: new AppearanceSet(
                border: new BorderSet(foreground: ThemeColor.ActiveBorder)));

        var resolved = profile.Resolve(VisualState.PointerOver);

        resolved.Border.Foreground.ThemeColor.ShouldBe(ThemeColor.ActiveBorder);
        resolved.Border.Background.ShouldBe(normal.Border.Background);
        resolved.Border.GlyphStyle.ShouldBe(normal.Border.GlyphStyle);
        resolved.Face.ShouldBe(normal.Face);
        resolved.Shadow.ShouldBe(normal.Shadow);
    }

    /// <summary>Verifies later state overlays retain the established deterministic precedence.</summary>
    [Fact]
    public void Resolve_WhenFocusedAndDisabled_DisabledContributionWins()
    {
        var profile = new ThemeProfile(
            CreateAppearance(),
            focused: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.FocusedText)),
            disabled: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.DisabledText)));

        var resolved = profile.Resolve(VisualState.Focused | VisualState.Disabled);

        resolved.Face.Foreground.ThemeColor.ShouldBe(ThemeColor.DisabledText);
    }

    /// <summary>Verifies a state combination valid only in its final folded form is accepted, even
    /// though an intermediate per-overlay step (before later overlays run) would conflict.</summary>
    [Fact]
    public void Resolve_WhenIntermediateOverlayStepWouldConflict_AcceptsValidFinalFold()
    {
        // OrderedOverlays processes PointerOver, then Focused, then Checked. Folding one overlay
        // at a time and validating on every step would reject the PointerOver+Focused
        // intermediate (legacy Underline attribute together with a typed Curly underline) before
        // Checked's clearing overlay ever runs, even though the true final fold — all three
        // active — is entirely valid.
        var profile = new ThemeProfile(
            CreateAppearance(),
            pointerOver: new AppearanceSet(face: new FaceSet(attributes: TerminalAttributes.Underline)),
            focused: new AppearanceSet(face: new FaceSet(underline: Underline.Curly)),
            @checked: new AppearanceSet(face: new FaceSet(attributes: TerminalAttributes.None)));

        var resolved = Should.NotThrow(() =>
            profile.Resolve(VisualState.PointerOver | VisualState.Focused | VisualState.Checked));

        resolved.Face.Attributes.Literal.ShouldBe(TerminalAttributes.None);
        resolved.Face.Underline.ShouldBe(Underline.Curly);
    }

    /// <summary>Verifies frozen themes resolve known global values without a control type.</summary>
    [Fact]
    public void ResolveColor_WhenKnownColorIsRequested_ReturnsConfiguredConcreteColor()
    {
        var theme = new Theme();
        theme.SetColor(ThemeColor.ActiveBorder, Color.Rgb(1, 2, 3));

        theme.ResolveColor(ThemeColor.ActiveBorder).ShouldBe(Color.Rgb(1, 2, 3));
    }

    /// <summary>Verifies two profiles built from equal normal and state-contribution values are
    /// equal and hash equally, and that changing any single one of the ten slots breaks equality
    /// (see #156).</summary>
    [Fact]
    public void Equals_WhenEveryStateSlotIsCompared_DetectsADifferenceInAnySingleSlot()
    {
        var normal = CreateAppearance();
        var pointerOver = new AppearanceSet(face: new FaceSet(foreground: ThemeColor.ActiveBorder));
        var focusWithin = new AppearanceSet(face: new FaceSet(foreground: ThemeColor.FocusedText));
        var focused = new AppearanceSet(face: new FaceSet(foreground: ThemeColor.DisabledText));
        var current = new AppearanceSet(face: new FaceSet(background: ThemeColor.Control));
        var selected = new AppearanceSet(face: new FaceSet(background: ThemeColor.ActiveBorder));
        var @checked = new AppearanceSet(face: new FaceSet(attributes: TerminalAttributes.Bold));
        var indeterminate = new AppearanceSet(face: new FaceSet(attributes: TerminalAttributes.Italic));
        var pressed = new AppearanceSet(face: new FaceSet(underline: Underline.Curly));
        var disabled = new AppearanceSet(face: new FaceSet(underline: Underline.Dashed));

        var baseline = new ThemeProfile(
            normal, pointerOver, focusWithin, focused, current, selected, @checked, indeterminate, pressed, disabled);
        var identicalValues = new ThemeProfile(
            normal, pointerOver, focusWithin, focused, current, selected, @checked, indeterminate, pressed, disabled);

        baseline.Equals(identicalValues).ShouldBeTrue();
        baseline.ShouldBe(identicalValues);
        baseline.GetHashCode().ShouldBe(identicalValues.GetHashCode());
        (baseline == identicalValues).ShouldBeTrue();
        (baseline != identicalValues).ShouldBeFalse();

        var differingProfiles = new[]
        {
            new ThemeProfile(
                normal, AppearanceSet.Empty, focusWithin, focused, current, selected, @checked, indeterminate, pressed, disabled),
            new ThemeProfile(
                normal, pointerOver, AppearanceSet.Empty, focused, current, selected, @checked, indeterminate, pressed, disabled),
            new ThemeProfile(
                normal, pointerOver, focusWithin, AppearanceSet.Empty, current, selected, @checked, indeterminate, pressed, disabled),
            new ThemeProfile(
                normal, pointerOver, focusWithin, focused, AppearanceSet.Empty, selected, @checked, indeterminate, pressed, disabled),
            new ThemeProfile(
                normal, pointerOver, focusWithin, focused, current, AppearanceSet.Empty, @checked, indeterminate, pressed, disabled),
            new ThemeProfile(
                normal, pointerOver, focusWithin, focused, current, selected, AppearanceSet.Empty, indeterminate, pressed, disabled),
            new ThemeProfile(
                normal, pointerOver, focusWithin, focused, current, selected, @checked, AppearanceSet.Empty, pressed, disabled),
            new ThemeProfile(
                normal, pointerOver, focusWithin, focused, current, selected, @checked, indeterminate, AppearanceSet.Empty, disabled),
            new ThemeProfile(
                normal, pointerOver, focusWithin, focused, current, selected, @checked, indeterminate, pressed, AppearanceSet.Empty)
        };

        foreach (var differing in differingProfiles)
        {
            baseline.Equals(differing).ShouldBeFalse();
        }
    }

    /// <summary>Verifies a programmatically constructed theme starts with usable role-specific chrome.</summary>
    [Fact]
    public void Constructor_WhenProfilesAreNotLoaded_UsesRoleSpecificDefaults()
    {
        var theme = new Theme();

        theme.Control.Normal.Border.Sides.ShouldBe(BorderSide.None);
        theme.Input.Normal.Border.Sides.ShouldBe(BorderSide.All);
        theme.Input.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        theme.Container.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
        theme.Window.Normal.Shadow.IsVisible.ShouldBeTrue();
        theme.Popup.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
    }

    private static ThemeAppearance CreateAppearance() => new(
        new Face(
            ThemeColor.ControlText,
            ThemeColor.Control,
            ThemeDecoration.NormalText,
            Underline.None,
            Color.Default),
        new Border(
            BorderSide.All,
            BorderGlyphStyle.Heavy,
            ThemeColor.ControlBorder,
            ThemeColor.Control,
            ThemeDecoration.Border),
        new Shadow(
            false,
            ShadowMode.Composite,
            default,
            new Rune('▓'),
            ThemeColor.ControlShadow,
            Color.Transparent,
            ThemeDecoration.Shadow));
}
