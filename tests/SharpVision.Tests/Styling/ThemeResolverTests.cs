// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies theme resolver precedence through the public cascade.</summary>
public sealed class ThemeResolverTests
{
    /// <summary>Verifies both public resolver overloads reject undefined visual-state bits.</summary>
    [Fact]
    public void Resolve_WhenVisualStateContainsUnknownBits_Throws()
    {
        var control = new ProbeControl();
        var theme = new Theme();
        var unknown = (State) (1 << 20);

        var live = Should.Throw<ArgumentOutOfRangeException>(() =>
            ThemeResolver.Resolve(control, Control.ForegroundProperty, unknown));
        var designTime = Should.Throw<ArgumentOutOfRangeException>(() =>
            ThemeResolver.Resolve(theme, typeof(ProbeControl), Control.ForegroundProperty, unknown));

        live.ParamName.ShouldBe("visualState");
        designTime.ParamName.ShouldBe("visualState");
    }

    /// <summary>Verifies local values win over themed and per-instance overlays.</summary>
    [Fact]
    public void Resolve_WhenLocalValueExists_WinsOverThemeAndInstanceStyle()
    {
        var theme = new Theme();
        var themed = new ControlStyle<Control>();
        themed.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(4));
        theme.SetStyle(themed);
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, theme);
        control.Foreground = Color.Indexed(9);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(9));
    }

    /// <summary>Verifies per-instance style overlays theme defaults without flowing to descendants.</summary>
    [Fact]
    public void Resolve_WhenInstanceStyleExists_OverlaysThemeWithoutInheritance()
    {
        var theme = new Theme();
        var themed = new ControlStyle<Control>();
        themed.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(1));
        theme.SetStyle(themed);
        var root = new ProbeContainer();
        var child = new ProbeControl();
        root.Children.Add(child);
        ThemeTestSupport.ApplyTheme(root, theme);
        var overlay = new ControlStyle<Control>();
        overlay.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(2));
        child.Style = overlay;

        ThemeTestSupport.Resolve(child, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(2));
        ThemeTestSupport.Resolve(root, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(1));
    }
}
