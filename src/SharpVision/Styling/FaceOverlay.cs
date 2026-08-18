// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines an optional member-wise face contribution.</summary>
public readonly record struct FaceOverlay
{
    /// <summary>Initializes a partial face contribution.</summary>
    /// <param name="foreground">The optional paintable foreground contribution.</param>
    /// <param name="background">The optional background contribution.</param>
    /// <param name="attributes">The optional attribute contribution.</param>
    /// <param name="underline">The optional defined underline style contribution.</param>
    /// <param name="underlineColor">The optional paintable underline color contribution.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="underline"/> is undefined, or <paramref name="foreground"/> or
    /// <paramref name="underlineColor"/> is not a paintable color.
    /// </exception>
    public FaceOverlay(
        ControlColor? foreground = null,
        ControlColor? background = null,
        ControlDecoration? attributes = null,
        Underline? underline = null,
        ControlColor? underlineColor = null)
    {
        if (foreground is { } foregroundValue)
        {
            ControlColor.ValidatePaint(foregroundValue, nameof(foreground));
        }

        if (underlineColor is { } underlineColorValue)
        {
            ControlColor.ValidatePaint(underlineColorValue, nameof(underlineColor));
        }

        if (underline is { } underlineValue)
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(underlineValue, nameof(underline), "The underline style is unknown.");
        }

        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Underline = underline;
        UnderlineColor = underlineColor;
    }

    /// <summary>Gets the optional foreground contribution.</summary>
    public ControlColor? Foreground { get; }

    /// <summary>Gets the optional background contribution.</summary>
    public ControlColor? Background { get; }

    /// <summary>Gets the optional attribute contribution.</summary>
    public ControlDecoration? Attributes { get; }

    /// <summary>Gets the optional underline-style contribution.</summary>
    public Underline? Underline { get; }

    /// <summary>Gets the optional underline-color contribution.</summary>
    public ControlColor? UnderlineColor { get; }

    /// <summary>Applies this contribution to a complete face.</summary>
    /// <param name="face">The earlier complete face.</param>
    /// <returns>The composed complete face.</returns>
    public Face Apply(Face face) => new(
        Foreground ?? face.Foreground,
        Background ?? face.Background,
        Attributes ?? face.Attributes,
        Underline ?? face.Underline,
        UnderlineColor ?? face.UnderlineColor);

    /// <summary>Overlays a later partial contribution over this contribution.</summary>
    /// <param name="later">The later contribution whose supplied members win.</param>
    /// <returns>The combined partial contribution.</returns>
    public FaceOverlay Overlay(FaceOverlay later) => new(
        later.Foreground ?? Foreground,
        later.Background ?? Background,
        later.Attributes ?? Attributes,
        later.Underline ?? Underline,
        later.UnderlineColor ?? UnderlineColor);
}
