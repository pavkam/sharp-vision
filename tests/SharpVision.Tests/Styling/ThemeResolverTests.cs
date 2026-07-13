// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Tests.Support;

using Shouldly;

/// <summary>Verifies theme resolver precedence through the public cascade.</summary>
public sealed class ThemeResolverTests
{
    /// <summary>Verifies local values win over themed and per-instance overlays.</summary>
    [Fact]
    public void Resolve_WhenLocalValueExists_WinsOverThemeAndInstanceStyle()
    {
        Theme theme = new Theme();
        ControlStyle<Control> themed = new ControlStyle<Control>();
        themed.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(4));
        theme.SetStyle(themed);
        ProbeControl control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, theme);
        control.Foreground = Color.Indexed(9);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(9));
    }

    /// <summary>Verifies per-instance style overlays theme defaults without flowing to descendants.</summary>
    [Fact]
    public void Resolve_WhenInstanceStyleExists_OverlaysThemeWithoutInheritance()
    {
        Theme theme = new Theme();
        ControlStyle<Control> themed = new ControlStyle<Control>();
        themed.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(1));
        theme.SetStyle(themed);
        ProbeContainer root = new ProbeContainer();
        ProbeControl child = new ProbeControl();
        root.Children.Add(child);
        ThemeTestSupport.ApplyTheme(root, theme);
        ControlStyle<Control> overlay = new ControlStyle<Control>();
        overlay.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(2));
        child.Style = overlay;

        ThemeTestSupport.Resolve(child, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(2));
        ThemeTestSupport.Resolve(root, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(1));
    }
}
