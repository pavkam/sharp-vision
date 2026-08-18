// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Tests.Controls;


/// <summary>Verifies ambient-face inheritance decisions against the fully folded appearance.</summary>
public sealed class AppearanceResolverTests
{
    /// <summary>Verifies a state-only transparent background — invisible on the opaque Normal
    /// face the inheritance decision used to run against — still triggers ambient inheritance.</summary>
    [Fact]
    public void ResolveSnapshot_WhenOnlyStateOverlayIsTransparent_InheritsParentAmbientFace()
    {
        var normalFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(1, 1, 1),
            background: Color.Rgb(2, 2, 2));
        var profile = new AppearanceStates(
            new ControlAppearance(
                normalFace,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)),
            disabled: new AppearanceOverlay(face: new FaceOverlay(background: Color.Transparent)));
        var control = new StyledProbe { AppearanceStatesOverride = profile };
        var parentFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(9, 9, 9),
            attributes: TerminalAttributes.None);

        var resolved = control.ResolveSnapshot(VisualState.Disabled, parentFace);

        resolved.Face.Foreground.Literal.ShouldBe(Color.Rgb(9, 9, 9));
        resolved.BackgroundMode.ShouldBe(BackgroundMode.Transparent);
    }

    /// <summary>Verifies a state overlay's own explicit foreground still wins over ambient
    /// inheritance, even though that same overlay's transparent background is what triggers it —
    /// ambient inheritance must remain a base the overlay folds on top of, not a final pass that
    /// clobbers what the overlay itself explicitly authored.</summary>
    [Fact]
    public void ResolveSnapshot_WhenStateOverlaySetsOwnForegroundAndTransparentBackground_KeepsOverlayForeground()
    {
        var normalFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(1, 1, 1),
            background: Color.Transparent);
        var profile = new AppearanceStates(
            new ControlAppearance(
                normalFace,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)),
            @checked: new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(5, 5, 5))));
        var control = new StyledProbe { AppearanceStatesOverride = profile };
        var parentFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(9, 9, 9),
            attributes: TerminalAttributes.None);

        var resolved = control.ResolveSnapshot(VisualState.Checked, parentFace);

        resolved.Face.Foreground.Literal.ShouldBe(Color.Rgb(5, 5, 5));
        resolved.BackgroundMode.ShouldBe(BackgroundMode.Transparent);
    }

    /// <summary>Verifies a LocalFace keeps opting out of ambient inheritance entirely regardless of
    /// its own transparency — unlike Normal or a state overlay, it is a complete override commonly
    /// authored with its own foreground and a left-default transparent background.</summary>
    [Fact]
    public void ResolveSnapshot_WhenLocalFaceBackgroundIsTransparent_PreservesOwnForeground()
    {
        var normalFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(1, 1, 1),
            background: Color.Rgb(2, 2, 2));
        var profile = new AppearanceStates(
            new ControlAppearance(
                normalFace,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)));
        var control = new StyledProbe
        {
            AppearanceStatesOverride = profile,
            Face = AppearanceTestValues.Face(
                foreground: Color.Rgb(3, 3, 3),
                background: Color.Transparent)
        };
        var parentFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(9, 9, 9),
            attributes: TerminalAttributes.None);

        var resolved = control.ResolveSnapshot(VisualState.Normal, parentFace);

        resolved.Face.Foreground.Literal.ShouldBe(Color.Rgb(3, 3, 3));
        resolved.BackgroundMode.ShouldBe(BackgroundMode.Transparent);
    }

    /// <summary>Verifies a Face whose theme-referenced attributes only conflict with its typed
    /// underline once the theme resolves them still throws, and that the exception now names the
    /// offending semantic roles instead of only the generic decoration-conflict message, because
    /// Face's own constructor cannot catch this: the attribute channel isn't literal yet.</summary>
    [Fact]
    public void ResolveSnapshot_WhenThemeResolvedAttributesConflictWithUnderline_ThrowsWithOffendingRoles()
    {
        var theme = new Theme();
        theme.SetAttributes(SemanticDecoration.NormalText, TerminalAttributes.Underline);
        var face = new Face(
            Color.Rgb(1, 1, 1),
            Color.Rgb(2, 2, 2),
            SemanticDecoration.NormalText,
            Underline.Straight,
            Color.Rgb(3, 3, 3));
        var profile = new AppearanceStates(
            new ControlAppearance(
                face,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)));
        var control = new StyledProbe { AppearanceStatesOverride = profile };

        var exception = Should.Throw<ArgumentException>(
            () => control.ResolveSnapshot(VisualState.Normal, theme, profile, parentAmbientFace: null));

        exception.Message.ShouldContain("SemanticDecoration.NormalText");
        exception.Message.ShouldContain("Straight");
    }

    /// <summary>Verifies mode is treated as geometry by the resolved-value comparison, alongside
    /// the offset change of identical magnitude that was already classified Measure.
    ///
    /// <para>"Does this need Measure, or only Render?" was answered in three places that did not
    /// agree. The two theme-side predicates listed <c>Border.Sides</c>, <c>Shadow.IsVisible</c>, and
    /// <c>Shadow.Offset</c>; the two local-overlay checks listed <c>Border.Sides</c> alone. So the
    /// same footprint change requested Measure when a theme authored it and Render when application
    /// code did - and <c>Shadow.Mode</c>, which selects between half-row and whole-cell shadow
    /// arithmetic and so resizes the visual-overflow rectangle on its own, appeared in none of the
    /// four.</para>
    /// </summary>
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

    /// <summary>Verifies registering a local shadow overlay requests the same invalidation the
    /// theme-authored equivalent does. It used to request Render while VisualBounds grew by two
    /// columns and a row.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe), rather than through the
    /// resolved-appearance comparison the GetImpact_* tests above exercise directly.</remarks>
    /// </summary>
    [Fact]
    public void SetStateAppearance_WhenALocalOverlayMovesTheShadow_RequestsMeasure()
    {
        using var probe = new ChromeProbe();
        probe.Clear(Invalidation.All);

        probe.SetStateAppearance(VisualState.Focused, ShadowGeometryOverlay);

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.Measure);
    }

    /// <summary>Verifies removing that same overlay is classified identically. Inspecting only the
    /// incoming value would leave the control laid out for a footprint it no longer has.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe).</remarks>
    /// </summary>
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
    /// every appearance edit into a re-measure.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe).</remarks>
    /// </summary>
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
    /// so a weaker answer here was wrong repeatedly rather than once.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe).</remarks>
    /// </summary>
    [Fact]
    public void VisualStateChange_WhenARegisteredOverlayMovesTheShadow_RequestsMeasure()
    {
        using var probe = new ChromeProbe();
        probe.SetStateAppearance(VisualState.Disabled, ShadowGeometryOverlay);
        probe.Clear(Invalidation.All);

        probe.IsEnabled = false;

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.Measure);
    }

    /// <summary>The counter-case for that site: a cosmetic registered overlay keeps a state change
    /// render-only.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe).</remarks>
    /// </summary>
    [Fact]
    public void VisualStateChange_WhenRegisteredOverlaysAreCosmetic_RequestsRenderOnly()
    {
        using var probe = new ChromeProbe();
        probe.SetStateAppearance(
            VisualState.Disabled,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(1, 2, 3))));
        probe.Clear(Invalidation.All);

        probe.IsEnabled = false;

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
    }

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
