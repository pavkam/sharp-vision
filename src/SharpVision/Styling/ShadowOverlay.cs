// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines an optional member-wise shadow contribution.</summary>
public readonly record struct ShadowOverlay
{
    /// <summary>Initializes a partial shadow contribution.</summary>
    /// <param name="isVisible">The optional visibility contribution.</param>
    /// <param name="mode">The optional defined shadow-mode contribution.</param>
    /// <param name="offset">The optional offset contribution.</param>
    /// <param name="glyph">The optional single-cell glyph contribution.</param>
    /// <param name="foreground">The optional paintable foreground contribution.</param>
    /// <param name="background">The optional background contribution.</param>
    /// <param name="attributes">The optional attribute contribution.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="mode"/> is undefined, <paramref name="glyph"/> is not exactly one cell wide,
    /// or <paramref name="foreground"/> is not a paintable color.
    /// </exception>
    public ShadowOverlay(
        bool? isVisible = null,
        ShadowMode? mode = null,
        Point? offset = null,
        Rune? glyph = null,
        ControlColor? foreground = null,
        ControlColor? background = null,
        ControlDecoration? attributes = null)
    {
        if (mode is { } modeValue && !Enum.IsDefined(modeValue))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The shadow mode is unknown.");
        }

        if (glyph is { } glyphValue)
        {
            _ = glyphValue.ValidateSingleCell(nameof(glyph));
        }

        if (foreground is { } foregroundValue)
        {
            ControlColor.ValidatePaint(foregroundValue, nameof(foreground));
        }

        Visible = isVisible;
        Mode = mode;
        Offset = offset;
        Glyph = glyph;
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
    }

    /// <summary>Gets the optional visibility contribution.</summary>
    public bool? Visible { get; }

    /// <summary>Gets the optional composition-mode contribution.</summary>
    public ShadowMode? Mode { get; }

    /// <summary>Gets the optional translation contribution.</summary>
    public Point? Offset { get; }

    /// <summary>Gets the optional block-glyph contribution.</summary>
    public Rune? Glyph { get; }

    /// <summary>Gets the optional foreground contribution.</summary>
    public ControlColor? Foreground { get; }

    /// <summary>Gets the optional background contribution.</summary>
    public ControlColor? Background { get; }

    /// <summary>Gets the optional attribute contribution.</summary>
    public ControlDecoration? Attributes { get; }

    /// <summary>Applies this contribution to a complete shadow.</summary>
    /// <param name="shadow">The earlier complete shadow.</param>
    /// <returns>The composed complete shadow.</returns>
    public Shadow Apply(Shadow shadow) => new(
        Visible ?? shadow.Visible,
        Mode ?? shadow.Mode,
        Offset ?? shadow.Offset,
        Glyph ?? shadow.Glyph,
        Foreground ?? shadow.Foreground,
        Background ?? shadow.Background,
        Attributes ?? shadow.Attributes);

    /// <summary>Overlays a later partial contribution over this contribution.</summary>
    /// <param name="later">The later contribution whose supplied members win.</param>
    /// <returns>The combined partial contribution.</returns>
    public ShadowOverlay Overlay(ShadowOverlay later) => new(
        later.Visible ?? Visible,
        later.Mode ?? Mode,
        later.Offset ?? Offset,
        later.Glyph ?? Glyph,
        later.Foreground ?? Foreground,
        later.Background ?? Background,
        later.Attributes ?? Attributes);
}
