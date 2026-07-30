// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Tests.Controls;

using TerminalAttributes = Attributes;

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
        var profile = new ThemeProfile(
            new ThemeAppearance(
                normalFace,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)),
            disabled: new AppearanceSet(face: new FaceSet(background: Color.Transparent)));
        var control = new StyledProbe { AppearanceProfileOverride = profile };
        var parentFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(9, 9, 9),
            attributes: TerminalAttributes.None);

        var resolved = AppearanceResolver.ResolveSnapshot(control, VisualState.Disabled, parentFace);

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
        var profile = new ThemeProfile(
            new ThemeAppearance(
                normalFace,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)),
            @checked: new AppearanceSet(face: new FaceSet(foreground: Color.Rgb(5, 5, 5))));
        var control = new StyledProbe { AppearanceProfileOverride = profile };
        var parentFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(9, 9, 9),
            attributes: TerminalAttributes.None);

        var resolved = AppearanceResolver.ResolveSnapshot(control, VisualState.Checked, parentFace);

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
        var profile = new ThemeProfile(
            new ThemeAppearance(
                normalFace,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)));
        var control = new StyledProbe
        {
            AppearanceProfileOverride = profile,
            Face = AppearanceTestValues.Face(
                foreground: Color.Rgb(3, 3, 3),
                background: Color.Transparent)
        };
        var parentFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(9, 9, 9),
            attributes: TerminalAttributes.None);

        var resolved = AppearanceResolver.ResolveSnapshot(control, VisualState.Normal, parentFace);

        resolved.Face.Foreground.Literal.ShouldBe(Color.Rgb(3, 3, 3));
        resolved.BackgroundMode.ShouldBe(BackgroundMode.Transparent);
    }
}
