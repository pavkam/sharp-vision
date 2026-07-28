// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Creates complete appearance values for focused behavior and rendering specimens.</summary>
internal static class AppearanceTestValues
{
    /// <summary>Creates a complete theme-responsive face with optional literal members.</summary>
    internal static Face Face(
        ColorValue? foreground = null,
        ColorValue? background = null,
        AttributeValue? attributes = null,
        Underline underline = Underline.None,
        ColorValue? underlineColor = null) => new(
            foreground ?? ThemeColor.ControlText,
            background ?? Color.Transparent,
            attributes ?? ThemeDecoration.NormalText,
            underline,
            underlineColor ?? Color.Default);

    /// <summary>Creates a complete theme-responsive border.</summary>
    internal static Border Border(
        BorderSide sides,
        BorderGlyphStyle glyphStyle = default,
        ColorValue? foreground = null,
        ColorValue? background = null,
        AttributeValue? attributes = null) => new(
            sides,
            glyphStyle,
            foreground ?? ThemeColor.ControlBorder,
            background ?? Color.Transparent,
            attributes ?? ThemeDecoration.Border);

    /// <summary>Creates a complete theme-responsive shadow.</summary>
    internal static Shadow Shadow(
        bool visible = true,
        ShadowMode mode = ShadowMode.Composite,
        Point offset = default,
        Rune glyph = default,
        ColorValue? foreground = null,
        ColorValue? background = null,
        AttributeValue? attributes = null) => new(
            visible,
            mode,
            offset,
            glyph,
            foreground ?? ThemeColor.ControlShadow,
            background ?? Color.Transparent,
            attributes ?? ThemeDecoration.Shadow);
}
