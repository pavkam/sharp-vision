// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies built-in controls select their library-owned semantic theme profiles.</summary>
public sealed class SemanticRoleTests
{
    /// <summary>Verifies representative built-ins receive the matching profile without application plumbing.</summary>
    [Fact]
    public void ActualBorder_WhenBuiltInControlsAreThemed_UsesLibraryOwnedRole()
    {
        ControlBase[] controls = [new Button(), new TextInput(), new ComboBox(), new GroupBox(), new Window(), new Popup()];
        var expected = new[]
        {
            Themes.Dark.Input.Normal.Border.Sides,
            Themes.Dark.Input.Normal.Border.Sides,
            Themes.Dark.Input.Normal.Border.Sides,
            Themes.Dark.Container.Normal.Border.Sides,
            Themes.Dark.Window.Normal.Border.Sides,
            Themes.Dark.Popup.Normal.Border.Sides
        };

        for (var index = 0; index < controls.Length; index++)
        {
            controls[index].SetTheme(Themes.Dark);

            controls[index].ActualBorder.Sides.ShouldBe(expected[index]);
        }
    }

    /// <summary>Verifies an uncustomized third-party control receives the global ControlBase role.</summary>
    [Fact]
    public void ActualFace_WhenThirdPartyControlDoesNotOverrideRole_UsesControlProfile()
    {
        var control = new ProbeControl();
        control.SetTheme(Themes.Dark);

        control.ActualFace.Background.Literal.ShouldBe(Themes.Dark.ResolveColor(ThemeColor.Control));
    }

    /// <summary>Verifies a third-party control author can reuse a standard semantic role through the protected hook.</summary>
    [Fact]
    public void ActualBorder_WhenThirdPartyControlOverridesRole_UsesSelectedProfile()
    {
        var control = new ThirdPartyThemeControl();
        var button = new Button();
        control.SetTheme(Themes.Dark);
        button.SetTheme(Themes.Dark);

        control.ActualBorder.ShouldBe(button.ActualBorder);
    }
}
