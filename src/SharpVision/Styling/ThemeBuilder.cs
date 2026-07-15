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
        style.Set(Control.AttributesProperty, State.Focused, TerminalAttributes.Underline);
        style.Set(Control.ForegroundProperty, State.Checked, ThemeColors.SelectionForeground);
        style.Set(Control.BackgroundProperty, State.Checked, ThemeColors.SelectionBackground);
        style.Set(Control.ForegroundProperty, State.Selected, ThemeColors.SelectionForeground);
        style.Set(Control.BackgroundProperty, State.Selected, ThemeColors.SelectionBackground);
        style.Set(Control.ForegroundProperty, State.Disabled, ThemeColors.Muted);
        style.Set(Control.ShadowForegroundProperty, State.Normal, ThemeColors.Border);

        return style;
    }
}
