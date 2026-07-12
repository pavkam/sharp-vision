using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Attributes = SharpVision.Terminal.Rendering.Attributes;

namespace SharpVision.Showcase;

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
    internal static Style Navigation()
    {
        var style = new Style();
        style.Set(State.Normal, new Appearance(Text, Panel));
        style.Set(State.Hovered, new Appearance(background: Hover));
        style.Set(State.Focused, new Appearance(foreground: Accent, attributes: Attributes.Bold));
        style.Set(State.Checked, new Appearance(Text, Highlight, Attributes.Bold));
        style.Set(State.Pressed, new Appearance(Text, Pressed, Attributes.Bold));
        style.Set(State.Disabled, new Appearance(Muted, Panel, Attributes.Dim));
        return style;
    }

    /// <summary>Creates a style for text that lives inside the page heading surface.</summary>
    internal static Style HeaderText()
    {
        var style = new Style();
        style.Set(State.Normal, new Appearance(Text, Canvas));
        return style;
    }

    /// <summary>Creates a style for text that lives inside a raised card surface.</summary>
    internal static Style CardText()
    {
        var style = new Style();
        style.Set(State.Normal, new Appearance(Text, Surface));
        return style;
    }

    /// <summary>Creates a full-surface style for editable text controls.</summary>
    internal static Style Editor()
    {
        var style = new Style();
        style.Set(State.Normal, new Appearance(Text, InputSurface));
        style.Set(State.Hovered, new Appearance(background: Hover));
        style.Set(State.Focused, new Appearance(Accent, Pressed, Attributes.Bold));
        style.Set(State.Disabled, new Appearance(Muted, Surface, Attributes.Dim));
        return style;
    }

    /// <summary>Creates a style for a low-emphasis dashboard label.</summary>
    internal static Style MutedText()
    {
        var style = new Style();
        style.Set(State.Normal, new Appearance(Muted, Panel));
        return style;
    }

    #endregion
}
