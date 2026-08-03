// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies semantic theme JSON loading and profile composition.</summary>
public sealed class ThemesControlStyleTests
{
    /// <summary>Verifies a complete semantic theme loads metadata and concrete global colors.</summary>
    [Fact]
    public void Parse_WhenSemanticThemeIsComplete_LoadsGlobalValues()
    {
        var theme = Themes.Parse(ThemeJson.Create(name: "Test", background: "#1a1a2e", foreground: "#e0e0e0"));

        theme.Name.ShouldBe("Test");
        theme.ResolveColor(ThemeColor.Control).ShouldBe(Color.FromHex("#1a1a2e"));
        theme.ResolveColor(ThemeColor.ControlText).ShouldBe(Color.FromHex("#e0e0e0"));
    }

    /// <summary>Verifies focused profile members overlay normal members without changing unrelated values.</summary>
    [Fact]
    public void Parse_WhenFocusedProfileIsResolved_OverlaysNormalProfile()
    {
        var theme = Themes.Parse(ThemeJson.Create(accent: "#5555ff"));

        var normal = theme.Input.Resolve(VisualState.Normal);
        var focused = theme.Input.Resolve(VisualState.Focused);

        theme.Resolve(focused.Face.Foreground).ShouldBe(Color.FromHex("#5555ff"));
        theme.Resolve(focused.Face.Attributes).ShouldBe(Attributes.Bold);
        theme.Resolve(focused.Border.Foreground).ShouldBe(Color.FromHex("#5555ff"));
        focused.Face.Background.ShouldBe(normal.Face.Background);
    }

    /// <summary>Verifies all high-level profiles inherit unspecified normal values from ControlBase.</summary>
    [Fact]
    public void Parse_WhenRoleNormalIsPartial_InheritsControlProfile()
    {
        var theme = Themes.Parse(ThemeJson.Create(foreground: "#eeeeee"));

        theme.Input.Normal.Face.Foreground.ShouldBe(theme.Control.Normal.Face.Foreground);
        theme.Input.Normal.Border.Sides.ShouldBe(BorderSide.All);
        theme.Input.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    }

    /// <summary>Verifies selector-era control maps are rejected as unknown input.</summary>
    [Fact]
    public void Parse_WhenLegacyControlsFieldIsPresent_Throws()
    {
        var json = /*lang=json,strict*/ """{"controls":{"Control":{}}}""";

        _ = Should.Throw<InvalidDataException>(() => Themes.Parse(json, "test"));
    }
}
