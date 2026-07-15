// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies role colors resolve to the active theme's palette during property resolution.</summary>
public sealed class SemanticColorResolutionTests
{
    private static Theme ThemeWith(Color foreground, Color accent)
    {
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, ThemeColors.Foreground);
        theme.SetStyle(style);
        theme.SetColor(ColorRole.Foreground, foreground);
        theme.SetColor(ColorRole.Accent, accent);
        theme.Freeze();
        return theme;
    }

    /// <summary>Verifies a role color assigned as the control's style value resolves to the theme's palette color.</summary>
    [Fact]
    public void GetValue_WhenPropertyIsRoleColor_ResolvesToPaletteColor()
    {
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, ThemeWith(Color.Indexed(15), Color.Rgb(10, 20, 30)));

        control.GetValue(Control.ForegroundProperty).ShouldBe(Color.Indexed(15));
    }

    /// <summary>Verifies the design-time theme/type resolve overload also collapses role colors.</summary>
    [Fact]
    public void DesignTimeResolve_WhenRoleColor_ResolvesAgainstTheme()
    {
        var theme = ThemeWith(Color.Indexed(7), Color.Indexed(4));

        ThemeResolver.Resolve(theme, typeof(Control), Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(7));
    }

    /// <summary>Verifies a local role-color override resolves and tracks a subsequent theme swap.</summary>
    [Fact]
    public void LocalRoleColor_ResolvesAndTracksThemeSwap()
    {
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, ThemeWith(Color.Indexed(1), Color.Indexed(2)));
        control.SetValue(Control.BackgroundProperty, ThemeColors.Accent);

        control.GetValue(Control.BackgroundProperty).ShouldBe(Color.Indexed(2));

        ThemeTestSupport.ApplyTheme(control, ThemeWith(Color.Indexed(1), Color.Indexed(9)));
        control.GetValue(Control.BackgroundProperty).ShouldBe(Color.Indexed(9));
    }

    /// <summary>Verifies the live control resolver overload collapses a local role color on its local-value path.</summary>
    [Fact]
    public void LiveResolve_WhenLocalRoleColor_ResolvesToPaletteColor()
    {
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, ThemeWith(Color.Indexed(1), Color.Indexed(2)));
        control.SetValue(Control.BackgroundProperty, ThemeColors.Accent);

        ThemeTestSupport.Resolve(control, Control.BackgroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(2));
    }

    /// <summary>Verifies a defined role the theme never sets falls back to <see cref="Color.Default"/>.</summary>
    [Fact]
    public void Resolve_WhenDefinedRoleUndefinedByTheme_FallsBackToDefault()
    {
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, ThemeColors.Warning);
        theme.SetStyle(style);
        theme.SetColor(ColorRole.Foreground, Color.Indexed(15));
        theme.SetColor(ColorRole.Background, Color.Indexed(0));
        theme.Freeze();

        ThemeResolver.Resolve(theme, typeof(Control), Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Default);
    }

    /// <summary>Verifies a role color carrying an unknown role id fails fast during resolution.</summary>
    [Fact]
    public void GetValue_WhenRoleIdIsUnknown_Throws()
    {
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, ThemeWith(Color.Indexed(1), Color.Indexed(2)));
        control.SetValue(Control.BackgroundProperty, Color.Role(99));

        _ = Should.Throw<ArgumentException>(() => control.GetValue(Control.BackgroundProperty));
    }
}
