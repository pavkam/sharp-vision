// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;


/// <summary>Builds a frozen <see cref="Theme"/> from resolved semantic role colors using the standard recipe.</summary>
internal static class ThemeBuilder
{
    /// <summary>Builds and freezes a theme from the twelve resolved role colors.</summary>
    /// <param name="roles">The resolved colors for every <see cref="ColorRole"/> member.</param>
    /// <returns>The frozen theme carrying the roles and one base control style.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="roles"/> is null.</exception>
    public static Theme Build(IReadOnlyDictionary<ColorRole, Color> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var theme = new Theme();

        foreach (var role in Enum.GetValues<ColorRole>())
        {
            theme.SetColor(role, roles[role]);
        }

        theme.SetStyle(BuildBaseStyle());
        theme.SetStyle(BuildContainerStyle());
        theme.SetStyle(BuildButtonStyle());
        theme.SetStyle(BuildComboBoxStyle());
        theme.SetStyle(BuildListStyle());
        theme.SetStyle(BuildTextInputStyle());
        theme.SetStyle(BuildScrollBarStyle());
        theme.SetStyle(BuildCheckBoxStyle());
        theme.SetStyle(BuildRadioButtonStyle());
        theme.Freeze();
        return theme;
    }

    /// <summary>Builds the base control style using semantic role colors.</summary>
    /// <returns>
    /// A theme-independent style: every color is a deferred <see cref="ColorKind.Role"/> value from
    /// <see cref="ThemeColors"/> that <see cref="ThemeResolver"/> collapses to the active theme's
    /// palette concrete during resolution, so the same style instance can back any theme.
    /// </returns>
    private static ControlStyle<Control> BuildBaseStyle()
    {
        var style = new ControlStyle<Control>();

        style.Set(Control.ForegroundProperty, State.Normal, ThemeColors.Foreground);
        style.Set(Control.BackgroundProperty, State.Normal, ThemeColors.Background);
        style.Set(Control.BorderColorProperty, State.Normal, ThemeColors.Border);
        style.Set(Control.ForegroundProperty, State.Hovered, ThemeColors.Accent);
        style.Set(Control.ForegroundProperty, State.Selected, ThemeColors.SelectionForeground);
        style.Set(Control.BackgroundProperty, State.Selected, ThemeColors.SelectionBackground);
        style.Set(Control.ForegroundProperty, State.Disabled, ThemeColors.Muted);
        style.Set(Control.ShadowForegroundProperty, State.Normal, ThemeColors.Border);

        return style;
    }

    private static ControlStyle<Button> BuildButtonStyle()
    {
        var style = new ControlStyle<Button>();

        style.Set(Control.BorderColorProperty, State.Hovered, ThemeColors.Accent);
        style.Set(Control.BorderColorProperty, State.Focused, ThemeColors.Accent);
        style.Set(Control.AttributesProperty, State.Focused, TerminalAttributes.Underline);
        style.Set(Control.BorderColorProperty, State.Pressed, ThemeColors.Accent);
        style.Set(Control.ForegroundProperty, State.Pressed, ThemeColors.Accent);

        return style;
    }

    private static ControlStyle<Container> BuildContainerStyle()
    {
        var style = new ControlStyle<Container>();

        style.Set(Container.ScrollBarChromeProperty, State.Normal, ScrollBarChrome.Thin);
        style.Set(Container.ScrollBarFillProperty, State.Normal, ScrollBarFill.Line);

        return style;
    }

    private static ControlStyle<ComboBox> BuildComboBoxStyle()
    {
        var style = new ControlStyle<ComboBox>();

        style.Set(Control.FillModeProperty, State.Normal, FillMode.Opaque);
        style.Set(Control.BackgroundProperty, State.Normal, ThemeColors.Surface);
        style.Set(Control.ForegroundProperty, State.Focused, ThemeColors.Accent);
        style.Set(Control.BorderColorProperty, State.Focused, ThemeColors.Accent);

        return style;
    }

    private static ControlStyle<List> BuildListStyle()
    {
        var style = new ControlStyle<List>();

        style.Set(Control.BackgroundProperty, State.Hovered, ThemeColors.Surface);

        return style;
    }

    private static ControlStyle<TextInput> BuildTextInputStyle()
    {
        var style = new ControlStyle<TextInput>();

        style.Set(TextInput.ScrollBarChromeProperty, State.Normal, ScrollBarChrome.Thin);
        style.Set(TextInput.ScrollBarFillProperty, State.Normal, ScrollBarFill.Line);

        return style;
    }

    private static ControlStyle<ScrollBar> BuildScrollBarStyle()
    {
        var style = new ControlStyle<ScrollBar>();

        style.Set(ScrollBar.ChromeProperty, State.Normal, ScrollBarChrome.Thin);
        style.Set(ScrollBar.FillProperty, State.Normal, ScrollBarFill.Line);
        style.Set(Control.ForegroundProperty, State.Focused, ThemeColors.Accent);
        style.Set(Control.ForegroundProperty, State.Pressed, ThemeColors.Accent);

        return style;
    }

    private static ControlStyle<CheckBox> BuildCheckBoxStyle()
    {
        var style = new ControlStyle<CheckBox>();

        style.Set(Control.ForegroundProperty, State.Focused, ThemeColors.Accent);
        style.Set(Control.ForegroundProperty, State.Checked, ThemeColors.Accent);
        style.Set(Control.ForegroundProperty, State.Indeterminate, ThemeColors.Warning);

        return style;
    }

    private static ControlStyle<RadioButton> BuildRadioButtonStyle()
    {
        var style = new ControlStyle<RadioButton>();

        style.Set(Control.ForegroundProperty, State.Focused, ThemeColors.Accent);
        style.Set(Control.ForegroundProperty, State.Checked, ThemeColors.Accent);

        return style;
    }
}
