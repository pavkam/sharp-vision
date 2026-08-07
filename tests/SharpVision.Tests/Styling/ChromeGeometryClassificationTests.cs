// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Tests.Controls;

/// <summary>Verifies one chrome change is classified the same way whoever expresses it.
///
/// <para>"Does this need Measure, or only Render?" was answered in three places that did not agree.
/// The two theme-side predicates listed <c>Border.Sides</c>, <c>Shadow.Visible</c>, and
/// <c>Shadow.Offset</c>; the two local-overlay checks listed <c>Border.Sides</c> alone. So the same
/// footprint change requested Measure when a theme authored it and Render when application code
/// did - and <c>Shadow.Mode</c>, which selects between half-row and whole-cell shadow arithmetic
/// and so resizes the visual-overflow rectangle on its own, appeared in none of the four.</para>
/// </summary>
public sealed class ChromeGeometryClassificationTests
{
    /// <summary>Verifies mode is treated as geometry by the resolved-value comparison, alongside
    /// the offset change of identical magnitude that was already classified Measure.</summary>
    [Fact]
    public void GetImpact_WhenOnlyShadowModeChanges_RequiresMeasure() =>
        Impact(ShadowMode.Composite, ShadowMode.FractionalBlock).ShouldBe(InvalidationImpact.Measure);

    /// <summary>The comparison the case above is calibrated against: the same-magnitude change
    /// spelled as an offset, which was always Measure.</summary>
    [Fact]
    public void GetImpact_WhenOnlyShadowOffsetChanges_RequiresMeasure()
    {
        var previous = States(ShadowMode.Composite, new Point(1, 2));
        var current = States(ShadowMode.Composite, new Point(2, 1));

        Impact(previous, current).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>The counter-case that keeps the escalation from swallowing everything: a pure color
    /// change still asks only for a repaint.</summary>
    [Fact]
    public void GetImpact_WhenOnlyAColorChanges_RequiresRenderOnly()
    {
        var previous = States(ShadowMode.Composite, new Point(1, 2));
        var current = States(ShadowMode.Composite, new Point(1, 2), Color.Rgb(9, 9, 9));

        Impact(previous, current).ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies an unchanged pair still reports no work at all.</summary>
    [Fact]
    public void GetImpact_WhenNothingChanges_RequiresNothing() =>
        Impact(ShadowMode.Composite, ShadowMode.Composite).ShouldBe(InvalidationImpact.None);

    /// <summary>Verifies the shared predicate classifies all four geometry members, which is what
    /// makes the local-overlay path agree with the theme path rather than being strictly weaker.</summary>
    /// <param name="overlay">The partial contribution under test.</param>
    [Theory]
    [MemberData(nameof(GeometryOverlays))]
    public void ChangesChromeGeometry_WhenAnOverlayTouchesAGeometryMember_ReportsTrue(AppearanceOverlay overlay) =>
        AppearanceStates.ChangesChromeGeometry(overlay).ShouldBeTrue();

    /// <summary>The counter-cases: a purely cosmetic overlay, and an empty one, must stay Render.</summary>
    [Fact]
    public void ChangesChromeGeometry_WhenAnOverlayIsCosmetic_ReportsFalse()
    {
        AppearanceStates.ChangesChromeGeometry(AppearanceOverlay.Empty).ShouldBeFalse();
        AppearanceStates.ChangesChromeGeometry(
                new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(1, 2, 3))))
            .ShouldBeFalse();
        AppearanceStates.ChangesChromeGeometry(
                new AppearanceOverlay(shadow: new ShadowOverlay(foreground: Color.Rgb(1, 2, 3))))
            .ShouldBeFalse();
    }

