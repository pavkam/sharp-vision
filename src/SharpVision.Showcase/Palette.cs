namespace SharpVision.Showcase;

using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Attributes = SharpVision.Terminal.Rendering.Attributes;

/// <summary>Defines the small semantic terminal palette used by the showcase dashboard.</summary>
internal static class Palette
{
    #region Colors

    /// <summary>Gets the darkest application canvas color.</summary>
    internal static Color Canvas => Color.Indexed(234);

    /// <summary>Gets the sidebar panel color.</summary>
    internal static Color Panel => Color.Indexed(236);

    /// <summary>Gets the raised card color.</summary>
    internal static Color Surface => Color.Indexed(238);

    /// <summary>Gets the distinct editable-input surface color.</summary>
    internal static Color InputSurface => Color.Indexed(240);

    /// <summary>Gets the primary cyan accent.</summary>
    internal static Color Accent => Color.Indexed(45);

    /// <summary>Gets the selected violet accent.</summary>
    internal static Color Highlight => Color.Indexed(99);

    /// <summary>Gets the bright readable foreground.</summary>
    internal static Color Text => Color.Indexed(255);

    /// <summary>Gets the low-emphasis foreground.</summary>
    internal static Color Muted => Color.Indexed(246);

    /// <summary>Gets the green affirmative color.</summary>
    internal static Color Success => Color.Indexed(78);

    /// <summary>Gets the amber attention color.</summary>
    internal static Color Warning => Color.Indexed(220);

    /// <summary>Gets the muted blue-gray border color.</summary>
    internal static Color Border => Color.Indexed(67);

    /// <summary>Gets the pointer-hover background color.</summary>
    internal static Color Hover => Color.Indexed(60);

    /// <summary>Gets the pressed background color.</summary>
    internal static Color Pressed => Color.Indexed(24);

    #endregion

    #region Styles

    /// <summary>Creates a style for a self-rendering navigation entry.</summary>
    internal static ControlStyle<Control> Navigation()
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Text);
        style.Set(Control.BackgroundProperty, State.Normal, Panel);
        style.Set(Control.BackgroundProperty, State.Hovered, Hover);
        style.Set(Control.ForegroundProperty, State.Focused, Accent);
        style.Set(Control.AttributesProperty, State.Focused, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Selected, Text);
        style.Set(Control.BackgroundProperty, State.Selected, Highlight);
        style.Set(Control.AttributesProperty, State.Selected, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Pressed, Text);
        style.Set(Control.BackgroundProperty, State.Pressed, Pressed);
        style.Set(Control.AttributesProperty, State.Pressed, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Disabled, Muted);
        style.Set(Control.BackgroundProperty, State.Disabled, Panel);
        style.Set(Control.AttributesProperty, State.Disabled, Attributes.Dim);
        return style;
    }

    /// <summary>Creates a style for text that lives inside the page heading surface.</summary>
    internal static ControlStyle<Control> HeaderText()
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Text);
        style.Set(Control.BackgroundProperty, State.Normal, Canvas);
        return style;
    }

    /// <summary>Creates a style for text that lives inside a raised card surface.</summary>
    internal static ControlStyle<Control> CardText()
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Text);
        style.Set(Control.BackgroundProperty, State.Normal, Surface);
        return style;
    }

    /// <summary>Creates a full-surface style for editable text controls.</summary>
    internal static ControlStyle<Control> Editor()
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Text);
        style.Set(Control.BackgroundProperty, State.Normal, InputSurface);
        style.Set(Control.BackgroundProperty, State.Hovered, Hover);
        style.Set(Control.ForegroundProperty, State.Focused, Accent);
        style.Set(Control.BackgroundProperty, State.Focused, Pressed);
        style.Set(Control.AttributesProperty, State.Focused, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Disabled, Muted);
        style.Set(Control.BackgroundProperty, State.Disabled, Surface);
        style.Set(Control.AttributesProperty, State.Disabled, Attributes.Dim);
        return style;
    }

    /// <summary>Creates a visibly stateful style for interactive live samples.</summary>
    internal static ControlStyle<Control> Interactive()
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Text);
        style.Set(Control.BackgroundProperty, State.Normal, Surface);
        style.Set(Control.ForegroundProperty, State.Hovered, Text);
        style.Set(Control.BackgroundProperty, State.Hovered, Hover);
        style.Set(Control.AttributesProperty, State.Hovered, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Focused, Accent);
        style.Set(Control.BackgroundProperty, State.Focused, Surface);
        style.Set(Control.AttributesProperty, State.Focused, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Pressed, Text);
        style.Set(Control.BackgroundProperty, State.Pressed, Pressed);
        style.Set(Control.AttributesProperty, State.Pressed, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Disabled, Muted);
        style.Set(Control.BackgroundProperty, State.Disabled, Panel);
        style.Set(Control.AttributesProperty, State.Disabled, Attributes.Dim);
        return style;
    }

    /// <summary>Creates the contrasting surface and selected-row treatment used by list controls.</summary>
    internal static ControlStyle<Control> List()
    {
        var style = new ControlStyle<Control>();
        ApplyListStyle(style);
        return style;
    }

    /// <summary>Creates a list-targeted style for application theme publication.</summary>
    internal static ControlStyle<List> ListForTheme()
    {
        var style = new ControlStyle<List>();
        ApplyListStyle(style);
        return style;
    }

    private static void ApplyListStyle<TControl>(ControlStyle<TControl> style)
        where TControl : Control
    {
        style.Set(Control.ForegroundProperty, State.Normal, Text);
        style.Set(Control.BackgroundProperty, State.Normal, InputSurface);
        style.Set(Control.ForegroundProperty, State.Hovered, Text);
        style.Set(Control.BackgroundProperty, State.Hovered, Hover);
        style.Set(Control.AttributesProperty, State.Hovered, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Focused, Accent);
        style.Set(Control.BackgroundProperty, State.Focused, InputSurface);
        style.Set(Control.AttributesProperty, State.Focused, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Selected, Text);
        style.Set(Control.BackgroundProperty, State.Selected, Highlight);
        style.Set(Control.AttributesProperty, State.Selected, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Pressed, Text);
        style.Set(Control.BackgroundProperty, State.Pressed, Pressed);
        style.Set(Control.AttributesProperty, State.Pressed, Attributes.Bold);
        style.Set(Control.ForegroundProperty, State.Disabled, Muted);
        style.Set(Control.BackgroundProperty, State.Disabled, Panel);
        style.Set(Control.AttributesProperty, State.Disabled, Attributes.Dim);
    }

    /// <summary>Creates a style for a low-emphasis dashboard label.</summary>
    internal static ControlStyle<Control> MutedText()
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Muted);
        style.Set(Control.BackgroundProperty, State.Normal, Panel);
        return style;
    }

    #endregion
}
