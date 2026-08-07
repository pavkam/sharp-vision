// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies that when two visual states are active at once and both claim the same member,
/// the later one in <c>VisualStateOrder</c> wins - including when the value it claims happens to be
/// the one Normal already carries.
///
/// <para>That last clause was the hole. Adapting a complete-per-state set into partial overlays
/// value-diffed each state against Normal, on the argument that a member resolving to Normal's own
/// value is observationally identical whether recorded or dropped. It is not, and the counterexample
/// is the fold the argument named: <c>ApplyStates</c> combines with <c>later.X ?? X</c>, so an
/// unset member does not fall back to Normal - it yields to whatever an <em>earlier</em> state
/// supplied. A member written back to Normal's value is therefore exactly the thing that stops the
/// earlier state from winning, and dropping it handed the member over.</para>
///
/// <para>An author had no way around it either, because the erasure keyed on the value being the
/// one they wanted: "when disabled, go back to the normal background" was unexpressible.</para>
/// </summary>
public sealed class MultiStateFoldProvenanceTests
{
    /// <summary>The regression this file exists to pin, in the shape the report gives it: a row that
    /// is both selected and disabled must paint the disabled background the theme asked for, not
    /// the selected one it displaced.</summary>
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
}
