// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Resolves one control's local appearance against ambient text and its immutable theme.</summary>
internal static class AppearanceResolver
{
    internal static ResolvedAppearance Resolve(Control control, VisualState visualState)
    {
        ArgumentNullException.ThrowIfNull(control);

        var normal = control.CreateDefaultAppearance(VisualState.Normal);
        normal = InheritAmbientText(control, normal);
        normal = control.ApplyLocalAppearance(normal);

        foreach (var overlay in VisualStateOrder.OrderedOverlays)
        {
            if ((visualState & overlay) != 0)
            {
                normal = normal.Overlay(control.CreateDefaultAppearance(overlay));
            }
        }

        var foreground = Resolve(control, normal.Foreground ?? ThemeColor.From(ColorRole.Foreground));
        var background = normal.Background is { } value
            ? Resolve(control, value)
            : Color.Default;
        var attributes = normal.Attributes ?? TerminalAttributes.None;
        var underline = normal.Underline;
        var underlineColor = normal.UnderlineColor is { } color
            ? Resolve(control, color)
            : Color.Default;
        (attributes, var resolvedUnderline, var resolvedUnderlineColor) = Decoration.Resolve(
            new TerminalStyle(foreground, background, attributes),
            attributes,
            underline,
            normal.UnderlineColor is { } ? underlineColor : null);

        var style = new TerminalStyle(
            foreground,
            background,
            attributes,
            underline: resolvedUnderline,
            underlineColor: resolvedUnderlineColor);
        var borderColor = normal.BorderColor is { } border
            ? Resolve(control, border)
            : Resolve(control, ThemeColor.From(ColorRole.Border));
        var borderAttributes = normal.BorderAttributes ?? attributes;
        var shadowForeground = normal.ShadowForeground is { } shadowForegroundValue
            ? Resolve(control, shadowForegroundValue)
            : Resolve(control, ThemeColor.From(ColorRole.Border));
        var shadowBackground = normal.ShadowBackground is { } shadowBackgroundValue
            ? Resolve(control, shadowBackgroundValue)
            : background;
        var shadowAttributes = normal.ShadowAttributes ?? attributes;

        return new ResolvedAppearance(
            style,
            normal.Background.HasValue ? BackgroundMode.Opaque : BackgroundMode.Transparent,
            new TerminalStyle(borderColor, background, borderAttributes),
            new TerminalStyle(shadowForeground, shadowBackground, shadowAttributes));
    }

    private static Appearance InheritAmbientText(Control control, Appearance normal)
    {
        if (control.AppearanceBoundary || control.Parent is null)
        {
            return normal;
        }

        var parent = control.Parent.GetNormalAmbientAppearance();
        return new Appearance(
            parent.Foreground ?? normal.Foreground,
            normal.Background,
            parent.Attributes ?? normal.Attributes,
            parent.Underline ?? normal.Underline,
            parent.UnderlineColor ?? normal.UnderlineColor,
            normal.BorderColor,
            normal.BorderAttributes,
            normal.ShadowForeground,
            normal.ShadowBackground,
            normal.ShadowAttributes);
    }

    private static Color Resolve(Control control, ThemeColor color) =>
        control.Theme?.Resolve(color) ?? ResolveWithoutTheme(color);

    private static Color ResolveWithoutTheme(ThemeColor color) => color.TryGetColor(out var concrete) ? concrete : Color.Default;
}
