// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies theme collection, style chains, freezing, and cloning.</summary>
public sealed class ThemeTests
{
    /// <summary>Verifies a theme stores and returns one style per control type.</summary>
    [Fact]
    public void SetStyle_WhenControlStyleIsRegistered_GetStyleReturnsIt()
    {
        Theme theme = new();
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(7));
        theme.SetStyle(style);

        theme.GetStyle<Control>()!.TryGet(Control.ForegroundProperty, State.Normal, out Color? value)
            .ShouldBeTrue();
        value.ShouldBe(Color.Indexed(7));
    }

    /// <summary>Verifies sparse type styles inherit through the control hierarchy chain.</summary>
    [Fact]
    public void GetStyleChain_WhenOnlyBaseStyleExists_ResolvesOnDerivedControl()
    {
        Theme theme = new();
        ControlStyle<Control> baseStyle = new();
        baseStyle.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(2));
        theme.SetStyle(baseStyle);
        ProbeControl control = new();
        ThemeTestSupport.ApplyTheme(control, theme);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(2));
    }

    /// <summary>Verifies derived-type styles overlay base styles in the chain.</summary>
    [Fact]
    public void GetStyleChain_WhenDerivedStyleExists_OverlaysBaseStyle()
    {
        Theme theme = new();
        ControlStyle<Control> baseStyle = new();
        baseStyle.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(1));
        ControlStyle<ProbeControl> derivedStyle = new();
        derivedStyle.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(4));
        theme.SetStyle(baseStyle);
        theme.SetStyle(derivedStyle);
        ProbeControl control = new();
        ThemeTestSupport.ApplyTheme(control, theme);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies frozen themes reject further mutation.</summary>
    [Fact]
    public void SetStyle_WhenThemeIsFrozen_Throws()
    {
        Theme theme = new();
        theme.Freeze();

        _ = Should.Throw<InvalidOperationException>(() => theme.SetStyle(new ControlStyle<Control>()));
    }

    /// <summary>Verifies cloned themes receive independent style copies.</summary>
    [Fact]
    public void Clone_WhenSourceMutatesAfterCopy_DoesNotAffectClone()
    {
        Theme theme = new();
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(3));
        theme.SetStyle(style);
        Theme clone = theme.Clone();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(9));

        clone.GetStyle<Control>()!.TryGet(Control.ForegroundProperty, State.Normal, out Color? value)
            .ShouldBeTrue();
        value.ShouldBe(Color.Indexed(3));
    }
}
