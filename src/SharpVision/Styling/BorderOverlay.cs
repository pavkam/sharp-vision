// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines an optional member-wise border contribution.</summary>
public readonly record struct BorderOverlay
{
    /// <summary>Initializes a partial border contribution.</summary>
    /// <param name="sides">The optional side-flag contribution, limited to defined flags.</param>
    /// <param name="glyphStyle">The optional glyph-style contribution.</param>
    /// <param name="foreground">The optional paintable foreground contribution.</param>
    /// <param name="background">The optional background contribution.</param>
    /// <param name="attributes">The optional attribute contribution.</param>
    /// <exception cref="ArgumentException"><paramref name="foreground"/> is transparent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sides"/> contains unknown flags.</exception>
    public BorderOverlay(
        BorderSide? sides = null,
        BorderGlyphStyle? glyphStyle = null,
        ControlColor? foreground = null,
        ControlColor? background = null,
        ControlDecoration? attributes = null) : this(
            sides,
            glyphStyle,
            foreground,
            background,
            attributes,
            null)
    {
    }

    /// <summary>Creates a partial border contribution with physical-edge colors.</summary>
    /// <param name="edgeColors">The physical-edge foreground contribution.</param>
    /// <param name="sides">The optional side-flag contribution, limited to defined flags.</param>
    /// <param name="glyphStyle">The optional glyph-style contribution.</param>
    /// <param name="foreground">The optional paintable foreground contribution.</param>
    /// <param name="background">The optional background contribution.</param>
    /// <param name="attributes">The optional attribute contribution.</param>
    /// <exception cref="ArgumentException"><paramref name="foreground"/> is transparent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sides"/> contains unknown flags.</exception>
    /// <returns>The partial border contribution.</returns>
    public static BorderOverlay WithEdgeColors(
        BorderEdgeColors edgeColors,
        BorderSide? sides = null,
        BorderGlyphStyle? glyphStyle = null,
        ControlColor? foreground = null,
        ControlColor? background = null,
        ControlDecoration? attributes = null) => new(
            sides,
            glyphStyle,
            foreground,
            background,
            attributes,
            edgeColors);

    private BorderOverlay(
        BorderSide? sides,
        BorderGlyphStyle? glyphStyle,
        ControlColor? foreground,
        ControlColor? background,
        ControlDecoration? attributes,
        BorderEdgeColors? edgeColors)
    {
        // Deliberately NOT ArgumentOutOfRangeException.ThrowIfUndefinedFlags here:
        // AppearanceStates.ApplyStates folds an overlay for every active VisualState bit through
        // BorderOverlay.Overlay, which constructs a new BorderOverlay per fold - the same
        // per-resolution hot path documented on Border's constructor. The generic helper's
        // Convert.ToUInt64 boxes both operands on every call; the direct bitwise check below costs
        // nothing since BorderSide's operators never box.
        if (sides is { } sideValue && (sideValue & ~BorderSide.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sides), sides, "The border sides contain unknown flags.");
        }

        if (foreground is { } foregroundValue)
        {
            ControlColor.ValidatePaint(foregroundValue, nameof(foreground));
        }

        Sides = sides;
        GlyphStyle = glyphStyle;
        Foreground = foreground;
        EdgeColors = edgeColors;
        Background = background;
        Attributes = attributes;
    }

    /// <summary>Gets the optional edge contribution.</summary>
    public BorderSide? Sides { get; }

    /// <summary>Gets the optional glyph-family contribution.</summary>
    public BorderGlyphStyle? GlyphStyle { get; }

    /// <summary>Gets the optional foreground contribution.</summary>
    public ControlColor? Foreground { get; }

    /// <summary>Gets the optional physical-edge foreground contribution.</summary>
    public BorderEdgeColors? EdgeColors { get; }

    /// <summary>Gets the optional background contribution.</summary>
    public ControlColor? Background { get; }

    /// <summary>Gets the optional attribute contribution.</summary>
    public ControlDecoration? Attributes { get; }

    /// <summary>Applies this contribution to a complete border.</summary>
    /// <param name="border">The earlier complete border.</param>
    /// <returns>The composed complete border.</returns>
    [Pure]
    public Border Apply(Border border) => new(
        Sides ?? border.Sides,
        GlyphStyle ?? border.GlyphStyle,
        Foreground ?? border.Foreground,
        EdgeColors ?? border.EdgeColors,
        Background ?? border.Background,
        Attributes ?? border.Attributes);

    /// <summary>Overlays a later partial contribution over this contribution.</summary>
    /// <param name="later">The later contribution whose supplied members win.</param>
    /// <returns>The combined partial contribution.</returns>
    [Pure]
    public BorderOverlay Overlay(BorderOverlay later) => new(
        later.Sides ?? Sides,
        later.GlyphStyle ?? GlyphStyle,
        later.Foreground ?? Foreground,
        later.Background ?? Background,
        later.Attributes ?? Attributes,
        later.EdgeColors ?? EdgeColors);
}
