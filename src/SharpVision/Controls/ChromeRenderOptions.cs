namespace SharpVision.Controls;

using SharpVision.Terminal.Geometry;

using TerminalStyle = Terminal.Rendering.Style;

/// <summary>Overrides one control chrome render pass.</summary>
internal readonly struct ChromeRenderOptions
{
    /// <summary>Gets the body rectangle receiving fill and border chrome.</summary>
    internal Rect? BodyBounds { get; init; }

    /// <summary>Gets the rectangle excluded from shadow rasterization.</summary>
    internal Rect? ShadowExcludeBounds { get; init; }

    /// <summary>Gets the inherited style used for detached shadow treatment.</summary>
    internal TerminalStyle? ShadowAppearanceSource { get; init; }

    /// <summary>Gets whether to preserve the button bottom-shadow gap.</summary>
    internal bool PreserveButtonShadowGap { get; init; }

    /// <summary>Gets whether to clear the pressed face even when fill mode is transparent.</summary>
    internal bool ClearBodyWhenPressedWithShadow { get; init; }

    /// <summary>Gets an alternate validated glyph family for border drawing.</summary>
    internal Glyphs? BorderGlyphs { get; init; }

    /// <summary>Gets whether shadow drawing is skipped.</summary>
    internal bool SkipShadow { get; init; }

    /// <summary>Gets whether border drawing is skipped.</summary>
    internal bool SkipBorder { get; init; }

    /// <summary>Gets whether body fill is skipped.</summary>
    internal bool SkipBodyFill { get; init; }
}
