// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines an optional member-wise face contribution.</summary>
public readonly record struct FaceSet
{
    /// <summary>Initializes a partial face contribution.</summary>
    public FaceSet(
        ColorValue? foreground = null,
        ColorValue? background = null,
        AttributeValue? attributes = null,
        Underline? underline = null,
        ColorValue? underlineColor = null)
    {
        if (foreground is { } foregroundValue)
        {
            ColorValue.ValidatePaint(foregroundValue, nameof(foreground));
        }

        if (underlineColor is { } underlineColorValue)
        {
            ColorValue.ValidatePaint(underlineColorValue, nameof(underlineColor));
        }

        if (underline is { } underlineValue && !Enum.IsDefined(underlineValue))
        {
            throw new ArgumentOutOfRangeException(nameof(underline), underline, "The underline style is unknown.");
        }

        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Underline = underline;
        UnderlineColor = underlineColor;
    }

    /// <summary>Gets the optional foreground contribution.</summary>
    public ColorValue? Foreground { get; }

    /// <summary>Gets the optional background contribution.</summary>
    public ColorValue? Background { get; }

    /// <summary>Gets the optional attribute contribution.</summary>
    public AttributeValue? Attributes { get; }

    /// <summary>Gets the optional underline-style contribution.</summary>
    public Underline? Underline { get; }

    /// <summary>Gets the optional underline-color contribution.</summary>
    public ColorValue? UnderlineColor { get; }

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
    public FaceSet Overlay(FaceSet later) => new(
        later.Foreground ?? Foreground,
        later.Background ?? Background,
        later.Attributes ?? Attributes,
        later.Underline ?? Underline,
        later.UnderlineColor ?? UnderlineColor);
}
