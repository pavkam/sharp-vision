// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Terminal.Protocols;

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

        Theme theme = new();

        foreach (ColorRole role in Enum.GetValues<ColorRole>())
        {
            theme.SetColor(role, roles[role]);
        }

        theme.SetStyle(BuildBaseStyle(roles));
        theme.Freeze();
        return theme;
    }

    private static ControlStyle<Control> BuildBaseStyle(IReadOnlyDictionary<ColorRole, Color> roles)
    {
        ControlStyle<Control> style = new();
        Color foreground = roles[ColorRole.Foreground];
        Color background = roles[ColorRole.Background];
        Color border = roles[ColorRole.Border];
        Color accent = roles[ColorRole.Accent];
        Color selectionBackground = roles[ColorRole.SelectionBackground];
        Color selectionForeground = roles[ColorRole.SelectionForeground];
        Color muted = roles[ColorRole.Muted];

        style.Set(Control.ForegroundProperty, State.Normal, foreground);
        style.Set(Control.BackgroundProperty, State.Normal, background);
        style.Set(Control.BorderColorProperty, State.Normal, border);
        style.Set(Control.ForegroundProperty, State.Hovered, accent);
        style.Set(Control.AttributesProperty, State.Focused, TerminalAttributes.Underline);
        style.Set(Control.ForegroundProperty, State.Checked, selectionForeground);
        style.Set(Control.BackgroundProperty, State.Checked, selectionBackground);
        style.Set(Control.ForegroundProperty, State.Selected, selectionForeground);
        style.Set(Control.BackgroundProperty, State.Selected, selectionBackground);
        style.Set(Control.ForegroundProperty, State.Disabled, muted);
        style.Set(Control.ShadowForegroundProperty, State.Normal, border);

        return style;
    }
}
