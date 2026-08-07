// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;


/// <summary>Verifies global semantic theme-profile composition.</summary>
public sealed class AppearanceStatesTests
{
    /// <summary>Verifies one state overlay changes only the supplied composite member.</summary>
    [Fact]
    public void Resolve_WhenPointerIsOver_OverlaysActiveBorderForeground()
    {
        var normal = CreateAppearance();
        var profile = new AppearanceStates(
            normal,
            pointerOver: new AppearanceOverlay(
                border: new BorderOverlay(foreground: SemanticColor.ActiveBorder)));

        var resolved = profile.Resolve(VisualState.PointerOver);

        resolved.Border.Foreground.SemanticColor.ShouldBe(SemanticColor.ActiveBorder);
        resolved.Border.Background.ShouldBe(normal.Border.Background);
        resolved.Border.GlyphStyle.ShouldBe(normal.Border.GlyphStyle);
        resolved.Face.ShouldBe(normal.Face);
        resolved.Shadow.ShouldBe(normal.Shadow);
    }

    /// <summary>Verifies later state overlays retain the established deterministic precedence.</summary>
    [Fact]
    public void Resolve_WhenFocusedAndDisabled_DisabledContributionWins()
    {
        var profile = new AppearanceStates(
            CreateAppearance(),
            focused: new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.FocusedText)),
            disabled: new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.DisabledText)));

        var resolved = profile.Resolve(VisualState.Focused | VisualState.Disabled);

        resolved.Face.Foreground.SemanticColor.ShouldBe(SemanticColor.DisabledText);
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
        var profile = new AppearanceStates(
            CreateAppearance(),
            pointerOver: new AppearanceOverlay(face: new FaceOverlay(attributes: TerminalAttributes.Underline)),
            focused: new AppearanceOverlay(face: new FaceOverlay(underline: Underline.Curly)),
            @checked: new AppearanceOverlay(face: new FaceOverlay(attributes: TerminalAttributes.None)));

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
        theme.SetColor(SemanticColor.ActiveBorder, Color.Rgb(1, 2, 3));

        theme.ResolveColor(SemanticColor.ActiveBorder).ShouldBe(Color.Rgb(1, 2, 3));
    }

    /// <summary>Verifies two profiles built from equal normal and state-contribution values are
    /// equal and hash equally, and that changing any single one of the ten slots breaks
    /// equality.</summary>
    [Fact]
    public void Equals_WhenEveryStateSlotIsCompared_DetectsADifferenceInAnySingleSlot()
    {
        var normal = CreateAppearance();
        var pointerOver = new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.ActiveBorder));
        var focusWithin = new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.FocusedText));
        var focused = new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.DisabledText));
        var current = new AppearanceOverlay(face: new FaceOverlay(background: SemanticColor.Control));
        var selected = new AppearanceOverlay(face: new FaceOverlay(background: SemanticColor.ActiveBorder));
        var @checked = new AppearanceOverlay(face: new FaceOverlay(attributes: TerminalAttributes.Bold));
        var indeterminate = new AppearanceOverlay(face: new FaceOverlay(attributes: TerminalAttributes.Italic));
        var pressed = new AppearanceOverlay(face: new FaceOverlay(underline: Underline.Curly));
        var disabled = new AppearanceOverlay(face: new FaceOverlay(underline: Underline.Dashed));

        var baseline = new AppearanceStates(
            normal, pointerOver, focusWithin, focused, current, selected, @checked, indeterminate, pressed, disabled);
        var identicalValues = new AppearanceStates(
            normal, pointerOver, focusWithin, focused, current, selected, @checked, indeterminate, pressed, disabled);

        baseline.Equals(identicalValues).ShouldBeTrue();
        baseline.ShouldBe(identicalValues);
        baseline.GetHashCode().ShouldBe(identicalValues.GetHashCode());
        (baseline == identicalValues).ShouldBeTrue();
        (baseline != identicalValues).ShouldBeFalse();

        var differingProfiles = new[]
        {
            new AppearanceStates(
                normal, AppearanceOverlay.Empty, focusWithin, focused, current, selected, @checked, indeterminate, pressed, disabled),
            new AppearanceStates(
                normal, pointerOver, AppearanceOverlay.Empty, focused, current, selected, @checked, indeterminate, pressed, disabled),
            new AppearanceStates(
                normal, pointerOver, focusWithin, AppearanceOverlay.Empty, current, selected, @checked, indeterminate, pressed, disabled),
            new AppearanceStates(
                normal, pointerOver, focusWithin, focused, AppearanceOverlay.Empty, selected, @checked, indeterminate, pressed, disabled),
            new AppearanceStates(
                normal, pointerOver, focusWithin, focused, current, AppearanceOverlay.Empty, @checked, indeterminate, pressed, disabled),
            new AppearanceStates(
                normal, pointerOver, focusWithin, focused, current, selected, AppearanceOverlay.Empty, indeterminate, pressed, disabled),
            new AppearanceStates(
                normal, pointerOver, focusWithin, focused, current, selected, @checked, AppearanceOverlay.Empty, pressed, disabled),
            new AppearanceStates(
                normal, pointerOver, focusWithin, focused, current, selected, @checked, indeterminate, AppearanceOverlay.Empty, disabled),
            new AppearanceStates(
                normal, pointerOver, focusWithin, focused, current, selected, @checked, indeterminate, pressed, AppearanceOverlay.Empty)
        };

        foreach (var differing in differingProfiles)
        {
            baseline.Equals(differing).ShouldBeFalse();
        }
    }

    /// <summary>Verifies a programmatically constructed theme starts with usable role-specific chrome.</summary>
    [Fact]
    public void Constructor_WhenThemeIsNotLoaded_UsesStyleSpecificDefaults()
    {
        var theme = new Theme();

        theme.Control.Normal.Border.Sides.ShouldBe(BorderSide.None);
        theme.Input.Normal.Border.Sides.ShouldBe(BorderSide.All);
        theme.Input.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        theme.Container.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
        theme.Window.Normal.Shadow.Visible.ShouldBeTrue();
        theme.Popup.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
    }

    private static ControlAppearance CreateAppearance() => new(
        new Face(
            SemanticColor.ControlText,
            SemanticColor.Control,
            SemanticDecoration.NormalText,
            Underline.None,
            Color.Default),
        new Border(
            BorderSide.All,
            BorderGlyphStyle.Heavy,
            SemanticColor.ControlBorder,
            SemanticColor.Control,
            SemanticDecoration.Border),
        new Shadow(
            false,
            ShadowMode.Composite,
            default,
            new Rune('▓'),
            SemanticColor.ControlShadow,
            Color.Transparent,
            SemanticDecoration.Shadow));
}