    /// <summary>Verifies registering a local shadow overlay requests the same invalidation the
    /// theme-authored equivalent does. It used to request Render while VisualBounds grew by two
    /// columns and a row.</summary>
    [Fact]
    public void SetStateAppearance_WhenALocalOverlayMovesTheShadow_RequestsMeasure()
    {
        using var probe = new ChromeProbe();
        probe.Clear(Invalidation.All);

        probe.SetStateAppearance(VisualState.Focused, ShadowGeometryOverlay);

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.Measure);
    }

    /// <summary>Verifies removing that same overlay is classified identically. Inspecting only the
    /// incoming value would leave the control laid out for a footprint it no longer has.</summary>
    [Fact]
    public void SetStateAppearance_WhenAGeometryOverlayIsRemoved_RequestsMeasure()
    {
        using var probe = new ChromeProbe();
        probe.SetStateAppearance(VisualState.Focused, ShadowGeometryOverlay);
        probe.Clear(Invalidation.All);

        probe.SetStateAppearance(VisualState.Focused, null);

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.Measure);
    }

    /// <summary>The counter-case: a cosmetic local overlay is still a repaint, so this did not turn
    /// every appearance edit into a re-measure.</summary>
    [Fact]
    public void SetStateAppearance_WhenALocalOverlayIsCosmetic_RequestsRenderOnly()
    {
        using var probe = new ChromeProbe();
        probe.Clear(Invalidation.All);

        probe.SetStateAppearance(
            VisualState.Focused,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(1, 2, 3))));

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies the second local-overlay site agrees too: entering a state whose registered
    /// overlay moves the shadow re-measures. This is the path that ran on every focus gain and loss,
    /// so a weaker answer here was wrong repeatedly rather than once.</summary>
    [Fact]
    public void VisualStateChange_WhenARegisteredOverlayMovesTheShadow_RequestsMeasure()
    {
        using var probe = new ChromeProbe();
        probe.SetStateAppearance(VisualState.Disabled, ShadowGeometryOverlay);
        probe.Clear(Invalidation.All);

        probe.Enabled = false;

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.Measure);
    }

    /// <summary>The counter-case for that site: a cosmetic registered overlay keeps a state change
    /// render-only.</summary>
    [Fact]
    public void VisualStateChange_WhenRegisteredOverlaysAreCosmetic_RequestsRenderOnly()
    {
        using var probe = new ChromeProbe();
        probe.SetStateAppearance(
            VisualState.Disabled,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(1, 2, 3))));
        probe.Clear(Invalidation.All);

        probe.Enabled = false;

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
    }

    /// <summary>Gets the four partial contributions that can move a control's footprint.</summary>
    /// <returns>One overlay per geometry member.</returns>
    public static TheoryData<AppearanceOverlay> GeometryOverlays() =>
    [
        new AppearanceOverlay(border: new BorderOverlay(sides: BorderSide.All)),
        new AppearanceOverlay(shadow: new ShadowOverlay(isVisible: true)),
        new AppearanceOverlay(shadow: new ShadowOverlay(offset: new Point(1, 1))),
        new AppearanceOverlay(shadow: new ShadowOverlay(mode: ShadowMode.FractionalBlock))
    ];

    private static AppearanceOverlay ShadowGeometryOverlay =>
        new(shadow: new ShadowOverlay(isVisible: true, offset: new Point(2, 1)));

    private static InvalidationImpact Impact(ShadowMode previous, ShadowMode current) =>
        Impact(States(previous, new Point(1, 2)), States(current, new Point(1, 2)));

    private static InvalidationImpact Impact(AppearanceStates previous, AppearanceStates current)
    {
        using var control = new StyledProbe { AppearanceStatesOverride = previous };
        return control.GetImpact(null, previous, null, current, null, null);
    }

    private static AppearanceStates States(ShadowMode mode, Point offset, Color? foreground = null) =>
        new(new ControlAppearance(
            AppearanceTestValues.Face(foreground: foreground ?? Color.Rgb(1, 1, 1)),
            AppearanceTestValues.Border(BorderSide.All),
            AppearanceTestValues.Shadow(visible: true, mode: mode, offset: offset)));
}
