namespace SharpVision.Styling;

using SharpVision.Controls;
using SharpVision.Terminal.Protocols;

using TerminalAttributes = Terminal.Rendering.Attributes;

/// <summary>Exposes frozen standard themes built from the public theme API.</summary>
public static class Themes
{
    /// <summary>Gets the frozen light standard theme.</summary>
    public static Theme White { get; } = CreateWhite();

    /// <summary>Gets the frozen dark standard theme.</summary>
    public static Theme Dark { get; } = CreateDark();

    private static Theme CreateWhite()
    {
        var theme = new Theme();
        theme.SetStyle(CreateBaseControlStyle(
            foreground: Color.Indexed(0),
            background: Color.Indexed(15),
            hoverForeground: Color.Indexed(4)));
        theme.Freeze();
        return theme;
    }

    private static Theme CreateDark()
    {
        var theme = new Theme();
        theme.SetStyle(CreateBaseControlStyle(
            foreground: Color.Indexed(15),
            background: Color.Indexed(0),
            hoverForeground: Color.Indexed(14)));
        theme.Freeze();
        return theme;
    }

    private static ControlStyle<Control> CreateBaseControlStyle(
        Color foreground,
        Color background,
        Color hoverForeground)
    {
        var style = new ControlStyle<Control>();
        var border = Color.Indexed(8);
        var shadowForeground = Color.Indexed(8);
        var selectedForeground = Color.Indexed(15);
        var selectedBackground = Color.Indexed(4);
        var disabledForeground = Color.Indexed(8);

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
