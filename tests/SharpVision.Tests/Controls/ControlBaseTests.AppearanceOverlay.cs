// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies one immutable control-owned appearance overlay is composed centrally.</summary>
public sealed partial class ControlBaseTests
{
    /// <summary>Verifies live and prospective resolution compose the registered overlay once
    /// against their respective Themes.</summary>
    [Fact]
    public void ResolveAppearance_WhenOverlayIsRegistered_MatchesLiveAndProspectiveThemes()
    {
        var first = ThemeCatalog.Parse(ThemeJson.Create(accent: "#ff0000"));
        var second = ThemeCatalog.Parse(ThemeJson.Create(accent: "#00ff00"));
        var overlay = new AppearanceStatesOverlay(
            normal: new AppearanceOverlay(
                face: new FaceOverlay(foreground: SemanticColor.Accent, background: Color.Transparent)));
        var probe = new AppearanceOverlayProbe(overlay);
        probe.SetTheme(first);

        var live = probe.GetActualFace(VisualState.Normal);
        var sameTheme = probe.ResolveAppearance(first).Face;
        var prospective = probe.ResolveAppearance(second).Face;

        live.ShouldBe(sameTheme);
        live.Foreground.ShouldBe(first.ResolveColor(SemanticColor.Accent));
        prospective.Foreground.ShouldBe(second.ResolveColor(SemanticColor.Accent));
    }

    /// <summary>Verifies a complete local Face remains authoritative over the registered overlay.</summary>
    [Fact]
    public void ResolveAppearance_WhenLocalFaceIsSet_LocalValueRetainsPrecedence()
    {
        var overlay = new AppearanceStatesOverlay(
            normal: new AppearanceOverlay(
                face: new FaceOverlay(foreground: SemanticColor.Accent, background: Color.Transparent)));
        var probe = new AppearanceOverlayProbe(overlay)
        {
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(3))
        };

        probe.ResolveAppearance(ThemeCatalog.Dark).Face.Foreground.ShouldBe(ReferenceColors.Get(3));
    }

    /// <summary>Verifies registering a second immutable overlay is rejected before state changes.</summary>
    [Fact]
    public void InitializeAppearanceOverlay_WhenCalledTwice_Throws()
    {
        var probe = new AppearanceOverlayProbe(default);

        _ = Should.Throw<InvalidOperationException>(probe.RegisterAgain);
    }

    /// <summary>Verifies a registered state overlay that changes chrome geometry keeps ordinary
    /// visual-state invalidation classification.</summary>
    [Fact]
    public void VisualState_WhenRegisteredOverlayChangesBorderFootprint_InvalidatesMeasure()
    {
        var overlay = new AppearanceStatesOverlay(
            selected: new AppearanceOverlay(
                border: new BorderOverlay(sides: BorderSide.All)));
        var probe = new AppearanceOverlayProbe(overlay);
        probe.Clear(Invalidation.All);

        probe.CommitSelection(true);

        probe.Pending.ShouldBe(Invalidation.All);
    }
}
