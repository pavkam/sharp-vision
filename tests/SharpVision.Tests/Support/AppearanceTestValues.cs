// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Creates complete appearance values for focused behavior and rendering specimens.</summary>
internal static class AppearanceTestValues
{
    /// <summary>Creates a complete theme-responsive face with optional literal members.</summary>
    internal static Face Face(
        ControlColor? foreground = null,
        ControlColor? background = null,
        ControlDecoration? attributes = null,
        Underline underline = Underline.None,
        ControlColor? underlineColor = null) => new(
            foreground ?? SemanticColor.ControlText,
            background ?? Color.Transparent,
            attributes ?? SemanticDecoration.NormalText,
            underline,
            underlineColor ?? Color.Default);

    /// <summary>Creates a complete theme-responsive border.</summary>
    internal static Border Border(
        BorderSide sides,
        BorderGlyphStyle glyphStyle = default,
        ControlColor? foreground = null,
        ControlColor? background = null,
        ControlDecoration? attributes = null) => new(
            sides,
            glyphStyle,
            foreground ?? SemanticColor.ControlBorder,
            background ?? Color.Transparent,
            attributes ?? SemanticDecoration.Border);

    /// <summary>Creates a complete theme-responsive shadow.</summary>
    internal static Shadow Shadow(
        bool visible = true,
        ShadowMode mode = ShadowMode.Composite,
        Point offset = default,
        Rune glyph = default,
        ControlColor? foreground = null,
        ControlColor? background = null,
        ControlDecoration? attributes = null) => new(
            visible,
            mode,
            offset,
            glyph,
            foreground ?? SemanticColor.ControlShadow,
            background ?? Color.Transparent,
            attributes ?? SemanticDecoration.Shadow);
}
