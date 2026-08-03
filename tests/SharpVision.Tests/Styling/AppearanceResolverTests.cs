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

        var resolved = control.ResolveSnapshot(VisualState.Normal, parentFace);

        resolved.Face.Foreground.Literal.ShouldBe(Color.Rgb(3, 3, 3));
        resolved.BackgroundMode.ShouldBe(BackgroundMode.Transparent);
    }

    /// <summary>Verifies a Face whose theme-referenced attributes only conflict with its typed
    /// underline once the theme resolves them still throws, and that the exception now names the
    /// offending semantic roles instead of only the generic decoration-conflict message (see #107:
    /// Face's own constructor cannot catch this because the attribute channel isn't literal yet).</summary>
    [Fact]
    public void ResolveSnapshot_WhenThemeResolvedAttributesConflictWithUnderline_ThrowsWithOffendingRoles()
    {
        var theme = new Theme();
        theme.SetAttributes(ThemeDecoration.NormalText, TerminalAttributes.Underline);
        var face = new Face(
            Color.Rgb(1, 1, 1),
            Color.Rgb(2, 2, 2),
            ThemeDecoration.NormalText,
            Underline.Straight,
            Color.Rgb(3, 3, 3));
        var profile = new ThemeProfile(
            new ThemeAppearance(
                face,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)));
        var control = new StyledProbe { AppearanceProfileOverride = profile };

        var exception = Should.Throw<ArgumentException>(
            () => control.ResolveSnapshot(VisualState.Normal, theme, profile, parentAmbientFace: null));

        exception.Message.ShouldContain("ThemeDecoration.NormalText");
        exception.Message.ShouldContain("Straight");
    }
}
