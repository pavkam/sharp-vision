// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies built-in controls select their library-owned semantic theme profiles.</summary>
public sealed class BuiltInControlStyleTests
{
    /// <summary>Verifies representative built-ins receive the matching profile without application plumbing.</summary>
    [Fact]
    public void ActualBorder_WhenBuiltInControlsAreThemed_UsesLibraryOwnedStyle()
    {
        ControlBase[] controls = [new Button(), new TextInput(), new ComboBox(), new GroupBox(), new Window(), new Popup()];
        var expected = new[]
        {
            ThemeCatalog.Dark.GetStyleSet(InputStyle.Default).Normal.Border.Sides,
            ThemeCatalog.Dark.GetStyleSet(InputStyle.Default).Normal.Border.Sides,
            ThemeCatalog.Dark.GetStyleSet(InputStyle.Default).Normal.Border.Sides,
            ThemeCatalog.Dark.GetStyleSet(ContainerStyle.Default).Normal.Border.Sides,
            ThemeCatalog.Dark.GetStyleSet(WindowStyle.Default).Normal.Border.Sides,
            ThemeCatalog.Dark.GetStyleSet(PopupStyle.Default).Normal.Border.Sides
        };

        for (var index = 0; index < controls.Length; index++)
        {
            controls[index].SetTheme(ThemeCatalog.Dark);

            controls[index].ActualBorder.Sides.ShouldBe(expected[index]);
        }
    }

    /// <summary>Verifies an uncustomized third-party control receives the global ControlBase role.</summary>
    [Fact]
    public void ActualFace_WhenThirdPartyControlDoesNotOverrideItsStyle_UsesControlAppearance()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);

        control.ActualFace.Background.Literal.ShouldBe(ThemeCatalog.Dark.ResolveColor(SemanticColor.Control));
    }

    /// <summary>Verifies a third-party control author can reuse a standard semantic role through the protected hook.</summary>
    [Fact]
    public void ActualBorder_WhenThirdPartyControlOverridesItsStyle_UsesSelectedAppearance()
    {
        var control = new ThirdPartyThemeControl();
        control.SetTheme(ThemeCatalog.Dark);
        var expected = ThemeCatalog.Dark.GetStyleSet(InputStyle.Default).Normal.Border;

        control.ActualBorder.Sides.ShouldBe(expected.Sides);
        control.ActualBorder.GlyphStyle.ShouldBe(expected.GlyphStyle);
        control.ActualBorder.Relief.ShouldBe(expected.Relief);
        control.ActualBorder.Foreground.Literal.ShouldBe(ThemeCatalog.Dark.Resolve(expected.Foreground));
        control.ActualBorder.Background.Literal.ShouldBe(ThemeCatalog.Dark.Resolve(expected.Background));
        control.ActualBorder.Attributes.Literal.ShouldBe(ThemeCatalog.Dark.Resolve(expected.Attributes));
    }
}
