// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies theme resolver precedence through the public cascade.</summary>
public sealed class ThemeResolverTests
{
    /// <summary>Verifies local values win over themed and per-instance overlays.</summary>
    [Fact]
    public void Resolve_WhenLocalValueExists_WinsOverThemeAndInstanceStyle()
    {
        Theme theme = new();
        ControlStyle<Control> themed = new();
        themed.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(4));
        theme.SetStyle(themed);
        ProbeControl control = new();
        ThemeTestSupport.ApplyTheme(control, theme);
        control.Foreground = Color.Indexed(9);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(9));
    }

    /// <summary>Verifies per-instance style overlays theme defaults without flowing to descendants.</summary>
    [Fact]
    public void Resolve_WhenInstanceStyleExists_OverlaysThemeWithoutInheritance()
    {
        Theme theme = new();
        ControlStyle<Control> themed = new();
        themed.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(1));
        theme.SetStyle(themed);
        ProbeContainer root = new();
        ProbeControl child = new();
        root.Children.Add(child);
        ThemeTestSupport.ApplyTheme(root, theme);
        ControlStyle<Control> overlay = new();
        overlay.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(2));
        child.Style = overlay;

        ThemeTestSupport.Resolve(child, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(2));
        ThemeTestSupport.Resolve(root, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(1));
    }
}
