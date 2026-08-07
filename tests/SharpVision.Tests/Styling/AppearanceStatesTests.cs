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

    /// <summary>The regression this file exists to pin, in the shape the report gives it: a row that
    /// is both selected and disabled must paint the disabled background the theme asked for, not
    /// the selected one it displaced.
    ///
    /// <para>When two visual states are active at once and both claim the same member, the later
    /// one in <c>VisualStateOrder</c> wins - including when the value it claims happens to be the
    /// one Normal already carries. That last clause was the hole. Adapting a complete-per-state set
    /// into partial overlays value-diffed each state against Normal, on the argument that a member
    /// resolving to Normal's own value is observationally identical whether recorded or dropped. It
    /// is not, and the counterexample is the fold the argument named: <c>ApplyStates</c> combines
    /// with <c>later.X ?? X</c>, so an unset member does not fall back to Normal - it yields to
    /// whatever an <em>earlier</em> state supplied. A member written back to Normal's value is
    /// therefore exactly the thing that stops the earlier state from winning, and dropping it
    /// handed the member over.</para>
    ///
    /// <para>An author had no way around it either, because the erasure keyed on the value being the
    /// one they wanted: "when disabled, go back to the normal background" was unexpressible.</para>
    /// </summary>
    [Fact]
    public void ApplyStates_WhenALaterStateAuthorsNormalsOwnValue_ItStillWinsOverAnEarlierState()
    {
        var states = AuthoredControlStates(
            """, "selected": { "face": { "background": "accent", "foreground": "selectedText" } }, "disabled": { "face": { "background": "control", "foreground": "muted" } } """);

        var folded = Fold(states, VisualState.Selected | VisualState.Disabled);

        folded.Face.Background.ShouldBe(
            (ControlColor) SemanticColor.Control,
            "disabled is later than selected in VisualStateOrder and authored this member");
        folded.Face.Foreground.ShouldBe((ControlColor) SemanticColor.Muted);
    }

    /// <summary>The counter-case that keeps the fix from becoming "later state always wins
    /// everything": a member the later state never mentioned must still leave the earlier state's
    /// contribution standing. Recording every member of a present state would pass the test above
    /// and fail this one.</summary>
    [Fact]
    public void ApplyStates_WhenALaterStateOmitsAMember_TheEarlierStatesContributionSurvives()
    {
        var states = AuthoredControlStates(
            """, "selected": { "face": { "background": "accent" } }, "disabled": { "face": { "foreground": "muted" } } """);

        var folded = Fold(states, VisualState.Selected | VisualState.Disabled);

        folded.Face.Background.ShouldBe((ControlColor) SemanticColor.Accent);
        folded.Face.Foreground.ShouldBe((ControlColor) SemanticColor.Muted);
    }

    /// <summary>Verifies the single-state case is unchanged. Provenance only ever adds members to an
    /// overlay, and for one active state an authored-equal member resolves to the same value it
    /// would have inherited - so this must read exactly as it did before.</summary>
    [Fact]
    public void ApplyStates_WhenOneStateAuthorsNormalsOwnValue_ResolvesToThatValue()
    {
        var states = AuthoredControlStates(
            """, "disabled": { "face": { "background": "control" } } """);

        Fold(states, VisualState.Disabled).Face.Background.ShouldBe((ControlColor) SemanticColor.Control);
    }

    /// <summary>Verifies a state no one authored still contributes nothing, so an unauthored later
    /// state cannot displace an earlier one. This is the case the value-diff got right and the
    /// reason "record everything for a present state" is not the fix.</summary>
    [Fact]
    public void ApplyStates_WhenTheLaterStateIsUnauthored_TheEarlierStateStands()
    {
        var states = AuthoredControlStates(
            """, "selected": { "face": { "background": "accent" } } """);

        Fold(states, VisualState.Selected | VisualState.Disabled)
            .Face.Background.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies the same contention resolves correctly one level down the cascade, where
    /// "input" inherits "control"'s per-state deltas. The delta is isolated by the same diff, so it
    /// dropped an authored-equal member there too - one layer earlier and just as quietly.</summary>
    [Fact]
    public void ApplyStates_WhenTheCascadedStateAuthorsControlsNormalValue_ItSurvivesIntoInput()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            controlExtra:
            """, "selected": { "face": { "background": "accent" } }, "disabled": { "face": { "background": "control" } } """));

        var folded = Fold(
            theme.GetStyleSet(InputStyle.Default).ToAppearanceStates(),
            VisualState.Selected | VisualState.Disabled);

        folded.Face.Background.ShouldBe((ControlColor) SemanticColor.Control);
    }

    /// <summary>Verifies border and shadow members carry provenance too, not just the face. All
    /// three fragments go through the same diff, so fixing one and not the others would leave the
    /// defect intact for the two that move layout.</summary>
    [Fact]
    public void ApplyStates_WhenALaterStateAuthorsNormalsBorderColor_ItStillWins()
    {
        var states = AuthoredControlStates(
            """, "selected": { "border": { "foreground": "accent" } }, "disabled": { "border": { "foreground": "controlBorder" } } """);

        Fold(states, VisualState.Selected | VisualState.Disabled)
            .Border.Foreground.ShouldBe((ControlColor) SemanticColor.ControlBorder);
    }

    private static AppearanceStates AuthoredControlStates(string controlExtra) =>
        ThemeCatalog.Parse(ThemeJson.Create(controlExtra: controlExtra))
            .GetStyleSet(ControlStyle.Default)
            .ToAppearanceStates();

    private static ControlAppearance Fold(AppearanceStates states, VisualState state) =>
        states.ApplyStates(states.Normal, state);

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
