// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Terminal.Protocols;

/// <summary>Exposes frozen standard themes built from the public theme API.</summary>
public static class Themes
{
    /// <summary>Gets the frozen light standard theme.</summary>
    public static Theme White { get; } = CreateWhite();

    /// <summary>Gets the frozen dark standard theme.</summary>
    public static Theme Dark { get; } = CreateDark();

    private static Theme CreateWhite()
    {
        Theme theme = new();
        theme.SetStyle(CreateBaseControlStyle(
            foreground: Color.Indexed(0),
            background: Color.Indexed(15),
            hoverForeground: Color.Indexed(4)));
        ApplyColors(
            theme,
            foreground: Color.Indexed(0),
            background: Color.Indexed(15),
            surface: Color.Indexed(7),
            accent: Color.Indexed(4));
        theme.Freeze();
        return theme;
    }

    private static Theme CreateDark()
    {
        Theme theme = new();
        theme.SetStyle(CreateBaseControlStyle(
            foreground: Color.Indexed(15),
            background: Color.Indexed(0),
            hoverForeground: Color.Indexed(14)));
        ApplyColors(
            theme,
            foreground: Color.Indexed(15),
            background: Color.Indexed(0),
            surface: Color.Indexed(8),
            accent: Color.Indexed(14));
        theme.Freeze();
        return theme;
    }

    private static void ApplyColors(Theme theme, Color foreground, Color background, Color surface, Color accent)
    {
        theme.SetColor(ColorRole.Foreground, foreground);
        theme.SetColor(ColorRole.Background, background);
        theme.SetColor(ColorRole.Surface, surface);
        theme.SetColor(ColorRole.Border, Color.Indexed(8));
        theme.SetColor(ColorRole.Accent, accent);
        theme.SetColor(ColorRole.Muted, Color.Indexed(8));
        theme.SetColor(ColorRole.SelectionBackground, Color.Indexed(4));
    }

    private static ControlStyle<Control> CreateBaseControlStyle(
        Color foreground,
        Color background,
        Color hoverForeground)
    {
        ControlStyle<Control> style = new();
        Color border = Color.Indexed(8);
        Color shadowForeground = Color.Indexed(8);
        Color selectedForeground = Color.Indexed(15);
        Color selectedBackground = Color.Indexed(4);
        Color disabledForeground = Color.Indexed(8);

        style.Set(Control.ForegroundProperty, State.Normal, foreground);
        style.Set(Control.BackgroundProperty, State.Normal, background);
        style.Set(Control.BorderColorProperty, State.Normal, border);
        style.Set(Control.ForegroundProperty, State.Hovered, hoverForeground);
        style.Set(Control.AttributesProperty, State.Focused, TerminalAttributes.Underline);
        style.Set(Control.ForegroundProperty, State.Checked, selectedForeground);
        style.Set(Control.BackgroundProperty, State.Checked, selectedBackground);
        style.Set(Control.ForegroundProperty, State.Selected, selectedForeground);
        style.Set(Control.BackgroundProperty, State.Selected, selectedBackground);
        style.Set(Control.ForegroundProperty, State.Disabled, disabledForeground);
        style.Set(Control.ShadowForegroundProperty, State.Normal, shadowForeground);

        return style;
    }
}
