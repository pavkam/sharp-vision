// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


/// <summary>Overrides one control chrome render pass.</summary>
public readonly struct ChromeRenderOptions
{
    /// <summary>Gets the body rectangle receiving fill and border chrome.</summary>
    public Rect? BodyBounds { get; init; }

    /// <summary>Gets the rectangle excluded from shadow rasterization.</summary>
    public Rect? ShadowExcludeBounds { get; init; }

    /// <summary>Gets the inherited style used for detached shadow treatment.</summary>
    public TerminalStyle? ShadowAppearanceSource { get; init; }

    /// <summary>Gets whether to preserve the button bottom-shadow gap.</summary>
    public bool PreserveButtonShadowGap { get; init; }

    /// <summary>Gets whether to clear the pressed face even when fill mode is transparent.</summary>
    public bool ClearBodyWhenPressedWithShadow { get; init; }

    /// <summary>Gets an alternate validated glyph family for border drawing.</summary>
    public Glyphs? BorderGlyphs { get; init; }

    /// <summary>Gets whether shadow drawing is skipped.</summary>
    public bool SkipShadow { get; init; }

    /// <summary>Gets whether border drawing is skipped.</summary>
    public bool SkipBorder { get; init; }

    /// <summary>Gets whether body fill is skipped.</summary>
    public bool SkipBodyFill { get; init; }
}
